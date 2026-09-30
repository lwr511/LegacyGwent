using System.Collections.Generic;
using UnityEngine;

namespace LegacyGwent.LadyLakeLab.Editor
{
    /// <summary>网格统计，写进 validation.json。</summary>
    public struct LadyLakeMeshStats
    {
        public int vertices;
        public int triangles;
        public int subMeshes;
        public Bounds bounds;

        public override string ToString()
        {
            return string.Format("{0} verts / {1} tris / {2} submeshes", vertices, triangles, subMeshes);
        }
    }

    public enum LadyLakeProfileMode
    {
        None = 0,
        /// <summary>uv3 = (呼吸系数, 发丝系数)。</summary>
        Figure = 1,
        /// <summary>uv3 = (摆动强度, 画面高度相位)。</summary>
        Sway = 2,
    }

    /// <summary>
    /// 图层注册：把「图像归一化坐标（左下原点，y 向上）」映射到「画面归一化坐标（左下原点，y 向上）」，
    /// 形式为相似变换 f = S*R*q + t（S 均匀缩放，R 旋转）。
    /// 这样位图本身不需要任何缩放/重采样 —— 只移动网格顶点，UV 保持整图不变。
    /// </summary>
    public struct LadyLakeLayerRegistration
    {
        public float a;   // s*cos
        public float b;   // s*sin
        public float tx;
        public float ty;
        public bool autoFitted;
        public string note;

        public static LadyLakeLayerRegistration Identity()
        {
            LadyLakeLayerRegistration r = new LadyLakeLayerRegistration();
            r.a = 1f;
            r.b = 0f;
            r.tx = 0f;
            r.ty = 0f;
            r.note = "identity (full frame)";
            return r;
        }

        /// <summary>
        /// 归一化「左上原点」矩形 -> 注册变换。
        /// 注册是相似变换（等比缩放 + 旋转），因此若矩形不是 7:10（宽高不等），
        /// 取宽高平均作为等比缩放，并在矩形内居中 —— 避免把人物拉伸变形。
        /// </summary>
        public static LadyLakeLayerRegistration FromTopLeftRect(float x, float yFromTop, float width, float height, string note)
        {
            float scale = (width + height) * 0.5f;
            LadyLakeLayerRegistration r = new LadyLakeLayerRegistration();
            r.a = scale;
            r.b = 0f;
            r.tx = x + (width - scale) * 0.5f;
            r.ty = (1f - yFromTop - height) + (height - scale) * 0.5f;
            r.note = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0} rect=({1:F3},{2:F3},{3:F3},{4:F3}) -> uniform scale {5:F4}",
                note, x, yFromTop, width, height, scale);
            return r;
        }

        /// <summary>由两组对应点解出相似变换（两点确定 缩放+旋转+平移）。</summary>
        public static LadyLakeLayerRegistration FromPointPairs(Vector2 q0, Vector2 f0, Vector2 q1, Vector2 f1, string note)
        {
            Vector2 u = q1 - q0;
            Vector2 v = f1 - f0;
            float lu = Mathf.Max(1e-6f, u.magnitude);
            float lv = v.magnitude;
            float inv = 1f / (lu * lu);
            float cos = Vector2.Dot(u, v) * inv;
            float sin = (u.x * v.y - u.y * v.x) * inv;

            LadyLakeLayerRegistration r = new LadyLakeLayerRegistration();
            r.a = cos;
            r.b = sin;
            r.tx = f0.x - (r.a * q0.x - r.b * q0.y);
            r.ty = f0.y - (r.b * q0.x + r.a * q0.y);
            r.note = note;
            return r;
        }

        public Vector2 Apply(Vector2 q)
        {
            return new Vector2(a * q.x - b * q.y + tx, b * q.x + a * q.y + ty);
        }

        public Vector2 ApplyInverse(Vector2 f)
        {
            float det = a * a + b * b;
            if (det < 1e-9f) return f;
            float x = f.x - tx;
            float y = f.y - ty;
            return new Vector2((a * x + b * y) / det, (-b * x + a * y) / det);
        }

        public float Scale { get { return Mathf.Sqrt(a * a + b * b); } }
        public float RotationDeg { get { return Mathf.Atan2(b, a) * Mathf.Rad2Deg; } }

        public override string ToString()
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "scale={0:F4} rot={1:F2}deg t=({2:F4},{3:F4}) {4}",
                Scale, RotationDeg, tx, ty, autoFitted ? "[auto]" : "[config]");
        }
    }

    /// <summary>
    /// 一层浮雕的深度参数。坐标一律是**画面像素、左下原点、y 向上**（用 LadyLakeLabConfig.AnchorToFrame
    /// 从原图左上原点的锚点换算）。
    /// </summary>
    public struct LadyLakeLayerShape
    {
        public float baseDepth;
        public float zOffset;
        public float inflateRadiusFramePx;
        public float edgeSoftnessFramePx;
        public float edgeCurvature;
        public LadyLakeProfileMode profileMode;

        public Vector2 bump1CenterFrame;
        public float bump1RadiusFramePx;
        public float bump1Depth;

        public Vector2 bump2CenterFrame;
        public float bump2RadiusFramePx;
        public float bump2Depth;

        public Vector2 recessCenterFrame;
        public float recessInnerFramePx;
        public float recessOuterFramePx;
        public float recessDepth;

        public static LadyLakeLayerShape Default()
        {
            LadyLakeLayerShape s = new LadyLakeLayerShape();
            s.baseDepth = 0.12f;
            s.zOffset = 0f;
            s.inflateRadiusFramePx = 24f;
            s.edgeSoftnessFramePx = 6f;
            s.edgeCurvature = 0f;
            s.profileMode = LadyLakeProfileMode.None;
            s.bump1RadiusFramePx = 0f;
            s.bump2RadiusFramePx = 0f;
            s.recessInnerFramePx = 0f;
            s.recessOuterFramePx = 0f;
            return s;
        }
    }

    public struct LadyLakeBeamSpec
    {
        public float xTopFrame;
        public float yTopFrame;
        public float widthTopFrame;
        public float xBottomFrame;
        public float yBottomFrame;
        public float widthBottomFrame;
        public float curveFrame;
        public float phase;
        public float intensity;
        public float tint;
        public int rows;
    }

    /// <summary>
    /// 网格工厂：把分层 PNG + 注册变换 + 深度参数变成真正细分的浮雕网格 / 薄厚度剑 / 体积光束 / 全屏面片。
    /// 所有顶点都放在「画面坐标系」，再用透视补偿推到深度 z 上，投影位置与原画一致。
    /// </summary>
    public static class LadyLakeLabMeshes
    {
        /// <summary>参与建面的最低 alpha（0..255）。</summary>
        public const byte EmitAlpha = 16;

        /// <summary>是否属于「不透明实体」的 alpha 阈值（距离场基准）。</summary>
        public const byte InsideAlpha = 128;

        /// <summary>距离场编码：单位 = 1/3 像素（3-4 chamfer）。</summary>
        private const int DistScale = 3;

        // ------------------------------------------------------------------
        // 基础换算
        // ------------------------------------------------------------------

        /// <summary>画面像素 + 深度 z -> 世界坐标（含透视补偿，投影位置 = 原画位置）。</summary>
        public static Vector3 PlaneToWorld(float xFramePx, float yFrameFromBottomPx, float z)
        {
            return LadyLakeLabConfig.FramePxToWorld(xFramePx, yFrameFromBottomPx, z);
        }

        /// <summary>图像像素（py 为自下而上的行号）-> 画面像素（左下原点）。</summary>
        public static void LayerToFrame(
            LadyLakeLayerRegistration registration,
            float px, float py, int width, int height,
            out float xFrame, out float yFrame)
        {
            Vector2 q = new Vector2((px + 0.5f) / Mathf.Max(1, width), (py + 0.5f) / Mathf.Max(1, height));
            Vector2 f = registration.Apply(q);
            xFrame = f.x * LadyLakeLabConfig.FrameWidthPx;
            yFrame = f.y * LadyLakeLabConfig.FrameHeightPx;
        }

        /// <summary>图像像素 -> 画面像素的局部比例（画面像素 / 图像像素）。</summary>
        public static float RegistrationScaleToFrame(LadyLakeLayerRegistration registration, int layerWidth)
        {
            return registration.Scale * LadyLakeLabConfig.FrameWidthPx / Mathf.Max(1, layerWidth);
        }

        // ------------------------------------------------------------------
        // 距离场
        // ------------------------------------------------------------------

        /// <summary>3-4 chamfer 距离变换：返回每个像素到「透明区」的距离（单位 1/3 像素）。</summary>
        public static int[] BuildDistanceField(byte[] alpha, int width, int height, byte insideThreshold)
        {
            int count = width * height;
            int[] dist = new int[count];
            const int far = 1 << 24;

            for (int i = 0; i < count; i++)
            {
                dist[i] = alpha[i] >= insideThreshold ? far : 0;
            }

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    int idx = row + x;
                    int d = dist[idx];
                    if (d == 0) continue;

                    if (x > 0) d = Mathf.Min(d, dist[idx - 1] + DistScale);
                    if (y > 0) d = Mathf.Min(d, dist[idx - width] + DistScale);
                    if (x > 0 && y > 0) d = Mathf.Min(d, dist[idx - width - 1] + 4);
                    if (x < width - 1 && y > 0) d = Mathf.Min(d, dist[idx - width + 1] + 4);
                    dist[idx] = d;
                }
            }

            for (int y = height - 1; y >= 0; y--)
            {
                int row = y * width;
                for (int x = width - 1; x >= 0; x--)
                {
                    int idx = row + x;
                    int d = dist[idx];
                    if (d == 0) continue;

                    if (x < width - 1) d = Mathf.Min(d, dist[idx + 1] + DistScale);
                    if (y < height - 1) d = Mathf.Min(d, dist[idx + width] + DistScale);
                    if (x < width - 1 && y < height - 1) d = Mathf.Min(d, dist[idx + width + 1] + 4);
                    if (x > 0 && y < height - 1) d = Mathf.Min(d, dist[idx + width - 1] + 4);
                    dist[idx] = d;
                }
            }

            return dist;
        }

        private static float SampleDistance(int[] dist, int width, int height, float px, float py)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(px), 0, width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(py), 0, height - 1);
            return dist[y * width + x] / (float)DistScale;
        }

        private static float SampleAlpha(byte[] alpha, int width, int height, float px, float py)
        {
            float x = Mathf.Clamp(px, 0f, width - 1.001f);
            float y = Mathf.Clamp(py, 0f, height - 1.001f);
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            int x1 = Mathf.Min(x0 + 1, width - 1);
            int y1 = Mathf.Min(y0 + 1, height - 1);
            float fx = x - x0;
            float fy = y - y0;

            float a00 = alpha[y0 * width + x0];
            float a10 = alpha[y0 * width + x1];
            float a01 = alpha[y1 * width + x0];
            float a11 = alpha[y1 * width + x1];

            float a0 = a00 + (a10 - a00) * fx;
            float a1 = a01 + (a11 - a01) * fx;
            return a0 + (a1 - a0) * fy;
        }

        // ------------------------------------------------------------------
        // 深度剖面
        // ------------------------------------------------------------------

        private static float RadialFalloff(float xFrame, float yFrame, Vector2 center, float radiusPx)
        {
            if (radiusPx <= 0.001f) return 0f;
            float dx = (xFrame - center.x) / radiusPx;
            float dy = (yFrame - center.y) / radiusPx;
            return Mathf.Exp(-(dx * dx + dy * dy) * 1.6f);
        }

        private static float RingFalloff(float xFrame, float yFrame, Vector2 center, float inner, float outer)
        {
            if (outer <= inner + 0.001f) return 0f;
            float r = Vector2.Distance(new Vector2(xFrame, yFrame), center);
            float mid = (inner + outer) * 0.5f;
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, mid, r));
            float fall = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(mid, outer, r));
            return rise * fall;
        }

        /// <summary>
        /// 计算「朝观众凸出的深度」（正数 = 更靠近观众）。轮廓处必然回到 0，
        /// 因此抠像边缘不会出现立体台阶。坐标是画面像素、左下原点。
        /// </summary>
        private static float ComputeDepth(
            LadyLakeLayerShape shape,
            float xFrame, float yFrame,
            float inflateProfile)
        {
            float depth = shape.baseDepth;

            if (shape.bump1RadiusFramePx > 0f)
            {
                depth += RadialFalloff(xFrame, yFrame, shape.bump1CenterFrame, shape.bump1RadiusFramePx) * shape.bump1Depth;
            }
            if (shape.bump2RadiusFramePx > 0f)
            {
                depth += RadialFalloff(xFrame, yFrame, shape.bump2CenterFrame, shape.bump2RadiusFramePx) * shape.bump2Depth;
            }
            if (shape.recessOuterFramePx > 0f)
            {
                depth -= RingFalloff(xFrame, yFrame, shape.recessCenterFrame, shape.recessInnerFramePx, shape.recessOuterFramePx) * shape.recessDepth;
            }

            depth *= inflateProfile;

            if (shape.edgeCurvature > 0f)
            {
                float rx = (xFrame - LadyLakeLabConfig.FrameWidthPx * 0.5f) / (LadyLakeLabConfig.FrameWidthPx * 0.5f);
                float ry = (yFrame - LadyLakeLabConfig.FrameHeightPx * 0.5f) / (LadyLakeLabConfig.FrameHeightPx * 0.5f);
                depth -= shape.edgeCurvature * (rx * rx + ry * ry);
            }

            return depth;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 <= 1e-6f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return Vector2.Distance(p, a + ab * t);
        }

        // ------------------------------------------------------------------
        // 浮雕层
        // ------------------------------------------------------------------

        /// <summary>
        /// 生成一层的浮雕网格。
        /// clipFrame 为可选的画面像素裁剪框（xmin,ymin,xmax,ymax，左下原点；zero-size = 不裁剪），
        /// 用于生成「握剑手局部上覆网格」。
        /// </summary>
        public static Mesh BuildReliefLayer(
            LadyLakeLayerTexture layer,
            LadyLakeLayerShape shape,
            LadyLakeLayerRole role,
            LadyLakeLayerRegistration registration,
            Vector4 clipFrame,
            out LadyLakeMeshStats stats)
        {
            int w = layer.width;
            int h = layer.height;
            byte[] alpha = layer.alpha;
            int[] dist = layer.distance;
            bool hasClip = clipFrame.z > clipFrame.x && clipFrame.w > clipFrame.y;

            float stepFrame = LadyLakeLabConfig.RoleToGridStep(role);
            float scaleToFrame = RegistrationScaleToFrame(registration, w);
            float stepLayer = Mathf.Max(1f, stepFrame / Mathf.Max(1e-5f, scaleToFrame));
            float inflateLayerPx = Mathf.Max(1f, shape.inflateRadiusFramePx / Mathf.Max(1e-5f, scaleToFrame));
            float edgeSoftLayerPx = Mathf.Max(1f, shape.edgeSoftnessFramePx / Mathf.Max(1e-5f, scaleToFrame));

            int minX, minY, maxX, maxY;
            layer.GetActiveBounds(EmitAlpha, out minX, out minY, out maxX, out maxY);

            if (hasClip)
            {
                ClampBoundsToClip(registration, clipFrame, w, h,
                    ref minX, ref minY, ref maxX, ref maxY);
            }

            int margin = Mathf.CeilToInt(3f / Mathf.Max(1e-5f, scaleToFrame)) + 2;
            minX = Mathf.Max(0, minX - margin);
            minY = Mathf.Max(0, minY - margin);
            maxX = Mathf.Min(w - 1, maxX + margin);
            maxY = Mathf.Min(h - 1, maxY + margin);

            int nx = Mathf.Max(2, Mathf.CeilToInt((maxX - minX) / stepLayer) + 1);
            int ny = Mathf.Max(2, Mathf.CeilToInt((maxY - minY) / stepLayer) + 1);

            int vertexCount = nx * ny;
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            Vector2[] metas = new Vector2[vertexCount];
            Vector2[] profiles = new Vector2[vertexCount];
            Color32[] weights = new Color32[vertexCount];
            float[] nodeAlpha = new float[vertexCount];
            bool[] insideClip = new bool[vertexCount];

            // 关节 / 头部锚点：原图是左上原点，这里统一换算成画面左下原点
            Vector2 shoulderFrame = LadyLakeLabConfig.AnchorToFrame(LadyLakeLabConfig.ShoulderAnchor);
            Vector2 elbowFrame = LadyLakeLabConfig.AnchorToFrame(LadyLakeLabConfig.ElbowAnchor);
            Vector2 wristFrame = LadyLakeLabConfig.AnchorToFrame(LadyLakeLabConfig.SwordHandAnchor);
            Vector2 faceFrame = LadyLakeLabConfig.AnchorToFrame(LadyLakeLabConfig.FaceAnchor);
            Vector2 hairFrame = LadyLakeLabConfig.AnchorToFrame(new Vector2(335f, 300f));
            const float ArmRadiusFramePx = 50f;

            for (int j = 0; j < ny; j++)
            {
                float py = Mathf.Min(minY + j * stepLayer, h - 1f);

                for (int i = 0; i < nx; i++)
                {
                    float px = Mathf.Min(minX + i * stepLayer, w - 1f);

                    float xFrame;
                    float yFrame;
                    LayerToFrame(registration, px, py, w, h, out xFrame, out yFrame);

                    float alphaValue = SampleAlpha(alpha, w, h, px, py);
                    float distPx = SampleDistance(dist, w, h, px, py);

                    float inflate = LadyLakeMath.Quintic(Mathf.Clamp01(distPx / inflateLayerPx));
                    float depth = ComputeDepth(shape, xFrame, yFrame, inflate);
                    float z = shape.zOffset - depth;

                    int idx = j * nx + i;
                    nodeAlpha[idx] = alphaValue;
                    insideClip[idx] = !hasClip
                                      || (xFrame >= clipFrame.x && xFrame <= clipFrame.z
                                          && yFrame >= clipFrame.y && yFrame <= clipFrame.w);
                    vertices[idx] = PlaneToWorld(xFrame, yFrame, z);
                    uvs[idx] = new Vector2((px + 0.5f) / w, (py + 0.5f) / h);
                    metas[idx] = new Vector2(
                        Mathf.Clamp01(1f - distPx / edgeSoftLayerPx),
                        Mathf.Clamp01(yFrame / LadyLakeLabConfig.FrameHeightPx));

                    float torso = 1f;
                    float upper = 0f;
                    float fore = 0f;

                    if (role == LadyLakeLayerRole.Figure)
                    {
                        Vector2 p = new Vector2(xFrame, yFrame);
                        float dUpper = DistanceToSegment(p, shoulderFrame, elbowFrame);
                        float dFore = DistanceToSegment(p, elbowFrame, wristFrame);
                        upper = LadyLakeMath.Quintic(Mathf.Clamp01(1f - dUpper / ArmRadiusFramePx));
                        fore = LadyLakeMath.Quintic(Mathf.Clamp01(1f - dFore / ArmRadiusFramePx));
                        float hand = LadyLakeMath.Quintic(
                            Mathf.Clamp01(1f - Vector2.Distance(p, wristFrame) / (ArmRadiusFramePx * 0.85f)));
                        if (hand > fore) fore = hand;

                        // 头部保护区：脸/头发不参与手臂蒙皮，避免脸部被手臂旋转带歪
                        float armMask = Mathf.Clamp01(1f - RadialFalloff(xFrame, yFrame, faceFrame, 96f));
                        upper *= armMask;
                        fore *= armMask;

                        if (alphaValue < 24f)
                        {
                            upper = 0f;
                            fore = 0f;
                        }

                        if (Vector2.Distance(p, LadyLakeLabConfig.AnchorToFrame(LadyLakeLabConfig.FaceAnchor)) < 62f) { upper = 0; fore = 0; }
                        if (p.y < 280f && dFore < 38f && p.x > 290f) { upper = 0; fore = 1; }
                        float total = upper + fore;
                        if (total > 1f)
                        {
                            float k = 1f / total;
                            upper *= k;
                            fore *= k;
                        }
                        torso = 1f - upper - fore;
                    }

                    weights[idx] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(torso * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(upper * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(fore * 255f), 0, 255),
                        0);

                    if (shape.profileMode == LadyLakeProfileMode.Figure)
                    {
                        float bodyFalloff = Mathf.Clamp01(1f - Mathf.Abs(yFrame - 380f) / 430f);
                        float breath = inflate * bodyFalloff;
                        float hair = RingFalloff(xFrame, yFrame, hairFrame, 92f, 168f) * inflate;
                        float handDistance = Vector2.Distance(new Vector2(xFrame, yFrame), wristFrame);
                        float raisedDistance = Vector2.Distance(new Vector2(xFrame, yFrame), LadyLakeLabConfig.AnchorToFrame(new Vector2(180,135)));
                        float stableWrist = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Mathf.Min(handDistance, raisedDistance)-45f)/40f));
                        profiles[idx] = new Vector2(breath * stableWrist, hair * stableWrist);
                    }
                    else if (shape.profileMode == LadyLakeProfileMode.Sway)
                    {
                        float sway = inflate * Mathf.Pow(Mathf.Clamp01(yFrame / LadyLakeLabConfig.FrameHeightPx), 1.4f);
                        profiles[idx] = new Vector2(sway, Mathf.Clamp01(yFrame / LadyLakeLabConfig.FrameHeightPx));
                    }
                    else
                    {
                        profiles[idx] = Vector2.zero;
                    }
                }
            }

            List<int> triangles = new List<int>((nx - 1) * (ny - 1) * 6);
            for (int j = 0; j < ny - 1; j++)
            {
                for (int i = 0; i < nx - 1; i++)
                {
                    int a = j * nx + i;
                    int b = j * nx + i + 1;
                    int c = (j + 1) * nx + i + 1;
                    int d = (j + 1) * nx + i;

                    if (!insideClip[a] && !insideClip[b] && !insideClip[c] && !insideClip[d]) continue;

                    bool active = role == LadyLakeLayerRole.Foreground || role == LadyLakeLayerRole.Figure || nodeAlpha[a] > EmitAlpha || nodeAlpha[b] > EmitAlpha
                                  || nodeAlpha[c] > EmitAlpha || nodeAlpha[d] > EmitAlpha;
                    if (!active) continue;

                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(a); triangles.Add(c); triangles.Add(d);
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "LadyLake_" + role + "_Relief";
            if (vertexCount > 65000)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.SetUVs(1, new List<Vector2>(metas));
            mesh.SetUVs(2, new List<Vector2>(profiles));
            mesh.colors32 = weights;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            stats = new LadyLakeMeshStats();
            stats.vertices = vertexCount;
            stats.triangles = triangles.Count / 3;
            stats.subMeshes = 1;
            stats.bounds = mesh.bounds;
            return mesh;
        }

        /// <summary>把图像像素包围盒收缩到「画面裁剪框」在图像空间中的逆映射范围。</summary>
        private static void ClampBoundsToClip(
            LadyLakeLayerRegistration registration,
            Vector4 clipFrame, int w, int h,
            ref int minX, ref int minY, ref int maxX, ref int maxY)
        {
            float minPx = float.MaxValue, minPy = float.MaxValue, maxPx = float.MinValue, maxPy = float.MinValue;
            float[] xs = { clipFrame.x, clipFrame.z, clipFrame.x, clipFrame.z };
            float[] ys = { clipFrame.y, clipFrame.y, clipFrame.w, clipFrame.w };

            for (int i = 0; i < 4; i++)
            {
                Vector2 f = new Vector2(xs[i] / LadyLakeLabConfig.FrameWidthPx, ys[i] / LadyLakeLabConfig.FrameHeightPx);
                Vector2 q = registration.ApplyInverse(f);
                float px = q.x * w - 0.5f;
                float py = q.y * h - 0.5f;
                if (px < minPx) minPx = px;
                if (px > maxPx) maxPx = px;
                if (py < minPy) minPy = py;
                if (py > maxPy) maxPy = py;
            }

            minX = Mathf.Max(minX, Mathf.FloorToInt(minPx));
            minY = Mathf.Max(minY, Mathf.FloorToInt(minPy));
            maxX = Mathf.Min(maxX, Mathf.CeilToInt(maxPx));
            maxY = Mathf.Min(maxY, Mathf.CeilToInt(maxPy));

            if (maxX < minX) { maxX = minX; }
            if (maxY < minY) { maxY = minY; }
        }

        // ------------------------------------------------------------------
        // 剑：剪影主轴配准 + 薄厚度挤出
        // ------------------------------------------------------------------

        /// <summary>
        /// 用源画锚点自动配准 figure.png。
        ///
        /// 图像生成器把人物放大 / 移位并补绘过，所以不能假定整图 UV 与画面重合。约束与目标：
        ///   * 硬约束 1：insideAnchors（脸/肩/肘/握剑手/高举手）必须落在 figure.png 剪影内部；
        ///   * 硬约束 2：outsideAnchors（剑尖、画面角落等源画里确定不属于人物的位置）必须落在透明区；
        ///   * 目标：在满足硬约束的候选里，取离 prior 最近的那个。
        ///
        /// prior 是由画中特征点实测得到的相似变换（见 Builder 的注释），搜索只在它 ±10% 内做微调。
        /// 注意：没有使用「肩-肘-手必须整条在剪影内」的骨架约束 —— AIGC 补绘的手臂走向与源画解剖点
        /// 并不完全一致，该约束会把正确解一起排除（已实测）。
        /// </summary>
        public static bool TryFitFigureRegistration(
            LadyLakeLayerTexture figure,
            Vector2[] insideAnchorsTopLeftPx,
            Vector2[] outsideAnchorsTopLeftPx,
            LadyLakeLayerRegistration prior,
            out LadyLakeLayerRegistration registration,
            out string report)
        {
            registration = prior;
            report = "";

            int w = figure.width;
            int h = figure.height;
            int[] dt = figure.distance;
            if (dt == null || dt.Length != w * h)
            {
                report = "figure 距离场缺失，无法自动配准。";
                return false;
            }
            if (insideAnchorsTopLeftPx == null || insideAnchorsTopLeftPx.Length < 3)
            {
                report = "内部锚点不足，无法自动配准。";
                return false;
            }

            Vector2[] inside = ToFrameNorm(insideAnchorsTopLeftPx);
            Vector2[] outside = outsideAnchorsTopLeftPx != null ? ToFrameNorm(outsideAnchorsTopLeftPx) : new Vector2[0];

            float priorScale = prior.Scale > 0.05f ? prior.Scale : 0.78f;
            const float ScaleWindow = 0.10f;
            const float RotWindow = 3f;
            const float ShiftWindow = 0.03f;
            const int Steps = 9;

            float bestScale = priorScale;
            float bestRot = prior.RotationDeg;
            float bestTx = prior.tx;
            float bestTy = prior.ty;
            float bestCost = float.PositiveInfinity;
            float bestMinDepth = 0f;
            bool found = false;
            int feasible = 0;

            for (int si = 0; si < Steps; si++)
            {
                float scale = priorScale * Mathf.Lerp(1f - ScaleWindow, 1f + ScaleWindow, si / (float)(Steps - 1));
                for (int ri = 0; ri < Steps; ri++)
                {
                    float rot = prior.RotationDeg + Mathf.Lerp(-RotWindow, RotWindow, ri / (float)(Steps - 1));
                    float rad = rot * Mathf.Deg2Rad;
                    float c = Mathf.Cos(rad);
                    float s = Mathf.Sin(rad);
                    float invScale = 1f / scale;

                    for (int xi = 0; xi < Steps; xi++)
                    {
                        float tx = prior.tx + Mathf.Lerp(-ShiftWindow, ShiftWindow, xi / (float)(Steps - 1));
                        for (int yi = 0; yi < Steps; yi++)
                        {
                            float ty = prior.ty + Mathf.Lerp(-ShiftWindow, ShiftWindow, yi / (float)(Steps - 1));

                            float minDepth = float.MaxValue;
                            bool ok = true;

                            for (int k = 0; k < inside.Length && ok; k++)
                            {
                                float depth;
                                if (!SampleDepth(inside[k], dt, w, h, 1e9f, c, s, invScale, tx, ty, out depth) || depth <= 0.01f)
                                {
                                    ok = false;
                                    break;
                                }
                                if (depth < minDepth) minDepth = depth;
                            }

                            for (int k = 0; k < outside.Length && ok; k++)
                            {
                                float depth;
                                if (!SampleDepth(outside[k], dt, w, h, 1e9f, c, s, invScale, tx, ty, out depth)) { ok = false; break; }
                                if (depth > 0.01f) { ok = false; break; }
                            }

                            if (!ok) continue;

                            feasible++;
                            float cost = Mathf.Abs(scale - priorScale) / priorScale * 4f
                                         + Mathf.Abs(rot - prior.RotationDeg) / 6f * 0.5f
                                         + Mathf.Abs(tx - prior.tx) / 0.06f * 0.5f
                                         + Mathf.Abs(ty - prior.ty) / 0.06f * 0.5f;
                            if (cost < bestCost)
                            {
                                bestCost = cost;
                                bestScale = scale;
                                bestRot = rot;
                                bestTx = tx;
                                bestTy = ty;
                                bestMinDepth = minDepth;
                                found = true;
                            }
                        }
                    }
                }
            }

            if (!found)
            {
                float priorMin = MinInsideDepth(inside, dt, w, h, prior);
                report = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "figure 自动精调未找到可行解，沿用实测 prior（prior 内部锚点最小深度 {0:F2}px）。", priorMin);
                return false;
            }

            LadyLakeLayerRegistration fitted = new LadyLakeLayerRegistration();
            float bestRad = bestRot * Mathf.Deg2Rad;
            fitted.a = bestScale * Mathf.Cos(bestRad);
            fitted.b = bestScale * Mathf.Sin(bestRad);
            fitted.tx = bestTx;
            fitted.ty = bestTy;
            fitted.autoFitted = true;
            fitted.note = "refined around measured prior within anchor constraints";

            registration = fitted;
            report = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "figure 自动配准：scale={0:F4} rot={1:F2}deg t=({2:F4},{3:F4})；" +
                "{4} 个解剖锚点全部在剪影内（最小深度 {5:F2}px），{6} 个外部锚点全部在透明区，" +
                "可行候选 {7} 个，取离实测 prior(scale={8:F4}) 最近者。",
                fitted.Scale, fitted.RotationDeg, fitted.tx, fitted.ty,
                inside.Length, bestMinDepth, outside.Length, feasible, priorScale);
            return true;
        }

        private static float MinInsideDepth(
            Vector2[] insideNorm, int[] dt, int w, int h, LadyLakeLayerRegistration registration)
        {
            float minDepth = float.MaxValue;
            for (int k = 0; k < insideNorm.Length; k++)
            {
                Vector2 q = registration.ApplyInverse(insideNorm[k]);
                float px = q.x * w - 0.5f;
                float py = q.y * h - 0.5f;
                float d = 0f;
                if (px >= 0f && py >= 0f && px <= w - 1f && py <= h - 1f)
                {
                    d = dt[Mathf.RoundToInt(py) * w + Mathf.RoundToInt(px)] / (float)DistScale;
                }
                if (d < minDepth) minDepth = d;
            }
            return minDepth;
        }

        private static Vector2[] ToFrameNorm(Vector2[] anchorsTopLeftPx)
        {
            Vector2[] result = new Vector2[anchorsTopLeftPx.Length];
            for (int i = 0; i < anchorsTopLeftPx.Length; i++)
            {
                Vector2 a = LadyLakeLabConfig.AnchorToFrame(anchorsTopLeftPx[i]);
                result[i] = new Vector2(
                    a.x / LadyLakeLabConfig.FrameWidthPx,
                    a.y / LadyLakeLabConfig.FrameHeightPx);
            }
            return result;
        }

        private static bool SampleDepth(
            Vector2 frameNorm, int[] dt, int w, int h, float capPx,
            float c, float s, float invScale, float tx, float ty, out float depth)
        {
            depth = 0f;
            float fx = frameNorm.x - tx;
            float fy = frameNorm.y - ty;
            float qx = (c * fx + s * fy) * invScale;
            float qy = (-s * fx + c * fy) * invScale;
            float px = qx * w - 0.5f;
            float py = qy * h - 0.5f;

            if (px < 0f || py < 0f || px > w - 1f || py > h - 1f) return false;

            depth = dt[Mathf.RoundToInt(py) * w + Mathf.RoundToInt(px)] / (float)DistScale;
            if (depth > capPx) depth = capPx;
            return true;
        }

        private static void ScoreAnchors(
            Vector2[] frameNorm, int[] dt, int w, int h, float capPx,
            LadyLakeLayerRegistration registration,
            out float minDepth, out float sumDepth)
        {
            minDepth = float.MaxValue;
            sumDepth = 0f;

            for (int k = 0; k < frameNorm.Length; k++)
            {
                Vector2 q = registration.ApplyInverse(frameNorm[k]);
                float px = q.x * w - 0.5f;
                float py = q.y * h - 0.5f;
                float d = 0f;
                if (px >= 0f && py >= 0f && px <= w - 1f && py <= h - 1f)
                {
                    d = dt[Mathf.RoundToInt(py) * w + Mathf.RoundToInt(px)] / (float)DistScale;
                }
                if (d > capPx) d = capPx;
                if (d < minDepth) minDepth = d;
                sumDepth += d;
            }
        }

        /// <summary>剑在源画里的三个锚点（画面像素，左下原点）。</summary>
        public static Vector2 SwordTipFramePx()
        {
            return LadyLakeLabConfig.AnchorToFrame(LadyLakeLabConfig.SwordTipAnchor);
        }

        public static Vector2 SwordPommelFramePx()
        {
            return LadyLakeLabConfig.AnchorToFrame(LadyLakeLabConfig.SwordPommelAnchor);
        }

        public static Vector2 SwordGripFramePx()
        {
            return LadyLakeLabConfig.AnchorToFrame(LadyLakeLabConfig.SwordHandAnchor);
        }

        /// <summary>握点在「剑尖 -> 剑柄底」主轴上的归一化位置（由源画三个锚点算出，约 0.864）。</summary>
        public static float SwordGripParameter()
        {
            Vector2 tip = SwordTipFramePx();
            Vector2 pommel = SwordPommelFramePx();
            Vector2 grip = SwordGripFramePx();
            float len = Vector2.Distance(tip, pommel);
            return len > 1e-3f ? Vector2.Distance(tip, grip) / len : 0.86f;
        }

        /// <summary>
        /// 用 sword.png 剪影的主轴把「画出来的剑」配准到源画锚点：
        /// 主轴两端分别对应源画的剑尖 (0.155,0.387) 与剑柄底 (0.785,0.775)，
        /// 两点解出相似变换（缩放 + 旋转 + 平移），于是画出来的护手 / 剑柄也落在源画位置，
        /// 剑的实际柄轴握点自然与手心锚点重合 —— 不再依赖人工 rect 猜测。
        /// </summary>
        public static bool TryFitSwordRegistration(
            LadyLakeLayerTexture sword,
            out LadyLakeLayerRegistration registration,
            out string report)
        {
            registration = LadyLakeLayerRegistration.Identity();
            report = "";

            int w = sword.width;
            int h = sword.height;
            byte[] alpha = sword.alpha;

            double sx = 0d, sy = 0d;
            long n = 0;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (alpha[row + x] < InsideAlpha) continue;
                    sx += x;
                    sy += y;
                    n++;
                }
            }

            if (n < 64)
            {
                report = "sword alpha 有效像素过少，无法拟合主轴。";
                return false;
            }

            double cx = sx / n;
            double cy = sy / n;
            double cxx = 0d, cxy = 0d, cyy = 0d;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (alpha[row + x] < InsideAlpha) continue;
                    double dx = x - cx;
                    double dy = y - cy;
                    cxx += dx * dx;
                    cxy += dx * dy;
                    cyy += dy * dy;
                }
            }

            double theta = 0.5d * System.Math.Atan2(2d * cxy, cxx - cyy);
            double ax = System.Math.Cos(theta);
            double ay = System.Math.Sin(theta);

            double tmin = double.MaxValue, tmax = double.MinValue;
            int tipX = 0, tipY = 0, pommelX = 0, pommelY = 0;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (alpha[row + x] < InsideAlpha) continue;
                    double t = (x - cx) * ax + (y - cy) * ay;
                    if (t < tmin) { tmin = t; tipX = x; tipY = y; }
                    if (t > tmax) { tmax = t; pommelX = x; pommelY = y; }
                }
            }

            // 主轴方向任意，用源画「剑尖 -> 剑柄底」的方向 (x 增大、y 向下) 判定哪端是柄底
            float dirX = pommelX - tipX;
            float dirY = pommelY - tipY;
            if (dirX * 0.852f + dirY * -0.524f < 0f)
            {
                int tx = tipX, ty = tipY;
                tipX = pommelX; tipY = pommelY;
                pommelX = tx; pommelY = ty;
            }

            Vector2 qTip = new Vector2(tipX / (float)w, tipY / (float)h);
            Vector2 qPommel = new Vector2(pommelX / (float)w, pommelY / (float)h);

            Vector2 fTip = SwordTipFramePx();
            Vector2 fPommel = SwordPommelFramePx();
            Vector2 nTip = new Vector2(fTip.x / LadyLakeLabConfig.FrameWidthPx, fTip.y / LadyLakeLabConfig.FrameHeightPx);
            Vector2 nPommel = new Vector2(fPommel.x / LadyLakeLabConfig.FrameWidthPx, fPommel.y / LadyLakeLabConfig.FrameHeightPx);

            LadyLakeLayerRegistration fitted = LadyLakeLayerRegistration.FromPointPairs(
                qTip, nTip, qPommel, nPommel,
                "auto fit from sword silhouette principal axis");
            fitted.autoFitted = true;

            if (fitted.Scale < 0.25f || fitted.Scale > 4f)
            {
                report = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "自动配准比例异常 scale={0:F3}（图像主轴 {1},{2} -> {3},{4}），已回退到配置 rect。",
                    fitted.Scale, tipX, tipY, pommelX, pommelY);
                return false;
            }

            // 配准后整把剑应落在画面附近
            Vector2 c0 = fitted.Apply(new Vector2(0f, 0f));
            Vector2 c1 = fitted.Apply(new Vector2(1f, 0f));
            Vector2 c2 = fitted.Apply(new Vector2(1f, 1f));
            Vector2 c3 = fitted.Apply(new Vector2(0f, 1f));
            float bx0 = Mathf.Min(Mathf.Min(c0.x, c1.x), Mathf.Min(c2.x, c3.x));
            float bx1 = Mathf.Max(Mathf.Max(c0.x, c1.x), Mathf.Max(c2.x, c3.x));
            float by0 = Mathf.Min(Mathf.Min(c0.y, c1.y), Mathf.Min(c2.y, c3.y));
            float by1 = Mathf.Max(Mathf.Max(c0.y, c1.y), Mathf.Max(c2.y, c3.y));

            if (bx0 < -0.6f || bx1 > 1.6f || by0 < -0.6f || by1 > 1.6f)
            {
                report = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "自动配准后 bbox 越界 x[{0:F3},{1:F3}] y[{2:F3},{3:F3}]，已回退到配置 rect。",
                    bx0, bx1, by0, by1);
                return false;
            }

            registration = fitted;
            report = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "sword 自动配准：图像主轴端 ({0},{1})->({2},{3}) 对应源画剑尖/剑柄底；scale={4:F4} rot={5:F2}deg；" +
                "配准后画面 bbox x[{6:F3},{7:F3}] y[{8:F3},{9:F3}]（归一化，左下原点）。",
                tipX, tipY, pommelX, pommelY, fitted.Scale, fitted.RotationDeg, bx0, bx1, by0, by1);
            return true;
        }

        /// <summary>
        /// 用 sword.png 的 alpha 轮廓挤出薄厚度剑。
        /// 本地原点 = 源画握点（配准之后画出来的剑柄就在那里），因此运行时只需 位置=当前握点、旋转=附加角度。
        /// submesh 0=正面(sword.png)，1=背面，2=侧壁（后两者暗银）。
        /// </summary>
        public static Mesh BuildSword(
            LadyLakeLayerTexture layer,
            LadyLakeLayerRegistration registration,
            float zCenter,
            float thickness,
            out LadyLakeMeshStats stats)
        {
            int w = layer.width;
            int h = layer.height;
            byte[] alpha = layer.alpha;
            int[] dist = layer.distance;

            float scaleToFrame = RegistrationScaleToFrame(registration, w);
            float stepFrame = LadyLakeLabConfig.GridStepSwordFramePx;
            float stepLayer = Mathf.Max(1f, stepFrame / Mathf.Max(1e-5f, scaleToFrame));
            float roundLayerPx = Mathf.Max(1f, 3f / Mathf.Max(1e-5f, scaleToFrame));
            float edgeSoftLayerPx = Mathf.Max(1f, 4f / Mathf.Max(1e-5f, scaleToFrame));

            int minX, minY, maxX, maxY;
            layer.GetActiveBounds(EmitAlpha, out minX, out minY, out maxX, out maxY);
            int margin = Mathf.CeilToInt(3f / Mathf.Max(1e-5f, scaleToFrame)) + 2;
            minX = Mathf.Max(0, minX - margin);
            minY = Mathf.Max(0, minY - margin);
            maxX = Mathf.Min(w - 1, maxX + margin);
            maxY = Mathf.Min(h - 1, maxY + margin);

            int nx = Mathf.Max(2, Mathf.CeilToInt((maxX - minX) / stepLayer) + 1);
            int ny = Mathf.Max(2, Mathf.CeilToInt((maxY - minY) / stepLayer) + 1);

            Vector2 gripPlane = LadyLakeLabConfig.AnchorToWorld(LadyLakeLabConfig.SwordHandAnchor);
            float gripCompensation = LadyLakeLabConfig.PerspectiveCompensation(zCenter);
            Vector3 gripOrigin = new Vector3(gripPlane.x * gripCompensation, gripPlane.y * gripCompensation, 0f);

            int nodeCount = nx * ny;
            Vector3[] front = new Vector3[nodeCount];
            Vector3[] back = new Vector3[nodeCount];
            Vector2[] uvs = new Vector2[nodeCount];
            Vector2[] metas = new Vector2[nodeCount];
            bool[] nodeActive = new bool[nodeCount];
            float half = thickness * 0.5f;

            for (int j = 0; j < ny; j++)
            {
                float py = Mathf.Min(minY + j * stepLayer, h - 1f);

                for (int i = 0; i < nx; i++)
                {
                    float px = Mathf.Min(minX + i * stepLayer, w - 1f);
                    float xFrame;
                    float yFrame;
                    LayerToFrame(registration, px, py, w, h, out xFrame, out yFrame);

                    float alphaValue = SampleAlpha(alpha, w, h, px, py);
                    float distPx = SampleDistance(dist, w, h, px, py);
                    float round = LadyLakeMath.Quintic(Mathf.Clamp01(distPx / roundLayerPx));

                    int idx = j * nx + i;
                    nodeActive[idx] = alphaValue > EmitAlpha;
                    uvs[idx] = new Vector2((px + 0.5f) / w, (py + 0.5f) / h);
                    metas[idx] = new Vector2(Mathf.Clamp01(1f - distPx / edgeSoftLayerPx), 1f);

                    Vector3 f = PlaneToWorld(xFrame, yFrame, zCenter - half * round);
                    Vector3 b = PlaneToWorld(xFrame, yFrame, zCenter + half * round);
                    front[idx] = f - gripOrigin;
                    back[idx] = b - gripOrigin;
                }
            }

            // 只有「四个角都透明」的格子才不建面，其余交给 shader 按 alpha 软边裁切
            bool[] cellActive = new bool[(nx - 1) * (ny - 1)];
            for (int j = 0; j < ny - 1; j++)
            {
                for (int i = 0; i < nx - 1; i++)
                {
                    int a = j * nx + i;
                    int b = j * nx + i + 1;
                    int c = (j + 1) * nx + i + 1;
                    int d = (j + 1) * nx + i;
                    cellActive[j * (nx - 1) + i] = nodeActive[a] || nodeActive[b] || nodeActive[c] || nodeActive[d];
                }
            }

            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvList = new List<Vector2>();
            List<Vector2> metaList = new List<Vector2>();
            List<int> frontTris = new List<int>();
            List<int> backTris = new List<int>();
            List<int> sideTris = new List<int>();

            // 正面（朝观众，法线 -Z）
            for (int j = 0; j < ny; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    int idx = j * nx + i;
                    verts.Add(front[idx]);
                    uvList.Add(uvs[idx]);
                    metaList.Add(metas[idx]);
                }
            }
            for (int j = 0; j < ny - 1; j++)
            {
                for (int i = 0; i < nx - 1; i++)
                {
                    if (!cellActive[j * (nx - 1) + i]) continue;
                    int a = j * nx + i;
                    int b = j * nx + i + 1;
                    int c = (j + 1) * nx + i + 1;
                    int d = (j + 1) * nx + i;
                    frontTris.Add(a); frontTris.Add(c); frontTris.Add(b);
                    frontTris.Add(a); frontTris.Add(d); frontTris.Add(c);
                }
            }

            // 背面（法线 +Z）
            int backBase = verts.Count;
            for (int j = 0; j < ny; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    int idx = j * nx + i;
                    verts.Add(back[idx]);
                    uvList.Add(uvs[idx]);
                    metaList.Add(metas[idx]);
                }
            }
            for (int j = 0; j < ny - 1; j++)
            {
                for (int i = 0; i < nx - 1; i++)
                {
                    if (!cellActive[j * (nx - 1) + i]) continue;
                    int a = backBase + j * nx + i;
                    int b = backBase + j * nx + i + 1;
                    int c = backBase + (j + 1) * nx + i + 1;
                    int d = backBase + (j + 1) * nx + i;
                    backTris.Add(a); backTris.Add(b); backTris.Add(c);
                    backTris.Add(a); backTris.Add(c); backTris.Add(d);
                }
            }

            AddSwordSideWalls(nx, ny, front, back, metas, cellActive, verts, uvList, metaList, sideTris);

            Mesh mesh = new Mesh();
            mesh.name = "LadyLake_Sword_Extruded";
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvList);
            mesh.SetUVs(1, metaList);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(frontTris, 0);
            mesh.SetTriangles(backTris, 1);
            mesh.SetTriangles(sideTris, 2);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            stats = new LadyLakeMeshStats();
            stats.vertices = verts.Count;
            stats.triangles = (frontTris.Count + backTris.Count + sideTris.Count) / 3;
            stats.subMeshes = 3;
            stats.bounds = mesh.bounds;
            return mesh;
        }

        /// <summary>
        /// 侧壁四边形。约定 front 在 -z（靠观众）、back 在 +z；四种绕序都经叉积验证，法线朝外，
        /// 因此 Cull Back 下侧面 / 背面都能正常显示，不会「渲染背面消失」。
        /// </summary>
        private static void AddSwordSideWalls(
            int nx, int ny,
            Vector3[] front, Vector3[] back, Vector2[] metas,
            bool[] cellActive,
            List<Vector3> verts, List<Vector2> uvList, List<Vector2> metaList,
            List<int> tris)
        {
            System.Action<Vector3, Vector3, Vector3, Vector3, Vector2> quad =
                delegate (Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector2 meta)
                {
                    int b = verts.Count;
                    verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
                    uvList.Add(new Vector2(0f, 0f));
                    uvList.Add(new Vector2(1f, 0f));
                    uvList.Add(new Vector2(1f, 1f));
                    uvList.Add(new Vector2(0f, 1f));
                    metaList.Add(meta); metaList.Add(meta); metaList.Add(meta); metaList.Add(meta);
                    tris.Add(b + 0); tris.Add(b + 1); tris.Add(b + 2);
                    tris.Add(b + 0); tris.Add(b + 2); tris.Add(b + 3);
                };

            for (int j = 0; j < ny - 1; j++)
            {
                for (int i = 0; i < nx - 1; i++)
                {
                    if (!cellActive[j * (nx - 1) + i]) continue;

                    int a = j * nx + i;
                    int b = j * nx + i + 1;
                    int c = (j + 1) * nx + i + 1;
                    int d = (j + 1) * nx + i;

                    bool above = j + 1 < ny - 1 && cellActive[(j + 1) * (nx - 1) + i];
                    if (!above) quad(front[d], back[d], back[c], front[c], metas[d]);

                    bool below = j - 1 >= 0 && cellActive[(j - 1) * (nx - 1) + i];
                    if (!below) quad(front[a], front[b], back[b], back[a], metas[a]);

                    bool right = i + 1 < nx - 1 && cellActive[j * (nx - 1) + i + 1];
                    if (!right) quad(front[b], front[c], back[c], back[b], metas[b]);

                    bool left = i - 1 >= 0 && cellActive[j * (nx - 1) + i - 1];
                    if (!left) quad(front[a], back[a], back[d], front[d], metas[a]);
                }
            }
        }

        // ------------------------------------------------------------------
        // 全屏面片 / 光束
        // ------------------------------------------------------------------

        /// <summary>生成一个覆盖视锥的平面四边形（uv 0..1，中心在原点）。</summary>
        public static Mesh BuildQuad(string name, float width, float height, float z, out LadyLakeMeshStats stats)
        {
            Mesh mesh = new Mesh();
            mesh.name = name;

            Vector3[] v =
            {
                new Vector3(-width * 0.5f, -height * 0.5f, z),
                new Vector3(width * 0.5f, -height * 0.5f, z),
                new Vector3(width * 0.5f, height * 0.5f, z),
                new Vector3(-width * 0.5f, height * 0.5f, z),
            };
            Vector2[] uv =
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            };
            int[] tris = { 0, 1, 2, 0, 2, 3 };

            mesh.vertices = v;
            mesh.uv = uv;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            stats = new LadyLakeMeshStats();
            stats.vertices = 4;
            stats.triangles = 2;
            stats.subMeshes = 1;
            stats.bounds = mesh.bounds;
            return mesh;
        }

        /// <summary>生成一组体积光束（软边梯形条带；uv.x 横向 0..1，uv.y 从下 0 到上 1）。</summary>
        public static Mesh BuildBeams(List<LadyLakeBeamSpec> beams, out LadyLakeMeshStats stats)
        {
            Mesh mesh = new Mesh();
            mesh.name = "LadyLake_LightBeams";

            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Vector2> metas = new List<Vector2>();
            List<Color32> colors = new List<Color32>();
            List<int> tris = new List<int>();

            for (int k = 0; k < beams.Count; k++)
            {
                LadyLakeBeamSpec spec = beams[k];
                int rows = Mathf.Max(2, spec.rows);
                int baseIndex = verts.Count;

                for (int r = 0; r < rows; r++)
                {
                    float t = r / (float)(rows - 1);
                    float y = Mathf.Lerp(spec.yBottomFrame, spec.yTopFrame, t);
                    float xCenter = Mathf.Lerp(spec.xBottomFrame, spec.xTopFrame, t)
                                    + Mathf.Sin(t * Mathf.PI) * spec.curveFrame;
                    float halfWidth = Mathf.Lerp(spec.widthBottomFrame, spec.widthTopFrame, t) * 0.5f;
                    float z = LadyLakeLabConfig.ZCaustics - 0.06f + k * 0.004f;

                    verts.Add(PlaneToWorld(xCenter - halfWidth, y, z));
                    verts.Add(PlaneToWorld(xCenter + halfWidth, y, z));
                    uvs.Add(new Vector2(0f, t));
                    uvs.Add(new Vector2(1f, t));
                    metas.Add(new Vector2(spec.phase, 0f));
                    metas.Add(new Vector2(spec.phase, 0f));

                    Color32 c = new Color(spec.tint, spec.tint, spec.tint, spec.intensity);
                    colors.Add(c);
                    colors.Add(c);
                }

                for (int r = 0; r < rows - 1; r++)
                {
                    int a = baseIndex + r * 2;
                    int b = a + 1;
                    int c2 = a + 3;
                    int d = a + 2;
                    tris.Add(a); tris.Add(b); tris.Add(c2);
                    tris.Add(a); tris.Add(c2); tris.Add(d);
                }
            }

            mesh.indexFormat = verts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, metas);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            stats = new LadyLakeMeshStats();
            stats.vertices = verts.Count;
            stats.triangles = tris.Count / 3;
            stats.subMeshes = 1;
            stats.bounds = mesh.bounds;
            return mesh;
        }
    }
}
