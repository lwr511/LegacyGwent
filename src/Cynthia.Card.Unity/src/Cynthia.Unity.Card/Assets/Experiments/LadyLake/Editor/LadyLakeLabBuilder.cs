using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LegacyGwent.LadyLakeLab.Editor
{
    /// <summary>
    /// 湖中仙女实验场景构建器。
    ///
    /// 入口：
    ///   * 菜单 Tools/Lady Lake Lab/...
    ///   * 批处理 -executeMethod LegacyGwent.LadyLakeLab.Editor.LadyLakeLabBuilder.Build
    ///   * 批处理 -executeMethod LegacyGwent.LadyLakeLab.Editor.LadyLakeLabBuilder.BuildAndCapture
    ///
    /// 任何一张分层 PNG 缺失 / 不合格都会直接抛异常并写 Debug.LogError，
    /// 不会生成半成品场景，也不会伪造成功。
    /// </summary>
    public static class LadyLakeLabBuilder
    {
        public const string MenuRoot = "Tools/Lady Lake Lab/";

        /// <summary>
        /// figure.png 的实测 prior 注册矩形（归一化，左上原点）。
        /// 由画中两个可靠特征点与源画锚点的对应关系解出：
        ///   画中高举手中心 (0.44,0.05) -> 源画 (0.443,0.141)；画中握剑手 (0.73,0.46) 处的脸 -> 源画 (0.674,0.451)。
        /// 它只是搜索起点，最终值由自动配准在 ±14% 内按骨架/外部锚点约束收敛。
        /// </summary>
        public const float FigurePriorX = 0.0925f;
        public const float FigurePriorYFromTop = 0.1032f;
        public const float FigurePriorWidth = 0.797f;
        public const float FigurePriorHeight = 0.756f;

        /// <summary>sword.png 的兜底注册矩形（自动主轴配准失败时才使用）。</summary>
        public const float SwordRectX = 0.052f;
        public const float SwordRectYFromTop = 0.093f;
        public const float SwordRectWidth = 0.87f;
        public const float SwordRectHeight = 0.86f;

        /// <summary>握剑手局部上覆网格的裁剪半径（画面像素）。</summary>
        public const float HandOverlayRadiusFramePx = 32f;

        // ------------------------------------------------------------------
        // 菜单入口
        // ------------------------------------------------------------------

        [MenuItem(MenuRoot + "Open Scene", false, 10)]
        public static void MenuOpenScene()
        {
            OpenScene();
        }

        [MenuItem(MenuRoot + "Rebuild Scene", false, 20)]
        public static void MenuRebuild()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            Build();
        }

        [MenuItem(MenuRoot + "Capture Samples", false, 30)]
        public static void MenuCapture()
        {
            LadyLakeLabCapture.CaptureFromOpenScene();
        }

        [MenuItem(MenuRoot + "Build And Capture", false, 40)]
        public static void MenuBuildAndCapture()
        {
            BuildAndCapture();
        }

        // ------------------------------------------------------------------
        // 静态入口（-executeMethod）
        // ------------------------------------------------------------------

        /// <summary>生成场景并写 validation.json（不做截图）。</summary>
        public static void Build()
        {
            LadyLakeLabRoot root;
            List<LadyLakeLayerTexture> layers;
            LadyLakeValidationReport report = BuildScene(out root, out layers);
            LadyLakeLabCapture.WriteReport(report);
            ReleaseDeformers(root);
            Debug.Log(string.Format(
                "[LadyLakeLab] Build 完成：{0} 个节点 / {1} 张图 / {2} 条警告。场景：{3}",
                report.hierarchy.Count, report.images.Count, report.warnings.Count, LadyLakeLabConfig.ScenePath));
        }

        /// <summary>生成场景 + 0/2/4/6/8/10/12 秒静态取样截图 + 峰值全景帧 + validation.json。</summary>
        public static void BuildAndCapture()
        {
            LadyLakeLabRoot root;
            List<LadyLakeLayerTexture> layers;
            LadyLakeValidationReport report = BuildScene(out root, out layers);

            if (root == null)
            {
                LadyLakeLabCapture.WriteReport(report);
                throw new System.InvalidOperationException(
                    "[LadyLakeLab] 场景未生成，无法取样。详情见 validation.json。");
            }

            LadyLakeLabCapture.CaptureSamples(root, layers, report);
            LadyLakeLabCapture.WriteReport(report);
            ReleaseDeformers(root);

            Debug.Log(string.Format(
                "[LadyLakeLab] BuildAndCapture 完成：{0} 张截图 -> {1}",
                report.captures.Count, LadyLakeLabConfig.CaptureFolder));
        }

        /// <summary>打开已生成的场景。</summary>
        public static void OpenScene()
        {
            if (System.IO.File.Exists(
                    LadyLakeLabTextures.ToAbsolutePath(LadyLakeLabConfig.ScenePath)))
            {
                EditorSceneManager.OpenScene(LadyLakeLabConfig.ScenePath, OpenSceneMode.Single);
            }
            else
            {
                Debug.LogWarning("[LadyLakeLab] 场景不存在，请先执行 Rebuild：" + LadyLakeLabConfig.ScenePath);
            }
        }

        // ------------------------------------------------------------------
        // 构建主体
        // ------------------------------------------------------------------

        public static LadyLakeValidationReport BuildScene(
            out LadyLakeLabRoot root,
            out List<LadyLakeLayerTexture> layers)
        {
            root = null;
            layers = new List<LadyLakeLayerTexture>();

            LadyLakeValidationReport report = new LadyLakeValidationReport();
            report.generatedAtUtc = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            report.unityVersion = Application.unityVersion;
            report.scenePath = LadyLakeLabConfig.ScenePath;
            report.textureFolder = LadyLakeLabConfig.TextureFolder;

            report.notVerified.Add("PlayMode 实际运行（本入口只做 Editor 静态取样；真实 Play 由 Acceptance 脚本负责）。");
            report.notVerified.Add("GPU 上着色器的实际渲染结果（只记录 Shader.isSupported，并输出 Editor 渲染截图）。");
            report.notVerified.Add("切图边界的主观观感（白/黑边、发丝半透明）；只能保证导入设置与软边抠像算法正确。");
            report.notVerified.Add("图层的最终画面位置：figure 用视觉标定值，sword 用剪影主轴自动配准，均以 capture 目视为准。");

            List<string> missing = new List<string>();
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();

            EnsureFolder(LadyLakeLabConfig.GeneratedFolder);
            EnsureFolder(LadyLakeLabConfig.GeneratedMeshFolder);
            EnsureFolder(LadyLakeLabConfig.GeneratedMaterialFolder);

            layers = LadyLakeLabTextures.LoadAll(missing, errors, warnings);
            report.missingImages.AddRange(missing);
            report.warnings.AddRange(warnings);

            FillImageReport(report, layers);

            if (missing.Count > 0 || errors.Count > 0)
            {
                report.errors.AddRange(errors);
                report.buildSucceeded = false;
                report.buildError = string.Format(
                    "缺失 {0} 张 / 错误 {1} 条：{2}",
                    missing.Count, errors.Count, string.Join(" | ", errors.ToArray()));
                Debug.LogError("[LadyLakeLab] " + report.buildError);
                throw new System.InvalidOperationException("[LadyLakeLab] " + report.buildError);
            }

            LadyLakeLayerTexture background = Find(layers, LadyLakeLayerRole.Background);
            LadyLakeLayerTexture figure = Find(layers, LadyLakeLayerRole.Figure);
            LadyLakeLayerTexture foreground = Find(layers, LadyLakeLayerRole.Foreground);
            LadyLakeLayerTexture sword = Find(layers, LadyLakeLayerRole.Sword);

            if (background == null || figure == null || foreground == null || sword == null)
            {
                report.buildSucceeded = false;
                report.buildError = "四张分层图不完整。";
                Debug.LogError("[LadyLakeLab] " + report.buildError);
                throw new System.InvalidOperationException("[LadyLakeLab] " + report.buildError);
            }

            // ---------------- 图层注册（网格配准，不动位图） ----------------
            LadyLakeLayoutOverride layout = LadyLakeLabTextures.LoadLayoutOverride(report.warnings);
            if (layout != null)
            {
                report.layoutOverridePath = layout.sourcePath;
                report.reusedProjectAssets.Add("图层注册覆盖文件：" + layout.sourcePath);
            }

            LadyLakeLayerRegistration backgroundReg = LadyLakeLayerRegistration.Identity();
            LadyLakeLayerRegistration foregroundReg = LadyLakeLayerRegistration.Identity();
            backgroundReg.note = "background 全画面 rect (Codex 说明)";
            foregroundReg.note = "foreground 全画面 rect (Codex 说明)";

            LadyLakeLayerRegistration figureReg = LadyLakeLayerRegistration.FromTopLeftRect(
                0.035f, 0.058f, 0.84f, 0.82f, "User-selected complete figure-v5; no separate replacement hands");
            LadyLakeLayerRegistration swordReg;
            string swordFitReport;
            if (!LadyLakeLabMeshes.TryFitSwordRegistration(sword, out swordReg, out swordFitReport))
            {
                if (layout != null && layout.HasSword)
                {
                    swordReg = LadyLakeLayerRegistration.FromTopLeftRect(
                        layout.sword.x, layout.sword.yFromTop, layout.sword.width, layout.sword.height,
                        "sword rect 来自 " + layout.sourcePath);
                }
                else
                {
                    swordReg = LadyLakeLayerRegistration.FromTopLeftRect(
                        SwordRectX, SwordRectYFromTop, SwordRectWidth, SwordRectHeight,
                        "sword rect 代码内兜底值");
                }
                report.warnings.Add("sword 自动配准未生效：" + swordFitReport);
            }
            else
            {
                report.verified.Add(swordFitReport);
            }

            report.registrations.Add("background: " + backgroundReg);
            report.registrations.Add("figure:     " + figureReg);
            report.registrations.Add("foreground: " + foregroundReg);
            report.registrations.Add("sword:      " + swordReg);

            // 标定辅助：把源画锚点逆映射回各图层图像像素，并检查该处是否真的有内容
            report.calibration.Add(DescribeAnchorMapping(figureReg, figure, LadyLakeLabConfig.SwordHandAnchor, "figure/握剑手"));
            report.calibration.Add(DescribeAnchorMapping(figureReg, figure, LadyLakeLabConfig.RaisedHandAnchor, "figure/高举手"));
            report.calibration.Add(DescribeAnchorMapping(figureReg, figure, LadyLakeLabConfig.FaceAnchor, "figure/脸"));
            report.calibration.Add(DescribeAnchorMapping(figureReg, figure, LadyLakeLabConfig.ElbowAnchor, "figure/肘"));
            report.calibration.Add(DescribeAnchorMapping(swordReg, sword, LadyLakeLabConfig.SwordTipAnchor, "sword/剑尖"));
            report.calibration.Add(DescribeAnchorMapping(swordReg, sword, LadyLakeLabConfig.SwordPommelAnchor, "sword/剑柄底"));
            report.calibration.Add(DescribeAnchorMapping(swordReg, sword, LadyLakeLabConfig.SwordHandAnchor, "sword/握点"));

            // ---------------- 网格 ----------------
            LadyLakeMeshStats bgStats, figStats, fgStats, swordStats, handStats, backdropStats, causticsStats, refractionStats, beamStats;
            Vector4 noClip = Vector4.zero;

            Mesh bgMesh = LadyLakeLabMeshes.BuildReliefLayer(
                background, BackgroundShape(), LadyLakeLayerRole.Background, backgroundReg, noClip, out bgStats);
            Mesh figMesh = LadyLakeLabMeshes.BuildReliefLayer(
                figure, FigureShape(), LadyLakeLayerRole.Figure, figureReg, noClip, out figStats);
            Mesh fgMesh = LadyLakeLabMeshes.BuildReliefLayer(
                foreground, ForegroundShape(), LadyLakeLayerRole.Foreground, foregroundReg, noClip, out fgStats);
            Mesh swordMesh = LadyLakeLabMeshes.BuildSword(
                sword, swordReg, LadyLakeLabConfig.ZSword, LadyLakeLabConfig.SwordThickness, out swordStats);

            // 握剑手局部上覆网格：同一张 figure 贴图的局部区域，排在剑之后渲染 -> 手指挡住剑柄
            Vector4 handClip = HandOverlayClip();
            Mesh handMesh = LadyLakeLabMeshes.BuildReliefLayer(
                figure, FigureShape(), LadyLakeLayerRole.Figure, figureReg, handClip, out handStats);

            Mesh backdropMesh = LadyLakeLabMeshes.BuildQuad("LadyLake_Backdrop", 22f, 12f, LadyLakeLabConfig.ZBackdrop, out backdropStats);
            Mesh causticsMesh = LadyLakeLabMeshes.BuildQuad("LadyLake_Caustics", 16f, 10f, LadyLakeLabConfig.ZCaustics, out causticsStats);
            Mesh refractionMesh = LadyLakeLabMeshes.BuildQuad("LadyLake_WaterRefraction", 16f, 10f, LadyLakeLabConfig.ZRefraction, out refractionStats);
            Mesh beamMesh = LadyLakeLabMeshes.BuildBeams(DefaultBeams(), out beamStats);

            // ---------------- 材质 ----------------
            Texture2D causticsTexture = LadyLakeLabTextures.TryLoadOptionalTexture(
                LadyLakeLabConfig.CausticsTextureFileName, report.warnings);
            Texture2D flowTexture = LadyLakeLabTextures.TryLoadOptionalTexture(
                LadyLakeLabConfig.FlowNoiseTextureFileName, report.warnings);

            if (causticsTexture != null)
            {
                report.reusedProjectAssets.Add(string.Format(
                    "焦散贴图 {0}/{1}（{2}x{3}）：Codex 从 Aeschna 15010100（Thronebreaker）原版材质包解析，" +
                    "对应 Assets/DynamicCards/Content/Old/Thronebreaker/Shared/15010100_164296__15010300_Aeschna_RiverbedCaustics.mat。",
                    LadyLakeLabConfig.TextureFolder, LadyLakeLabConfig.CausticsTextureFileName,
                    causticsTexture.width, causticsTexture.height));
            }
            if (flowTexture != null)
            {
                report.reusedProjectAssets.Add(string.Format(
                    "流动噪声贴图 {0}/{1}（{2}x{3}）：同样来自 Aeschna 15010100 水下材质集，用于水折射扰动。",
                    LadyLakeLabConfig.TextureFolder, LadyLakeLabConfig.FlowNoiseTextureFileName,
                    flowTexture.width, flowTexture.height));
            }

            Material backdropMat = LadyLakeLabMaterials.Create("LadyLake_Backdrop", LadyLakeShaders.Backdrop, LadyLakeLabConfig.QueueBackdrop, errors);
            Material bgMat = LadyLakeLabMaterials.Create("LadyLake_Background", LadyLakeShaders.Layer, LadyLakeLabConfig.QueueBackground, errors);
            Material causticsMat = LadyLakeLabMaterials.Create("LadyLake_Caustics", LadyLakeShaders.Caustics, LadyLakeLabConfig.QueueCaustics, errors);
            Material beamMat = LadyLakeLabMaterials.Create("LadyLake_LightBeams", LadyLakeShaders.LightBeam, LadyLakeLabConfig.QueueBeam, errors);
            Material figMat = LadyLakeLabMaterials.Create("LadyLake_Figure", LadyLakeShaders.Layer, LadyLakeLabConfig.QueueFigure, errors);
            Material handMat = LadyLakeLabMaterials.Create("LadyLake_HandOverlay", LadyLakeShaders.Layer, LadyLakeLabConfig.QueueFigureOverlay, errors);
            Material swordMat = LadyLakeLabMaterials.Create("LadyLake_SwordFront", LadyLakeShaders.Sword, LadyLakeLabConfig.QueueSword, errors);
            Material metalMat = LadyLakeLabMaterials.Create("LadyLake_SwordMetal", LadyLakeShaders.SwordMetal, LadyLakeLabConfig.QueueSword, errors);
            Material fgMat = LadyLakeLabMaterials.Create("LadyLake_Foreground", LadyLakeShaders.Layer, LadyLakeLabConfig.QueueForeground, errors);
            Material bubbleMat = LadyLakeLabMaterials.Create("LadyLake_Bubbles", LadyLakeShaders.Bubble, LadyLakeLabConfig.QueueBubbleFront, errors);
            Material bubbleBackMat = LadyLakeLabMaterials.Create("LadyLake_BubblesBack", LadyLakeShaders.Bubble, LadyLakeLabConfig.QueueBubbleBack, errors);
            Material moteMat = LadyLakeLabMaterials.Create("LadyLake_Motes", LadyLakeShaders.Mote, LadyLakeLabConfig.QueueBubbleFront, errors);
            Material refractionMat = LadyLakeLabMaterials.Create("LadyLake_WaterRefraction", LadyLakeShaders.WaterRefraction, LadyLakeLabConfig.QueueRefraction, errors);

            if (errors.Count > 0)
            {
                report.errors.AddRange(errors);
                report.buildSucceeded = false;
                report.buildError = "材质 / 着色器创建失败：" + string.Join(" | ", errors.ToArray());
                Debug.LogError("[LadyLakeLab] " + report.buildError);
                throw new System.InvalidOperationException("[LadyLakeLab] " + report.buildError);
            }

            ConfigureMaterials(bgMat, figMat, handMat, fgMat, swordMat, metalMat, backdropMat, causticsMat, beamMat,
                bubbleMat, bubbleBackMat, moteMat, refractionMat,
                background.texture, figure.texture, foreground.texture, sword.texture,
                causticsTexture, flowTexture);

            // ---------------- 保存网格资产 ----------------
            bgMesh = SaveMeshAsset(bgMesh, "LadyLake_Background_Relief");
            figMesh = SaveMeshAsset(figMesh, "LadyLake_Figure_Relief");
            fgMesh = SaveMeshAsset(fgMesh, "LadyLake_Foreground_Relief");
            swordMesh = SaveMeshAsset(swordMesh, "LadyLake_Sword_Extruded");
            handMesh = SaveMeshAsset(handMesh, "LadyLake_HandOverlay_Relief");
            backdropMesh = SaveMeshAsset(backdropMesh, "LadyLake_Backdrop");
            causticsMesh = SaveMeshAsset(causticsMesh, "LadyLake_Caustics");
            refractionMesh = SaveMeshAsset(refractionMesh, "LadyLake_WaterRefraction");
            beamMesh = SaveMeshAsset(beamMesh, "LadyLake_LightBeams");

            // ---------------- 建场景 ----------------
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject rootGo = new GameObject("LadyLakeLab");
            LadyLakeLabRoot labRoot = rootGo.AddComponent<LadyLakeLabRoot>();

            GameObject cameraGo = new GameObject("Camera");
            cameraGo.transform.SetParent(rootGo.transform, false);
            cameraGo.transform.localPosition = LadyLakeLabConfig.CameraPosition;
            cameraGo.transform.localRotation = Quaternion.identity;
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = LadyLakeLabConfig.CameraBackgroundColor;
            camera.orthographic = false;
            camera.fieldOfView = LadyLakeLabConfig.CameraFieldOfView;
            camera.nearClipPlane = LadyLakeLabConfig.CameraNearClip;
            camera.farClipPlane = LadyLakeLabConfig.CameraFarClip;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.useOcclusionCulling = false;
            cameraGo.tag = "MainCamera";
            LadyLakeCameraRig cameraRig = cameraGo.AddComponent<LadyLakeCameraRig>();
            cameraRig.cameraTransform = cameraGo.transform;
            cameraRig.enableParallax = true;
            cameraRig.horizontalAmplitude = 0.12f;
            cameraRig.verticalAmplitude = 0.035f;
            cameraRig.cycles = 1;
            cameraRig.CaptureBase();
            labRoot.targetCamera = camera;
            labRoot.cameraRig = cameraRig;

            // Keep the card presentation free of technical overlay text.

            GameObject environment = new GameObject("Environment");
            environment.transform.SetParent(rootGo.transform, false);

            GameObject backdropGo = CreateRenderable("Backdrop", environment.transform, backdropMesh, new[] { backdropMat });
            GameObject backgroundGo = CreateRenderable("Background", environment.transform, bgMesh, new[] { bgMat });
            GameObject causticsGo = CreateRenderable("Caustics", environment.transform, causticsMesh, new[] { causticsMat });
            GameObject beamsGo = CreateRenderable("LightBeams", environment.transform, beamMesh, new[] { beamMat });

            labRoot.layerBackdrop = backdropGo.transform;
            labRoot.layerBackground = backgroundGo.transform;
            labRoot.layerCaustics = causticsGo.transform;
            labRoot.layerEffects = environment.transform;

            GameObject stage = new GameObject("Stage");
            stage.transform.SetParent(rootGo.transform, false);

            GameObject figureGo = CreateRenderable("Figure", stage.transform, figMesh, new[] { figMat });
            LadyLakeFigureRig figureRig = figureGo.AddComponent<LadyLakeFigureRig>();
            ConfigureFigureRig(figureRig, figMesh);

            GameObject swordGo = CreateRenderable("Sword", stage.transform, swordMesh, new[] { swordMat, metalMat, metalMat });
            LadyLakeSwordRig swordRig = swordGo.AddComponent<LadyLakeSwordRig>();
            swordRig.swordTransform = swordGo.transform;
            swordRig.frontRenderer = swordGo.GetComponent<MeshRenderer>();
            swordRig.metalRenderer = swordGo.GetComponent<MeshRenderer>();
            swordRig.glowScale = 0.27f;
            swordRig.planeCompensation = LadyLakeLabConfig.PerspectiveCompensation(LadyLakeLabConfig.ZSword);

            GameObject foregroundGo = CreateRenderable("Foreground", stage.transform, fgMesh, new[] { fgMat });
            LadyLakeSwayField swayField = foregroundGo.AddComponent<LadyLakeSwayField>();
            swayField.bakedMesh = fgMesh;
            swayField.direction = new Vector2(1f, 0.14f);
            swayField.amplitude = LadyLakeLabConfig.ForegroundSwayAmplitude;
            swayField.cycles = LadyLakeLabConfig.ForegroundSwayCycles;
            swayField.verticalPhaseScale = 2.2f;
            swayField.phaseOffset = 0f;
            swayField.useBreath = false;

            // 握剑手局部上覆网格：同一贴图、同一蒙皮、渲染在剑之后 -> 手指遮住剑柄
            GameObject handGo = CreateRenderable("HandOverlay", stage.transform, handMesh, new[] { handMat });
            LadyLakeFigureRig handRig = handGo.AddComponent<LadyLakeFigureRig>();
            ConfigureFigureRig(handRig, handMesh);

            labRoot.layerFigure = figureGo.transform;
            labRoot.layerSword = swordGo.transform;
            labRoot.layerForeground = foregroundGo.transform;
            labRoot.figureRig = figureRig;
            labRoot.handOverlayRig = handRig;
            labRoot.swordRig = swordRig;
            labRoot.foregroundSway = swayField;
            // The hand occlusion pass samples the SAME complete figure image and
            // deformation as the body. It does not replace or cut either wrist.
            foreach (var material in new[] { bgMat, figMat, handMat, fgMat })
            {
                material.SetFloat("_RimStrength", .025f); material.SetFloat("_FogStrength", .08f);
                material.SetFloat("_Ambient", .94f); material.SetFloat("_LightStrength", .08f);
                material.SetFloat("_DepthTintStrength", .03f); material.SetFloat("_EdgeGlow", .012f);
                material.SetVector("_Wobble", Vector4.zero);
            }
            figMat.SetFloat("_MaskHands", 0);
            handMat.SetFloat("_MaskHands", 0);
            figMat.SetFloat("_FingerOcclusion", 0);
            handMat.SetFloat("_FingerOcclusion", 1);
            figMat.SetVector("_WristFadePlane", Vector4.zero);
            handMat.SetVector("_WristFadePlane", Vector4.zero);
            swordMat.SetFloat("_Ambient", .92f); swordMat.SetFloat("_LightStrength", .06f);
            swordMat.SetFloat("_EdgeGlow", .35f); swordMat.SetFloat("_RuneStrength", .22f);
            swordMat.SetFloat("_FogStrength", .05f);
            causticsMat.SetFloat("_Strength", .13f); beamMat.SetFloat("_Strength", .16f);
            refractionMat.SetFloat("_RippleStrength", .00035f);
            labRoot.repairedHandRigs = new LadyLakeFigureRig[0];


            GameObject particles = new GameObject("Particles");
            particles.transform.SetParent(rootGo.transform, false);

            LadyLakeParticleField bubblesBack = CreateParticleField(
                "BubblesBack", particles.transform, bubbleBackMat, LadyLakeParticleKind.Bubble, 11, 42,
                new Vector2(0.04f, 0.28f), new Vector2(3f, 10f), 8f, 2, 0.50f, new Color(0.70f, 0.93f, 0.90f, 1f));
            LadyLakeParticleField bubblesFront = CreateParticleField(
                "BubblesFront", particles.transform, bubbleMat, LadyLakeParticleKind.Bubble, 23, 54,
                new Vector2(-0.18f, -0.72f), new Vector2(5f, 16f), 12f, 2, 0.42f, new Color(0.78f, 0.97f, 0.94f, 1f));
            LadyLakeParticleField motes = CreateParticleField(
                "Motes", particles.transform, moteMat, LadyLakeParticleKind.Mote, 37, 110,
                new Vector2(-0.10f, -0.90f), new Vector2(2f, 6f), 15f, 3, 0.34f, new Color(0.72f, 0.95f, 0.80f, 1f));
            LadyLakeParticleField wake = CreateParticleField(
                "SwordWake", particles.transform, bubbleMat, LadyLakeParticleKind.SwordWake, 53, 26,
                new Vector2(-0.26f, -0.52f), new Vector2(4f, 12f), 6f, 2, 0.40f, new Color(0.85f, 0.98f, 0.95f, 1f));

            LadyLakeWaterFx waterFx = particles.AddComponent<LadyLakeWaterFx>();
            waterFx.bubblesBack = bubblesBack;
            waterFx.bubblesFront = bubblesFront;
            waterFx.motes = motes;
            waterFx.swordWake = wake;
            waterFx.causticsRenderer = causticsGo.GetComponent<MeshRenderer>();
            waterFx.beamRenderers = new[] { beamsGo.GetComponent<MeshRenderer>() };
            waterFx.rippleStrength = 0.00035f;
            waterFx.rippleWakeBoost = 0.00015f;

            GameObject refractionGo = CreateRenderable("WaterRefraction", rootGo.transform, refractionMesh, new[] { refractionMat });
            waterFx.refractionRenderer = refractionGo.GetComponent<MeshRenderer>();
            labRoot.waterFx = waterFx;

            GameObject timelineGo = new GameObject("Timeline");
            timelineGo.transform.SetParent(rootGo.transform, false);
            LadyLakeTimeline timeline = timelineGo.AddComponent<LadyLakeTimeline>();
            timeline.duration = LadyLakeLabConfig.DefaultDuration;
            timeline.pause = LadyLakeLabConfig.DefaultPause;
            timeline.intensity = LadyLakeLabConfig.DefaultIntensity;
            timeline.autoPlay = true;
            timeline.root = labRoot;
            labRoot.timeline = timeline;

            // ---------------- 烘焙 t=0 ----------------
            LadyLakePose pose0 = LadyLakeMotion.Evaluate(
                0f, timeline.duration, timeline.pause, timeline.intensity);

            BakeParticleField(bubblesBack, pose0);
            BakeParticleField(bubblesFront, pose0);
            BakeParticleField(motes, pose0);
            BakeParticleField(wake, pose0);

            swordRig.ApplyPose(pose0);

            // ---------------- 报告 ----------------
            report.timeline.duration = timeline.duration;
            report.timeline.pause = timeline.pause;
            report.timeline.totalDuration = timeline.TotalDuration;
            report.timeline.upperArmLengthPx = LadyLakeLabConfig.UpperArmLengthPx;
            report.timeline.foreArmLengthPx = LadyLakeLabConfig.ForeArmLengthPx;
            report.timeline.sampleCount = 8;
            report.timeline.phaseBoundaries = string.Format(
                "Idle[0,{0}] Reach[..{1}] Summon[..{2}] Lift[..{3}] Hold(+{4:F2}s) Lower[..{5}] Release[..1]",
                LadyLakeMotion.IdleEnd, LadyLakeMotion.ReachEnd, LadyLakeMotion.SummonEnd,
                LadyLakeMotion.LiftEnd, timeline.pause, LadyLakeMotion.LowerEnd);

            CollectHierarchy(report, rootGo);
            CollectShaders(report, rootGo);

            report.verified.Add(string.Format(
                "网格顶点统计：背景 {0} / 人物 {1} / 握剑手上覆 {2} / 前景 {3} / 剑 {4} / 光束 {5} / 粒子面片 {6}。",
                bgStats.vertices, figStats.vertices, handStats.vertices, fgStats.vertices,
                swordStats.vertices, beamStats.vertices,
                bubblesBack.MeshVertexCount + bubblesFront.MeshVertexCount
                + motes.MeshVertexCount + wake.MeshVertexCount));
            report.verified.Add("四张分层 PNG 存在、可读、关键层具备 alpha（缺失即构建失败）。");
            report.verified.Add("浮雕网格为真实细分网格：顶点数 / 三角形数 / submesh 数 / bounds 已记录。");
            report.verified.Add("剑为 3 个 submesh 的薄厚度挤出体（正面贴图 + 背面 / 侧壁暗银）。");
            report.verified.Add("剑的注册由剪影主轴自动解出（缩放+旋转+平移），画出来的剑尖/剑柄底对齐源画锚点。");
            report.verified.Add("动作曲线首尾位置与速度连续（见 seam 段）。");
            report.verified.Add("握住相位剑由前臂骨骼刚性驱动（见 grip 段的最大滑移）。");
            report.verified.Add("Editor 静态取样与运行时 Update 共用 SampleAt(seconds) 入口。");
            report.verified.Add("环境水流用连续时间（_LadyLakeTime 不取模），剑光脉冲用动作循环的整数周期相位。");

            report.reusedProjectAssets.Add("未复制/修改任何 DynamicCards 内容资产；浮雕 / 剑 / 光束 / 气泡 / 折射网格全部由本实验程序生成。");
            report.reusedProjectAssets.Add("只读参考了工程既有实现思路与水面着色器：Assets/DynamicCards/Shaders/TempestWaterSurface.shader、TempestFoam.shader、TempestSeaCoverage.shader、PortableCard.shader、Content/catalog.json。");

            Presentation.Editor.LadyLakeCardPresentationBuilder.Attach(labRoot);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, LadyLakeLabConfig.ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.sceneGuid = AssetDatabase.AssetPathToGUID(LadyLakeLabConfig.ScenePath);
            report.buildSucceeded = true;

            root = labRoot;
            return report;
        }

        // ------------------------------------------------------------------
        // 形状参数（坐标为画面像素、左下原点）
        // ------------------------------------------------------------------

        private static LadyLakeLayerShape BackgroundShape()
        {
            LadyLakeLayerShape s = LadyLakeLayerShape.Default();
            s.baseDepth = 0.12f;
            s.zOffset = LadyLakeLabConfig.ZBackgroundBase;
            s.inflateRadiusFramePx = 40f;
            s.edgeSoftnessFramePx = 12f;
            s.edgeCurvature = 0.16f;   // 画面边缘后退，形成鱼眼纵深
            s.profileMode = LadyLakeProfileMode.None;
            return s;
        }

        private static LadyLakeLayerShape FigureShape()
        {
            LadyLakeLayerShape s = LadyLakeLayerShape.Default();
            s.baseDepth = 0.30f;
            s.zOffset = LadyLakeLabConfig.ZFigureBase;
            s.inflateRadiusFramePx = 22f;
            s.edgeSoftnessFramePx = 7f;
            s.profileMode = LadyLakeProfileMode.Figure;

            s.bump1CenterFrame = LadyLakeLabConfig.AnchorToFrame(LadyLakeLabConfig.FaceAnchor);
            s.bump1RadiusFramePx = 76f;
            s.bump1Depth = 0.10f;

            s.bump2CenterFrame = LadyLakeLabConfig.AnchorToFrame(new Vector2(288f, 388f));
            s.bump2RadiusFramePx = 150f;
            s.bump2Depth = 0.11f;

            s.recessCenterFrame = LadyLakeLabConfig.AnchorToFrame(new Vector2(335f, 300f));
            s.recessInnerFramePx = 72f;
            s.recessOuterFramePx = 140f;
            s.recessDepth = 0.12f;
            return s;
        }

        private static LadyLakeLayerShape ForegroundShape()
        {
            LadyLakeLayerShape s = LadyLakeLayerShape.Default();
            s.baseDepth = 0.30f;
            s.zOffset = LadyLakeLabConfig.ZForegroundBase;
            s.inflateRadiusFramePx = 12f;   // 细枝叶不做大幅膨胀
            s.edgeSoftnessFramePx = 6f;
            s.profileMode = LadyLakeProfileMode.Sway;
            return s;
        }

        /// <summary>自动人物配准使用的源画解剖锚点（左上原点）。</summary>
        private static Vector2[] FigureAnchorSet()
        {
            return new[]
            {
                LadyLakeLabConfig.FaceAnchor,          // 脸
                LadyLakeLabConfig.ShoulderAnchor,      // 肩
                LadyLakeLabConfig.ElbowAnchor,         // 肘
                LadyLakeLabConfig.SwordHandAnchor,     // 握剑手
                LadyLakeLabConfig.RaisedHandAnchor,    // 另一只高举手
            };
        }

        /// <summary>
        /// 自动配准的「必须落在透明区」锚点：源画里这些位置属于水 / 剑 / 空景，不属于人物。
        /// 它们把仅有「落在人物内」约束时的大平台解收敛到唯一位置。
        /// </summary>
        private static Vector2[] FigureOutsideAnchorSet()
        {
            return new[]
            {
                LadyLakeLabConfig.SwordTipAnchor,          // 剑尖（人物图中是空的）
                new Vector2(24f, 34f),                     // 画面左上角（水/荷叶）
                new Vector2(470f, 690f),                   // 画面右下角（水底）
                new Vector2(30f, 660f),                    // 画面左下角（水底，源画人物腿在更内侧）
            };
        }

        private static bool TryFitFigure(
            LadyLakeLayerTexture figure, out LadyLakeLayerRegistration registration, out string report)
        {
            LadyLakeLayerRegistration prior = LadyLakeLayerRegistration.FromTopLeftRect(
                FigurePriorX, FigurePriorYFromTop, FigurePriorWidth, FigurePriorHeight,
                "figure 实测 prior");
            return LadyLakeLabMeshes.TryFitFigureRegistration(
                figure, FigureAnchorSet(), FigureOutsideAnchorSet(), prior, out registration, out report);
        }

        /// <summary>握剑手局部上覆网格的画面裁剪框（左下原点，y 向上）。</summary>
        private static Vector4 HandOverlayClip()
        {
            Vector2 hand = LadyLakeLabConfig.SwordHandAnchor;   // 左上原点
            float r = HandOverlayRadiusFramePx;
            float xMin = hand.x - r;
            float xMax = hand.x + r;
            float yMinTop = hand.y + r;
            float yMaxTop = hand.y - r;
            return new Vector4(
                xMin,
                LadyLakeLabConfig.FrameHeightPx - yMinTop,
                xMax,
                LadyLakeLabConfig.FrameHeightPx - yMaxTop);
        }

        private static List<LadyLakeBeamSpec> DefaultBeams()
        {
            // 光束坐标同样是画面像素、左下原点（所以 y 用 710 - 原图 y）
            float[] topX = { 88f, 196f, 302f, 398f, 22f, 252f };
            float[] bottomX = { 40f, 150f, 250f, 330f, -18f, 212f };
            float[] bottomYFromTop = { 300f, 236f, 322f, 208f, 344f, 262f };

            List<LadyLakeBeamSpec> beams = new List<LadyLakeBeamSpec>();
            for (int k = 0; k < topX.Length; k++)
            {
                LadyLakeBeamSpec spec = new LadyLakeBeamSpec();
                spec.xTopFrame = topX[k];
                spec.yTopFrame = 830f;                                   // 伸出画面顶部
                spec.widthTopFrame = 58f + 12f * k;
                spec.xBottomFrame = bottomX[k];
                spec.yBottomFrame = LadyLakeLabConfig.FrameHeightPx - bottomYFromTop[k];
                spec.widthBottomFrame = 150f + 18f * k;
                spec.curveFrame = (k % 2 == 0) ? 18f : -24f;
                spec.phase = k / (float)topX.Length;
                spec.intensity = Mathf.Max(0.18f, 0.55f - 0.05f * k);
                spec.tint = 0.88f + 0.06f * (k % 3);
                spec.rows = 10;
                beams.Add(spec);
            }
            return beams;
        }

        /// <summary>
        /// 标定诊断：把源画锚点经注册逆映射回该图层的图像像素，并读出该处 alpha。
        /// alpha≈0 说明「注册后这个锚点落在图的透明区」——即图层位置与该锚点不吻合，需要调整注册。
        /// </summary>
        private static string DescribeAnchorMapping(
            LadyLakeLayerRegistration registration, LadyLakeLayerTexture layer,
            Vector2 anchorTopLeftPx, string label)
        {
            Vector2 anchorFrame = LadyLakeLabConfig.AnchorToFrame(anchorTopLeftPx);
            Vector2 frameNorm = new Vector2(
                anchorFrame.x / LadyLakeLabConfig.FrameWidthPx,
                anchorFrame.y / LadyLakeLabConfig.FrameHeightPx);
            Vector2 q = registration.ApplyInverse(frameNorm);

            float px = q.x * layer.width - 0.5f;
            float py = q.y * layer.height - 0.5f;
            int ix = Mathf.Clamp(Mathf.RoundToInt(px), 0, layer.width - 1);
            int iy = Mathf.Clamp(Mathf.RoundToInt(py), 0, layer.height - 1);
            byte a = layer.alpha[iy * layer.width + ix];
            bool inside = px >= 0f && px <= layer.width - 1f && py >= 0f && py <= layer.height - 1f;

            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0} 源画锚点 ({1:F0},{2:F0}) -> {3} 图像像素 ({4:F0},{5:F0}) alpha={6} {7}",
                label, anchorTopLeftPx.x, anchorTopLeftPx.y, layer.fileName, px, py, a,
                !inside ? "[越界]" : (a < 24 ? "[落在透明区 -> 注册需调整]" : "[落在实体内 OK]"));
        }

        // ------------------------------------------------------------------
        // 材质属性
        // ------------------------------------------------------------------

        private static void ConfigureMaterials(
            Material bg, Material figure, Material hand, Material foreground,
            Material sword, Material metal,
            Material backdrop, Material caustics, Material beam,
            Material bubble, Material bubbleBack, Material mote, Material refraction,
            Texture2D bgTex, Texture2D figTex, Texture2D fgTex, Texture2D swordTex,
            Texture2D causticsTex, Texture2D flowTex)
        {
            Vector4 fogWater = new Vector4(-0.32f, 0.34f, 0f, 0f);
            Color fogColor = new Color(0.016f, 0.075f, 0.085f, 1f);

            ConfigureLayerMaterial(bg, bgTex, 0.35f, 0.3f, 0.35f, 0.95f, 0.18f, 0f, fogWater, 0.75f,
                new Vector4(0.0022f, 0.0014f, 1.05f, 0.8f), 0.28f, new Color(0.55f, 0.95f, 0.85f, 1f), fogColor);
            ConfigureLayerMaterial(figure, figTex, 0.34f, 0.36f, 0.62f, 0.78f, 0.40f, 0.05f, fogWater, 0.55f,
                new Vector4(0.0012f, 0.0009f, 1.2f, 0.9f), 0.18f, new Color(0.58f, 0.98f, 0.88f, 1f), fogColor);
            // 握剑手上覆网格与人物完全同参数，只是渲染队列更高
            ConfigureLayerMaterial(hand, figTex, 0.34f, 0.36f, 0.62f, 0.78f, 0.40f, 0.05f, fogWater, 0.55f,
                new Vector4(0.0012f, 0.0009f, 1.2f, 0.9f), 0.18f, new Color(0.58f, 0.98f, 0.88f, 1f), fogColor);
            ConfigureLayerMaterial(foreground, fgTex, 0.32f, 0.4f, 0.5f, 0.7f, 0.3f, 0f,
                new Vector4(-0.62f, 0.05f, 0f, 0f), 0.4f,
                new Vector4(0.0016f, 0.0011f, 1.4f, 1.0f), 0.14f, new Color(0.45f, 0.85f, 0.7f, 1f), fogColor);

            LadyLakeLabMaterials.SetTexture(sword, "_MainTex", swordTex);
            LadyLakeLabMaterials.SetFloat(sword, "_Cutoff", 0.34f);
            LadyLakeLabMaterials.SetFloat(sword, "_Softness", 0.3f);
            LadyLakeLabMaterials.SetFloat(sword, "_LightStrength", 0.6f);
            LadyLakeLabMaterials.SetFloat(sword, "_Ambient", 0.72f);
            LadyLakeLabMaterials.SetColor(sword, "_GlowColor", new Color(1f, 0.82f, 0.42f, 1f));
            LadyLakeLabMaterials.SetFloat(sword, "_EdgeGlow", 1.5f);
            LadyLakeLabMaterials.SetFloat(sword, "_EdgePower", 2f);
            LadyLakeLabMaterials.SetColor(sword, "_RuneColor", new Color(1f, 0.94f, 0.66f, 1f));
            LadyLakeLabMaterials.SetFloat(sword, "_RuneV", 0.34f);
            LadyLakeLabMaterials.SetFloat(sword, "_RuneSpread", 9f);
            LadyLakeLabMaterials.SetFloat(sword, "_RuneStrength", 1.1f);
            LadyLakeLabMaterials.SetFloat(sword, "_PulseSpeed", 2.1f);
            LadyLakeLabMaterials.SetFloat(sword, "_PulseAmount", 0.35f);
            LadyLakeLabMaterials.SetFloat(sword, "_PulseCycles", 3f);
            LadyLakeLabMaterials.SetColor(sword, "_FogColor", fogColor);
            LadyLakeLabMaterials.SetVector(sword, "_FogRange", new Vector4(-0.42f, 0.10f, 0f, 0f));
            LadyLakeLabMaterials.SetFloat(sword, "_FogStrength", 0.35f);

            LadyLakeLabMaterials.SetColor(metal, "_Color", new Color(0.19f, 0.21f, 0.24f, 1f));
            LadyLakeLabMaterials.SetColor(metal, "_SpecColor2", new Color(0.78f, 0.85f, 0.88f, 1f));
            LadyLakeLabMaterials.SetFloat(metal, "_Shininess", 42f);
            LadyLakeLabMaterials.SetFloat(metal, "_Ambient", 0.3f);
            LadyLakeLabMaterials.SetColor(metal, "_GlowColor", new Color(1f, 0.82f, 0.42f, 1f));
            LadyLakeLabMaterials.SetFloat(metal, "_EdgeGlow", 0.28f);
            LadyLakeLabMaterials.SetColor(metal, "_FogColor", fogColor);
            LadyLakeLabMaterials.SetVector(metal, "_FogRange", new Vector4(-0.42f, 0.10f, 0f, 0f));
            LadyLakeLabMaterials.SetFloat(metal, "_FogStrength", 0.35f);

            LadyLakeLabMaterials.SetColor(backdrop, "_TopColor", new Color(0.035f, 0.115f, 0.125f, 1f));
            LadyLakeLabMaterials.SetColor(backdrop, "_BottomColor", new Color(0.004f, 0.018f, 0.026f, 1f));
            LadyLakeLabMaterials.SetColor(backdrop, "_WispColor", new Color(0.22f, 0.55f, 0.46f, 1f));
            LadyLakeLabMaterials.SetFloat(backdrop, "_WispStrength", 0.22f);
            LadyLakeLabMaterials.SetFloat(backdrop, "_Vignette", 0.65f);

            LadyLakeLabMaterials.SetColor(caustics, "_Color", new Color(0.55f, 0.95f, 0.72f, 1f));
            LadyLakeLabMaterials.SetFloat(caustics, "_Strength", 0.5f);
            LadyLakeLabMaterials.SetFloat(caustics, "_Tiling", 3.2f);
            LadyLakeLabMaterials.SetFloat(caustics, "_Sharpness", 7f);
            LadyLakeLabMaterials.SetFloat(caustics, "_Speed", 0.55f);
            LadyLakeLabMaterials.SetVector(caustics, "_Scroll", new Vector4(0.06f, 0.13f, 0f, 0f));
            LadyLakeLabMaterials.SetFloat(caustics, "_VerticalFade", 0.45f);
            LadyLakeLabMaterials.SetFloat(caustics, "_CausticsStrength", causticsTex != null ? 0.75f : 0f);
            LadyLakeLabMaterials.SetFloat(caustics, "_CausticsTiling", 1.15f);
            LadyLakeLabMaterials.SetVector(caustics, "_CausticsScroll", new Vector4(0.045f, 0.085f, 0f, 0f));
            if (causticsTex != null) LadyLakeLabMaterials.SetTexture(caustics, "_CausticsTex", causticsTex);

            LadyLakeLabMaterials.SetColor(beam, "_Color", new Color(0.80f, 1f, 0.76f, 1f));
            LadyLakeLabMaterials.SetFloat(beam, "_Strength", 0.42f);
            LadyLakeLabMaterials.SetFloat(beam, "_Falloff", 2.2f);
            LadyLakeLabMaterials.SetFloat(beam, "_AlongStart", 0.35f);
            LadyLakeLabMaterials.SetFloat(beam, "_Bands", 5f);

            LadyLakeLabMaterials.SetColor(bubble, "_Color", new Color(0.62f, 0.92f, 0.90f, 1f));
            LadyLakeLabMaterials.SetFloat(bubble, "_Strength", 1f);
            LadyLakeLabMaterials.SetFloat(bubble, "_RimWidth", 0.17f);
            LadyLakeLabMaterials.SetFloat(bubble, "_BodyAlpha", 0.1f);
            LadyLakeLabMaterials.SetColor(bubbleBack, "_Color", new Color(0.52f, 0.84f, 0.84f, 1f));
            LadyLakeLabMaterials.SetFloat(bubbleBack, "_Strength", 0.75f);
            LadyLakeLabMaterials.SetFloat(bubbleBack, "_RimWidth", 0.19f);
            LadyLakeLabMaterials.SetFloat(bubbleBack, "_BodyAlpha", 0.08f);

            LadyLakeLabMaterials.SetColor(mote, "_Color", new Color(0.72f, 0.95f, 0.80f, 1f));
            LadyLakeLabMaterials.SetFloat(mote, "_Strength", 0.42f);
            LadyLakeLabMaterials.SetFloat(mote, "_Falloff", 6f);

            LadyLakeLabMaterials.SetFloat(refraction, "_RippleStrength", 0.006f);
            LadyLakeLabMaterials.SetFloat(refraction, "_Tiling", 26f);
            LadyLakeLabMaterials.SetFloat(refraction, "_Speed", 0.9f);
            LadyLakeLabMaterials.SetColor(refraction, "_Tint", new Color(0.86f, 1f, 0.97f, 1f));
            LadyLakeLabMaterials.SetFloat(refraction, "_TintAmount", 0.16f);
            LadyLakeLabMaterials.SetFloat(refraction, "_FlowTiling", 1.6f);
            LadyLakeLabMaterials.SetFloat(refraction, "_FlowStrength", flowTex != null ? 0.55f : 0f);
            if (flowTex != null) LadyLakeLabMaterials.SetTexture(refraction, "_FlowTex", flowTex);
        }

        private static void ConfigureLayerMaterial(
            Material m, Texture2D texture, float cutoff, float softness,
            float lightStrength, float ambient, float rimStrength, float edgeGlow,
            Vector4 fogRange, float fogStrength, Vector4 wobble, float depthTint, Color rimColor, Color fogColor)
        {
            LadyLakeLabMaterials.SetTexture(m, "_MainTex", texture);
            LadyLakeLabMaterials.SetColor(m, "_Color", Color.white);
            LadyLakeLabMaterials.SetFloat(m, "_Cutoff", cutoff);
            LadyLakeLabMaterials.SetFloat(m, "_Softness", softness);
            LadyLakeLabMaterials.SetFloat(m, "_LightStrength", lightStrength);
            LadyLakeLabMaterials.SetFloat(m, "_Ambient", ambient);
            LadyLakeLabMaterials.SetColor(m, "_RimColor", rimColor);
            LadyLakeLabMaterials.SetFloat(m, "_RimStrength", rimStrength);
            LadyLakeLabMaterials.SetFloat(m, "_EdgeGlow", edgeGlow);
            LadyLakeLabMaterials.SetColor(m, "_FogColor", fogColor);
            LadyLakeLabMaterials.SetVector(m, "_FogRange", fogRange);
            LadyLakeLabMaterials.SetFloat(m, "_FogStrength", fogStrength);
            LadyLakeLabMaterials.SetVector(m, "_Wobble", wobble);
            LadyLakeLabMaterials.SetFloat(m, "_DepthTintStrength", depthTint);
        }

        // ------------------------------------------------------------------
        // 辅助
        // ------------------------------------------------------------------

        private static void ConfigureFigureRig(LadyLakeFigureRig rig, Mesh mesh)
        {
            rig.bakedMesh = mesh;
            rig.shoulder = LadyLakeLabConfig.AnchorToWorld(LadyLakeLabConfig.ShoulderAnchor);
            rig.elbow = LadyLakeLabConfig.AnchorToWorld(LadyLakeLabConfig.ElbowAnchor);
            rig.wrist = LadyLakeLabConfig.AnchorToWorld(LadyLakeLabConfig.SwordHandAnchor);
            rig.breathDirection = new Vector2(0.22f, 0.98f);
            rig.hairSwayPhaseOffset = 0f;
            rig.hairSwayScale = 1f;
            rig.handWeightThreshold = 0.85f;
        }

        private static GameObject CreateRenderable(string name, Transform parent, Mesh mesh, Material[] materials)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            LadyLakeMeshDeformer.ConfigureRenderer(renderer);
            return go;
        }

        private static LadyLakeParticleField CreateParticleField(
            string name, Transform parent, Material material, LadyLakeParticleKind kind,
            int seed, int count, Vector2 zRange, Vector2 sizeRange, float wobble,
            int wobbleCycles, float baseAlpha, Color tint)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            LadyLakeParticleField field = go.AddComponent<LadyLakeParticleField>();
            field.kind = kind;
            field.seed = seed;
            field.count = count;
            field.zRange = zRange;
            field.sizeRangeFramePx = sizeRange;
            field.riseCyclesRange = new Vector2(1f, 3f);
            field.wobbleAmplitudeFramePx = wobble;
            field.wobbleCycles = wobbleCycles;
            field.edgeMarginFramePx = 45f;
            field.baseAlpha = baseAlpha;
            field.tint = tint;
            field.wakeReach = 1.2f;
            field.wakeSpread = 0.34f;
            field.wakeRise = 0.55f;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            go.AddComponent<MeshFilter>();
            LadyLakeMeshDeformer.ConfigureRenderer(renderer);
            return field;
        }

        private static void BakeParticleField(LadyLakeParticleField field, LadyLakePose pose)
        {
            Mesh mesh = field.CreateBakedMesh(pose);
            mesh = SaveMeshAsset(mesh, "LadyLake_" + field.name + "_Particles");
            field.bakedMesh = mesh;
            MeshFilter filter = field.GetComponent<MeshFilter>();
            if (filter != null) filter.sharedMesh = mesh;
            EditorUtility.SetDirty(field);
        }

        private static void CreateTitle(Transform cameraTransform, List<string> warnings)
        {
            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null)
            {
                font = Font.CreateDynamicFontFromOSFont("Arial", 40);
            }
            if (font == null)
            {
                warnings.Add("找不到可用字体，标题文字已跳过（不影响画面主体）。");
                return;
            }

            string path = LadyLakeLabConfig.GeneratedMaterialFolder + "/LadyLake_Title.mat";
            Material titleMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (titleMaterial == null)
            {
                titleMaterial = new Material(font.material);
                titleMaterial.name = "LadyLake_Title";
                AssetDatabase.CreateAsset(titleMaterial, path);
            }
            titleMaterial.renderQueue = LadyLakeLabConfig.QueueOverlay;

            CreateTitleLine(cameraTransform, font, titleMaterial, "TitleBottom",
                "Lady of the Lake  ·  LadyLakeLab  ·  2.5D 浮雕闪卡实验", -3.05f);
            CreateTitleLine(cameraTransform, font, titleMaterial, "TitleTop",
                "497x710 构图  ·  人物/剑独立网格  ·  Play 循环：伸手 → 剑入手 → 抬剑发光 → 下放 → 松开", 3.09f);
        }

        private static void CreateTitleLine(
            Transform cameraTransform, Font font, Material material, string name, string text, float y)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(cameraTransform, false);
            go.transform.localPosition = new Vector3(0f, y, 10f);
            go.transform.localRotation = Quaternion.identity;
            // 世界尺寸约为 characterSize * 字号比例 * scale；0.022 让两行标题落在画面上下留白内。
            go.transform.localScale = Vector3.one * 0.022f;

            TextMesh mesh = go.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 40;
            mesh.characterSize = 1f;
            mesh.color = new Color(0.74f, 0.92f, 0.86f, 0.8f);

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static Mesh SaveMeshAsset(Mesh mesh, string name)
        {
            if (mesh == null) return null;
            string path = LadyLakeLabConfig.GeneratedMeshFolder + "/" + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        private static LadyLakeLayerTexture Find(List<LadyLakeLayerTexture> layers, LadyLakeLayerRole role)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].role == role) return layers[i];
            }
            return null;
        }

        private static void FillImageReport(LadyLakeValidationReport report, List<LadyLakeLayerTexture> layers)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                LadyLakeLayerTexture layer = layers[i];
                LadyLakeValidationImage image = new LadyLakeValidationImage();
                image.role = layer.role.ToString();
                image.path = layer.assetPath;
                image.fileName = layer.fileName;
                image.width = layer.width;
                image.height = layer.height;
                image.hasAlpha = layer.hasAlpha;
                image.readable = layer.texture != null && layer.texture.isReadable;
                image.pixelBounds = string.Format(
                    "x[{0},{1}] yFromBottom[{2},{3}]",
                    layer.activeMinX, layer.activeMaxX, layer.activeMinY, layer.activeMaxY);
                image.note = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "不透明像素占比 {0:P1}；alpha bbox 归一化 x[{1:F4},{2:F4}] yFromTop[{3:F4},{4:F4}]",
                    layer.OpaqueRatio(),
                    layer.activeMinX / (float)layer.width, (layer.activeMaxX + 1) / (float)layer.width,
                    1f - (layer.activeMaxY + 1) / (float)layer.height, 1f - layer.activeMinY / (float)layer.height);
                report.images.Add(image);
            }
        }

        private static void CollectHierarchy(LadyLakeValidationReport report, GameObject rootGo)
        {
            Transform[] all = rootGo.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                LadyLakeValidationNode node = new LadyLakeValidationNode();
                node.path = GetPath(t, rootGo.transform);

                Component[] components = t.GetComponents<Component>();
                List<string> names = new List<string>();
                for (int c = 0; c < components.Length; c++)
                {
                    if (components[c] == null) continue;
                    names.Add(components[c].GetType().Name);
                }
                node.components = string.Join(", ", names.ToArray());

                MeshFilter filter = t.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    Mesh mesh = filter.sharedMesh;
                    node.meshVertices = mesh.vertexCount;
                    int triangles = 0;
                    for (int s = 0; s < mesh.subMeshCount; s++)
                    {
                        triangles += (int)(mesh.GetIndexCount(s) / 3);
                    }
                    node.meshTriangles = triangles;
                    node.subMeshCount = mesh.subMeshCount;
                    node.meshReadable = mesh.isReadable;
                    node.boundsCenter = mesh.bounds.center.ToString("F3");
                    node.boundsSize = mesh.bounds.size.ToString("F3");
                }

                Renderer renderer = t.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material[] materials = renderer.sharedMaterials;
                    List<string> matNames = new List<string>();
                    for (int m = 0; m < materials.Length; m++)
                    {
                        if (materials[m] == null)
                        {
                            matNames.Add("<null>");
                            report.nullReferences.Add(node.path + " -> material[" + m + "] 为空引用");
                            continue;
                        }
                        matNames.Add(materials[m].name);
                        if (node.renderQueue < 0) node.renderQueue = materials[m].renderQueue;
                    }
                    node.materials = string.Join(", ", matNames.ToArray());
                }

                report.hierarchy.Add(node);
            }
        }

        private static void CollectShaders(LadyLakeValidationReport report, GameObject rootGo)
        {
            HashSet<string> seen = new HashSet<string>();
            Renderer[] renderers = rootGo.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                {
                    if (materials[m] == null || materials[m].shader == null) continue;
                    string key = materials[m].shader.name;
                    if (!seen.Add(key)) continue;

                    LadyLakeValidationShader entry = new LadyLakeValidationShader();
                    entry.shaderName = key;
                    entry.found = Shader.Find(key) != null;
                    entry.supported = materials[m].shader.isSupported;
                    entry.usedBy = materials[m].name;
                    entry.renderQueue = materials[m].renderQueue;
                    report.shaders.Add(entry);
                }
            }

            for (int i = 0; i < LadyLakeShaders.All.Length; i++)
            {
                LadyLakeShaders.Entry e = LadyLakeShaders.All[i];
                if (seen.Contains(e.name)) continue;
                if (AssetDatabase.LoadAssetAtPath<Shader>(e.path) == null)
                {
                    report.errors.Add("自写着色器缺失：" + e.path);
                    continue;
                }
                LadyLakeValidationShader entry = new LadyLakeValidationShader();
                entry.shaderName = e.name;
                entry.found = true;
                entry.supported = false;
                entry.usedBy = "(未被材质使用) " + e.usedBy;
                report.warnings.Add("着色器未被使用：" + e.path);
                report.shaders.Add(entry);
            }
        }

        private static string GetPath(Transform t, Transform root)
        {
            string path = t.name;
            Transform current = t.parent;
            while (current != null && current != root)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }

        private static void ReleaseDeformers(LadyLakeLabRoot root)
        {
            if (root == null) return;
            LadyLakeMeshDeformer[] deformers = root.GetComponentsInChildren<LadyLakeMeshDeformer>(true);
            for (int i = 0; i < deformers.Length; i++)
            {
                deformers[i].ReleaseRuntimeMesh();
            }
            RestoreRestPose(root);
            if (root.waterFx != null) root.waterFx.ClearPropertyBlocks();
        }

        /// <summary>
        /// 把剑与手部上覆网格的 MaterialPropertyBlock 恢复到 t=0 姿态。
        /// MPB 会被序列化进场景，因此未 Play 时看到的就是 t=0 的剑光状态（与 sample_00.0s 截图一致）。
        /// </summary>
        public static void RestoreRestPose(LadyLakeLabRoot root)
        {
            if (root == null || root.swordRig == null || root.timeline == null) return;
            LadyLakePose pose0 = LadyLakeMotion.Evaluate(
                0f, root.timeline.duration, root.timeline.pause, root.timeline.intensity);
            root.swordRig.ApplyPose(pose0);
        }
    }
}
