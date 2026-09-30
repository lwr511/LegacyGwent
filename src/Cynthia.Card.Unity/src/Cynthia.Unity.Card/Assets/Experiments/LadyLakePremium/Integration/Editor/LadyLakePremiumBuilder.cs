using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Assets.Script.DynamicCards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LegacyGwent.LadyLakePremium.Editor
{
    /// <summary>
    /// INTEGRATION assembler. Owns only Assets/Experiments/LadyLakePremium/Integration/**.
    ///
    /// Build() does three things:
    ///   1. calls the concurrent model and environment workers through reflection
    ///      (<c>LegacyGwent.LadyLakePremium.Editor.LadyLakeModelBuilder.Build()</c> and
    ///       <c>LegacyGwent.LadyLakePremium.Editor.LadyLakeEnvironmentBuilder.Build()</c>),
    ///      throwing a useful error when either is absent;
    ///   2. assembles <c>Integration/Card.prefab</c> as <c>Card/Pivot</c> with the two authored
    ///      prefabs as nested prefab instances, applying the exact appearance-offset compensation;
    ///   3. builds the standalone <c>Integration/LadyLakePremium.unity</c> scene that drives the REAL
    ///      collection ArtCard + production DynamicCardView.
    ///
    /// It never edits production scripts, catalog.json or the dynamic card bundle: the root merges
    /// the emitted catalog entry and rebuilds the packages. See Integration/integration-report.md.
    /// </summary>
    public static class LadyLakePremiumBuilder
    {
        public const string IntegrationRoot = "Assets/Experiments/LadyLakePremium/Integration";
        public const string ModelPrefabPath = "Assets/Experiments/LadyLakePremium/Model/FigureRig.prefab";
        public const string EnvironmentPrefabPath = "Assets/Experiments/LadyLakePremium/Environment/Environment.prefab";
        public const string EnvironmentAudioPath = "Assets/Experiments/LadyLakePremium/Environment/Audio/LadyLakeLoop.wav";
        public const string AssembledPrefabPath = IntegrationRoot + "/Card.prefab";
        public const string ScenePath = IntegrationRoot + "/LadyLakePremium.unity";
        public const string CatalogEntryPath = IntegrationRoot + "/catalog-entry.json";
        public const string ValidationJsonPath = IntegrationRoot + "/validation-report.json";
        public const string ReportPath = IntegrationRoot + "/integration-report.md";
        public const string ArtCardPrefabPath = "Assets/Resources/Prefab/Cards/ArtCard.prefab";
        public const string StaticArtPath = "Assets/Addressables/Cards/c10000000.png";
        public const string CatalogSceneId = "lady-lake-authored";

        private const string ModelBuilderType = "LegacyGwent.LadyLakePremium.Editor.LadyLakeModelBuilder";
        private const string EnvironmentBuilderType = "LegacyGwent.LadyLakePremium.Editor.LadyLakeEnvironmentBuilder";

        // ------------------------------------------------------------------
        // Public entry points (root contract)
        // ------------------------------------------------------------------
        [MenuItem("Tools/Lady Lake Premium/Build Card + Scene", false, 10)]
        public static void Build()
        {
            BuildInternal();
            ValidateAndWrite();
            Debug.Log("[LadyLakePremium] Assembled " + AssembledPrefabPath + " and " + ScenePath +
                ". Root must merge " + CatalogEntryPath + " into Assets/DynamicCards/Content/catalog.json and rebuild the dynamic card packages.");
        }

        public static void BuildAndOpen()
        {
            BuildInternal();
            ValidateAndWrite();
            Open();
        }

        [MenuItem("Tools/Lady Lake Premium/Open Scene", false, 11)]
        public static void Open()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("[LadyLakePremium] Open must run in Edit Mode (exit Play first).");
            if (!File.Exists(ScenePath))
                throw new FileNotFoundException("[LadyLakePremium] Scene is missing. Run LadyLakePremiumBuilder.Build() first.", ScenePath);
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/Lady Lake Premium/Validate Prefab", false, 12)]
        public static void ValidateAndWrite()
        {
            var report = LadyLakePremiumValidator.Validate();
            File.WriteAllText(ValidationJsonPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            Debug.Log("[LadyLakePremium] " + report.Summary() + " -> " + ValidationJsonPath);
        }

        // ------------------------------------------------------------------
        // Assembly
        // ------------------------------------------------------------------
        public static void AssembleExisting()
        {
            var figure=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefabPath);
            var environment=AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPrefabPath);
            if(!figure||!environment)throw new InvalidOperationException("Build source prefabs first");
            BuildAssembledPrefab(figure,environment);BuildScene();WriteCatalogEntry();
        }

        private static void BuildInternal()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("[LadyLakePremium] Build must run in Edit Mode (exit Play first).");
            InvokeWorker(ModelBuilderType, "Build", "model", "Model/Editor/LadyLakeModelBuilder.cs");
            InvokeWorker(EnvironmentBuilderType, "Build", "environment", "Environment/Editor/LadyLakeEnvironmentBuilder.cs");

            var figure = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefabPath);
            if (figure == null)
                throw new InvalidOperationException("[LadyLakePremium] The model worker completed but '" +
                    ModelPrefabPath + "' does not exist. Expected FigureRig.prefab with the skinned rig, GripSocket and Intro/Loop clips.");
            var environment = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPrefabPath);
            if (environment == null)
                throw new InvalidOperationException("[LadyLakePremium] The environment worker completed but '" +
                    EnvironmentPrefabPath + "' does not exist. Expected Environment.prefab with the waterbed, plants, effects and Audio/LadyLakeLoop.wav.");

            BuildAssembledPrefab(figure, environment);
            BuildScene();
            WriteCatalogEntry();
        }

        private static void BuildAssembledPrefab(GameObject figure, GameObject environment)
        {
            var root = new GameObject("Card");
            try
            {
                var pivot = new GameObject("Pivot");
                pivot.transform.SetParent(root.transform, false);
                // Exact framing compensation (see LadyLakePremiumFraming): remove the +2 appearance
                // anchor and centre the 497x710 painting inside the unchanged ArtRegion.
                pivot.transform.localPosition = LadyLakePremiumFraming.PivotLocalPosition();
                pivot.transform.localRotation = Quaternion.identity;
                pivot.transform.localScale = Vector3.one;

                var floating = LadyLakeBuoyancy.Create(pivot.transform);
                var figureInstance = (GameObject)PrefabUtility.InstantiatePrefab(figure, floating);
                figureInstance.name = "FigureRig";
                ResetLocal(figureInstance.transform);

                var environmentInstance = (GameObject)PrefabUtility.InstantiatePrefab(environment, pivot.transform);
                environmentInstance.name = "Environment";
                ResetLocal(environmentInstance.transform);
                LadyLakeProductionEffects.Bake(environmentInstance);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, AssembledPrefabPath);
                if (saved == null)
                    throw new InvalidOperationException("[LadyLakePremium] Failed to save " + AssembledPrefabPath + ".");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
        }

        private static void BuildScene()
        {
            var previousActive = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);

                var root = new GameObject("LadyLakePremium");
                var listener = new GameObject("Preview audio listener", typeof(AudioListener));
                listener.transform.SetParent(root.transform, false);

                var canvasGo = new GameObject("CardCanvas", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasGo.transform.SetParent(root.transform, false);
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.pixelPerfect = false;
                canvas.sortingOrder = 100;
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = LadyLakePremiumStage.ReferenceResolution;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = .5f;

                var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                backdrop.transform.SetParent(canvasGo.transform, false);
                Stretch((RectTransform)backdrop.transform);
                var backdropImage = backdrop.GetComponent<Image>();
                backdropImage.sprite = null;
                backdropImage.color = LadyLakePremiumStage.BackdropColor;
                backdropImage.raycastTarget = false;

                var host = new GameObject("CardHost", typeof(RectTransform));
                host.transform.SetParent(canvasGo.transform, false);
                Stretch((RectTransform)host.transform);

                var artCardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArtCardPrefabPath);
                if (artCardPrefab == null)
                    throw new InvalidOperationException("[LadyLakePremium] Missing real collection prefab " + ArtCardPrefabPath + ".");
                var cardInstance = (GameObject)PrefabUtility.InstantiatePrefab(artCardPrefab, host.transform);
                cardInstance.name = "ArtCard";
                var cardRect = (RectTransform)cardInstance.transform;
                cardRect.anchorMin = cardRect.anchorMax = new Vector2(.5f, .5f);
                cardRect.pivot = new Vector2(.5f, .5f);
                cardRect.anchoredPosition = Vector2.zero;
                cardRect.localRotation = Quaternion.identity;
                cardRect.localScale = Vector3.one;

                var eventGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                eventGo.transform.SetParent(root.transform, false);

                var stage = root.AddComponent<LadyLakePremiumStage>();
                stage.card = cardInstance.GetComponent<ArtCard>();
                LadyLakePremiumStage.ApplyGoldNeutralFrame(stage.card);
                stage.cardHost = (RectTransform)host.transform;
                // Keep the selected static painting visible until the dynamic view is ready.
                stage.staticArt = AssetDatabase.LoadAssetAtPath<Sprite>(StaticArtPath);
                if (stage.card != null && stage.card.CardImg != null && stage.staticArt != null)
                    stage.card.CardImg.sprite = stage.staticArt;

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException("[LadyLakePremium] Failed to save " + ScenePath + ".");
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            }
            AssetDatabase.SaveAssets();
        }

        private static void WriteCatalogEntry()
        {
            var entry = new DynamicCardEntry
            {
                id = CatalogSceneId,
                sourceId = CatalogSceneId,
                sourceVersion = "Authored",
                artIds = new[] { LadyLakePremiumStage.ArtId },
                prefab = AssembledPrefabPath,
                audio = EnvironmentAudioPath,
                pivot = "Pivot",
                fieldOfView = LadyLakePremiumFraming.FieldOfView,
                cameraDistance = LadyLakePremiumFraming.CameraDistance,
                nearClip = LadyLakePremiumFraming.NearClip,
                farClip = LadyLakePremiumFraming.FarClip,
                xStart = -6f, xEnd = 6f, yStart = -2f, yEnd = 2f,
                introDuration = 12f,
                loopDuration = 12f,
                cutTime = -1f,
                particleEvents = new DynamicCardParticleEvent[0],
                uvMotions = new DynamicCardUvMotion[0],
                transformPairs = new DynamicCardTransformPair[0],
                viewMotions = new DynamicCardViewMotion[0],
                cameraParents = new DynamicCardCameraParent[0],
                jiggles = new DynamicCardJiggle[0],
                wiggles = new DynamicCardWiggle[0],
                materialValues = new DynamicCardMaterialValue[0],
                initialTransforms = new DynamicCardInitialTransform[0],
                nonRenderingPaths = new string[0]
            };
            string fragmentPath = "Assets/Experiments/LadyLakePremium/Environment/catalog-fragment.json";
            if (File.Exists(fragmentPath))
            {
                var fragment = JsonUtility.FromJson<DynamicCardEntry>(File.ReadAllText(fragmentPath));
                entry.particleEvents = fragment.particleEvents ?? new DynamicCardParticleEvent[0];
                foreach (var item in entry.particleEvents) item.path = "Pivot/Environment/" + item.path;
                entry.uvMotions = fragment.uvMotions ?? new DynamicCardUvMotion[0];
                foreach (var item in entry.uvMotions) item.path = "Pivot/Environment/" + item.path;
            }
            var assembled = AssetDatabase.LoadAssetAtPath<GameObject>(AssembledPrefabPath);
            var marker = assembled.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Grip");
            if (marker == null) throw new InvalidOperationException("Sword Grip marker missing");
            entry.transformPairs = new[] { new DynamicCardTransformPair {
                source = AnimationUtility.CalculateTransformPath(marker, assembled.transform),
                target = "Pivot/Environment/SwordGlow"
            }, new DynamicCardTransformPair {
                source = AnimationUtility.CalculateTransformPath(marker, assembled.transform),
                target = "Pivot/Environment/CatchBubbles"
            } };
            entry.particleEvents = entry.particleEvents.Concat(new[] {new DynamicCardParticleEvent {
                path="Pivot/Environment/CatchBubbles", time=3.16f, loop=true, sourceTiming=true, phaseStart=0f, period=12f
            }}).ToArray();
            File.WriteAllText(CatalogEntryPath, JsonUtility.ToJson(entry, true), new UTF8Encoding(false));
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------
        private static void ResetLocal(Transform transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void InvokeWorker(string typeName, string methodName, string label, string expectedPath)
        {
            var type = FindType(typeName);
            if (type == null)
                throw new InvalidOperationException("[LadyLakePremium] Cannot assemble the premium card: the " +
                    label + " builder '" + typeName + "' is not compiled. Expected a public static " + methodName +
                    "() in Assets/Experiments/LadyLakePremium/" + expectedPath + ". The assembler does not substitute placeholders.");
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (method == null)
                throw new InvalidOperationException("[LadyLakePremium] " + label + " builder '" + typeName +
                    "' has no public static " + methodName + "().");
            try
            {
                method.Invoke(null, null);
            }
            catch (TargetInvocationException exception)
            {
                var inner = exception.InnerException ?? exception;
                throw new InvalidOperationException("[LadyLakePremium] " + label + " builder " + typeName + "." +
                    methodName + "() failed: " + inner.Message, inner);
            }
        }

        private static Type FindType(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(name, false);
                if (type != null) return type;
            }
            return null;
        }
    }
}

