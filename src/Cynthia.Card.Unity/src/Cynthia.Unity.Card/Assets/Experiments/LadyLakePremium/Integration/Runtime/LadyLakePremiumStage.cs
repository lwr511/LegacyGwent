using System;
using Assets.Script.DynamicCards;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LegacyGwent.LadyLakePremium
{
    /// <summary>
    /// Runtime stage for the standalone LadyLakePremium scene.
    ///
    /// It is deliberately thin: it only builds the collection's UI layout, applies the original
    /// gold / neutral ArtCard assets, and then hands the card to the REAL production
    /// <see cref="DynamicCardView.Bind"/> path. The 3D source prefab, its isolated scene, its
    /// camera, RenderTexture, Animator clock, idle sway and <see cref="DynamicCardDragHandle"/>
    /// are all created by <c>DynamicCardView</c> itself; this file clones none of that.
    ///
    /// Wiring is written by <c>LegacyGwent.LadyLakePremium.Editor.LadyLakePremiumBuilder</c>.
    /// A runtime fallback instantiates the real ArtCard prefab from Resources if the scene has no
    /// instance, so the stage is also usable in a bare additive scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LadyLakePremiumStage : MonoBehaviour
    {
        /// <summary>Real collection ArtCard prefab (Resources path), same one ArtCard.SetCard uses.</summary>
        public const string CollectionCardPrefab = "Prefab/Cards/ArtCard";
        /// <summary>Card art id shared by GwentMap 70006 / 70011.</summary>
        public const string ArtId = "c10000000";
        /// <summary>GwentMap.cs:11253 70006 Strength=25.</summary>
        public const int CardStrength = 25;
        /// <summary>Backdrop only; no text overlays.</summary>
        public static readonly Color BackdropColor = new Color(.012f, .035f, .043f, 1f);
        public static readonly Vector2 ReferenceResolution = new Vector2(1600f, 900f);

        [Header("Wiring (written by LadyLakePremiumBuilder)")]
        public ArtCard card;
        public RectTransform cardHost;
        [Tooltip("Static c10000000 sprite kept as the fallback until the dynamic view is ready.")]
        public Sprite staticArt;
        [Range(.2f, 1f)] public float cardHeightFraction = .72f;
        [Tooltip("Plays the catalog's 12s Environment/Audio/LadyLakeLoop.wav on the dynamic model. " +
            "ArtCard.SetCard uses false for hover detail; the collection grid (CardShowInfo) uses true.")]
        public bool playPreviewAudio = true;

        [Header("Diagnostics")]
        public bool buildOnAwake = true;
        [Tooltip("Temporarily enables DynamicCardSettings for this scene; the previous tier is restored on destroy.")]
        public bool enableDynamicCards = true;

        private bool ready;
        private float appliedScale = -1f;
        private bool changedQuality;
        private DynamicCardQuality previousQuality;
        private float startedAt;
        private bool reported;

        public bool IsReady { get { return ready; } }
        public ArtCard Card { get { return card; } }
        public DynamicCardView View
        {
            get { return card != null && card.CardImg != null ? card.CardImg.GetComponent<DynamicCardView>() : null; }
        }

        private void Awake()
        {
            if (buildOnAwake) Build();
        }

        private void OnDestroy()
        {
            RestoreDynamicSetting();
        }

        // ------------------------------------------------------------------
        // Build
        // ------------------------------------------------------------------
        /// <summary>Idempotent runtime assembly. Mirrors ArtCard.SetCard's frame + Bind usage.</summary>
        public void Build()
        {
            if (ready) return;
            if (card == null) card = GetComponentInChildren<ArtCard>(true);
            if (card == null) card = CreateCollectionCard();
            if (card == null)
            {
                Debug.LogError("[LadyLakePremium] No ArtCard for art " + ArtId + ". Expected Resources/" +
                    CollectionCardPrefab + " or a serialized stage.card reference.");
                return;
            }
            if (card.CardImg == null || card.CardBorder == null)
            {
                Debug.LogError("[LadyLakePremium] ArtCard is missing CardImg/CardBorder; cannot attach the dynamic art.");
                return;
            }

            ApplyGoldNeutralFrame(card);

            // ArtCard.SetCard does this before Bind so DynamicCardPresentation owns the visual pivot.
            var presentation = card.GetComponent<DynamicCardPresentation>();
            if (presentation == null) presentation = card.gameObject.AddComponent<DynamicCardPresentation>();
            presentation.Configure(card.CardBorder.rectTransform,
                card.Content != null ? card.Content.transform : (Transform)null);

            EnableDynamicCards();

            // The one and only production entry point. wholeCard/presentationRoot/portraitBorder are
            // the same collection references ArtCard.SetCard passes; premium=true is explicit because
            // PremiumCollectionClient.Owns() needs the login container that a standalone scene lacks.
            DynamicCardView.Bind(card.CardImg, ArtId, false, true, card.CardBorder.rectTransform, false,
                playPreviewAudio, (RectTransform)card.transform, card.CardBorder.rectTransform, true);

            EnsureEventSystem();
            ready = true;
            startedAt = Time.realtimeSinceStartup;
        }

        private ArtCard CreateCollectionCard()
        {
            if (cardHost == null) cardHost = FindOrCreateHost();
            if (cardHost == null) return null;
            var prefab = Resources.Load<GameObject>(CollectionCardPrefab);
            if (prefab == null) return null;
            var instance = Instantiate(prefab, cardHost);
            instance.name = "ArtCard";
            var rect = instance.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero;
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one;
            }
            return instance.GetComponent<ArtCard>();
        }

        private RectTransform FindOrCreateHost()
        {
            var canvas = GetComponentInChildren<Canvas>(true);
            if (canvas == null)
            {
                var canvasGo = new GameObject("CardCanvas", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasGo.transform.SetParent(transform, false);
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            var host = canvas.transform.Find("CardHost") as RectTransform;
            if (host == null)
            {
                var hostGo = new GameObject("CardHost", typeof(RectTransform));
                hostGo.transform.SetParent(canvas.transform, false);
                host = (RectTransform)hostGo.transform;
            }
            host.anchorMin = Vector2.zero; host.anchorMax = Vector2.one;
            host.offsetMin = Vector2.zero; host.offsetMax = Vector2.zero;
            return host;
        }

        /// <summary>
        /// Applies the original serialized gold border and neutral gold faction icon from the real
        /// ArtCard prefab (ArtCard.cs:117-136 branches) without touching the prefab asset. Content
        /// (name/rules banner) is hidden so the scene shows only the complete framed card.
        /// </summary>
        public static void ApplyGoldNeutralFrame(ArtCard target)
        {
            if (target == null) return;
            if (target.CardImg != null) target.CardImg.raycastTarget = false;
            if (target.CardBorder != null)
            {
                target.CardBorder.raycastTarget = true;
                if (target.GoldBorder != null) target.CardBorder.sprite = target.GoldBorder;
            }
            if (target.FactionIcon != null && target.NeutralGoldIcon != null)
                target.FactionIcon.sprite = target.NeutralGoldIcon;
            if (target.CardBack != null) target.CardBack.gameObject.SetActive(false);
            if (target.StrengthShow != null) target.StrengthShow.SetActive(true);
            if (target.Strength != null) target.Strength.text = CardStrength.ToString();
            if (target.ArmorShow != null) target.ArmorShow.SetActive(false);
            if (target.CountdownShow != null) target.CountdownShow.SetActive(false);
            if (target.Content != null) target.Content.gameObject.SetActive(false);
        }

        private void EnableDynamicCards()
        {
            if (!enableDynamicCards) return;
            if (DynamicCardSettings.Enabled) return;
            previousQuality = DynamicCardSettings.Quality;
            // Scoped to this scene: restored in OnDestroy. No long-lived global change.
            DynamicCardSettings.Enabled = true;
            changedQuality = true;
        }

        private void RestoreDynamicSetting()
        {
            if (!changedQuality) return;
            DynamicCardSettings.Quality = previousQuality;
            changedQuality = false;
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
        }

        // ------------------------------------------------------------------
        // Layout + diagnostics (no motion/drag code: production owns those)
        // ------------------------------------------------------------------
        private void LateUpdate()
        {
            if (!ready) return;
            FitCardToHost();
            if (reported || Time.realtimeSinceStartup - startedAt < 3f) return;
            reported = true;
            var view = View;
            if (view == null || view.RenderTarget == null || !view.RenderTarget.IsCreated())
                Debug.LogWarning("[LadyLakePremium] Dynamic card for " + ArtId + " is not active; the " +
                    "static c10000000 art stays visible. Bundled packages take precedence over editor " +
                    "source fallback, so the root must merge " + IntegrationPrefix +
                    "catalog-entry.json into Assets/DynamicCards/Content/catalog.json and rebuild the " +
                    "editor bundle before Play. See Integration/integration-report.md.");
            else
                Debug.Log("[LadyLakePremium] Dynamic card ready for " + ArtId + ".");
        }

        private const string IntegrationPrefix = "Assets/Experiments/LadyLakePremium/Integration/";

        private void FitCardToHost()
        {
            if (card == null || cardHost == null) return;
            var rect = card.transform as RectTransform;
            if (rect == null || rect.rect.height <= 0f) return;
            float scale = Mathf.Max(.01f, cardHost.rect.height * cardHeightFraction) / rect.rect.height;
            if (Mathf.Abs(scale - appliedScale) < .0005f) return;
            appliedScale = scale;
            card.transform.localScale = new Vector3(scale, scale, 1f);
        }

        public LadyLakePremiumSnapshot Snapshot()
        {
            var view = View;
            var snapshot = new LadyLakePremiumSnapshot();
            snapshot.ready = ready;
            snapshot.artId = ArtId;
            snapshot.dynamicCardsEnabled = DynamicCardSettings.Enabled;
            snapshot.viewPresent = view != null;
            snapshot.dynamicArtNodePresent = card != null && card.CardImg != null &&
                card.CardImg.transform.Find("Dynamic art") != null;
            snapshot.presentationReady = view != null && view.IsPresentationReady;
            snapshot.renderTargetCreated = view != null && view.RenderTarget != null && view.RenderTarget.IsCreated();
            snapshot.renderSize = view != null && view.RenderTarget != null
                ? view.RenderTarget.width + "x" + view.RenderTarget.height : null;
            snapshot.dragging = view != null && view.IsDragging;
            snapshot.staticArtVisible = card != null && card.CardImg != null && card.CardImg.sprite != null;
            snapshot.goldFrame = card != null && card.CardBorder != null && card.GoldBorder != null &&
                card.CardBorder.sprite == card.GoldBorder;
            snapshot.neutralGoldIcon = card != null && card.FactionIcon != null && card.NeutralGoldIcon != null &&
                card.FactionIcon.sprite == card.NeutralGoldIcon;
            snapshot.strength = card != null && card.Strength != null ? card.Strength.text : null;
            snapshot.pivotLocalPosition = LadyLakePremiumFraming.PivotLocalPosition().ToString("F6");
            snapshot.artRegionWorldHeight = LadyLakePremiumFraming.ArtRegionWorldHeight();
            snapshot.artRegionWorldWidth = LadyLakePremiumFraming.ArtRegionWorldWidth();
            snapshot.paintingWorldHeight = LadyLakePremiumFraming.PaintingHeightUnits;
            return snapshot;
        }

        public string Describe()
        {
            return JsonUtility.ToJson(Snapshot());
        }
    }

    /// <summary>Serializable read-only snapshot (no runtime dependency on editor types).</summary>
    [Serializable]
    public struct LadyLakePremiumSnapshot
    {
        public bool ready;
        public string artId;
        public bool dynamicCardsEnabled;
        public bool viewPresent;
        public bool dynamicArtNodePresent;
        public bool presentationReady;
        public bool renderTargetCreated;
        public string renderSize;
        public bool dragging;
        public bool staticArtVisible;
        public bool goldFrame;
        public bool neutralGoldIcon;
        public string strength;
        public string pivotLocalPosition;
        public float artRegionWorldHeight;
        public float artRegionWorldWidth;
        public float paintingWorldHeight;
    }
}
