using UnityEngine;

namespace LegacyGwent.LadyLakeLab
{
    public enum LadyLakeParticleKind
    {
        /// <summary>水下气泡（多层 z 深度，向上漂浮）。</summary>
        Bubble = 0,
        /// <summary>漂浮微尘（更小、更慢、叠加发光）。</summary>
        Mote = 1,
        /// <summary>剑移动时的局部水流气泡（跟随剑轴，强度由 pose.wake 驱动）。</summary>
        SwordWake = 2,
    }

    /// <summary>
    /// 程序化粒子面片场。不使用 ParticleSystem：
    /// 位置完全由 (归一化时间, seed) 解析计算，Editor 取样与 Play 结果一致，
    /// 上升周期与摆动周期都取整数，因此循环首尾无缝。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LadyLakeParticleField : LadyLakeMeshDeformer
    {
        [Header("类型")]
        public LadyLakeParticleKind kind = LadyLakeParticleKind.Bubble;

        [Header("分布")]
        public int seed = 11;
        [Range(1, 400)] public int count = 48;
        public Vector2 zRange = new Vector2(-0.15f, 0.25f);
        public Vector2 sizeRangeFramePx = new Vector2(3.5f, 12f);
        [Tooltip("每个循环内上升的画面高度倍数（整数范围）。")]
        public Vector2 riseCyclesRange = new Vector2(1f, 3f);
        public float wobbleAmplitudeFramePx = 9f;
        public int wobbleCycles = 2;
        public float edgeMarginFramePx = 40f;

        [Header("外观")]
        [Range(0f, 1f)] public float baseAlpha = 0.5f;
        public Color tint = new Color(0.72f, 0.95f, 0.92f, 1f);

        [Header("剑尾迹专用")]
        public float wakeReach = 1.15f;
        public float wakeSpread = 0.34f;
        public float wakeRise = 0.5f;

        private Vector3[] _positions;
        private Color32[] _colors;
        private Vector2[] _uvs;

        public int ParticleCount { get { return Mathf.Max(1, count); } }
        public int MeshVertexCount { get { return ParticleCount * 4; } }

        private static readonly Vector2[] CornerOffsets =
        {
            new Vector2(-0.5f, -0.5f),
            new Vector2(0.5f, -0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(-0.5f, 0.5f),
        };

        private static readonly Vector2[] CornerUvs =
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
        };

        protected override Vector3 GetBoundsPadding()
        {
            // 粒子会漂到画面之外，给足余量避免被视锥剔除。
            return new Vector3(6f, 6f, 2f);
        }

        protected override void Awake()
        {
            base.Awake();
            EnsureArrays();
        }

        private void EnsureArrays()
        {
            int n = ParticleCount;
            if (_positions != null && _positions.Length == n * 4) return;

            _positions = new Vector3[n * 4];
            _colors = new Color32[n * 4];
            _uvs = new Vector2[n * 4];
            for (int i = 0; i < n; i++)
            {
                for (int c = 0; c < 4; c++) _uvs[i * 4 + c] = CornerUvs[c];
            }
        }

        /// <summary>在给定网格上建立拓扑（每粒子 4 顶点 / 2 三角形）。</summary>
        public void BuildTopology(Mesh mesh)
        {
            if (mesh == null) return;
            EnsureArrays();

            int n = ParticleCount;
            int[] tris = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                int v = i * 4;
                int t = i * 6;
                tris[t + 0] = v + 0;
                tris[t + 1] = v + 1;
                tris[t + 2] = v + 2;
                tris[t + 3] = v + 2;
                tris[t + 4] = v + 3;
                tris[t + 5] = v + 0;
            }

            mesh.Clear();
            mesh.name = name + "_Particles";
            mesh.vertices = _positions;
            mesh.uv = _uvs;
            mesh.colors32 = _colors;
            mesh.triangles = tris;
        }

        /// <summary>Builder 用：生成静止姿态（t=0）的可保存网格资产。</summary>
        public Mesh CreateBakedMesh(LadyLakePose pose)
        {
            EnsureArrays();
            Mesh mesh = new Mesh();
            BuildTopology(mesh);
            UpdateVertices(pose);
            mesh.vertices = _positions;
            mesh.colors32 = _colors;
            mesh.RecalculateBounds();
            return mesh;
        }

        public override void ApplyPose(LadyLakePose pose)
        {
            if (!EnsureRuntimeMesh()) return;
            EnsureArrays();

            if (runtimeMesh.vertexCount != ParticleCount * 4)
            {
                BuildTopology(runtimeMesh);
                runtimeMesh.MarkDynamic();
            }

            UpdateVertices(pose);
            runtimeMesh.vertices = _positions;
            runtimeMesh.colors32 = _colors;
        }

        private void UpdateVertices(LadyLakePose pose)
        {
            int n = ParticleCount;
            float norm = pose.normalizedTime;
            float intensity = Mathf.Clamp(pose.intensity, 0f, 2f);
            float spanPx = LadyLakeLabConfig.FrameHeightPx + edgeMarginFramePx * 2f;

            int maxRise = Mathf.Max(1, Mathf.RoundToInt(riseCyclesRange.y));
            int minRise = Mathf.Clamp(Mathf.RoundToInt(riseCyclesRange.x), 1, maxRise);
            int maxWobble = Mathf.Max(1, wobbleCycles);

            for (int i = 0; i < n; i++)
            {
                int b = seed * 7919 + i * 13;
                float sizePx = LadyLakeMath.HashRange(b + 1, sizeRangeFramePx.x, sizeRangeFramePx.y);
                float z = LadyLakeMath.HashRange(b + 2, zRange.x, zRange.y);
                float phase = LadyLakeMath.Hash01(b + 3);
                int riseCycles = minRise + Mathf.FloorToInt(LadyLakeMath.Hash01(b + 4) * (maxRise - minRise + 1));
                if (riseCycles > maxRise) riseCycles = maxRise;
                float alphaJitter = LadyLakeMath.HashRange(b + 8, 0.55f, 1f);

                Vector2 plane;
                float alpha;
                float sizeScale;

                if (kind == LadyLakeParticleKind.SwordWake)
                {
                    float along = LadyLakeMath.HashRange(b + 9, 0.05f, wakeReach);
                    float lateral = LadyLakeMath.HashRange(b + 10, -wakeSpread, wakeSpread);
                    int wakeCycles = 1 + Mathf.FloorToInt(LadyLakeMath.Hash01(b + 11) * 2f);
                    float cyclePos = LadyLakeMath.Wrap(norm * wakeCycles + phase, 1f);

                    float rad = pose.swordAngleDeg * Mathf.Deg2Rad;
                    Vector2 axis = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                    Vector2 perp = new Vector2(-axis.y, axis.x);

                    plane = pose.swordGrip + axis * along + perp * lateral + new Vector2(0f, cyclePos * wakeRise);

                    float fade = Mathf.Sin(Mathf.PI * cyclePos);
                    alpha = baseAlpha * alphaJitter * fade * Mathf.Clamp01(pose.wake) * intensity;
                    sizeScale = 0.5f + 0.5f * (1f - cyclePos);
                }
                else
                {
                    float cycleY = LadyLakeMath.Wrap(norm * riseCycles + phase, 1f);
                    float yFrame = cycleY * spanPx - edgeMarginFramePx;
                    float x0 = LadyLakeMath.HashRange(b + 5, -edgeMarginFramePx, LadyLakeLabConfig.FrameWidthPx + edgeMarginFramePx);
                    float wobblePhase = LadyLakeMath.Hash01(b + 6) * LadyLakeMath.TwoPi;
                    int wobble = 1 + Mathf.FloorToInt(LadyLakeMath.Hash01(b + 7) * maxWobble);
                    if (wobble > maxWobble) wobble = maxWobble;
                    float wobbleOffset = Mathf.Sin(LadyLakeMath.TwoPi * norm * wobble + wobblePhase) * wobbleAmplitudeFramePx;

                    plane = LadyLakeLabConfig.FramePxToPlane(x0 + wobbleOffset, yFrame);

                    float fade = Mathf.Sin(Mathf.PI * Mathf.Clamp01(cycleY));
                    alpha = baseAlpha * alphaJitter * fade * intensity;
                    sizeScale = 1f;
                }

                float k = LadyLakeLabConfig.PerspectiveCompensation(z);
                Vector2 world = new Vector2(plane.x * k, plane.y * k);
                float size = (sizePx / LadyLakeLabConfig.PixelsPerUnit) * sizeScale * k;
                Color32 col = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(alpha));

                for (int c = 0; c < 4; c++)
                {
                    int v = i * 4 + c;
                    _positions[v] = new Vector3(
                        world.x + CornerOffsets[c].x * size,
                        world.y + CornerOffsets[c].y * size,
                        z);
                    _colors[v] = col;
                }
            }
        }
    }
}
