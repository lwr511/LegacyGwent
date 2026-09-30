using UnityEngine;

namespace LegacyGwent.LadyLakeLab
{
    /// <summary>
    /// 水效调度：把一次姿态分发到各粒子场，并只更新少量必须逐帧变化的材质参数。
    /// 光束 / 焦散 / 背景的动画全部由全局 _LadyLakeTime 驱动（着色器内），
    /// 所以 Editor 静态取样与 Play 画面一致。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LadyLakeWaterFx : MonoBehaviour
    {
        [Header("粒子场")]
        public LadyLakeParticleField bubblesBack;
        public LadyLakeParticleField bubblesFront;
        public LadyLakeParticleField motes;
        public LadyLakeParticleField swordWake;

        [Header("全屏水折射")]
        public Renderer refractionRenderer;
        [Range(0f, 0.05f)] public float rippleStrength = 0.006f;
        [Range(0f, 0.05f)] public float rippleWakeBoost = 0.004f;

        [Header("参考（验收统计用，不参与逐帧更新）")]
        public Renderer causticsRenderer;
        public Renderer[] beamRenderers;

        private MaterialPropertyBlock _block;
        private static readonly int RippleId = Shader.PropertyToID("_RippleStrength");
        private static readonly int IntensityId = Shader.PropertyToID("_FxIntensity");

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
        }

        public void ApplyPose(LadyLakePose pose)
        {
            if (bubblesBack != null) bubblesBack.ApplyPose(pose);
            if (bubblesFront != null) bubblesFront.ApplyPose(pose);
            if (motes != null) motes.ApplyPose(pose);
            if (swordWake != null) swordWake.ApplyPose(pose);

            if (refractionRenderer == null) return;
            if (_block == null) _block = new MaterialPropertyBlock();

            float intensity = Mathf.Clamp(pose.intensity, 0f, 2f);
            float ripple = (rippleStrength + rippleWakeBoost * Mathf.Clamp01(pose.wake)) * intensity;

            _block.Clear();
            _block.SetFloat(RippleId, ripple);
            _block.SetFloat(IntensityId, intensity);
            refractionRenderer.SetPropertyBlock(_block);
        }

        /// <summary>Editor 取样结束后清掉 MPB，避免残留临时状态。</summary>
        public void ClearPropertyBlocks()
        {
            if (refractionRenderer != null) refractionRenderer.SetPropertyBlock(null);
        }
    }
}
