using UnityEngine;

namespace LegacyGwent.LadyLakeLab
{
    /// <summary>
    /// 独立剑对象。网格以「原画握点」为本地原点构建，因此：
    ///   位置 = 当前握点，旋转 = 相对原画角度的附加旋转。
    /// 握住相位下握点等于前臂骨骼作用在原画腕点上的结果，剑随手刚性移动，零滑移。
    /// 剑的正面用 sword.png，背面 / 侧壁用暗银材质（不会出现第二把剑）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LadyLakeSwordRig : MonoBehaviour
    {
        [Tooltip("剑网格（本地原点 = 原画握点）。")]
        public Transform swordTransform;

        [Tooltip("剑刃发光 / rune 用材质（正面）。")]
        public Renderer frontRenderer;

        [Tooltip("暗银材质（背面 + 侧壁）。")]
        public Renderer metalRenderer;

        [Range(0f, 4f)] public float glowScale = 1f;

        /// <summary>剑网格本地原点已经过透视补偿，位置驱动时要乘上同一个系数。</summary>
        public float planeCompensation = 1f;

        public LadyLakePose LastPose { get; private set; }

        private MaterialPropertyBlock _block;
        private static readonly int GlowStrengthId = Shader.PropertyToID("_GlowStrength");
        private static readonly int WakeId = Shader.PropertyToID("_WakeStrength");
        private static readonly int HeldId = Shader.PropertyToID("_HeldPhase");

        private void Awake()
        {
            if (swordTransform == null) swordTransform = transform;
            _block = new MaterialPropertyBlock();
        }

        public void ApplyPose(LadyLakePose pose)
        {
            LastPose = pose;
            if (swordTransform == null) swordTransform = transform;

            Vector3 p = swordTransform.localPosition;
            swordTransform.localPosition = new Vector3(
                pose.swordGrip.x * planeCompensation,
                pose.swordGrip.y * planeCompensation,
                p.z);
            swordTransform.localRotation = Quaternion.Euler(0f, 0f, pose.swordAngleDeg);

            if (_block == null) _block = new MaterialPropertyBlock();

            // frontRenderer 与 metalRenderer 通常是同一个 MeshRenderer（3 个 submesh 共用一个渲染器），
            // 而 MaterialPropertyBlock 是 per-renderer 的，因此这里只写一份、两处共用同样的发光强度。
            _block.Clear();
            _block.SetFloat(GlowStrengthId, pose.glow * glowScale);
            _block.SetFloat(WakeId, Mathf.Clamp01(pose.wake));
            _block.SetFloat(HeldId, pose.held ? 1f : 0f);

            if (frontRenderer != null) frontRenderer.SetPropertyBlock(_block);
            if (metalRenderer != null && metalRenderer != frontRenderer) metalRenderer.SetPropertyBlock(_block);
        }

        /// <summary>供 Editor 取样后恢复材质状态（MPB 不落盘）。</summary>
        public void ClearPropertyBlocks()
        {
            if (frontRenderer != null) frontRenderer.SetPropertyBlock(null);
            if (metalRenderer != null) metalRenderer.SetPropertyBlock(null);
        }
    }
}
