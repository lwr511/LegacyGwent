using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Assets.Script.DynamicCards;
using LegacyGwent.LadyLakePremium;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LegacyGwent.LadyLakePremium.Editor
{
    /// <summary>
    /// Lady Lake premium ENVIRONMENT builder.
    ///
    /// The environment is a faithful 2.5D presentation of the two paintings the
    /// card was drawn from:
    ///
    ///   Assets/Experiments/LadyLake/Textures/background.png  (1049x1499, the whole
    ///                                                         underwater scene)
    ///   Assets/Experiments/LadyLake/Textures/foreground.png  (1049x1499, straight
    ///                                                         alpha, the lily pads,
    ///                                                         stems and leaf fan)
    ///
    /// Each painting is carried by ONE full-frame relief plate whose mesh UV is the
    /// painting's own normalised coordinate, so the art is reproduced pixel for
    /// pixel instead of being cut into strips. The plate is registered to the shared
    /// contract frame, which is what keeps the painted water, the lily pads and the
    /// leaf fan aligned with the model rig:
    ///
    ///   P(px, py, z) = ((px - 248.5) / 100, (355 - py) / 100, z)
    ///   painting frame: 497 x 710 px -> x in [-2.485, 2.485], y in [-3.55, 3.55]
    ///
    /// The plate geometry is slightly larger than the 497x710 frame (a 0.4 % margin
    /// past the 35 deg / 11.5 u frustum) and the UV mirrors back inside the frame
    /// outside it, so the extra ring is a seamless continuation of the painting
    /// rather than the camera clear colour. The production ArtRegion is narrower than
    /// the full frustum, so that ring is cropped there and the painting registers
    /// exactly with the model rig. Vertex depth is perspective compensated
    /// (1 + z / cameraDistance) so an off-plane vertex still projects exactly where
    /// the painting puts it.
    ///
    /// Everything else is an accent that cannot damage the painting:
    ///   * the two additive water layers (WaterbedCaustics, CausticSheen) are
    ///     full-frame plates whose vertex alpha is baked from the BACKGROUND
    ///     PAINTING'S OWN LUMINANCE, so they only glow where the art is already
    ///     luminous. There is no plate edge anywhere, which is what removes the old
    ///     hard horizontal waterbed seam and the flat green upper third.
    ///   * SunRays is a handful of soft-sided ribbons at a whisper alpha: the
    ///     painting already carries the shafts, the layer only adds drift.
    ///   * the plant plate uses the original Aeschna wavey foliage shader with a
    ///     WHITE tint, no main-UV scroll, and a generated _WaveyMask derived from
    ///     the plant painting's own alpha, so only the stems wave and the lily pads
    ///     stay rigid.
    ///
    /// Deliverables:
    ///   * Assets/Experiments/LadyLakePremium/Environment/Environment.prefab
    ///   * generated 3D mesh assets in Environment/Meshes
    ///   * generated materials in Environment/Materials (original DynamicCards shaders)
    ///   * byte-identical copies of the source textures in Environment/Textures,
    ///     plus the generated Environment/Textures/Env_SwayMask.png
    ///   * Environment/catalog-fragment.json (production event fragment)
    ///   * Environment/environment-build.json (machine readable build report)
    ///
    /// The prefab keeps the object names the rest of the pipeline looks up:
    /// <c>SwordGlow</c> and <c>CausticSheen</c> as direct children (Integration's
    /// LadyLakeProductionEffects and the catalogue transform pair), and
    /// <c>Bubbles</c>/<c>Motes</c>/<c>Glints</c>/<c>WaterbedCaustics</c> at the root
    /// for the catalogue particle events and UV motions.
    ///
    /// It never builds a character, a sword, an AudioListener, a second AudioSource,
    /// a Camera or a Light, and it never touches the audio assets. Audio is
    /// delivered through the production catalog `audio` field, so the environment
    /// only ships the wav file.
    /// </summary>
    public static class LadyLakeEnvironmentBuilder
    {
        public const string Root = "Assets/Experiments/LadyLakePremium/Environment";
        public const string MeshFolder = Root + "/Meshes";
        public const string MaterialFolder = Root + "/Materials";
        public const string TextureFolder = Root + "/Textures";
        public const string AudioFolder = Root + "/Audio";
        public const string PrefabPath = Root + "/Environment.prefab";
        public const string ControllerPath = Root + "/LadyLakeEnvironment.controller";
        public const string ClipIntroPath = Root + "/LadyLakeEnvironment_Intro.anim";
        public const string ClipLoopPath = Root + "/LadyLakeEnvironment_Loop.anim";
        public const string FragmentPath = Root + "/catalog-fragment.json";
        public const string BuildReportPath = Root + "/environment-build.json";
        public const string AudioLoopPath = AudioFolder + "/LadyLakeLoop.wav";
        public const string SwayMaskPath = TextureFolder + "/Env_SwayMask.png";

        public const float CycleDuration = 12f;
        public const float CameraDistance = 11.5f;

        // ------------------------------------------------------------------
        // Shared frame (see the class comment). The capture and the production
        // ArtRegion both look through a 35 deg lens at 11.5 units.
        // ------------------------------------------------------------------
        private const float FieldOfView = 35f;
        private const float PaintingWidth = 4.97f;      // 497 px / 100
        private const float PaintingHeight = 7.10f;     // 710 px / 100
        private const float HalfPaintingW = PaintingWidth * 0.5f;
        private const float HalfPaintingH = PaintingHeight * 0.5f;

        private static readonly float FrustumHalfHeight = CameraDistance * Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
        private static readonly float FrustumHalfWidth = FrustumHalfHeight * (714f / 1024f);

        // The plates run a fraction of a percent past the frustum so the mirrored UV
        // ring can never show the camera background. The ring is kept as thin as
        // possible because the 497x710 painting is a hair shorter than the 7.2519 u
        // frustum, so the ring is what fills that difference.
        private static readonly float PlateHalfW = FrustumHalfWidth * 1.004f;
        private static readonly float PlateHalfH = FrustumHalfHeight * 1.004f;

        // ------------------------------------------------------------------
        // Depth planes. +z is away from the camera (camera at z = -11.5). The model
        // rig lives in z in [-0.25, +0.25] with the blade at z ~ -0.14, so the water
        // layers sit behind it and the plant painting in front of it.
        // ------------------------------------------------------------------
        private const float ZBackdrop = 1.55f;
        private const float ZBedCaustics = 0.95f;
        private const float ZSunRays = 0.80f;
        private const float ZBubbles = 0.65f;
        private const float ZSheen = 0.50f;
        private const float ZMotes = 0.30f;
        private const float ZGlints = -0.10f;
        private const float ZSwordGlow = -0.20f;
        private const float ZPlants = -0.34f;

        // ------------------------------------------------------------------
        // Render queues. Everything here is transparent (ZWrite Off, ZTest LEqual),
        // so the opaque model still occludes the water and is still occluded by the
        // plant plate; the queue only orders the environment's own layers.
        // ------------------------------------------------------------------
        private const int QBackdrop = 2990;
        private const int QBedCaustics = 2994;
        private const int QSunRays = 2996;
        private const int QSheen = 2998;
        private const int QBubbles = 2999;
        private const int QMotes = 3000;
        private const int QGlints = 3003;
        private const int QPlants = 3006;
        private const int QSwordGlow = 3021;

        // ------------------------------------------------------------------
        // Source assets (read only). The Aeschna 15010100 scene is the only
        // original premium card with a numbered underwater layer set.
        // ------------------------------------------------------------------
        private const string SrcBackground = "Assets/Experiments/LadyLake/Textures/background.png";
        private const string SrcForeground = "Assets/Experiments/LadyLake/Textures/foreground.png";
        private const string ContentTb = "Assets/DynamicCards/Content/Old/Thronebreaker";
        private const string SrcCaustics = ContentTb + "/OriginalTextures/74809d523530d3f7696d6e455aeca9dd.png";
        private const string SrcBubbles = ContentTb + "/OriginalTextures/963cbb367e53ed41e9e55e8a50c83fc5.png";
        private const string SrcShafts = ContentTb + "/OriginalTextures/9fc0f47eaffcb437f12633105906e41b.png";
        private const string SrcNoiseTiling = ContentTb + "/Shared/13890100_105470_TilingNoise3D_small_uncompressed.png";
        private const string SrcNoiseForce = ContentTb + "/Shared/14990100_163662_Noise3D_ForceTiling.png";
        private const string FbCaustics = "Assets/Experiments/LadyLake/Textures/AeschnaCaustics.png";
        private const string FbBubbles = "Assets/Experiments/LadyLake/Textures/AeschnaBubbles.png";
        private const string FbNoise = "Assets/Experiments/LadyLake/Textures/AeschnaFlowNoise.png";

        // Original DynamicCards shaders (already in the repository, built-in RP).
        private const string ShAlphaUvNoise = "DynamicCards/Native/VFX_Common_AlphaBlended_moveInDirection_VertexColor_UVNoise";
        private const string ShAddUvNoise = "DynamicCards/Native/VFX_Common_Additive_moveInDirection_VertexColor_UVNoise";
        private const string ShAddUvNoiseLegacy = "DynamicCards/Legacy/VFX_Common_Additive_moveInDirection_VertexColor_UVNoise";
        private const string ShWavey2Sided = "DynamicCards/Legacy/VFX_Common_AlphaBlended_moveInDirection_VertexColor_Wavey_2Sided";
        private const string ShAlphaUvNoisePath = "Assets/DynamicCards/Shaders/Native/VFX_Common_AlphaBlended_moveInDirection_VertexColor_UVNoise.shader";
        private const string ShAddUvNoisePath = "Assets/DynamicCards/Shaders/Native/VFX_Common_Additive_moveInDirection_VertexColor_UVNoise.shader";
        private const string ShAddUvNoiseLegacyPath = "Assets/DynamicCards/Shaders/Legacy/VFX_Common_Additive_moveInDirection_VertexColor_UVNoise.shader";
        private const string ShWavey2SidedPath = "Assets/DynamicCards/Shaders/Legacy/VFX_Common_AlphaBlended_moveInDirection_VertexColor_Wavey_2Sided.shader";

        private sealed class BuildState
        {
            public List<string> verified = new List<string>();
            public List<string> warnings = new List<string>();
            public List<string> unverified = new List<string>();
            public List<MeshStat> meshes = new List<MeshStat>();
            public List<MaterialStat> materials = new List<MaterialStat>();
            public List<CopyStat> copies = new List<CopyStat>();
            public List<string> overrides = new List<string>();
        }

        [Serializable]
        private sealed class MeshStat
        {
            public string name;
            public string path;
            public int vertices;
            public int triangles;
            public int subMeshes;
            public string boundsCenter;
            public string boundsSize;
        }

        [Serializable]
        private sealed class MaterialStat
        {
            public string name;
            public string shader;
            public int queue;
            public string provenance;
        }

        [Serializable]
        private sealed class CopyStat
        {
            public string destination;
            public string source;
            public long bytes;
            public string kind;
        }

        // ------------------------------------------------------------------
        // Report payloads (JsonUtility)
        // ------------------------------------------------------------------
        [Serializable]
        private sealed class Report
        {
            public string generatedAtUtc;
            public string unityVersion;
            public string prefabPath;
            public string controllerPath;
            public float cycleDuration;
            public float cameraDistance;
            public string audioPath;
            public string[] verified;
            public string[] warnings;
            public string[] unverified;
            public MeshStat[] meshes;
            public MaterialStat[] materials;
            public CopyStat[] copies;
            public string[] groupingOverrides;
        }

        [Serializable]
        private sealed class FragmentParticleEvent
        {
            public string path;
            public float time;
            public bool loop;
            public bool sourceTiming;
            public float phaseStart;
            public float period;
        }

        [Serializable]
        private sealed class FragmentUvMotion
        {
            public string path;
            public string property;
            public int materialIndex;
            public Vector2 speed;
            public bool forceStart;
            public Vector2 start;
        }

        [Serializable]
        private sealed class Fragment
        {
            public string id;
            public string[] artIds;
            public string audio;
            public float introDuration;
            public float loopDuration;
            public string prefab;
            public string pivot;
            public string pathNote;
            public FragmentParticleEvent[] particleEvents;
            public FragmentUvMotion[] uvMotions;
        }

        // ------------------------------------------------------------------
        // Entry points
        // ------------------------------------------------------------------

        [MenuItem("Tools/Lady Lake Premium/Build Environment", false, 20)]
        public static void MenuBuild()
        {
            Build();
        }

        /// <summary>Public static entry required by the shared contract.</summary>
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("[LadyLakeEnv] Build must run in edit mode.");

            BuildState state = new BuildState();
            state.unverified.Add("Visual acceptance: nothing was rendered by this builder. A Unity render/capture is required (root owns it).");
            state.unverified.Add("ParticleSystem appearance with the original premium shaders was not observed; only the created assets and parameters were verified.");
            state.unverified.Add("The catalog fragment paths are relative to the Environment prefab root; Integration/root must prefix them with the real instance path inside Card.prefab.");
            state.unverified.Add("The builder never enters play mode, so Animator transitions/clip playback were not observed at runtime.");
            state.unverified.Add("The model rig is authored by another worker; GripSocket/Grip presence is discovered at runtime and was not verified here.");

            EnsureFolder(Root);
            EnsureFolder(MeshFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(TextureFolder);
            EnsureFolder(AudioFolder);

            // ---- 1. copy original source textures unchanged ------------------
            string texBackground = CopyTexture(SrcBackground, TextureFolder + "/Env_Background.png",
                TextureWrap.Clamp, false, TextureImporterCompression.Uncompressed, true, state,
                "selected custom art / old experiment backdrop painting (byte-identical copy, whole-plate source)");
            string texForeground = CopyTexture(SrcForeground, TextureFolder + "/Env_Foreground.png",
                TextureWrap.Clamp, true, TextureImporterCompression.Uncompressed, true, state,
                "selected custom art / old experiment foreground plants (byte-identical copy, whole-plate source, straight alpha)");
            string texCaustics = CopyTexture(SrcCaustics, TextureFolder + "/AeschnaCaustics.png",
                TextureWrap.Repeat, false, TextureImporterCompression.Compressed, false, state,
                "Aeschna 15010100 RiverbedCaustics _MainTex");
            string texBubbles = CopyTexture(SrcBubbles, TextureFolder + "/AeschnaBubbles.png",
                TextureWrap.Repeat, false, TextureImporterCompression.Compressed, false, state,
                "Aeschna 15010100 FX_Bubbles _MainTex");
            string texShafts = CopyTexture(SrcShafts, TextureFolder + "/AeschnaShafts.png",
                TextureWrap.Repeat, false, TextureImporterCompression.Compressed, false, state,
                "Aeschna 15010100 Shafts _MainTex");
            string texNoiseTiling = CopyTexture(SrcNoiseTiling, TextureFolder + "/AeschnaNoiseTiling.png",
                TextureWrap.Repeat, false, TextureImporterCompression.Compressed, false, state,
                "Aeschna FX noise _NoiseTex");
            string texNoiseForce = CopyTexture(SrcNoiseForce, TextureFolder + "/AeschnaNoiseForceTiling.png",
                TextureWrap.Repeat, false, TextureImporterCompression.Compressed, false, state,
                "Aeschna caustics _NoiseTex");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Texture2D tBackground = Load<Texture2D>(texBackground);
            Texture2D tForeground = Load<Texture2D>(texForeground);
            Texture2D tCaustics = Load<Texture2D>(texCaustics);
            Texture2D tBubbles = Load<Texture2D>(texBubbles);
            Texture2D tShafts = Load<Texture2D>(texShafts);
            Texture2D tNoiseTiling = Load<Texture2D>(texNoiseTiling);
            Texture2D tNoiseForce = Load<Texture2D>(texNoiseForce);

            Require(tBackground != null, "background texture copy failed: " + texBackground);
            Require(tForeground != null, "foreground texture copy failed: " + texForeground);
            Require(tCaustics != null, "caustics texture copy failed: " + texCaustics);
            Require(tShafts != null, "shafts texture copy failed: " + texShafts);

            // ---- 2. sample the paintings -------------------------------------
            // Both plates are imported readable so the accents can be gated by the
            // art itself instead of by hand cut rectangles that seam.
            Field artLuma = Field.FromTexture(tBackground, 96, 137, false);
            artLuma.Blur(3, 2);
            Field plantAlpha = Field.FromTexture(tForeground, 168, 240, true);
            plantAlpha.Blur(1, 1);
            Field plantAlphaSoft = Field.FromTexture(tForeground, 96, 137, true);
            plantAlphaSoft.Blur(4, 2);
            state.verified.Add("Accent gates are sampled from background.png's own luminance and the plant wave " +
                "mask from foreground.png's own alpha; nothing is gated by a rectangle with a visible edge.");

            // ---- 3. generated sway mask --------------------------------------
            Texture2D tSwayMask = BuildSwayMask(plantAlphaSoft, state);

            // ---- 4. shaders ---------------------------------------------------
            Shader shAlphaUv = RequireShader(ShAlphaUvNoise, ShAlphaUvNoisePath);
            Shader shAddUv = RequireShader(ShAddUvNoise, ShAddUvNoisePath);
            Shader shAddUvLegacy = RequireShader(ShAddUvNoiseLegacy, ShAddUvNoiseLegacyPath);
            Shader shWavey = RequireShader(ShWavey2Sided, ShWavey2SidedPath);
            state.verified.Add("All materials use original DynamicCards shaders imported from the real premium pipeline; no new shader was written.");

            // ---- 5. meshes ----------------------------------------------------
            Mesh meshBackdrop = SaveMesh("Env_Backdrop", BuildBackdrop(artLuma), state);
            Mesh meshBedCaustics = SaveMesh("Env_WaterbedCaustics", BuildBedCaustics(artLuma), state);
            Mesh meshSheen = SaveMesh("Env_CausticSheen", BuildCausticSheen(artLuma), state);
            Mesh meshSunRays = SaveMesh("Env_SunRays", BuildSunRays(), state);
            Mesh meshPlants = SaveMesh("Env_Foreground", BuildPlants(plantAlpha), state);
            Mesh meshGlow = SaveMesh("Env_SwordGlowHalo", BuildGlowHalo(), state);
            Mesh meshParticle = SaveMesh("Env_ParticleDisc", BuildParticleDisc(), state);

            // ---- 6. materials -------------------------------------------------
            Material matBackdrop = MakeMaterial("LadyLake_Env_Backdrop", shAlphaUv, QBackdrop, state,
                "Aeschna _0_Riverbed / WaterSurface shader configuration, pass-through tint, carrying the whole 497x710 backdrop painting at 1:1");
            Material matBedCaustics = MakeMaterial("LadyLake_Env_WaterbedCaustics", shAddUv, QBedCaustics, state,
                "Aeschna _1_RiverbedCaustics configuration; the bed gate is baked into mesh vertex alpha from the painting's own luminance so the layer has no plate edge");
            Material matSheenMat = MakeMaterial("LadyLake_Env_CausticSheen", shAddUv, QSheen, state,
                "Aeschna RiverbedCaustics shader at the production sheen level (_AlphaMult .26; Integration's SheenAnimation drives .12-.40)");
            Material matSunRays = MakeMaterial("LadyLake_Env_SunRays", shAddUv, QSunRays, state,
                "Aeschna _Aeschna_Shafts parameters at a whisper level; the painting already carries the shafts, this layer only drifts");
            Material matPlants = MakeMaterial("LadyLake_Env_Foreground", shWavey, QPlants, state,
                "Aeschna _Aeschna_FoliageFront wavey foliage shader carrying the whole plant painting: white tint, no main-UV scroll, art-weighted _WaveyMask");
            Material matBubbles = MakeMaterial("LadyLake_Env_Bubbles", shAlphaUv, QBubbles, state,
                "Aeschna _Aeschna_FX_Bubbles parameters");
            Material matMotes = MakeMaterial("LadyLake_Env_Motes", shAlphaUv, QMotes, state,
                "Aeschna _Aeschna_FX_FloatingDirt / noise configuration");
            Material matGlint = MakeMaterial("LadyLake_Env_Glint", shAddUv, QGlints, state,
                "Aeschna caustics additive shader, small sparkle variant");
            Material matSwordGlow = MakeMaterial("LadyLake_Env_SwordGlow", shAddUvLegacy, QSwordGlow, state,
                "Seltkirk 20161801 SwordGlow: same legacy additive shader, queue 3021, idle _AlphaMult .10 driven to 1.0 by Integration's GlowAnimation");

            // Backdrop: an exact, untinted pass-through of the painting.
            ConfigureTextures(matBackdrop, tBackground, tNoiseForce);
            ZeroMainScroll(matBackdrop);
            matBackdrop.SetTextureScale("_MainTex", Vector2.one);
            matBackdrop.SetFloat("_NoiseMult", 0.012f);
            matBackdrop.SetFloat("_NoiseU", 0.006f);
            matBackdrop.SetFloat("_NoiseV", 0.010f);
            matBackdrop.SetFloat("_NoiseMultOverLifetime", 0f);
            matBackdrop.SetFloat("_ColorGain", 1f);
            matBackdrop.SetFloat("_AlphaMult", 1f);
            matBackdrop.SetFloat("_AlphaPower", 1f);
            matBackdrop.SetColor("_TintColor", Color.white);

            ConfigureTextures(matBedCaustics, tCaustics, tNoiseForce);
            matBedCaustics.SetTextureScale("_MainTex", Vector2.one);   // tiling is baked into the mesh UV
            matBedCaustics.SetFloat("_ColorGain", 0.50f);
            matBedCaustics.SetFloat("_AlphaMult", 0.62f);
            matBedCaustics.SetFloat("_AlphaPower", 1f);
            matBedCaustics.SetFloat("_NoiseMult", 0.12f);
            matBedCaustics.SetFloat("_MainU", 0.014f);
            matBedCaustics.SetFloat("_MainV", 0.022f);
            matBedCaustics.SetFloat("_NoiseU", -0.030f);
            matBedCaustics.SetFloat("_NoiseV", -0.024f);
            matBedCaustics.SetColor("_TintColor", new Color(1f, 1f, 0.55294118f, 1f));

            ConfigureTextures(matSheenMat, tCaustics, tNoiseTiling);
            matSheenMat.SetTextureScale("_MainTex", Vector2.one);
            matSheenMat.SetFloat("_ColorGain", 0.70f);
            matSheenMat.SetFloat("_AlphaMult", 0.26f);
            matSheenMat.SetFloat("_AlphaPower", 1f);
            matSheenMat.SetFloat("_NoiseMult", 0.25f);
            matSheenMat.SetFloat("_MainU", 0.020f);
            matSheenMat.SetFloat("_MainV", 0.034f);
            matSheenMat.SetFloat("_NoiseU", 0.012f);
            matSheenMat.SetFloat("_NoiseV", 0.018f);
            matSheenMat.SetColor("_TintColor", new Color(0.62f, 1f, 0.88f, 1f));

            ConfigureTextures(matSunRays, tShafts, tNoiseTiling);
            matSunRays.SetTextureScale("_MainTex", new Vector2(0.7f, 1.5f));
            matSunRays.SetFloat("_ColorGain", 0.75f);
            matSunRays.SetFloat("_AlphaMult", 0.35f);
            matSunRays.SetFloat("_AlphaPower", 1f);
            matSunRays.SetFloat("_NoiseMult", 0.30f);
            matSunRays.SetFloat("_MainU", 0.012f);
            matSunRays.SetFloat("_MainV", -0.030f);
            matSunRays.SetFloat("_NoiseU", -0.040f);
            matSunRays.SetFloat("_NoiseV", 0.030f);
            matSunRays.SetColor("_TintColor", new Color(0.97f, 1f, 0.72f, 1f));

            // Plants: the painting must reach the screen untouched, so the tint is
            // pure white and neither the main UV nor the vertex wave may slide the
            // art sideways (that is what produced the old striped stems).
            matPlants.SetTexture("_MainTex", tForeground);
            matPlants.SetTexture("_Turbulence", tNoiseTiling);
            matPlants.SetTexture("_WaveyMask", tSwayMask != null ? (Texture)tSwayMask : Texture2D.whiteTexture);
            matPlants.SetColor("_TintColor", Color.white);
            matPlants.SetFloat("_AlphaMult", 1f);
            matPlants.SetFloat("_AlphaPower", 1f);
            matPlants.SetFloat("_Cutoff", 0.5f);
            matPlants.SetFloat("_VertexOffset", 0.12f);
            ZeroMainScroll(matPlants);
            matPlants.SetFloat("_TurbU", 0.010f);
            matPlants.SetFloat("_TurbV", 0.004f);
            matPlants.SetTextureScale("_MainTex", Vector2.one);
            matPlants.SetTextureOffset("_MainTex", Vector2.zero);
            matPlants.SetTextureScale("_Turbulence", Vector2.one);
            matPlants.SetTextureScale("_WaveyMask", Vector2.one);
            matPlants.SetTextureOffset("_Turbulence", Vector2.zero);
            matPlants.SetTextureOffset("_WaveyMask", Vector2.zero);

            ConfigureTextures(matBubbles, tBubbles, tNoiseTiling);
            matBubbles.SetFloat("_ColorGain", 1f);
            matBubbles.SetFloat("_AlphaMult", 0.95f);
            matBubbles.SetFloat("_AlphaPower", 1.25f);
            matBubbles.SetFloat("_NoiseMult", 0.16f);
            matBubbles.SetFloat("_NoiseU", 0.30f);
            matBubbles.SetFloat("_NoiseV", 0.24f);
            matBubbles.SetColor("_TintColor", new Color(0.86f, 1f, 0.98f, 1f));

            ConfigureTextures(matMotes, tBubbles, tNoiseForce);
            matMotes.SetFloat("_ColorGain", 1f);
            matMotes.SetFloat("_AlphaMult", 0.42f);
            matMotes.SetFloat("_AlphaPower", 1.70f);
            matMotes.SetFloat("_NoiseMult", 0.28f);
            matMotes.SetFloat("_NoiseU", 0.10f);
            matMotes.SetFloat("_NoiseV", -0.08f);
            matMotes.SetColor("_TintColor", new Color(0.78f, 0.98f, 0.86f, 1f));

            ConfigureTextures(matGlint, tCaustics, tNoiseTiling);
            matGlint.SetFloat("_ColorGain", 3.0f);
            matGlint.SetFloat("_AlphaMult", 0.35f);
            matGlint.SetFloat("_AlphaPower", 1f);
            matGlint.SetFloat("_NoiseMult", 0.22f);
            matGlint.SetFloat("_NoiseU", 0.22f);
            matGlint.SetFloat("_NoiseV", 0.16f);
            matGlint.SetColor("_TintColor", new Color(0.86f, 1f, 0.96f, 1f));

            ConfigureTextures(matSwordGlow, tCaustics, tNoiseForce);
            matSwordGlow.SetTextureScale("_MainTex", Vector2.one);
            matSwordGlow.SetFloat("_ColorGain", 2.0f);
            matSwordGlow.SetFloat("_AlphaMult", 0.10f);
            matSwordGlow.SetFloat("_AlphaPower", 1f);
            matSwordGlow.SetFloat("_AlphaOverride", 1f);
            matSwordGlow.SetFloat("_NoiseMult", 0.10f);
            matSwordGlow.SetFloat("_MainU", 0.020f);
            matSwordGlow.SetFloat("_MainV", 0.050f);
            matSwordGlow.SetFloat("_NoiseU", 0f);
            matSwordGlow.SetFloat("_NoiseV", -0.35f);
            matSwordGlow.SetColor("_TintColor", new Color(0.80f, 1f, 0.99f, 1f));

            // ---- 7. animator assets -------------------------------------------
            AnimationClip intro = BuildClip("LadyLakeEnvironment_Intro", true);
            AnimationClip loop = BuildClip("LadyLakeEnvironment_Loop", true);
            SaveOrReplace(intro, ClipIntroPath);
            SaveOrReplace(loop, ClipLoopPath);
            AnimatorController controller = BuildController(ClipIntroPath, ClipLoopPath);
            state.verified.Add("Animator controller has Intro (12 s) -> Loop (12 s, looping) with both clips baked from the shared 12 s envelope; " +
                "every sway curve uses an integer cycle count so the seam is continuous.");

            // ---- 8. assemble the prefab ---------------------------------------
            GameObject root = new GameObject("Environment");
            try
            {
                BuildHierarchy(root, new[]
                {
                    meshBackdrop, meshBedCaustics, meshSheen, meshSunRays,
                    meshPlants, meshGlow, meshParticle
                }, new[]
                {
                    matBackdrop, matBedCaustics, matSheenMat, matSunRays, matPlants,
                    matBubbles, matMotes, matGlint, matSwordGlow
                }, controller);

                SaveOrReplacePrefab(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            // ---- 9. fragments + report ----------------------------------------
            WriteFragment(state);
            WriteReport(state);

            // ---- 10. retire the meshes/materials of the previous design --------
            RemoveObsoleteGenerated(state);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(string.Format(
                "[LadyLakeEnv] built {0} with {1} meshes / {2} materials / {3} texture copies. Report: {4}",
                PrefabPath, state.meshes.Count, state.materials.Count, state.copies.Count, BuildReportPath));
        }

        // ------------------------------------------------------------------
        // Hierarchy
        // ------------------------------------------------------------------

        private static void BuildHierarchy(
            GameObject root, Mesh[] meshes, Material[] materials, AnimatorController controller)
        {
            Mesh backdrop = meshes[0], bedCaustics = meshes[1], sheen = meshes[2], sunRays = meshes[3];
            Mesh plants = meshes[4], glow = meshes[5], particle = meshes[6];

            Material matBackdrop = materials[0], matBedCaustics = materials[1], matSheenMat = materials[2], matSunRays = materials[3];
            Material matPlants = materials[4], matBubbles = materials[5], matMotes = materials[6], matGlint = materials[7], matGlow = materials[8];

            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;

            LadyLakeEnvironmentCycle cycle = root.AddComponent<LadyLakeEnvironmentCycle>();
            cycle.duration = CycleDuration;
            cycle.catchTime = 3.2f;
            cycle.liftStart = 5.6f;
            cycle.liftEnd = 8.3f;
            cycle.glowIdle = 0.10f;
            // Integration's LadyLakeProductionEffects.Bake owns these two levels in
            // production; the values here match it exactly so the un-baked prefab
            // (the environment-only capture) looks the way production will.
            cycle.causticIdle = 0.26f;
            cycle.causticBoost = 0.14f;

            // The painted water and the painted bed. No duplicate bed plate exists
            // any more, which is what removes the old horizontal seam.
            AddRenderable("Backdrop", root.transform, backdrop, matBackdrop);
            GameObject bedCausticsGo = AddRenderable("WaterbedCaustics", root.transform, bedCaustics, matBedCaustics);
            GameObject sheenGo = AddRenderable("CausticSheen", root.transform, sheen, matSheenMat);

            GameObject swayRays = AddGroup("Sway_Rays", root.transform);
            AddRenderable("SunRays", swayRays.transform, sunRays, matSunRays);

            AddParticles("Bubbles", root.transform, particle, matBubbles,
                new Vector3(0f, 0f, ZBubbles), new Vector3(5.2f, 6.8f, 0.8f),
                new Color(0.86f, 1f, 0.98f, 0.50f), 0.30f, 26f, 190, 0.10f, 0.30f, 8f, 15f);
            AddParticles("Motes", root.transform, particle, matMotes,
                new Vector3(0f, 0f, ZMotes), new Vector3(5.6f, 7.0f, 1.6f),
                new Color(0.78f, 0.98f, 0.86f, 0.36f), 0.10f, 40f, 260, 0.02f, 0.09f, 7f, 13f);
            AddParticles("Glints", root.transform, particle, matGlint,
                new Vector3(-0.6f, 0.4f, ZGlints), new Vector3(4.0f, 5.2f, 1.2f),
                new Color(0.86f, 1f, 0.96f, 0.70f), 0.06f, 12f, 90, 0.30f, 0.90f, 2.5f, 5f);

            // The plant painting, in front of the model like the finished card art.
            GameObject swayPlants = AddGroup("Sway_Plants", root.transform);
            AddRenderable("Foreground", swayPlants.transform, plants, matPlants);

            GameObject glowGo = AddRenderable("SwordGlow", root.transform, glow, matGlow);
            glowGo.transform.localPosition = new Vector3(1.315f, -1.88f, ZSwordGlow);

            // Production material animation component. The Animator clip drives
            // m_Float, exactly like an imported premium card animates it.
            GameObject materialAnimation = new GameObject("MaterialAnimation");
            materialAnimation.transform.SetParent(root.transform, false);
            DynamicCardAnimatedMaterialProperty animated = materialAnimation.AddComponent<DynamicCardAnimatedMaterialProperty>();
            animated.Type = 2;                    // float
            animated.Slot = 0;
            animated.Name = "_AlphaMult";
            animated.m_Float = 0.62f;
            animated.Targets = new Renderer[] { bedCausticsGo.GetComponent<Renderer>() };

            cycle.glowRenderers = new Renderer[] { glowGo.GetComponent<Renderer>() };
            cycle.causticRenderers = new Renderer[] { sheenGo.GetComponent<Renderer>() };
            cycle.glowRoot = glowGo.transform;
            cycle.followGrip = true;
            // Model manifest sword grip_world is (1.315, -1.88, -0.15) and the blade
            // plane sits at z ~ -0.12..-0.16; the glint is authored just in front of
            // it. Used only if GripSocket/Grip is absent.
            cycle.gripFallback = new Vector3(1.315f, -1.88f, ZSwordGlow);

            // Deterministic start pose so the baked t=0 frame equals the loop seam.
            ApplyPose(new[] { swayRays, swayPlants });
        }

        private static GameObject AddGroup(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }

        private static GameObject AddRenderable(string name, Transform parent, Mesh mesh, Material material)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return go;
        }

        private static GameObject AddParticles(
            string name, Transform parent, Mesh particleMesh, Material material,
            Vector3 centre, Vector3 box, Color startColor, float startSize, float rate,
            int maxParticles, float riseMin, float riseMax, float lifeMin, float lifeMax)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;
            main.duration = CycleDuration;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(startSize * 0.55f, startSize * 1.55f);
            main.startColor = new ParticleSystem.MinMaxGradient(startColor);
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = -0.004f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = rate;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            shape.position = Vector3.zero;

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            velocity.y = new ParticleSystem.MinMaxCurve(riseMin, riseMax);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);

            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.10f;
            noise.frequency = 0.22f;
            noise.scrollSpeed = 0.04f;

            AnimationCurve fade = new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.16f, 1f), new Keyframe(0.80f, 1f), new Keyframe(1f, 0f));

            ParticleSystem.ColorOverLifetimeModule colour = ps.colorOverLifetime;
            colour.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.16f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            colour.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, fade);

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.mesh = particleMesh;
                renderer.alignment = ParticleSystemRenderSpace.View;
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sortingFudge = 0.01f;
            }
            return go;
        }

        // ------------------------------------------------------------------
        // Animator
        // ------------------------------------------------------------------

        private static AnimationClip BuildClip(string name, bool loopTime)
        {
            AnimationClip clip = new AnimationClip();
            clip.name = name;
            clip.frameRate = 30f;
            clip.wrapMode = WrapMode.Loop;
            clip.legacy = false;

            // Sway groups: integer cycle counts keep the loop seam continuous. The
            // amplitudes are deliberately tiny; the water motion comes from the
            // shaders, not from sliding the paintings around.
            AddEulerZCurve(clip, "Sway_Rays", 2, 0.55f);
            AddPositionCurve(clip, "Sway_Rays", 0, 2, 0.022f);
            AddEulerZCurve(clip, "Sway_Plants", 2, 0.45f);
            AddPositionCurve(clip, "Sway_Plants", 0, 1, 0.030f);
            AddPositionCurve(clip, "Sway_Plants", 1, 2, 0.018f);

            // Caustics intensity on the production animated-material component.
            AddMonoFloatCurve(clip, "MaterialAnimation", 0.62f, 0.33f, 2);

            SerializedObject serialized = new SerializedObject(clip);
            SerializedProperty settings = serialized.FindProperty("m_AnimationClipSettings");
            if (settings != null)
            {
                SerializedProperty value = settings.FindPropertyRelative("m_LoopTime");
                if (value != null) value.boolValue = loopTime;
            }
            serialized.ApplyModifiedProperties();
            return clip;
        }

        private static AnimatorController BuildController(string introPath, string loopPath)
        {
            AnimationClip intro = Load<AnimationClip>(introPath);
            AnimationClip loop = Load<AnimationClip>(loopPath);
            Require(intro != null && loop != null, "environment clips were not imported");

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            for (int i = machine.states.Length - 1; i >= 0; i--)
                machine.RemoveState(machine.states[i].state);

            AnimatorState introState = machine.AddState("Intro", new Vector3(320f, 0f, 0f));
            introState.motion = intro;
            introState.speed = 1f;
            AnimatorState loopState = machine.AddState("Loop", new Vector3(320f, 90f, 0f));
            loopState.motion = loop;
            loopState.speed = 1f;
            machine.defaultState = introState;

            AnimatorStateTransition transition = introState.AddTransition(loopState);
            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.duration = 0f;
            transition.hasFixedDuration = true;
            transition.canTransitionToSelf = false;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddEulerZCurve(AnimationClip clip, string path, int cycles, float amplitude)
        {
            const int keys = 49;
            AnimationCurve cx = new AnimationCurve();
            AnimationCurve cy = new AnimationCurve();
            AnimationCurve cz = new AnimationCurve();
            AnimationCurve cw = new AnimationCurve();
            float[] xs = new float[keys], ys = new float[keys], zs = new float[keys], ws = new float[keys];
            for (int i = 0; i < keys; i++)
            {
                float t = CycleDuration * i / (keys - 1);
                float deg = amplitude * Mathf.Sin(t / CycleDuration * Mathf.PI * 2f * cycles);
                Quaternion q = Quaternion.Euler(0f, 0f, deg);
                xs[i] = q.x; ys[i] = q.y; zs[i] = q.z; ws[i] = q.w;
            }
            BuildCurve(cx, xs); BuildCurve(cy, ys); BuildCurve(cz, zs); BuildCurve(cw, ws);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.x"), cx);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.y"), cy);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.z"), cz);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.w"), cw);
        }

        private static void AddPositionCurve(AnimationClip clip, string path, int axis, int cycles, float amplitude)
        {
            const int keys = 49;
            string[] names = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };
            float[] values = new float[keys];
            for (int i = 0; i < keys; i++)
            {
                float t = CycleDuration * i / (keys - 1);
                values[i] = amplitude * Mathf.Sin(t / CycleDuration * Mathf.PI * 2f * cycles);
            }
            AnimationCurve curve = new AnimationCurve();
            BuildCurve(curve, values);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), names[axis]), curve);
        }

        private static void AddMonoFloatCurve(AnimationClip clip, string path, float baseline, float amplitude, int cycles)
        {
            const int keys = 49;
            float[] values = new float[keys];
            for (int i = 0; i < keys; i++)
            {
                float t = CycleDuration * i / (keys - 1);
                values[i] = baseline + amplitude * (0.5f + 0.5f * Mathf.Sin(t / CycleDuration * Mathf.PI * 2f * cycles - Mathf.PI * 0.5f));
            }
            AnimationCurve curve = new AnimationCurve();
            BuildCurve(curve, values);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve(path, typeof(DynamicCardAnimatedMaterialProperty), "m_Float"), curve);
        }

        private static void BuildCurve(AnimationCurve curve, float[] values)
        {
            int n = values.Length;
            for (int i = 0; i < n; i++)
            {
                float t = CycleDuration * i / (n - 1);
                float before = values[Mathf.Max(0, i - 1)];
                float after = values[Mathf.Min(n - 1, i + 1)];
                float dt = CycleDuration * (Mathf.Min(n - 1, i + 1) - Mathf.Max(0, i - 1)) / (n - 1);
                float tangent = dt > 0f ? (after - before) / dt : 0f;
                curve.AddKey(new Keyframe(t, values[i], tangent, tangent));
            }
        }

        private static void ApplyPose(GameObject[] groups)
        {
            // The sway groups are baked at the t=0 sine zero crossing, so the prefab
            // rest pose already equals the loop seam and no runtime offset is needed.
            if (groups == null) return;
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] == null) continue;
                groups[i].transform.localRotation = Quaternion.identity;
                groups[i].transform.localPosition = Vector3.zero;
            }
        }

        // ------------------------------------------------------------------
        // Frame helpers (all geometry is authored in the shared frame)
        // ------------------------------------------------------------------

        /// <summary>
        /// Frame position + depth -> world position, with the shared perspective
        /// compensation so a displaced vertex still projects onto the painting.
        /// </summary>
        private static Vector3 P(float fx, float fy, float z)
        {
            float k = 1f + z / CameraDistance;
            return new Vector3(fx * k, fy * k, z);
        }

        /// <summary>
        /// Frame x -> painting U (0 left, 1 right). Outside the 497 px frame the
        /// coordinate mirrors back inside, so the thin cover ring past the frustum
        /// continues the painting smoothly instead of smearing one texel row.
        /// </summary>
        private static float FrameU(float fx)
        {
            return Mirror((fx + HalfPaintingW) / PaintingWidth);
        }

        /// <summary>Frame y -> painting V (0 bottom, 1 top), mirrored outside the frame.</summary>
        private static float FrameV(float fy)
        {
            return Mirror((fy + HalfPaintingH) / PaintingHeight);
        }

        private static float Mirror(float t)
        {
            if (t < 0f) return -t;
            if (t > 1f) return 2f - t;
            return t;
        }

        private static Mesh NewMesh(string name)
        {
            Mesh mesh = new Mesh();
            mesh.name = name;
            return mesh;
        }

        private static void AddQuad(List<int> tris, int a, int b, int c, int d)
        {
            tris.Add(a); tris.Add(b); tris.Add(c);
            tris.Add(a); tris.Add(c); tris.Add(d);
        }

        /// <summary>
        /// Grid quad with the winding the original DynamicCards VFX shaders need.
        /// Those shaders are Cull Back, and the camera looks along +z, so the front
        /// face must be the one whose vertices read counter-clockwise in the XY
        /// plane. a = (i,j), a+1 = (i+1,j), a+cols+1 = (i,j+1), a+cols+2 = (i+1,j+1).
        /// </summary>
        private static void AddGridQuad(List<int> tris, int a, int cols)
        {
            AddQuad(tris, a, a + cols + 1, a + cols + 2, a + 1);
        }

        // ------------------------------------------------------------------
        // Art sampling (the paintings are imported readable so the accents can be
        // gated by the art itself; hand cut rectangles are what used to seam)
        // ------------------------------------------------------------------

        private sealed class Field
        {
            public int w;
            public int h;
            public float[] v;

            public float Sample(float u, float vv)
            {
                float fx = Mathf.Clamp01(u) * (w - 1);
                float fy = Mathf.Clamp01(vv) * (h - 1);
                int x0 = (int)fx, y0 = (int)fy;
                int x1 = Mathf.Min(x0 + 1, w - 1), y1 = Mathf.Min(y0 + 1, h - 1);
                float tx = fx - x0, ty = fy - y0;
                float a = v[y0 * w + x0], b = v[y0 * w + x1];
                float c = v[y1 * w + x0], d = v[y1 * w + x1];
                return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
            }

            public void Blur(int radius, int passes)
            {
                if (radius <= 0 || passes <= 0) return;
                float[] tmp = new float[w * h];
                for (int p = 0; p < passes; p++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            float sum = 0f;
                            int n = 0;
                            for (int k = -radius; k <= radius; k++)
                            {
                                sum += v[y * w + Mathf.Clamp(x + k, 0, w - 1)];
                                n++;
                            }
                            tmp[y * w + x] = sum / n;
                        }
                    }
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            float sum = 0f;
                            int n = 0;
                            for (int k = -radius; k <= radius; k++)
                            {
                                sum += tmp[Mathf.Clamp(y + k, 0, h - 1) * w + x];
                                n++;
                            }
                            v[y * w + x] = sum / n;
                        }
                    }
                }
            }

            /// <summary>
            /// Box-average a readable texture into a small field. Row 0 of the result
            /// is the BOTTOM of the painting, matching Unity's v = 0 and
            /// GetPixels32()'s bottom-left origin.
            /// </summary>
            public static Field FromTexture(Texture2D texture, int w, int h, bool alpha)
            {
                Field field = new Field();
                field.w = w;
                field.h = h;
                field.v = new float[w * h];
                int tw = texture.width, th = texture.height;
                Color32[] pixels = texture.GetPixels32();
                for (int y = 0; y < h; y++)
                {
                    int y0 = y * th / h;
                    int y1 = Mathf.Max(y0 + 1, (y + 1) * th / h);
                    for (int x = 0; x < w; x++)
                    {
                        int x0 = x * tw / w;
                        int x1 = Mathf.Max(x0 + 1, (x + 1) * tw / w);
                        float sum = 0f;
                        int n = 0;
                        for (int py = y0; py < y1; py++)
                        {
                            for (int px = x0; px < x1; px++)
                            {
                                Color32 c = pixels[py * tw + px];
                                sum += alpha
                                    ? c.a / 255f
                                    : (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
                                n++;
                            }
                        }
                        field.v[y * w + x] = n > 0 ? sum / n : 0f;
                    }
                }
                return field;
            }
        }

        /// <summary>
        /// Generated _WaveyMask for the plant plate. The weight comes from the plant
        /// painting's own alpha (so only real plant pixels move) times a height ramp
        /// that pins the lily pad band at the top of the painting.
        /// </summary>
        private static Texture2D BuildSwayMask(Field plants, BuildState state)
        {
            const int mw = 128, mh = 183;
            Texture2D temp = new Texture2D(mw, mh, TextureFormat.RGB24, false);
            Color32[] pixels = new Color32[mw * mh];
            for (int y = 0; y < mh; y++)
            {
                float v = (y + 0.5f) / mh;
                float height = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((0.86f - v) / (0.86f - 0.30f)));
                for (int x = 0; x < mw; x++)
                {
                    float u = (x + 0.5f) / mw;
                    float body = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((plants.Sample(u, v) - 0.01f) / 0.14f));
                    byte value = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(body * height) * 255f), 0, 255);
                    pixels[y * mw + x] = new Color32(value, value, value, 255);
                }
            }
            temp.SetPixels32(pixels);
            temp.Apply();
            string absolute = Absolute(SwayMaskPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            File.WriteAllBytes(absolute, temp.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(temp);

            AssetDatabase.ImportAsset(SwayMaskPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(SwayMaskPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;          // a raw weight, never a colour
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 256;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            Texture2D mask = Load<Texture2D>(SwayMaskPath);
            Require(mask != null, "sway mask was not imported: " + SwayMaskPath);
            state.verified.Add("Env_SwayMask.png is generated from the plant painting's own alpha times a height ramp, " +
                "so only the stems carry the wavey-shader displacement and the lily pads stay rigid.");
            return mask;
        }

        // ------------------------------------------------------------------
        // Mesh builders
        // ------------------------------------------------------------------

        /// <summary>
        /// A full-frame painted plate. UV is the painting's own coordinate (mirrored
        /// outside the 497x710 frame) so the art is reproduced 1:1 and stays
        /// registered with the model rig. Depth relief is optional and always
        /// perspective compensated.
        /// </summary>
        private static void PlateGrid(
            Mesh mesh, int cols, int rows, float baseZ, float tilt,
            Field reliefAlpha, float reliefAlphaGain,
            Field reliefLuma, float reliefLumaGain)
        {
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();
            for (int j = 0; j <= rows; j++)
            {
                float ty = (float)j / rows;
                float fy = Mathf.Lerp(-PlateHalfH, PlateHalfH, ty);
                float v = FrameV(fy);
                for (int i = 0; i <= cols; i++)
                {
                    float tx = (float)i / cols;
                    float fx = Mathf.Lerp(-PlateHalfW, PlateHalfW, tx);
                    float u = FrameU(fx);

                    // Tilt (the bed recedes, the surface comes forward) plus, when
                    // asked for, relief taken from the art itself.
                    float z = baseZ + (0.5f - v) * tilt;
                    if (reliefAlpha != null)
                        z -= reliefAlphaGain * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(reliefAlpha.Sample(u, v) * 1.4f));
                    if (reliefLuma != null)
                        z += reliefLumaGain * (0.45f - reliefLuma.Sample(u, v));

                    verts.Add(P(fx, fy, z));
                    uvs.Add(new Vector2(u, v));
                    colors.Add(new Color(1f, 1f, 1f, 1f));
                }
            }
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < cols; i++)
                    AddGridQuad(tris, j * (cols + 1) + i, cols);
            Apply(mesh, verts, uvs, colors, tris);
        }

        /// <summary>
        /// Full-frame additive accent plate. The vertex alpha comes from the backdrop
        /// painting's own luminance inside an optional vertical zone, so the layer has
        /// no boundary of its own: it simply fades wherever the art is dark. The UV is
        /// the plate parameter, not the painting, so the accent texture can tile.
        /// </summary>
        private static void AccentGrid(
            Mesh mesh, int cols, int rows, float z, float tileU, float tileV,
            float maskFloor, float maskGain, float zoneLow, float zoneHigh,
            float lumaLow, float lumaHigh, Field luma)
        {
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();
            float zoneSpan = Mathf.Max(1e-4f, zoneHigh - zoneLow);
            float lumaSpan = Mathf.Max(1e-4f, lumaHigh - lumaLow);
            for (int j = 0; j <= rows; j++)
            {
                float ty = (float)j / rows;
                float fy = Mathf.Lerp(-PlateHalfH, PlateHalfH, ty);
                float v = FrameV(fy);
                float zone = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((zoneHigh - v) / zoneSpan));
                for (int i = 0; i <= cols; i++)
                {
                    float tx = (float)i / cols;
                    float fx = Mathf.Lerp(-PlateHalfW, PlateHalfW, tx);
                    float u = FrameU(fx);
                    float mask = maskFloor + maskGain *
                        Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((luma.Sample(u, v) - lumaLow) / lumaSpan));
                    verts.Add(P(fx, fy, z));
                    uvs.Add(new Vector2(tx * tileU, ty * tileV));
                    colors.Add(new Color(1f, 1f, 1f, Mathf.Clamp01(mask * zone)));
                }
            }
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < cols; i++)
                    AddGridQuad(tris, j * (cols + 1) + i, cols);
            Apply(mesh, verts, uvs, colors, tris);
        }

        /// <summary>
        /// The whole backdrop painting. Relief follows the painting's luminance so
        /// the bright surface and the bright bed stand slightly proud of the mid
        /// water; the amplitude is far too small to disturb the registration.
        /// </summary>
        private static Mesh BuildBackdrop(Field artLuma)
        {
            Mesh mesh = NewMesh("Env_Backdrop");
            PlateGrid(mesh, 56, 80, ZBackdrop, 0.16f, null, 0f, artLuma, 0.10f);
            return mesh;
        }

        /// <summary>The whole plant painting, in front of the model.</summary>
        private static Mesh BuildPlants(Field plantAlpha)
        {
            Mesh mesh = NewMesh("Env_Foreground");
            PlateGrid(mesh, 72, 100, ZPlants, 0f, plantAlpha, 0.16f, null, 0f);
            return mesh;
        }

        /// <summary>
        /// Caustic shimmer over the lakebed. There is no separate bed plate any more
        /// (that duplicate was the hard horizontal seam); this is one full-frame
        /// additive layer gated by the painting's own bright bed.
        /// </summary>
        private static Mesh BuildBedCaustics(Field artLuma)
        {
            Mesh mesh = NewMesh("Env_WaterbedCaustics");
            AccentGrid(mesh, 30, 42, ZBedCaustics, 2.6f, 1.5f, 0f, 1f, 0.06f, 0.62f, 0.34f, 0.72f, artLuma);
            return mesh;
        }

        /// <summary>Full-frame water sheen at the production 0.26 alpha level.</summary>
        private static Mesh BuildCausticSheen(Field artLuma)
        {
            Mesh mesh = NewMesh("Env_CausticSheen");
            AccentGrid(mesh, 26, 36, ZSheen, 2.2f, 2.2f, 0f, 0.45f, 1.2f, 2.0f, 0.34f, 0.72f, artLuma);
            return mesh;
        }

        /// <summary>
        /// Soft-sided light shafts. Five columns per ribbon with the outer columns at
        /// zero alpha, so a shaft has no hard left/right edge, and the alpha also
        /// falls to zero at both ends.
        /// </summary>
        private static Mesh BuildSunRays()
        {
            Mesh mesh = NewMesh("Env_SunRays");
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            float[] topX = { -1.30f, -0.55f, 0.10f, 0.70f, 1.35f, 1.90f };
            float[] botX = { -2.25f, -1.45f, -0.75f, -0.10f, 0.55f, 1.20f };
            float[] topW = { 0.30f, 0.34f, 0.40f, 0.34f, 0.28f, 0.26f };
            float[] botW = { 0.70f, 0.80f, 0.95f, 0.80f, 0.66f, 0.60f };
            float[] botY = { 0.10f, -0.05f, 0.20f, 0.05f, -0.10f, -0.25f };
            float[] bow = { -0.14f, 0.10f, -0.12f, 0.12f, -0.09f, 0.07f };
            float[] gain = { 0.55f, 0.75f, 0.92f, 0.76f, 0.58f, 0.44f };
            float[] side = { 0f, 0.55f, 1f, 0.55f, 0f };
            const int columns = 5;

            for (int s = 0; s < topX.Length; s++)
            {
                const int segments = 20;
                int first = verts.Count;
                for (int k = 0; k <= segments; k++)
                {
                    float t = (float)k / segments;
                    float y = Mathf.Lerp(PlateHalfH, botY[s], t);
                    float centreX = Mathf.Lerp(topX[s], botX[s], t) + bow[s] * Mathf.Sin(t * Mathf.PI);
                    float width = Mathf.Lerp(topW[s], botW[s], t);
                    float endFade = Mathf.Pow(Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI), 0.75f);
                    for (int c = 0; c < columns; c++)
                    {
                        float fx = centreX + (c / (columns - 1f) - 0.5f) * width;
                        verts.Add(P(fx, y, ZSunRays));
                        uvs.Add(new Vector2(c / (columns - 1f), t));
                        colors.Add(new Color(1f, 1f, 1f, endFade * side[c] * gain[s]));
                    }
                }
                for (int k = 0; k < segments; k++)
                {
                    for (int c = 0; c < columns - 1; c++)
                    {
                        int a = first + k * columns + c;
                        AddQuad(tris, a, a + columns, a + columns + 1, a + 1);
                    }
                }
            }
            Apply(mesh, verts, uvs, colors, tris);
            return mesh;
        }

        /// <summary>
        /// Two concentric soft radial discs facing the camera. Radially symmetric, so
        /// the glint stays correct whichever way the runtime grip socket is rotated.
        /// Single sided: the additive shaders are Cull Back and Blend One One, so a
        /// second winding would double the contribution.
        /// </summary>
        private static Mesh BuildGlowHalo()
        {
            Mesh mesh = NewMesh("Env_SwordGlowHalo");
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();
            AppendDisc(verts, uvs, colors, tris, 0.85f, 1.6f, 0.42f);
            AppendDisc(verts, uvs, colors, tris, 0.34f, 2.4f, 0.85f);
            Apply(mesh, verts, uvs, colors, tris);
            return mesh;
        }

        private static void AppendDisc(
            List<Vector3> verts, List<Vector2> uvs, List<Color> colors, List<int> tris,
            float radius, float falloff, float gain)
        {
            const int rings = 4;
            const int segments = 20;
            int centre = verts.Count;
            verts.Add(Vector3.zero);
            uvs.Add(new Vector2(0.5f, 0.5f));
            colors.Add(new Color(1f, 1f, 1f, gain));
            for (int r = 1; r <= rings; r++)
            {
                float rr = (float)r / rings;
                for (int k = 0; k < segments; k++)
                {
                    float a = Mathf.PI * 2f * k / segments;
                    float x = Mathf.Cos(a) * rr * radius;
                    float y = Mathf.Sin(a) * rr * radius;
                    verts.Add(new Vector3(x, y, 0f));
                    uvs.Add(new Vector2(0.5f + x, 0.5f + y));
                    colors.Add(new Color(1f, 1f, 1f, gain * Mathf.Pow(Mathf.Clamp01(1f - rr), falloff)));
                }
            }
            for (int k = 0; k < segments; k++)
                tris.AddRange(new[] { centre, centre + 1 + k, centre + 1 + (k + 1) % segments });
            for (int r = 1; r < rings; r++)
            {
                int rowA = centre + 1 + (r - 1) * segments;
                int rowB = centre + 1 + r * segments;
                for (int k = 0; k < segments; k++)
                {
                    int k1 = (k + 1) % segments;
                    AddQuad(tris, rowA + k, rowA + k1, rowB + k1, rowB + k);
                }
            }
        }

        private static Mesh BuildParticleDisc()
        {
            Mesh mesh = NewMesh("Env_ParticleDisc");
            const int rings = 5;
            const int segments = 18;
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            verts.Add(Vector3.zero);
            uvs.Add(new Vector2(0.5f, 0.5f));
            colors.Add(new Color(1f, 1f, 1f, 1f));
            for (int r = 1; r <= rings; r++)
            {
                float rr = (float)r / rings;
                // Slight doming gives the billboard a lens-like highlight instead
                // of a flat sticker when the original shader shifts its UVs.
                float z = -0.10f * (1f - rr * rr);
                for (int k = 0; k < segments; k++)
                {
                    float a = Mathf.PI * 2f * k / segments;
                    float x = Mathf.Cos(a) * rr * 0.5f;
                    float y = Mathf.Sin(a) * rr * 0.5f;
                    verts.Add(new Vector3(x, y, z));
                    uvs.Add(new Vector2(0.5f + x, 0.5f + y));
                    // Hard alpha falloff from real vertex data: the bubble shape
                    // is authored geometry, not a runtime procedural effect.
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - rr), 0.85f);
                    float rim = 1f + 0.55f * Mathf.Exp(-Mathf.Pow((rr - 0.72f) / 0.16f, 2f));
                    colors.Add(new Color(rim, rim, rim, alpha));
                }
            }
            for (int k = 0; k < segments; k++)
                tris.AddRange(new[] { 0, 1 + k, 1 + (k + 1) % segments });
            for (int r = 1; r < rings; r++)
            {
                int rowA = 1 + (r - 1) * segments;
                int rowB = 1 + r * segments;
                for (int k = 0; k < segments; k++)
                {
                    int k1 = (k + 1) % segments;
                    AddQuad(tris, rowA + k, rowA + k1, rowB + k1, rowB + k);
                }
            }
            Apply(mesh, verts, uvs, colors, tris);
            return mesh;
        }

        private static void Apply(Mesh mesh, List<Vector3> verts, List<Vector2> uvs, List<Color> colors, List<int> tris)
        {
            mesh.SetVertices(verts);
            // Native client shaders use TEXCOORD0.zw for lifetime and random offset.
            // Missing W defaults to one on D3D, which offsets clamped artwork to a corner.
            mesh.SetUVs(0, uvs.ConvertAll(uv => new Vector4(uv.x, uv.y, 0f, 0f)));
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }

        // ------------------------------------------------------------------
        // Asset helpers
        // ------------------------------------------------------------------

        private enum TextureWrap { Repeat, Clamp }

        private static string CopyTexture(string sourceAsset, string destinationAsset, TextureWrap wrap,
            bool alphaIsTransparency, TextureImporterCompression compression, bool readable,
            BuildState state, string kind)
        {
            string source = Absolute(sourceAsset);
            string destination = Absolute(destinationAsset);
            if (!File.Exists(source))
            {
                // Documented fallback: the old experiment already holds byte
                // copies of the same Aeschna textures.
                string fallback = null;
                if (sourceAsset.Contains("74809d52")) fallback = FbCaustics;
                else if (sourceAsset.Contains("963cbb36")) fallback = FbBubbles;
                else if (sourceAsset.Contains("13890100")) fallback = FbNoise;
                else if (sourceAsset.Contains("14990100")) fallback = FbNoise;
                if (fallback != null)
                {
                    state.warnings.Add(sourceAsset + " missing; used the existing byte copy " + fallback);
                    source = Absolute(fallback);
                }
            }
            Require(File.Exists(source), "missing source texture: " + sourceAsset);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            if (!File.Exists(destination) || !FilesEqual(source, destination))
                File.Copy(source, destination, true);
            AssetDatabase.ImportAsset(destinationAsset, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(destinationAsset) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.alphaIsTransparency = alphaIsTransparency;
                importer.wrapMode = wrap == TextureWrap.Repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 2048;   // both paintings are 1049x1499: never downscaled
                importer.textureCompression = compression;
                // The two paintings are the final art, so they are read back at build
                // time to gate the accent layers. The PNG on disk stays byte identical.
                importer.isReadable = readable;
                importer.SaveAndReimport();
            }

            state.copies.Add(new CopyStat
            {
                destination = destinationAsset,
                source = sourceAsset,
                bytes = new FileInfo(destination).Length,
                kind = kind
            });
            return destinationAsset;
        }

        private static bool FilesEqual(string a, string b)
        {
            FileInfo fa = new FileInfo(a), fb = new FileInfo(b);
            if (!fb.Exists || fa.Length != fb.Length) return false;
            using (FileStream sa = File.OpenRead(a))
            using (FileStream sb = File.OpenRead(b))
            {
                byte[] ba = new byte[8192], bb = new byte[8192];
                while (true)
                {
                    int na = sa.Read(ba, 0, ba.Length);
                    int nb = sb.Read(bb, 0, bb.Length);
                    if (na != nb) return false;
                    if (na == 0) return true;
                    for (int i = 0; i < na; i++) if (ba[i] != bb[i]) return false;
                }
            }
        }

        private static Shader RequireShader(string name, string path)
        {
            Shader shader = Shader.Find(name);
            if (shader == null) shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Require(shader != null, "missing original shader: " + name);
            return shader;
        }

        private static Material MakeMaterial(string name, Shader shader, int queue, BuildState state, string provenance)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material material = new Material(shader);
            material.name = name;
            material.renderQueue = queue;
            material.SetOverrideTag("RenderType", "Transparent");
            SaveOrReplace(material, path);
            state.materials.Add(new MaterialStat
            {
                name = name,
                shader = shader.name,
                queue = queue,
                provenance = provenance
            });
            return material;
        }

        private static void ConfigureTextures(Material material, Texture2D main, Texture2D noise)
        {
            if (main != null) material.SetTexture("_MainTex", main);
            if (noise != null) material.SetTexture("_NoiseTex", noise);
            material.SetTextureOffset("_MainTex", Vector2.zero);
            material.SetTextureOffset("_NoiseTex", Vector2.zero);
        }

        private static void ZeroMainScroll(Material material)
        {
            material.SetFloat("_MainU", 0f);
            material.SetFloat("_MainV", 0f);
        }

        /// <summary>
        /// Retire the meshes and materials of the previous (stripped-ribbon) design so
        /// the delivered folder contains only what the prefab actually uses. Runs
        /// after the prefab has been replaced, so nothing references them any more.
        /// </summary>
        private static void RemoveObsoleteGenerated(BuildState state)
        {
            string[] obsolete =
            {
                MeshFolder + "/Env_Waterbed.asset",
                MeshFolder + "/Env_KelpBack.asset",
                MeshFolder + "/Env_KelpMid.asset",
                MeshFolder + "/Env_KelpFront.asset",
                MeshFolder + "/Env_LilyCanopy.asset",
                MaterialFolder + "/LadyLake_Env_Waterbed.mat",
                MaterialFolder + "/LadyLake_Env_KelpBack.mat",
                MaterialFolder + "/LadyLake_Env_KelpMid.mat",
                MaterialFolder + "/LadyLake_Env_KelpFront.mat",
                MaterialFolder + "/LadyLake_Env_LilyCanopy.mat",
            };
            for (int i = 0; i < obsolete.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(obsolete[i]) == null) continue;
                AssetDatabase.DeleteAsset(obsolete[i]);
                state.verified.Add("removed the obsolete generated asset " + obsolete[i]);
            }
        }

        private static Mesh SaveMesh(string name, Mesh mesh, BuildState state)
        {
            string path = MeshFolder + "/" + name + ".asset";
            mesh.name = name;
            mesh.RecalculateBounds();
            int vertices = mesh.vertexCount;
            int triangles = mesh.triangles.Length / 3;
            int subMeshes = mesh.subMeshCount;
            string boundsCenter = mesh.bounds.center.ToString("F3");
            string boundsSize = mesh.bounds.size.ToString("F3");
            SaveOrReplace(mesh, path);
            state.meshes.Add(new MeshStat
            {
                name = name,
                path = path,
                vertices = vertices,
                triangles = triangles,
                subMeshes = subMeshes,
                boundsCenter = boundsCenter,
                boundsSize = boundsSize
            });
            return Load<Mesh>(path);
        }

        private static void SaveOrReplace(UnityEngine.Object asset, string path)
        {
            UnityEngine.Object existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (existing != null && existing != asset)
                AssetDatabase.DeleteAsset(path);
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) == null)
                AssetDatabase.CreateAsset(asset, path);
            else
                EditorUtility.SetDirty(asset);
        }

        private static void SaveOrReplacePrefab(GameObject root, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || path == "Assets") return;
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path);
            if (parent != null) parent = parent.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static string Absolute(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[LadyLakeEnv] " + message);
        }

        // ------------------------------------------------------------------
        // Fragment + report
        // ------------------------------------------------------------------

        private static void WriteFragment(BuildState state)
        {
            Fragment fragment = new Fragment();
            fragment.id = "lady-lake-authored";
            fragment.artIds = new[] { "c10000000" };
            fragment.audio = AudioLoopPath;
            fragment.introDuration = CycleDuration;
            fragment.loopDuration = CycleDuration;
            fragment.prefab = PrefabPath;
            fragment.pivot = "Pivot";
            fragment.pathNote =
                "Paths are relative to the Environment prefab root. Prefix them with the real path " +
                "from the Card.prefab root to the Environment instance (for example 'Pivot/Environment/'). " +
                "particleEvents restart the looping ambient fields at the shared 12 s boundary; uvMotions " +
                "scroll the caustics through the production _ST path. Integration/root owns the final " +
                "catalog entry; this file is only the fragment this worker contributes. This file is " +
                "regenerated byte-compatibly by LegacyGwent.LadyLakePremium.Editor.LadyLakeEnvironmentBuilder.Build().";
            fragment.particleEvents = new[]
            {
                Event("Bubbles", 0f, 0f, CycleDuration),
                Event("Motes", 0f, 0f, CycleDuration),
                Event("Glints", 0f, 0f, CycleDuration),
            };
            fragment.uvMotions = new[]
            {
                Uv("CausticSheen", "_MainTex", 0, new Vector2(0.020f, 0.034f)),
                Uv("WaterbedCaustics", "_MainTex", 0, new Vector2(0.014f, 0.022f)),
            };
            File.WriteAllText(Absolute(FragmentPath), JsonUtility.ToJson(fragment, true), new UTF8Encoding(false));
            state.verified.Add("catalog-fragment.json written with production particleEvents/uvMotions on the shared 12 s cycle.");
        }

        private static FragmentParticleEvent Event(string path, float time, float phaseStart, float period)
        {
            FragmentParticleEvent e = new FragmentParticleEvent();
            e.path = path;
            e.time = time;
            e.loop = true;
            e.sourceTiming = true;
            e.phaseStart = phaseStart;
            e.period = period;
            return e;
        }

        private static FragmentUvMotion Uv(string path, string property, int index, Vector2 speed)
        {
            FragmentUvMotion m = new FragmentUvMotion();
            m.path = path;
            m.property = property;
            m.materialIndex = index;
            m.speed = speed;
            m.forceStart = false;
            m.start = Vector2.zero;
            return m;
        }

        private static void WriteReport(BuildState state)
        {
            Report report = new Report();
            report.generatedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            report.unityVersion = Application.unityVersion;
            report.prefabPath = PrefabPath;
            report.controllerPath = ControllerPath;
            report.cycleDuration = CycleDuration;
            report.cameraDistance = CameraDistance;
            report.audioPath = AudioLoopPath;
            report.verified = state.verified.ToArray();
            report.warnings = state.warnings.ToArray();
            report.unverified = state.unverified.ToArray();
            report.meshes = state.meshes.ToArray();
            report.materials = state.materials.ToArray();
            report.copies = state.copies.ToArray();
            report.groupingOverrides = state.overrides.ToArray();
            File.WriteAllText(Absolute(BuildReportPath), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
        }
    }
}
