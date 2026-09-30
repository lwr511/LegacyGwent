using UnityEngine;

namespace LegacyGwent.LadyLakeLab
{
    /// <summary>
    /// 微小相机视差。默认只有 0.12 单位的极小幅位移（画面不会被拉伸），
    /// enableParallax=false 即可完全关闭，回到严格的原画构图。
    /// 因为浮雕层是真实带 z 的网格，这点位移会带来真实的层间视差。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LadyLakeCameraRig : MonoBehaviour
    {
        public Transform cameraTransform;

        [Tooltip("关闭后摄像机完全静止（0 视差）。")]
        public bool enableParallax = true;

        [Tooltip("最大水平位移（单位）。0.12 约为画面宽度的 2.4%。")]
        public float horizontalAmplitude = 0.12f;

        [Tooltip("最大垂直位移（单位）。")]
        public float verticalAmplitude = 0.035f;

        [Tooltip("一个循环内往复的次数（整数，保证首尾连续）。")]
        public int cycles = 1;

        public Vector3 baseLocalPosition;
        public Vector3 baseLocalEuler;

        private bool _captured;

        private void Awake()
        {
            CaptureBase();
        }

        public void CaptureBase()
        {
            if (cameraTransform == null) cameraTransform = transform;
            baseLocalPosition = cameraTransform.localPosition;
            baseLocalEuler = cameraTransform.localEulerAngles;
            _captured = true;
        }

        public void ApplyPose(LadyLakePose pose)
        {
            if (cameraTransform == null) cameraTransform = transform;
            if (!_captured) CaptureBase();

            if (!enableParallax)
            {
                cameraTransform.localPosition = baseLocalPosition;
                return;
            }

            float phase = LadyLakeMath.TwoPi * pose.normalizedTime * Mathf.Max(1, cycles);
            float dx = Mathf.Sin(phase) * horizontalAmplitude;
            float dy = Mathf.Sin(phase * 2f + 0.9f) * verticalAmplitude;
            cameraTransform.localPosition = baseLocalPosition + new Vector3(dx, dy, 0f);
        }
    }
}
