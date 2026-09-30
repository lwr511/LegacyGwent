using System.Collections.Generic;
using UnityEngine;

namespace LegacyGwent.LadyLakeLab
{
    /// <summary>
    /// 人物网格 CPU 蒙皮。
    ///
    /// 权重来源：Mesh 顶点色 (r=躯干, g=上臂, b=前臂)，另用 uv3.x/.y 存放
    /// 「呼吸系数 / 发丝系数」两个平滑剖面。上臂/前臂权重按到「肩-肘-腕」折线的
    /// 距离做平滑衰减并且只作用于手臂附近，因此：
    ///   * 脸、躯干、高举的另一只手只吃躯干权重 -> 不会被拉变形（脸不扭曲）；
    ///   * 手臂像真实关节一样绕肩 / 肘旋转，骨骼长度不变，原画手臂不会被拉伸；
    ///   * 抬高在画中的左手没有被当成握剑手，它只吃躯干权重 + 微小呼吸。
    ///
    /// 剑的握点直接取前臂骨骼矩阵作用在原画腕点上的结果，与网格里手部顶点的
    /// 位移完全一致，所以「握住」相位握点距离恒为 0（无滑移）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LadyLakeFigureRig : LadyLakeMeshDeformer
    {
        [Header("关节（来自原图锚点，世界单位）")]
        public Vector2 shoulder = new Vector2(0.075f, 1.05f);
        public Vector2 elbow = new Vector2(0.725f, -0.69f);
        public Vector2 wrist = new Vector2(1.015f, -1.55f);

        [Header("呼吸 / 发丝")]
        public Vector2 breathDirection = new Vector2(0.22f, 0.98f);
        public float hairSwayPhaseOffset = 0.6f;
        [Range(0.1f, 8f)] public float hairSwayScale = 1f;

        [Header("握点测量")]
        [Tooltip("前臂权重高于该阈值的顶点视为「手部顶点」，用于握剑距离/滑移验收。")]
        public float handWeightThreshold = 0.85f;

        private Vector3[] _restPositions;
        private Vector3[] _restNormals;
        private Color32[] _weights;
        private Vector2[] _profiles;
        private Vector3[] _positions;
        private Vector3[] _normals;
        private int[] _handVertexIndices = new int[0];

        private Vector2 _breathDirNormalized;
        private Vector2 _hairDirNormalized;

        public int VertexCount { get; private set; }
        public int HandVertexCount { get { return _handVertexIndices.Length; } }
        public Vector3[] DeformedPositions { get { return _positions; } }
        public int[] HandVertexIndices { get { return _handVertexIndices; } }

        protected override Vector3 GetBoundsPadding()
        {
            // 手臂绕肩可摆动约 1.6 单位，留够余量避免剔除闪烁。
            return new Vector3(2.0f, 2.0f, 0.8f);
        }

        protected override void Awake()
        {
            base.Awake();
            CacheBuffers();
        }

        /// <summary>缓存静止数据与手部顶点列表。</summary>
        public bool CacheBuffers()
        {
            Mesh source = SourceMesh;
            if (source == null) return false;

            _restPositions = source.vertices;
            _restNormals = source.normals;
            _weights = source.colors32;
            _profiles = source.uv3;

            VertexCount = _restPositions.Length;
            _positions = new Vector3[VertexCount];
            _normals = new Vector3[VertexCount];

            if (_weights == null || _weights.Length != VertexCount)
            {
                _weights = new Color32[VertexCount];
                for (int i = 0; i < VertexCount; i++) _weights[i] = new Color32(255, 0, 0, 0);
            }

            if (_profiles == null || _profiles.Length != VertexCount)
            {
                _profiles = new Vector2[VertexCount];
            }

            if (_restNormals == null || _restNormals.Length != VertexCount)
            {
                source.RecalculateNormals();
                _restNormals = source.normals;
                if (_restNormals == null || _restNormals.Length != VertexCount)
                {
                    _restNormals = new Vector3[VertexCount];
                    for (int i = 0; i < VertexCount; i++) _restNormals[i] = Vector3.back;
                }
            }

            List<int> hand = new List<int>();
            var handRenderer = GetComponent<Renderer>();
            var handTexture = handRenderer != null && handRenderer.sharedMaterial != null ? handRenderer.sharedMaterial.mainTexture as Texture2D : null;
            var handUvs = source.uv;
            float threshold = Mathf.Clamp01(handWeightThreshold) * 255f;
            for (int i = 0; i < VertexCount; i++)
            {
                float k = LadyLakeLabConfig.PerspectiveCompensation(_restPositions[i].z);
                Vector2 restPoint = new Vector2(_restPositions[i].x, _restPositions[i].y) / k;
                if (_weights[i].b >= threshold && Vector2.Distance(restPoint, wrist) < .42f &&
                    (handTexture == null || !handTexture.isReadable || handTexture.GetPixelBilinear(handUvs[i].x, handUvs[i].y).a > .5f)) hand.Add(i);
            }
            _handVertexIndices = hand.ToArray();

            _breathDirNormalized = breathDirection.sqrMagnitude > 1e-6f ? breathDirection.normalized : new Vector2(0f, 1f);
            _hairDirNormalized = new Vector2(0.86f, 0.51f);

            return true;
        }

        private static Vector3 RotateAroundZ(Vector3 v, Vector2 pivot, float degrees)
        {
            if (Mathf.Abs(degrees) < 1e-6f) return v;
            Vector2 r = LadyLakeMath.RotateAround(new Vector2(v.x, v.y), pivot, degrees);
            return new Vector3(r.x, r.y, v.z);
        }

        /// <summary>只旋转方向（绕原点），用于法线。</summary>
        private static Vector3 RotateDirectionZ(Vector3 v, float degrees)
        {
            if (Mathf.Abs(degrees) < 1e-6f) return v;
            Vector2 r = LadyLakeMath.Rotate(new Vector2(v.x, v.y), degrees);
            return new Vector3(r.x, r.y, v.z);
        }

        public override void ApplyPose(LadyLakePose pose)
        {
            if (!EnsureRuntimeMesh()) return;
            if (_restPositions == null || _restPositions.Length == 0)
            {
                if (!CacheBuffers()) return;
            }

            float upper = pose.upperAngleDeg;
            float fore = pose.foreAngleDeg;
            Vector2 elbowRotated = LadyLakeMath.RotateAround(elbow, shoulder, upper);

            float breathPhase = LadyLakeMath.TwoPi * pose.normalizedTime * LadyLakeLabConfig.BreathCycles;
            Vector2 breathVec = _breathDirNormalized * (Mathf.Sin(breathPhase) * LadyLakeLabConfig.BreathAmplitude);

            float hairPhase = LadyLakeMath.TwoPi * pose.normalizedTime * LadyLakeLabConfig.HairCycles + hairSwayPhaseOffset;
            Vector2 hairVec = _hairDirNormalized * (Mathf.Sin(hairPhase) * LadyLakeLabConfig.HairAmplitude * hairSwayScale);

            bool armMoves = Mathf.Abs(upper) > 1e-4f || Mathf.Abs(fore) > 1e-4f;

            for (int i = 0; i < VertexCount; i++)
            {
                Vector3 p = _restPositions[i];
                float compensation = LadyLakeLabConfig.PerspectiveCompensation(p.z);
                p.x /= compensation; p.y /= compensation;
                Vector3 n = _restNormals[i];
                Color32 c = _weights[i];

                float wt = c.r * (1f / 255f);
                float wu = c.g * (1f / 255f);
                float wf = c.b * (1f / 255f);

                Vector3 pos;
                Vector3 nrm;

                if (!armMoves || (wu <= 0.002f && wf <= 0.002f))
                {
                    pos = p;
                    nrm = n;
                }
                else
                {
                    Vector3 pu = RotateAroundZ(p, shoulder, upper);
                    Vector3 nu = RotateDirectionZ(n, upper);
                    Vector3 pf = RotateAroundZ(pu, elbowRotated, fore);
                    Vector3 nf = RotateDirectionZ(nu, fore);

                    pos = p * wt + pu * wu + pf * wf;
                    nrm = n * wt + nu * wu + nf * wf;
                }

                Vector2 profile = _profiles[i];
                if (profile.x > 0.0005f)
                {
                    pos.x += breathVec.x * profile.x;
                    pos.y += breathVec.y * profile.x;
                }
                if (profile.y > 0.0005f)
                {
                    pos.x += hairVec.x * profile.y;
                    pos.y += hairVec.y * profile.y;
                }

                pos.x *= compensation; pos.y *= compensation;
                _positions[i] = pos;
                _normals[i] = nrm.sqrMagnitude > 1e-8f ? nrm.normalized : n;
            }

            runtimeMesh.vertices = _positions;
            runtimeMesh.normals = _normals;
        }

        /// <summary>
        /// 取出手部顶点（前臂权重 &gt;= 阈值）相对剑握点的偏移，用于握剑距离 / 滑移 / 手心对齐验收。
        /// centroid 是手部顶点在网格空间（= 人物对象本地空间，本对象在原点）的质心。
        /// </summary>
        public void SampleHandOffsets(
            Vector2 swordGrip, List<Vector2> offsets, out float minDistance, out Vector2 centroid)
        {
            offsets.Clear();
            minDistance = float.PositiveInfinity;
            centroid = Vector2.zero;
            if (_positions == null || _handVertexIndices.Length == 0) return;

            Vector2 sum = Vector2.zero;
            for (int k = 0; k < _handVertexIndices.Length; k++)
            {
                int i = _handVertexIndices[k];
                float compensation = LadyLakeLabConfig.PerspectiveCompensation(_positions[i].z);
                Vector2 p = new Vector2(_positions[i].x, _positions[i].y) / compensation;
                offsets.Add(p - swordGrip);
                sum += p;
                float d = Vector2.Distance(p, swordGrip);
                if (d < minDistance) minDistance = d;
            }

            centroid = sum / _handVertexIndices.Length;
            if (float.IsPositiveInfinity(minDistance)) minDistance = -1f;
        }
    }
}
