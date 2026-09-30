using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Assets.Script.DynamicCards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LegacyGwent.LadyLakePremium.Editor
{
    /// <summary>
    /// Static/editor validation for the assembled premium card. It never enters Play Mode and never
    /// mutates production assets. Findings that can only be judged visually are listed explicitly
    /// in <c>unverifiedRuntime</c> instead of being silently reported as passing.
    /// </summary>
    public static class LadyLakePremiumValidator
    {
        public static LadyLakePremiumValidationReport Validate()
        {
            var report = new LadyLakePremiumValidationReport();

            report.assembledPrefabPath = LadyLakePremiumBuilder.AssembledPrefabPath;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(report.assembledPrefabPath);
            Check(report, prefab != null, "Assembled prefab exists: " + report.assembledPrefabPath);
            if (prefab == null)
            {
                report.failures.Add("Run LegacyGwent.LadyLakePremium.Editor.LadyLakePremiumBuilder.Build() first.");
                AddUnverifiedRuntime(report);
                AddFraming(report);
                ValidateScene(report);
                return report;
            }

            var root = prefab.transform;
            report.rootName = root.name;
            Check(report, root.name == "Card", "Prefab root is named 'Card'");

            var pivot = root.Find("Pivot");
            report.pivotPresent = pivot != null;
            Check(report, pivot != null, "Card/Pivot stage exists");
            if (pivot != null)
            {
                report.pivotLocalPosition = Format(pivot.localPosition);
                var expected = LadyLakePremiumFraming.PivotLocalPosition();
                Check(report, (pivot.localPosition - expected).magnitude < .0005f,
                    "Pivot carries the exact appearance-offset compensation " + Format(expected));
                Check(report, Mathf.Abs(pivot.localPosition.y + 2f) < .1f,
                    "Pivot compensates the production appearance anchor (y ~= -2)");
            }

            report.figureRigPresent = FindByName(root, "FigureRig") != null;
            report.environmentPresent = FindByName(root, "Environment") != null;
            Check(report, report.figureRigPresent, "Model worker prefab instance 'FigureRig' is nested under Pivot");
            Check(report, report.environmentPresent, "Environment worker prefab instance 'Environment' is nested under Pivot");

            report.prefabDependencies = AssetDatabase.GetDependencies(report.assembledPrefabPath, true)
                .OrderBy(p => p, StringComparer.Ordinal).ToArray();
            Check(report, report.prefabDependencies.Length > 2, "Prefab has resolvable dependencies (" +
                report.prefabDependencies.Length + ")");

            // Bones / skinning.
            var skins = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            report.skinnedMeshRendererCount = skins.Length;
            var boneSummaries = new List<string>();
            int totalBones = 0;
            foreach (var skin in skins)
            {
                int bones = skin.bones == null ? 0 : skin.bones.Length;
                totalBones += bones;
                boneSummaries.Add(DynamicCardPaths.RelativePath(skin.transform, root) + " -> bones=" + bones +
                    " rootBone=" + (skin.rootBone != null ? skin.rootBone.name : "null"));
            }
            report.boneSummaries = boneSummaries.ToArray();
            report.totalBoneCount = totalBones;
            Check(report, skinToAuthorCheck(skins), "Authored skinned mesh present with bound bones (skins=" +
                skins.Length + ", bones=" + totalBones + ")");

            var grip = FindByName(root, "GripSocket");
            report.gripSocketPresent = grip != null;
            report.gripSocketPath = grip != null ? DynamicCardPaths.RelativePath(grip, root) : null;
            Check(report, grip != null, "Named GripSocket marker is present on the rig");

            // Animator clips.
            var animators = prefab.GetComponentsInChildren<Animator>(true);
            var animatorSummaries = new List<string>();
            var clipNames = new HashSet<string>();
            foreach (var animator in animators)
            {
                var controller = animator.runtimeAnimatorController;
                var clips = controller != null ? controller.animationClips : null;
                if (clips != null) foreach (var clip in clips) if (clip != null) clipNames.Add(clip.name);
                animatorSummaries.Add(DynamicCardPaths.RelativePath(animator.transform, root) + " -> controller=" +
                    (controller != null ? controller.name : "null") + " clips=" +
                    (clips == null ? "none" : string.Join(",", clips.Where(c => c != null).Select(c => c.name).ToArray())));
            }
            report.animatorSummaries = animatorSummaries.ToArray();
            report.animationClipNames = clipNames.OrderBy(n => n, StringComparer.Ordinal).ToArray();
            Check(report, animators.Length > 0, "Animator component present on the assembled card");
            Check(report, clipNames.Count > 0, "Animator exposes animation clips (" + string.Join(",", report.animationClipNames) + ")");

            // Audio declared by the catalog entry.
            var audioClips = new HashSet<string>();
            foreach (var source in prefab.GetComponentsInChildren<AudioSource>(true))
                if (source.clip != null) audioClips.Add(source.clip.name);
            report.audioClips = audioClips.OrderBy(n => n, StringComparer.Ordinal).ToArray();
            var environmentAudio = AssetDatabase.LoadAssetAtPath<AudioClip>(LadyLakePremiumBuilder.EnvironmentAudioPath);
            report.environmentAudioPresent = environmentAudio != null;
            Check(report, environmentAudio != null, "Environment audio exists: " + LadyLakePremiumBuilder.EnvironmentAudioPath);

            // Integrities.
            report.missingScriptComponents = CountMissingScripts(root);
            Check(report, report.missingScriptComponents == 0, "No missing script components on the assembled card");
            report.nullMaterialSlots = CountNullMaterialSlots(prefab);
            Check(report, report.nullMaterialSlots == 0, "No null material slots (no pink fallback surfaces)");

            AddFraming(report);
            ValidateScene(report);
            AddUnverifiedRuntime(report);
            return report;
        }

        private static bool skinToAuthorCheck(SkinnedMeshRenderer[] skins)
        {
            if (skins == null || skins.Length == 0) return false;
            foreach (var skin in skins)
                if (skin.bones != null && skin.bones.Length > 0 && skin.sharedMesh != null) return true;
            return false;
        }

        private static void AddFraming(LadyLakePremiumValidationReport report)
        {
            report.fieldOfView = LadyLakePremiumFraming.FieldOfView;
            report.cameraDistance = LadyLakePremiumFraming.CameraDistance;
            report.artRegionWorldWidth = LadyLakePremiumFraming.ArtRegionWorldWidth();
            report.artRegionWorldHeight = LadyLakePremiumFraming.ArtRegionWorldHeight();
            report.paintingWorldWidth = LadyLakePremiumFraming.PaintingWidthUnits;
            report.paintingWorldHeight = LadyLakePremiumFraming.PaintingHeightUnits;
            report.paintingHeightFits = report.paintingWorldHeight <= report.artRegionWorldHeight + 1e-4f;
            Check(report, report.paintingHeightFits,
                "497x710 painting (7.10 world high) fits the unchanged ArtRegion (" +
                report.artRegionWorldHeight.ToString("F4") + " world high)");
            Check(report, report.paintingWorldWidth <= report.artRegionWorldWidth + .05f,
                "497-wide painting is within the ArtRegion width by height-fit (" +
                report.artRegionWorldWidth.ToString("F4") + " world wide; <=0.4% horizontal crop)");
        }

        private static void ValidateScene(LadyLakePremiumValidationReport report)
        {
            report.scenePath = LadyLakePremiumBuilder.ScenePath;
            report.scenePresent = File.Exists(report.scenePath);
            Check(report, report.scenePresent, "Standalone scene exists: " + report.scenePath);
            if (!report.scenePresent || EditorApplication.isPlaying) return;

            Scene scene = SceneManager.GetSceneByPath(report.scenePath);
            bool opened = false;
            try
            {
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(report.scenePath, OpenSceneMode.Additive);
                    opened = true;
                }
                var roots = scene.GetRootGameObjects();
                var stage = roots.Select(r => r.GetComponentInChildren<LadyLakePremiumStage>(true)).FirstOrDefault(s => s != null);
                var card = roots.Select(r => r.GetComponentInChildren<ArtCard>(true)).FirstOrDefault(c => c != null);
                var canvas = roots.Select(r => r.GetComponentInChildren<Canvas>(true)).FirstOrDefault(c => c != null);
                var eventSystem = roots.Select(r => r.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true)).FirstOrDefault(e => e != null);

                report.sceneHasStage = stage != null;
                report.sceneHasArtCard = card != null;
                report.sceneHasCanvas = canvas != null;
                report.sceneHasEventSystem = eventSystem != null;
                report.sceneStaticArtAssigned = card != null && card.CardImg != null && card.CardImg.sprite != null;
                report.sceneHasDynamicPresentation = card != null && card.GetComponent<DynamicCardPresentation>() != null;
                report.sceneHasDynamicView = card != null && card.CardImg != null && card.CardImg.GetComponent<DynamicCardView>() != null;

                Check(report, stage != null, "Scene has LadyLakePremiumStage");
                Check(report, card != null, "Scene has the real collection ArtCard instance");
                Check(report, card == null || card.CardBorder != null && card.GoldBorder != null && card.CardBorder.sprite == card.GoldBorder,
                    "ArtCard uses the original gold neutral border sprite");
                Check(report, card == null || card.FactionIcon != null && card.NeutralGoldIcon != null && card.FactionIcon.sprite == card.NeutralGoldIcon,
                    "ArtCard uses the original neutral gold faction icon");
                Check(report, canvas != null, "Scene has the card canvas");
                Check(report, eventSystem != null, "Scene has an EventSystem for the production drag handle");
                Check(report, report.sceneStaticArtAssigned, "Static c10000000 fallback sprite is assigned before Play");
                Check(report, card == null || card.CardImg == null || card.CardImg.raycastTarget == false, "CardImg does not receive raycasts");
                Check(report, card == null || card.Content == null || !card.Content.gameObject.activeSelf,
                    "No rules/name banner text overlay in the standalone scene");
            }
            catch (Exception exception)
            {
                report.failures.Add("Scene inspection failed: " + exception.Message);
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void AddUnverifiedRuntime(LadyLakePremiumValidationReport report)
        {
            report.unverifiedRuntime = new[]
            {
                "Rendered visual parity against figure-v5.png (face, hair, gold skin) was not produced here.",
                "The 12s cycle (open hand 0-1.3s, intercept, fingers close at 3.2s, lift, serene hold 5.6-8.3s, release) is authored in the model clips and is not evaluated statically.",
                "Animator Intro -> Loop transition continuity and the seamless 10.8-12s return are not evaluated statically.",
                "Reused premium shader/material appearance and the environment particle timing are not rendered here.",
                "DynamicCardView.ActualPlay framing can only be confirmed in Play Mode after the root merges Integration/catalog-entry.json and rebuilds the dynamic card packages.",
                "The editor bundled cache takes precedence over AssetDatabase source fallback, so a stale cards.bundle would show static art only.",
                "Audio loop loudness/attribution was validated by the environment worker, not by this integration check."
            };
            report.runtimeChecklist = new[]
            {
                "DynamicCardSettings is enabled for the scene and the previous tier is restored on destroy.",
                "DynamicCardView.Bind is called with largePreview=true, premium=true, wholeCard=CardBorder, presentationRoot=card.transform.",
                "The scene keeps the static c10000000 sprite if the dynamic package is absent.",
                "No custom RenderTexture backend, no reflection-injected library entries, no cloned player/camera/drag code."
            };
        }

        private static int CountMissingScripts(Transform root)
        {
            int count = 0;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                foreach (var component in transform.GetComponents<Component>())
                    if (component == null) count++;
            return count;
        }

        private static int CountNullMaterialSlots(GameObject prefab)
        {
            int count = 0;
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                if (materials == null) continue;
                foreach (var material in materials) if (material == null) count++;
            }
            return count;
        }

        private static Transform FindByName(Transform root, string name)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.name == name) return transform;
            return null;
        }

        private static string Format(Vector3 value)
        {
            return "(" + value.x.ToString("F6") + ", " + value.y.ToString("F6") + ", " + value.z.ToString("F6") + ")";
        }

        private static void Check(LadyLakePremiumValidationReport report, bool ok, string what)
        {
            report.checks.Add((ok ? "PASS  " : "FAIL  ") + what);
            if (!ok) report.failures.Add(what);
        }
    }

    /// <summary>Serializable validation result written to Integration/validation-report.json.</summary>
    [Serializable]
    public sealed class LadyLakePremiumValidationReport
    {
        public string generatedAtUtc;
        public string assembledPrefabPath;
        public string rootName;
        public bool pivotPresent;
        public string pivotLocalPosition;
        public bool figureRigPresent;
        public bool environmentPresent;
        public string[] prefabDependencies = new string[0];
        public int skinnedMeshRendererCount;
        public int totalBoneCount;
        public string[] boneSummaries = new string[0];
        public string[] animatorSummaries = new string[0];
        public string[] animationClipNames = new string[0];
        public bool gripSocketPresent;
        public string gripSocketPath;
        public string[] audioClips = new string[0];
        public bool environmentAudioPresent;
        public int missingScriptComponents;
        public int nullMaterialSlots;
        public float fieldOfView;
        public float cameraDistance;
        public float artRegionWorldWidth;
        public float artRegionWorldHeight;
        public float paintingWorldWidth;
        public float paintingWorldHeight;
        public bool paintingHeightFits;
        public string scenePath;
        public bool scenePresent;
        public bool sceneHasStage;
        public bool sceneHasArtCard;
        public bool sceneHasCanvas;
        public bool sceneHasEventSystem;
        public bool sceneStaticArtAssigned;
        public bool sceneHasDynamicPresentation;
        public bool sceneHasDynamicView;
        public System.Collections.Generic.List<string> checks = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<string> failures = new System.Collections.Generic.List<string>();
        public string[] unverifiedRuntime = new string[0];
        public string[] runtimeChecklist = new string[0];

        public LadyLakePremiumValidationReport()
        {
            generatedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        }

        public string Summary()
        {
            return "checks=" + (checks == null ? 0 : checks.Count) + " failures=" + (failures == null ? 0 : failures.Count) +
                " bones=" + totalBoneCount + " clips=" + (animationClipNames == null ? 0 : animationClipNames.Length) +
                " unverifiedRuntime=" + (unverifiedRuntime == null ? 0 : unverifiedRuntime.Length);
        }
    }
}
