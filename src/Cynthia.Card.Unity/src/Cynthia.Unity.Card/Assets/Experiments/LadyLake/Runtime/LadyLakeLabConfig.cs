using UnityEngine;

namespace LegacyGwent.LadyLakeLab
{
    /// <summary>四张分层图各自的角色。</summary>
    public enum LadyLakeLayerRole
    {
        Background = 0,
        Figure = 1,
        Foreground = 2,
        Sword = 3,
    }

    /// <summary>
    /// 湖中仙女（Lady of the Lake）2.5D/3D 闪卡实验的共享常量。
    ///
    /// 全部构图坐标以「原图有效画面」为基准：497 x 710 像素，左上角为原点，y 轴向下。
    /// 之所以不用 1024 atlas 坐标，是因为 Codex 提供的四张分层 PNG 尺寸可能互不相同，
    /// 但都覆盖同一块 7:10 画面；Runtime/Mesh 工厂按「图层像素 -> 画面比例 -> 世界单位」
    /// 逐层换算，因此这里只保留与分辨率无关的画面坐标。
    ///
    /// 世界坐标约定：
    ///   * 画面中心 = (0,0)，画面高度 710px = 7.10 世界单位（100px = 1 单位）；
    ///     +x 在屏幕右侧、+y 在屏幕上方（与摄像机 identity 朝向一致，画面不会镜像）。
    ///   * 摄像机在 (0,0,-CameraDistance) 且 rotation = identity，看向 +z。
    ///     因此距离 = z + CameraDistance；**+z 表示更远（更深）**，-z 表示更靠近观众。
    ///   * 各层 z 依次为：背景 +0.30 → 焦散 +0.16 → 人物 0.00（凸起至 -0.34）
    ///     → 剑 -0.36 → 前景 -0.30（叶尖 -0.62）→ 全屏折射 -1.15；深海背景 +2.0。
    /// </summary>
    public static class LadyLakeLabConfig
    {
        // ------------------------------------------------------------------
        // 路径（生成物一律落在 Assets/Experiments/LadyLake/Generated 下）
        // ------------------------------------------------------------------
        public const string LabFolder = "Assets/Experiments/LadyLake";
        public const string TextureFolder = LabFolder + "/Textures";
        public const string GeneratedFolder = LabFolder + "/Generated";
        public const string GeneratedMeshFolder = GeneratedFolder + "/Meshes";
        public const string GeneratedMaterialFolder = GeneratedFolder + "/Materials";
        public const string ScenePath = LabFolder + "/LadyLakeLab.unity";

        /// <summary>Editor 取样截图的输出目录（任务指定的绝对路径）。</summary>
        public const string CaptureFolder = @"C:\UnityProjects\LegacyGwent\work\LadyLakeLab\captures";

        /// <summary>Editor 验证报告输出路径（任务指定的绝对路径）。</summary>
        public const string ValidationFilePath = @"C:\UnityProjects\LegacyGwent\work\LadyLakeLab\validation.json";

        public const string BackgroundFileName = "background.png";
        public const string FigureFileName = "figure-v5.png";
        public const string ForegroundFileName = "foreground.png";
        public const string SwordFileName = "sword.png";

        /// <summary>Codex 从 Aeschna（15010100 / Thronebreaker）原版材质解析出的水下贴图。</summary>
        public const string CausticsTextureFileName = "AeschnaCaustics.png";
        public const string FlowNoiseTextureFileName = "AeschnaFlowNoise.png";
        public const string BubblesTextureFileName = "AeschnaBubbles.png";

        /// <summary>可选的图层注册覆盖文件（放在 Textures 目录，缺失时用代码内默认值）。</summary>
        public const string LayoutOverrideFileName = "layout.json";

        // ------------------------------------------------------------------
        // 画面坐标系
        // ------------------------------------------------------------------
        public const float FrameWidthPx = 497f;
        public const float FrameHeightPx = 710f;
        public const float PixelsPerUnit = 100f;
        public const float FrameWidthUnits = FrameWidthPx / PixelsPerUnit;
        public const float FrameHeightUnits = FrameHeightPx / PixelsPerUnit;

        // ------------------------------------------------------------------
        // 原图关键点（左上角原点，y 向下，单位=有效画面像素）
        // 说明：这些点位来自美术资料，不依赖任何位图处理，直接写死便于逐条核对。
        // ------------------------------------------------------------------
        public static readonly Vector2 FaceAnchor = new Vector2(335f, 320f);        // 脸
        public static readonly Vector2 ShoulderAnchor = new Vector2(256f, 250f);    // 肩
        public static readonly Vector2 ElbowAnchor = new Vector2(338f, 440f);      // 完整修正版前臂
        public static readonly Vector2 SwordHandAnchor = new Vector2(380f, 543f);  // 用户选定完整人物的握点
        public static readonly Vector2 RaisedHandAnchor = new Vector2(203f, 97f); // 自然伸展的上手
        public static readonly Vector2 SwordTipAnchor = new Vector2(107f, 308f);   // 剑整体随握点配准
        public static readonly Vector2 SwordGuardAnchor = new Vector2(364f, 529f);
        public static readonly Vector2 SwordPommelAnchor = new Vector2(420f, 583f);

        // ------------------------------------------------------------------
        // 摄像机（透视；画面平面 z = 0）
        // ------------------------------------------------------------------
        public const float CameraDistance = 12.4f;
        public const float CameraFieldOfView = 35f;
        public const float CameraNearClip = 0.3f;
        public const float CameraFarClip = 60f;
        public static readonly Color CameraBackgroundColor = new Color(0.012f, 0.035f, 0.043f, 1f);

        /// <summary>摄像机位置（identity 旋转，看向 +z）。</summary>
        public static readonly Vector3 CameraPosition = new Vector3(0f, 0f, -CameraDistance);

        /// <summary>
        /// 透视补偿系数。把顶点放在深度 z 上时，用 (1 + z/D) 缩放它的 x/y，
        /// 使投影位置与 z=0 平面上的原画面位置完全一致 -> 保留原画透视、不拉伸构图。
        /// （距离 = z + D，故 x/(z+D) == x0/D  =>  x = x0 * (1 + z/D)。）
        /// </summary>
        public static float PerspectiveCompensation(float z)
        {
            return 1f + z / CameraDistance;
        }

        // ------------------------------------------------------------------
        // 渲染队列（全部走 Built-in 内置管线，ZWrite Off + Cull Off，靠队列排序）
        // ------------------------------------------------------------------
        public const int QueueBackdrop = 2000;      // 不透明深海背景（写深度）
        public const int QueueBackground = 3000;    // 背景层
        public const int QueueCaustics = 3010;      // 水底焦散
        public const int QueueBeam = 3012;          // 体积光束（压在人物之后，避免糊脸）
        public const int QueueBubbleBack = 3015;    // 深层气泡
        public const int QueueFigure = 3020;        // 人物
        public const int QueueSword = 3030;         // 剑
        public const int QueueFigureOverlay = 3040; // 握剑手局部上覆网格（手指遮住剑柄）
        public const int QueueForeground = 3050;    // 前景荷叶/水草
        public const int QueueBubbleFront = 3060;   // 浅层气泡 / 微尘
        public const int QueueRefraction = 3100;    // 全屏水折射叠加
        public const int QueueOverlay = 3150;       // 标题文字

        // ------------------------------------------------------------------
        // 各层在 z 轴上的浅纵深范围（单位；+z 更远 / 更深，-z 更靠近观众）
        // ------------------------------------------------------------------
        public const float ZBackdrop = 2.0f;            // 深海背景（最远）
        public const float ZBackgroundBase = 0.30f;     // 背景层底板
        public const float ZCaustics = 0.16f;           // 水底焦散
        public const float ZFigureBase = 0.0f;          // 人物底板（凸起朝 -z）
        public const float ZSword = -0.36f;             // 剑所在平面
        public const float ZForegroundBase = -0.30f;    // 前景荷叶 / 水草底板
        public const float ZRefraction = -1.15f;        // 全屏折射叠加（最近）

        // ------------------------------------------------------------------
        // 网格细分（单位=有效画面像素；Mesh 工厂按图层分辨率等比换算）
        // ------------------------------------------------------------------
        public const float GridStepBackgroundFramePx = 8f;
        public const float GridStepFigureFramePx = 4f;
        public const float GridStepForegroundFramePx = 6f;
        public const float GridStepSwordFramePx = 2.5f;

        /// <summary>剑的薄厚度（单位）。厚度剖面在轮廓处收窄，形成真实刀刃薄边。</summary>
        public const float SwordThickness = 0.075f;

        // ------------------------------------------------------------------
        // 骨架（世界单位，由上面的锚点换算而来）
        // ------------------------------------------------------------------
        public const float UpperArmLengthPx = 185.68f;  // |肩-肘|
        public const float ForeArmLengthPx = 90.76f;    // |肘-腕|

        // 手臂摆动角度（世界坐标，逆时针为正；0 = 原画姿态）
        public const float ReachUpperAngleDeg = -14f;   // 伸手：手臂向剑的方向（左下）摆
        public const float LiftUpperAngleDeg = 9f;      // 抬剑：手臂摆回并略过高
        public const float LiftForeAngleDeg = -8f;      // 抬剑：肘部轻微弯曲

        // 剑脱离手时的漂浮姿态（相对原画姿态）
        public static readonly Vector2 SwordFreeOffset = new Vector2(-0.50f, -0.75f);
        public const float SwordFreeAngleDeg = -22f;    // 原画剑轴约 -41.3°，再转 -22° 更陡更靠下
        public const int SwordFreeBobCycles = 3;        // 整数周期 -> 循环首尾速度连续

        // ------------------------------------------------------------------
        // 时序默认值（秒）
        // ------------------------------------------------------------------
        public const float DefaultDuration = 11.4f;
        public const float DefaultPause = 0.6f;
        public const float DefaultIntensity = 1f;

        // 行动时长内的相位分割（归一化，见 LadyLakeMotion）
        public const float PhaseIdleEnd = 0.16f;
        public const float PhaseReachEnd = 0.44f;
        public const float PhaseSummonEnd = 0.62f;
        public const float PhaseLiftEnd = 0.80f;
        public const float PhaseLowerEnd = 0.92f;

        public const float GlowIdle = 0.18f;
        public const float GlowSummon = 0.72f;
        public const float GlowPeak = 1f;
        public const float GlowLower = 0.42f;

        // 呼吸 / 发丝 / 水草摆动周期（整数，保证循环无缝）
        public const int BreathCycles = 3;
        public const int HairCycles = 2;
        public const float BreathAmplitude = 0.013f;
        public const float HairAmplitude = 0.030f;
        public const float ForegroundSwayAmplitude = 0.055f;
        public const int ForegroundSwayCycles = 2;

        // ------------------------------------------------------------------
        // 工具
        // ------------------------------------------------------------------

        /// <summary>原图画面坐标（左上原点，y 向下，单位 px）-> 世界坐标（画面中心为原点，y 向上）。</summary>
        public static Vector2 ArtToWorld(float xFromLeftPx, float yFromTopPx)
        {
            float x = (xFromLeftPx - FrameWidthPx * 0.5f) / PixelsPerUnit;
            float y = (FrameHeightPx * 0.5f - yFromTopPx) / PixelsPerUnit;
            return new Vector2(x, y);
        }

        public static Vector2 AnchorToWorld(Vector2 anchorPx)
        {
            return ArtToWorld(anchorPx.x, anchorPx.y);
        }

        /// <summary>
        /// 原图锚点（左上原点，y 向下）-> 画面坐标（左下原点，y 向上，单位 px）。
        /// 网格工厂一律使用「左下原点」空间；把带 y 偏移的特征（蒙皮权重、脸/躯干凸起、发丝）
        /// 从原图坐标搬进网格空间时必须经过这里，否则会上下镜像。
        /// </summary>
        public static Vector2 AnchorToFrame(Vector2 anchorPx)
        {
            return new Vector2(anchorPx.x, FrameHeightPx - anchorPx.y);
        }

        /// <summary>画面归一化坐标（左上原点）-> 画面像素（左下原点）。</summary>
        public static Vector2 FrameNormTopLeftToFramePx(float xNorm, float yFromTopNorm)
        {
            return new Vector2(xNorm * FrameWidthPx, (1f - yFromTopNorm) * FrameHeightPx);
        }

        /// <summary>画面坐标（左下原点，y 向上，单位 px）-> 未做透视补偿的平面坐标。</summary>
        public static Vector2 FramePxToPlane(float xFromLeftPx, float yFromBottomPx)
        {
            return new Vector2(
                (xFromLeftPx - FrameWidthPx * 0.5f) / PixelsPerUnit,
                (yFromBottomPx - FrameHeightPx * 0.5f) / PixelsPerUnit);
        }

        /// <summary>
        /// 画面像素 + 深度 z -> 世界坐标（含透视补偿）。
        /// 投影后与把该点放在 z=0 平面时的屏幕位置完全一致，因此浮雕不会拉伸原画构图。
        /// </summary>
        public static Vector3 FramePxToWorld(float xFramePx, float yFrameFromBottomPx, float z)
        {
            Vector2 plane = FramePxToPlane(xFramePx, yFrameFromBottomPx);
            float k = PerspectiveCompensation(z);
            return new Vector3(plane.x * k, plane.y * k, z);
        }

        public static float RoleToGridStep(LadyLakeLayerRole role)
        {
            switch (role)
            {
                case LadyLakeLayerRole.Background: return GridStepBackgroundFramePx;
                case LadyLakeLayerRole.Foreground: return GridStepForegroundFramePx;
                case LadyLakeLayerRole.Sword: return GridStepSwordFramePx;
                default: return GridStepFigureFramePx;
            }
        }

        public static string RoleToFileName(LadyLakeLayerRole role)
        {
            switch (role)
            {
                case LadyLakeLayerRole.Background: return BackgroundFileName;
                case LadyLakeLayerRole.Foreground: return ForegroundFileName;
                case LadyLakeLayerRole.Sword: return SwordFileName;
                default: return FigureFileName;
            }
        }
    }

    /// <summary>材质/着色器上使用的全局动画参数。Editor 取样与运行时 Update 共用同一条路径。</summary>
    public static class LadyLakeShaderGlobals
    {
        public static readonly int TimeId = Shader.PropertyToID("_LadyLakeTime");
        public static readonly int LoopId = Shader.PropertyToID("_LadyLakeLoop");
        public static readonly int IntensityId = Shader.PropertyToID("_LadyLakeIntensity");

        /// <summary>
        /// _LadyLakeTime 是**不取模**的环境时间：水流 / 焦散 / 光束持续演进，
        /// 动作循环回卷时不会发生相位跳变。
        /// _LadyLakeLoop 是 0..1 的动作循环相位，供必须严格无缝的效果使用整数周期相位。
        /// </summary>
        public static void Set(float ambientSeconds, float loopPhase, float intensity)
        {
            Shader.SetGlobalFloat(TimeId, ambientSeconds);
            Shader.SetGlobalFloat(LoopId, loopPhase);
            Shader.SetGlobalFloat(IntensityId, intensity);
        }
    }
}
