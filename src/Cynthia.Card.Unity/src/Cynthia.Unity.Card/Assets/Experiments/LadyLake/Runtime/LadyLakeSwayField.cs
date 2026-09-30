using UnityEngine;

namespace LegacyGwent.LadyLakeLab
{
    /// <summary>
    /// 通用「柔性摆动」变形器：按 uv3.x（摆动强度剖面）与 uv3.y（沿画面高度的相位）
    /// 施加一个整数周期的正弦位移。前景荷叶 / 水草用它做随水流摇摆，
    /// 幅度在根部为 0、叶尖最大，因此与背景不会脱开。
    /// 整数周期保证循环首尾位置与速度都连续。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LadyLakeSwayField : LadyLakeMeshDeformer
    {
        [Header("摆动")]
        public Vector2 direction = new Vector2(1f, 0.12f);
        public float amplitude = LadyLakeLabConfig.ForegroundSwayAmplitude;
        public int cycles = LadyLakeLabConfig.ForegroundSwayCycles;
        [Tooltip("沿画面高度的相位差（弧度/单位归一化高度）。")]
        public float verticalPhaseScale = 2.2f;
        public float phaseOffset = 0.35f;

        [Header("呼吸（可关）")]
        public bool useBreath = false;

        private Vector3[] _restPositions;
        private Vector2[] _profiles;
        private Vector3[] _positions;
        private Vector2 _dirNormalized;

        public int VertexCount { get; private set; }
        public Vector3[] DeformedPositions { get { return _positions; } }

        protected override Vector3 GetBoundsPadding()
        {
            return new Vector3(1.0f, 1.0f, 0.4f);
        }

        protected override void Awake()
        {
            base.Awake();
            CacheBuffers();
        }

        public bool CacheBuffers()
        {
            Mesh source = SourceMesh;
            if (source == null) return false;

            _restPositions = source.vertices;
            _profiles = source.uv3;
            VertexCount = _restPositions.Length;
            _positions = new Vector3[VertexCount];

            if (_profiles == null || _profiles.Length != VertexCount)
            {
                _profiles = new Vector2[VertexCount];
            }

            _dirNormalized = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector2.right;
            return true;
        }

        public override void ApplyPose(LadyLakePose pose)
        {
            if (!EnsureRuntimeMesh()) return;
            if (_restPositions == null || _restPositions.Length == 0)
            {
                if (!CacheBuffers()) return;
            }

            float basePhase = LadyLakeMath.TwoPi * pose.normalizedTime * Mathf.Max(1, cycles) + phaseOffset;
            float breathPhase = LadyLakeMath.TwoPi * pose.normalizedTime * LadyLakeLabConfig.BreathCycles;

            for (int i = 0; i < VertexCount; i++)
            {
                Vector3 p = _restPositions[i];
                Vector2 profile = _profiles[i];
                float weight = profile.x;

                if (weight > 0.0005f)
                {
                    float phase = basePhase - profile.y * verticalPhaseScale;
                    float amount = Mathf.Sin(phase) * amplitude * weight;
                    p.x += _dirNormalized.x * amount;
                    p.y += _dirNormalized.y * amount;

                    if (useBreath)
                    {
                        p.y += Mathf.Sin(breathPhase) * LadyLakeLabConfig.BreathAmplitude * weight * 0.5f;
                    }
                }

                _positions[i] = p;
            }

            runtimeMesh.vertices = _positions;
        }
    }
}
