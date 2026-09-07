using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets.Script.DynamicCards
{
    // A child of the existing art image: existing borders, badges and card backs retain their draw order.
    public sealed class DynamicCardView : MonoBehaviour
    {
        private Image art;
        private RawImage surface;
        private Material surfaceMaterial;
        private string artId;
        private bool hidden, preview, dragging, miniature;
        private bool previewAudio = true;
        private DynamicCardPresentation presentation;
        private int generation;
        private GameObject model;
        private Camera renderCamera;
        private RenderTexture texture;
        private AudioSource sound;
        private DynamicCardEffects effects;
        private DynamicCardSourceControllers sourceControllers;
        private Transform pivot;
        private RectTransform frame;
        private DynamicCardDragHandle dragHandle;
        private Quaternion frameRest, pivotRest;
        private Vector2 dragOrigin, dragStart, target, current;
        private DynamicCardEntry entry;
        private float age;
        private Scene stage;
        private static readonly HashSet<DynamicCardView> ActiveViews = new HashSet<DynamicCardView>();
        internal static bool HasVisiblePreview
        {
            get
            {
                foreach (var view in ActiveViews)
                    if (view != null && view.preview && !view.hidden && view.IsVisible()) return true;
                return false;
            }
        }
        private static int stageId;
        private static int nextCell;
        private static readonly Stack<int> FreeCells = new Stack<int>();
        private int cell;
        private float invisibleSince = -1;
        private readonly Vector3[] corners = new Vector3[4];
        internal Vector2 DisplayPosition
        {
            get
            {
                var canvas = art.canvas;
                var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                return ScreenBounds(art.rectTransform, camera).center;
            }
        }
        internal bool IsPreview { get { return preview; } }
        internal bool IsCurrent(int version)
        { return this != null && version == generation && isActiveAndEnabled && !hidden && DynamicCardSettings.Enabled; }

        internal bool IsVisible()
        {
            if (art == null || !art.isActiveAndEnabled || art.color.a <= .001f || art.canvas == null || !art.canvas.isActiveAndEnabled) return false;
            if (presentation != null && presentation.Waiting)
            { if (!presentation.AncestorsVisible(art.transform)) return false; }
            else if (art.canvasRenderer.cull || art.canvasRenderer.GetInheritedAlpha() <= .001f) return false;
            var canvas = art.canvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Rect bounds = ScreenBounds(art.rectTransform, camera);
            Rect screen = camera != null ? camera.pixelRect : new Rect(0, 0, Screen.width, Screen.height);
            if (!bounds.Overlaps(screen)) return false;
            for (var parent = art.transform.parent; parent != null; parent = parent.parent)
            {
                var clip = parent.GetComponent<RectMask2D>();
                var mask = parent.GetComponent<Mask>();
                if ((clip != null && clip.isActiveAndEnabled) || (mask != null && mask.isActiveAndEnabled))
                    if (!bounds.Overlaps(ScreenBounds((RectTransform)parent, camera))) return false;
            }
            return true;
        }

        private Rect ScreenBounds(RectTransform rect, Camera camera)
        {
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]), max = min;
            for (int i = 1; i < 4; i++)
            {
                var point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private bool CanCreate(int version)
        {
            if (!IsCurrent(version)) return false;
            if (IsVisible() && (preview || !HasVisiblePreview)) return true;
            // A scroll can hide a card while its asset request is in flight.
            DynamicCardLibrary.Instance.Enqueue(this, version);
            return false;
        }

        public static RectTransform FindCardRoot(Transform artTransform, Transform borderTransform)
        {
            for (var parent = artTransform.parent; parent != null; parent = parent.parent)
                if (borderTransform == parent || borderTransform.IsChildOf(parent)) return parent as RectTransform;
            return null;
        }

        public static void Bind(Image image, string id, bool concealed = false, bool largePreview = false, RectTransform wholeCard = null, bool listThumbnail = false, bool playPreviewAudio = true, RectTransform presentationRoot = null)
        {
            if (image == null) return;
            var view = image.GetComponent<DynamicCardView>();
            if (view == null) view = image.gameObject.AddComponent<DynamicCardView>();
            view.art = image;
            bool changed = view.artId != id || view.hidden != concealed || view.preview != largePreview || view.miniature != listThumbnail || view.previewAudio != playPreviewAudio;
            view.artId = id; view.hidden = concealed; view.preview = largePreview; view.miniature = listThumbnail;
            view.previewAudio = playPreviewAudio;
            var root = presentationRoot != null ? presentationRoot : wholeCard;
            if (largePreview && root != null && (view.presentation == null || view.presentation.transform != root))
            {
                if (view.presentation != null) view.presentation.Restore();
                view.presentation = root.GetComponent<DynamicCardPresentation>();
                if (view.presentation == null) view.presentation = root.gameObject.AddComponent<DynamicCardPresentation>();
                changed = true;
            }
            if (wholeCard != null && view.frame != wholeCard)
            {
                view.frame = wholeCard; view.frameRest = wholeCard.localRotation;
                var handle = wholeCard.GetComponent<DynamicCardDragHandle>();
                if (handle == null) handle = wholeCard.gameObject.AddComponent<DynamicCardDragHandle>();
                handle.View = view;view.dragHandle=handle;handle.enabled=view.preview && view.model!=null;
            }
            if (changed) view.Refresh();
        }

        private void OnEnable() { ActiveViews.Add(this); DynamicCardSettings.Changed += Refresh; if (art != null) Refresh(); }
        private void OnDisable() { ActiveViews.Remove(this); DynamicCardSettings.Changed -= Refresh; Clear(); }
        private void OnDestroy() { Clear(); }
        private void OnApplicationFocus(bool focused) { if (!focused) ReturnToCenter(); }

        private void Refresh()
        {
            Clear();
            if (!isActiveAndEnabled || hidden || !DynamicCardSettings.Enabled || string.IsNullOrEmpty(artId)) return;
            if (preview && presentation != null) presentation.Begin();
            DynamicCardLibrary.Instance.Enqueue(this, generation);
        }

        internal IEnumerator Create(int version)
        {
            if (!CanCreate(version)) yield break;
            GameObject prefab = null; AudioClip clip = null; DynamicCardEntry data = null;
            yield return DynamicCardLibrary.Instance.Load(artId, preview && previewAudio, (d, p, a) => { data = d; prefab = p; clip = a; });
            if (!preview) yield return null;
            if (prefab == null)
            { if (IsCurrent(version) && presentation != null) presentation.Reveal(false); yield break; }
            if (!CanCreate(version)) { DynamicCardLibrary.Released(data); yield break; }
            entry = data;
            // An isolated scene plus a unique distant cell prevents card lights/cameras touching gameplay.
            cell = FreeCells.Count > 0 ? FreeCells.Pop() : ++nextCell;
            stage = SceneManager.CreateScene("DynamicCard_" + ++stageId);
            // Keep the isolation offset outside all source Animator bindings. A source root curve
            // can reset its local position without moving this card (or its world-space particles) into gameplay.
            var stagingRoot = new GameObject("Card placement");
            SceneManager.MoveGameObjectToScene(stagingRoot, stage);
            stagingRoot.transform.position = new Vector3(10000 + (cell % 32) * 1024, 10000 + (cell / 32) * 1024, 0);
            model = Instantiate(prefab, stagingRoot.transform, false);
            RestoreSceneFacing();
            model.SetActive(true);
            // Premium meshes retain their source skinning even when the game's global quality uses one bone.
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.quality = SkinQuality.Bone4;
            foreach (Transform child in model.transform)
                if (child.localPosition.sqrMagnitude > 1000000) child.localPosition = Vector3.zero;
            // Spread grid setup across frames; the selected detail card presents immediately.
            if (!preview) yield return null;
            if (!IsCurrent(version)) yield break;
            var cameraObject = new GameObject("Card camera");
            SceneManager.MoveGameObjectToScene(cameraObject, stage);
            cameraObject.transform.position = model.transform.position + new Vector3(0, 0, entry.cameraDistance);
            renderCamera = cameraObject.AddComponent<Camera>();
            renderCamera.enabled = false;
            renderCamera.fieldOfView = entry.fieldOfView;
            renderCamera.nearClipPlane = entry.nearClip;
            renderCamera.farClipPlane = entry.farClip;
            renderCamera.clearFlags = CameraClearFlags.SolidColor;
            renderCamera.backgroundColor = Color.clear;
            renderCamera.allowHDR = false;
            renderCamera.allowMSAA = false;
            // Source cameras render a square; the card art is a portrait region inside that square.
            texture = new RenderTexture(preview ? 1024 : 384, preview ? 1024 : 384, 24, RenderTextureFormat.ARGB32);
            texture.Create(); renderCamera.targetTexture = texture;
            DynamicCardFraming.Apply(renderCamera, entry);
            pivot = string.IsNullOrEmpty(entry.pivot) ? null : model.transform.Find(entry.pivot);
            if (pivot != null) pivotRest = pivot.localRotation;
            var overlay = new GameObject("Dynamic art", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            overlay.transform.SetParent(art.transform, false);
            surface = overlay.GetComponent<RawImage>();
            surfaceMaterial = new Material(Resources.Load<Shader>("DynamicCardSurface"));
            surfaceMaterial.hideFlags = HideFlags.HideAndDontSave;
            surface.material = surfaceMaterial;
            surface.enabled = false;
            surface.raycastTarget = false;
            // Legacy art sprites contain padding on the right/bottom. Match their content region,
            // not the entire padded texture, so the original portrait mask and frame stay aligned.
            surface.rectTransform.anchorMin = new Vector2(0, 1 - 713f / 1024);
            surface.rectTransform.anchorMax = new Vector2(497f / 1024, 1);
            surface.rectTransform.offsetMin = Vector2.zero; surface.rectTransform.offsetMax = Vector2.zero;
            surface.texture = texture;
            surface.uvRect = DynamicCardFraming.ArtRegion(entry);
            if (miniature)
            {
                surface.rectTransform.anchorMin = Vector2.zero; surface.rectTransform.anchorMax = Vector2.one;
                surface.uvRect = new Rect(.185f, .52f, .633f, .19f);
            }
            foreach (var animator in model.GetComponentsInChildren<Animator>(true)) { animator.applyRootMotion=false; animator.Rebind(); animator.Update(0); }
            NormalizeStageRoot();
            foreach (var particles in model.GetComponentsInChildren<ParticleSystem>(true))
            { particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); if (particles.main.playOnAwake) particles.Play(false); }
            effects = model.AddComponent<DynamicCardEffects>(); effects.Initialize(entry); effects.Tick(0);
            sourceControllers=model.AddComponent<DynamicCardSourceControllers>();sourceControllers.Initialize(entry,renderCamera.transform);
            sourceControllers.Tick(0,0,(entry.xStart+entry.xEnd)*.5f,(entry.yStart+entry.yEnd)*.5f);
            if(dragHandle!=null)dragHandle.enabled=preview;
            if (preview && clip != null)
            { sound = model.AddComponent<AudioSource>(); sound.clip = clip; sound.loop = true; sound.spatialBlend = 0; sound.Play(); }
            ApplyCut();
            NormalizeStageRoot();
            renderCamera.Render();
            surface.enabled = true;
            if (presentation != null) presentation.Reveal(true);
        }

        private float nextRender;
        private void LateUpdate()
        {
            if (model == null || renderCamera == null || surface == null || !surface.enabled) return;
            bool visible = IsVisible();
            bool running = visible && (preview || !HasVisiblePreview);
            if (model.activeSelf != running) model.SetActive(running);
            if (!visible)
            {
                if (invisibleSince < 0) invisibleSince = Time.realtimeSinceStartup;
                if (Time.realtimeSinceStartup - invisibleSince > 1.5f) Refresh();
                return;
            }
            invisibleSince = -1;
            // Keep the last thumbnail image while the detail card owns the animation budget.
            if (!running) return;
            NormalizeStageRoot();
            age += Time.unscaledDeltaTime;
            current = Vector2.Lerp(current, target, 1 - Mathf.Exp(-(dragging ? 18 : 12) * Time.unscaledDeltaTime));
            float pitch = Mathf.Lerp(entry.xStart, entry.xEnd, (current.y * .5f + 1) * .5f);
            float yaw = Mathf.Lerp(entry.yStart, entry.yEnd, (current.x + 1) * .5f);
            if (pivot != null)
            {
                // Half the source vertical travel. Inner viewpoint and outer card have opposite pitch.
                pivot.localRotation = pivotRest * Quaternion.Euler(pitch, yaw, 0);
                if (frame != null) frame.localRotation = frameRest * Quaternion.Euler(-pitch + (entry.xStart + entry.xEnd) * .5f, yaw - (entry.yStart + entry.yEnd) * .5f, 0);
            }
            if (sound != null) sound.volume = PlayerPrefs.GetInt("isCloseSound", 1) == 0 ? 0 : PlayerPrefs.GetInt("effectVolum", 7) / 10f;
            ApplyCut();
            if (effects != null) effects.Tick(age);
            if (presentation != null) presentation.Tick();
            if(sourceControllers!=null)sourceControllers.Tick(age,Time.unscaledDeltaTime,pitch,yaw);
            surface.color = art.color;
            // Thumbnail textures need fewer redraws; each card has its own phase to spread camera work.
            if(preview || (!HasVisiblePreview && Time.unscaledTime>=nextRender))
            {
                renderCamera.Render();
                float step=1f/24f;float phase=(GetInstanceID()&31)/32f*step;
                nextRender=(Mathf.Floor((Time.unscaledTime-phase)/step)+1)*step+phase;
            }
        }

        private Transform beforeCutGroup,afterCutGroup;
        private Renderer[] beforeCutMeshes,afterCutMeshes;
        private bool cutKnown,cutApplied;
        private void ApplyCut()
        {
            if(entry.cutTime<0)return;
            bool after=age>=entry.cutTime;
            if(cutKnown && cutApplied==after)return;
            if(!cutKnown)
            {
                beforeCutGroup=DynamicCardPaths.Find(model.transform,entry.beforeCut);afterCutGroup=DynamicCardPaths.Find(model.transform,entry.afterCut);
                beforeCutMeshes=beforeCutGroup==null?null:beforeCutGroup.GetComponentsInChildren<Renderer>(true);
                afterCutMeshes=afterCutGroup==null?null:afterCutGroup.GetComponentsInChildren<Renderer>(true);
            }
            // The source rig-switch event controls mesh visibility, as in the Godot adaptation.
            // Particle and trail renderers retain their independent animation/event state.
            if(beforeCutGroup!=null)beforeCutGroup.gameObject.SetActive(!after);
            if(afterCutGroup!=null)afterCutGroup.gameObject.SetActive(after);
            var meshes=after?afterCutMeshes:beforeCutMeshes;
            if(meshes!=null)foreach(var renderer in meshes)
                if(renderer is SkinnedMeshRenderer || renderer is MeshRenderer)renderer.enabled=true;
            cutKnown=true;cutApplied=after;
        }

        private void NormalizeStageRoot()
        {
            // Some source clips key their parked scene root at y=-10000. Keep that staging
            // offset out of the portable camera, including after Animator has evaluated.
            foreach(Transform child in model.transform)
                if(child.localPosition.sqrMagnitude>1000000)child.localPosition=Vector3.zero;
        }

        private void RestoreSceneFacing()
        {
            // Older exported scenes can be parked facing the card back. An explicit
            // source startup transform takes precedence (some cards intentionally turn).
            if (!entry.prefab.Contains("/Legacy2017/")) return;
            var root = model.transform.Find(entry.id);
            if (root == null) return;
            foreach (var setup in entry.initialTransforms ?? new DynamicCardInitialTransform[0])
                if (setup.path == entry.id) return;
            if (Quaternion.Angle(root.localRotation, Quaternion.Euler(0, 180, 0)) < .01f)
                root.localRotation = Quaternion.identity;
        }

        public void OnBeginDrag(PointerEventData data)
        { if (!preview || model == null) return; if (presentation != null) presentation.Restore(); dragging = true; dragOrigin = data.position; dragStart = target; }
        public void OnDrag(PointerEventData data)
        {
            if (!dragging) return;
            var delta = data.position - dragOrigin;
            target = dragStart + new Vector2(-delta.x / 200, delta.y / 260);
            target = new Vector2(Mathf.Clamp(target.x, -1, 1), Mathf.Clamp(target.y, -1, 1));
        }
        public void OnEndDrag(PointerEventData data) { ReturnToCenter(); }
        private void ReturnToCenter() { dragging = false; target = Vector2.zero; }

        private void Clear()
        {
            if (presentation != null) presentation.Restore();
            if (entry != null) DynamicCardLibrary.Released(entry);
            invisibleSince = -1;
            cutKnown=false;beforeCutGroup=null;afterCutGroup=null;beforeCutMeshes=null;afterCutMeshes=null;
            generation++;
            if(dragHandle!=null)dragHandle.enabled=false;
            DynamicCardLibrary.Cancel(this);
            if (surface != null) { surface.enabled = false; Destroy(surface.gameObject); }
            if (surfaceMaterial != null) { Destroy(surfaceMaterial); surfaceMaterial = null; }
            if (renderCamera != null) { renderCamera.targetTexture = null; Destroy(renderCamera.gameObject); }
            if (model != null) { model.SetActive(false); Destroy(model); }
            if (texture != null) { texture.Release(); Destroy(texture); }
            if (stage.IsValid() && stage.isLoaded) SceneManager.UnloadSceneAsync(stage);
            stage = default(Scene);
            if (cell > 0) { FreeCells.Push(cell); cell = 0; }
            if (frame != null) frame.localRotation = frameRest;
            model = null; renderCamera = null; texture = null; surface = null; sound = null; pivot = null;
            effects = null; sourceControllers = null; entry = null;
            target = current = Vector2.zero; dragging = false; age = 0;
        }
    }
}
