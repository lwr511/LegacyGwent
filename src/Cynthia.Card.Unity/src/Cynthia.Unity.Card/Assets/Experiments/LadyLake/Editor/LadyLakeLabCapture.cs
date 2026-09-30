using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LegacyGwent.LadyLakeLab.Editor
{
    /// <summary>
    /// Editor 静态取样与验证报告。
    ///
    /// 关键点：截图走 root.SampleAt(seconds) —— 与运行时 Update 完全同一条姿态求值路径，
    /// 因此「Editor 截图」就是「Play 到第 N 秒」的画面（唯一差别是没有真实时间流逝）。
    /// 取样时间：0 / 2 / 4 / 6 / 8 / 10 / 12 秒，另加一张峰值全景帧。
    /// </summary>
    public static class LadyLakeLabCapture
    {
        public const int PortraitWidth = 1050;   // >= 700x1000
        public const int PortraitHeight = 1500;
        public const int PanoramaWidth = 1920;
        public const int PanoramaHeight = 1080;

        private static readonly float[] SampleTimes = { 0f, 2f, 4f, 6f, 8f, 10f, 12f };

        // ------------------------------------------------------------------
        // 菜单入口：对已经打开的场景取样
        // ------------------------------------------------------------------

        public static void CaptureFromOpenScene()
        {
            LadyLakeLabRoot root = Object.FindObjectOfType<LadyLakeLabRoot>();
            if (root == null)
            {
                if (System.IO.File.Exists(LadyLakeLabTextures.ToAbsolutePath(LadyLakeLabConfig.ScenePath)))
                {
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        LadyLakeLabConfig.ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
                    root = Object.FindObjectOfType<LadyLakeLabRoot>();
                }
            }

            if (root == null)
            {
                Debug.LogError("[LadyLakeLab] 场景里找不到 LadyLakeLabRoot，请先 Rebuild。");
                return;
            }

            LadyLakeValidationReport report = new LadyLakeValidationReport();
            report.generatedAtUtc = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            report.unityVersion = Application.unityVersion;
            report.scenePath = LadyLakeLabConfig.ScenePath;
            report.textureFolder = LadyLakeLabConfig.TextureFolder;
            report.buildSucceeded = true;
            report.warnings.Add("本次为 Capture-only 运行：images / hierarchy / shaders 段为空，请以 BuildAndCapture 的报告为准。");

            CaptureSamples(root, null, report);
            WriteReport(report);

            LadyLakeMeshDeformer[] deformers = root.GetComponentsInChildren<LadyLakeMeshDeformer>(true);
            for (int i = 0; i < deformers.Length; i++) deformers[i].ReleaseRuntimeMesh();
            LadyLakeLabBuilder.RestoreRestPose(root);
            if (root.waterFx != null) root.waterFx.ClearPropertyBlocks();

            Debug.Log("[LadyLakeLab] 取样完成 -> " + LadyLakeLabConfig.CaptureFolder);
        }

        // ------------------------------------------------------------------
        // 取样
        // ------------------------------------------------------------------

        public static void CaptureSamples(
            LadyLakeLabRoot root,
            List<LadyLakeLayerTexture> layers,
            LadyLakeValidationReport report)
        {
            if (root == null) return;

            Camera camera = root.targetCamera;
            if (camera == null) camera = root.GetComponentInChildren<Camera>(true);
            if (camera == null)
            {
                report.errors.Add("场景里没有摄像机，无法取样。");
                return;
            }

            if (root.timeline == null)
            {
                report.errors.Add("场景里没有 LadyLakeTimeline，无法取样。");
                return;
            }

            try
            {
                Directory.CreateDirectory(LadyLakeLabConfig.CaptureFolder);
            }
            catch (System.Exception ex)
            {
                report.errors.Add("无法创建截图目录 " + LadyLakeLabConfig.CaptureFolder + " -> " + ex.Message);
                return;
            }

            for (int i = 0; i < SampleTimes.Length; i++)
            {
                float seconds = SampleTimes[i];
                root.SampleAt(seconds);
                LadyLakePose pose = root.timeline.CurrentPose;

                string fileName = string.Format(
                    CultureInfo.InvariantCulture,
                    "sample_{0:00.0}s_{1}.png", seconds, pose.PhaseName);
                string path = Path.Combine(LadyLakeLabConfig.CaptureFolder, fileName);

                float nonEmpty;
                float meanLuma;
                if (RenderToFile(camera, PortraitWidth, PortraitHeight, path, out nonEmpty, out meanLuma))
                {
                    LadyLakeValidationCapture capture = new LadyLakeValidationCapture();
                    capture.seconds = pose.time;
                    capture.phase = pose.PhaseName;
                    capture.path = path;
                    capture.width = PortraitWidth;
                    capture.height = PortraitHeight;
                    capture.nonEmptyRatio = nonEmpty;
                    capture.meanLuma = meanLuma;
                    capture.note = string.Format(CultureInfo.InvariantCulture,
                        "requested={0:F2}s loopTime={1:F3}s normalized={2:F3} held={3} glow={4:F2} wake={5:F2}",
                        seconds, pose.time, pose.normalizedTime, pose.held, pose.glow, pose.wake);
                    report.captures.Add(capture);
                }
                else
                {
                    report.errors.Add("截图失败：" + path);
                }
            }

            // 峰值全景帧（pause 停留段中间，剑与手都在最高点、发光最强）
            float peakSeconds = root.timeline.duration * LadyLakeMotion.LiftEnd + root.timeline.pause * 0.5f;
            root.SampleAt(peakSeconds);
            LadyLakePose peakPose = root.timeline.CurrentPose;
            string peakPath = Path.Combine(LadyLakeLabConfig.CaptureFolder, "panorama_peak.png");

            float peakNonEmpty;
            float peakLuma;
            if (RenderToFile(camera, PanoramaWidth, PanoramaHeight, peakPath, out peakNonEmpty, out peakLuma))
            {
                LadyLakeValidationCapture capture = new LadyLakeValidationCapture();
                capture.seconds = peakPose.time;
                capture.phase = peakPose.PhaseName;
                capture.path = peakPath;
                capture.width = PanoramaWidth;
                capture.height = PanoramaHeight;
                capture.nonEmptyRatio = peakNonEmpty;
                capture.meanLuma = peakLuma;
                capture.note = string.Format(CultureInfo.InvariantCulture,
                    "全景帧（16:9）：peakSeconds={0:F2}s loopTime={1:F3}s held={2}",
                    peakSeconds, peakPose.time, peakPose.held);
                report.captures.Add(capture);
            }
            else
            {
                report.errors.Add("全景截图失败：" + peakPath);
            }

            MeasureGrip(root, report);
            MeasureSeam(root, report);

            if (layers != null)
            {
                report.reusedProjectAssets.Add(string.Format(
                    "本次构建读取的分层图：{0} 张（全部来自 {1}）。",
                    layers.Count, LadyLakeLabConfig.TextureFolder));
            }
        }

        // ------------------------------------------------------------------
        // 渲染
        // ------------------------------------------------------------------

        private static bool RenderToFile(
            Camera camera, int width, int height, string path,
            out float nonEmptyRatio, out float meanLuma)
        {
            nonEmptyRatio = 0f;
            meanLuma = 0f;

            const int SuperSample = 2;
            RenderTextureDescriptor bigDescriptor = new RenderTextureDescriptor(
                width * SuperSample, height * SuperSample, RenderTextureFormat.ARGB32, 24);
            bigDescriptor.msaaSamples = 4;

            RenderTexture big = RenderTexture.GetTemporary(bigDescriptor);
            RenderTexture small = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;

            try
            {
                camera.targetTexture = big;
                camera.Render();

                Graphics.Blit(big, small);

                RenderTexture.active = small;
                Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();

                Color32[] pixels = texture.GetPixels32();
                ComputeStats(pixels, width, height, out nonEmptyRatio, out meanLuma);

                byte[] png = texture.EncodeToPNG();
                Object.DestroyImmediate(texture);

                if (png == null || png.Length == 0) return false;
                File.WriteAllBytes(path, png);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[LadyLakeLab] 渲染失败：" + path + " -> " + ex.Message);
                return false;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(big);
                RenderTexture.ReleaseTemporary(small);
            }
        }

        private static void ComputeStats(Color32[] pixels, int width, int height, out float nonEmptyRatio, out float meanLuma)
        {
            // 以四个角的中位亮度作为「背景基准」，统计明显不同的像素占比
            float corner = 0f;
            int[] corners = { 0, width - 1, (height - 1) * width, height * width - 1 };
            for (int i = 0; i < corners.Length; i++)
            {
                Color32 c = pixels[corners[i]];
                corner += Luma(c);
            }
            corner /= corners.Length;

            int nonEmpty = 0;
            double sum = 0d;
            for (int i = 0; i < pixels.Length; i++)
            {
                float l = Luma(pixels[i]);
                sum += l;
                if (Mathf.Abs(l - corner) > 0.05f) nonEmpty++;
            }

            nonEmptyRatio = pixels.Length > 0 ? nonEmpty / (float)pixels.Length : 0f;
            meanLuma = pixels.Length > 0 ? (float)(sum / pixels.Length) : 0f;
        }

        private static float Luma(Color32 c)
        {
            return (c.r * 0.299f + c.g * 0.587f + c.b * 0.114f) / 255f;
        }

        // ------------------------------------------------------------------
        // 握剑距离 / 滑移
        // ------------------------------------------------------------------

        private static void MeasureGrip(LadyLakeLabRoot root, LadyLakeValidationReport report)
        {
            LadyLakeFigureRig rig = root.handOverlayRig != null ? root.handOverlayRig : root.figureRig;
            LadyLakeTimeline timeline = root.timeline;
            if (rig == null || timeline == null) return;

            if (!rig.EnsureRuntimeMesh() || !rig.CacheBuffers())
            {
                report.warnings.Add("人物 rig 无法准备网格，跳过握剑距离测量。");
                return;
            }

            float duration = timeline.duration;
            float pause = timeline.pause;
            float start;
            float end;
            LadyLakeMotion.HeldWindow(duration, pause, out start, out end);
            report.grip.heldWindowStart = start;
            report.grip.heldWindowEnd = end;
            report.grip.handVertexCount = rig.HandVertexCount;

            List<Vector2> offsets = new List<Vector2>();
            Vector2[] reference = null;
            bool hasReference = false;

            float maxHeld = 0f;
            float maxFree = 0f;
            float maxSlip = 0f;
            float maxHandDistance = 0f;
            float palmAlignmentSum = 0f;
            float palmAlignmentWorst = 0f;
            int palmSamples = 0;

            float total = timeline.TotalDuration;
            float step = 0.1f;
            StringBuilder samples = new StringBuilder();

            for (float t = 0f; t <= total + 1e-4f; t += step)
            {
                LadyLakePose pose = LadyLakeMotion.Evaluate(t, duration, pause, timeline.intensity);
                rig.ApplyPose(pose);

                float minDistance;
                Vector2 centroid;
                rig.SampleHandOffsets(pose.swordGrip, offsets, out minDistance, out centroid);
                if (minDistance < 0f) continue;

                float maxDistance = 0f;
                for (int i = 0; i < offsets.Count; i++)
                {
                    float d = offsets[i].magnitude;
                    if (d > maxDistance) maxDistance = d;
                }

                if (pose.held)
                {
                    if (minDistance > maxHeld) maxHeld = minDistance;
                    if (maxDistance > maxHandDistance) maxHandDistance = maxDistance;

                    // 手心对齐：手部顶点云质心与剑握点（= 前臂骨骼驱动出的点）的距离
                    float alignment = Vector2.Distance(centroid, pose.swordGrip);
                    palmAlignmentSum += alignment;
                    if (alignment > palmAlignmentWorst) palmAlignmentWorst = alignment;
                    palmSamples++;

                    if (!hasReference)
                    {
                        reference = offsets.ConvertAll(p => LadyLakeMath.Rotate(p, -pose.swordAngleDeg)).ToArray();
                        hasReference = true;
                    }
                    else
                    {
                        for (int i = 0; i < offsets.Count; i++)
                        {
                            float slip = (LadyLakeMath.Rotate(offsets[i], -pose.swordAngleDeg) - reference[i]).magnitude;
                            if (slip > maxSlip) maxSlip = slip;
                        }
                    }
                }
                else if (minDistance > maxFree)
                {
                    maxFree = minDistance;
                }

                if (samples.Length < 2000)
                {
                    samples.AppendFormat(CultureInfo.InvariantCulture,
                        "{0:F1}s:{1}{2:F3} ", t, pose.held ? "H" : "f", minDistance);
                }
            }

            report.grip.maxGripDistanceWhileHeld = maxHeld;
            report.grip.maxGripDistanceWhileFree = maxFree;
            report.grip.maxHandSlipWhileHeld = maxSlip;
            report.grip.maxHandDistanceWhileHeld = maxHandDistance;

            // 对「真实 Mesh 顶点」而言，握点落在手部顶点云内部，因此最小距离是几何半径；
            // 判定无滑移用 maxHandSlipWhileHeld（手部顶点相对剑握点的偏移变化），它应当接近 0。
            report.grip.note =
                "单位=世界单位(100px=1)。maxGripDistanceWhileHeld = 握住相位内剑握点到最近手部顶点的距离" +
                "（手是有厚度的一团顶点，因此不为 0 而是手部几何半径）；maxHandSlipWhileHeld = " +
                "透明像素已排除；同一批可见手部顶点先消除透视缩放，再转入剑的局部坐标，逐顶点比较首个握持样本的最大位移。刚性握持时应接近 0。";

            report.grip.palmAlignment = palmSamples > 0 ? palmAlignmentSum / palmSamples : -1f;
            report.grip.palmAlignmentWorst = palmSamples > 0 ? palmAlignmentWorst : -1f;
            report.grip.palmNote =
                "palmAlignment = 握住相位「前臂权重>=阈值的手部顶点云质心」与「剑握点」的平均距离，" +
                "palmAlignmentWorst 为其最大值。两者都来自真实网格顶点（手部顶点由 figure 网格注册决定，" +
                "剑握点由剑剪影主轴配准决定），不是两个空物体的距离。";

            if (maxSlip > 0.002f)
            {
                report.warnings.Add(string.Format(CultureInfo.InvariantCulture,
                    "握住相位手部顶点相对剑握点有 {0:F5} 单位滑移，请检查前臂权重与剑挂点是否一致。", maxSlip));
            }
            if (report.grip.palmAlignment > 0.35f)
            {
                report.warnings.Add(string.Format(CultureInfo.InvariantCulture,
                    "手心对齐偏差 {0:F3} 单位（约 {1:F0} 像素）：figure 图层注册与实际画面不吻合，" +
                    "请按 validation.json 的 calibration 段用 Textures/layout.json 微调 figure rect。",
                    report.grip.palmAlignment, report.grip.palmAlignment * LadyLakeLabConfig.PixelsPerUnit));
            }

            report.grip.samples = samples.ToString();
        }

        private static Vector2 Average(List<Vector2> values)
        {
            if (values.Count == 0) return Vector2.zero;
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < values.Count; i++) sum += values[i];
            return sum / values.Count;
        }

        // ------------------------------------------------------------------
        // 循环接缝
        // ------------------------------------------------------------------

        private static void MeasureSeam(LadyLakeLabRoot root, LadyLakeValidationReport report)
        {
            LadyLakeTimeline timeline = root.timeline;
            if (timeline == null) return;

            float duration = timeline.duration;
            float pause = timeline.pause;
            float total = timeline.TotalDuration;
            float intensity = timeline.intensity;

            LadyLakePose start = LadyLakeMotion.Evaluate(0f, duration, pause, intensity);
            LadyLakePose end = LadyLakeMotion.Evaluate(total, duration, pause, intensity);

            report.seam.positionDelta = Vector2.Distance(start.wrist, end.wrist);
            report.seam.wristDelta = Vector2.Distance(start.wrist, end.wrist);
            report.seam.gripDelta = Vector2.Distance(start.swordGrip, end.swordGrip);
            report.seam.rotationDeltaDeg = Mathf.Abs(Mathf.DeltaAngle(start.swordAngleDeg, end.swordAngleDeg));

            LadyLakeFigureRig rig = root.figureRig;
            float maxVertexDelta = 0f;
            if (rig != null && rig.EnsureRuntimeMesh() && rig.CacheBuffers())
            {
                rig.ApplyPose(start);
                Vector3[] a = rig.DeformedPositions;
                Vector3[] copy = a != null ? (Vector3[])a.Clone() : null;

                rig.ApplyPose(end);
                Vector3[] b = rig.DeformedPositions;

                if (copy != null && b != null && copy.Length == b.Length)
                {
                    for (int i = 0; i < copy.Length; i++)
                    {
                        float d = Vector3.Distance(copy[i], b[i]);
                        if (d > maxVertexDelta) maxVertexDelta = d;
                    }
                }
            }
            report.seam.maxVertexDelta = maxVertexDelta;

            // 速度连续性：比较循环结束前一刻与开始后一刻的握点速度
            float h = 0.01f;
            LadyLakePose p0 = LadyLakeMotion.Evaluate(0f, duration, pause, intensity);
            LadyLakePose p1 = LadyLakeMotion.Evaluate(h, duration, pause, intensity);
            LadyLakePose p2 = LadyLakeMotion.Evaluate(total - h, duration, pause, intensity);
            LadyLakePose p3 = LadyLakeMotion.Evaluate(total, duration, pause, intensity);

            Vector2 v0 = (p1.swordGrip - p0.swordGrip) / h;
            Vector2 v1 = (p3.swordGrip - p2.swordGrip) / h;
            report.seam.velocityDelta = Vector2.Distance(v0, v1);

            report.seam.note = string.Format(CultureInfo.InvariantCulture,
                "positionDelta/rotationDelta/maxVertexDelta 是 t=0 与 t=total(={0:F2}s) 的差异；" +
                "由于循环取模，这两点本来就是同一姿态（因此为 0）。真正的连续性证据是 velocityDelta " +
                "与角度速度差：两次采样步长 {1}s，握点速度差 {2:F5} 单位/秒，角度速度差 {3:F5} 度/秒。",
                total, h, report.seam.velocityDelta,
                Mathf.Abs((Mathf.DeltaAngle(p0.upperAngleDeg, p1.upperAngleDeg) / h)
                          - (Mathf.DeltaAngle(p2.upperAngleDeg, p3.upperAngleDeg) / h)));
        }

        // ------------------------------------------------------------------
        // 报告输出
        // ------------------------------------------------------------------

        public static void WriteReport(LadyLakeValidationReport report)
        {
            try
            {
                string directory = Path.GetDirectoryName(LadyLakeLabConfig.ValidationFilePath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                string json = JsonUtility.ToJson(report, true);
                File.WriteAllText(LadyLakeLabConfig.ValidationFilePath, json, new UTF8Encoding(false));
                Debug.Log("[LadyLakeLab] validation.json -> " + LadyLakeLabConfig.ValidationFilePath);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[LadyLakeLab] 写 validation.json 失败：" + ex.Message);
            }
        }
    }
}
