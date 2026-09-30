using UnityEngine;

namespace LegacyGwent.LadyLakeLab
{
    /// <summary>
    /// 循环动作的驱动源。Play 模式下 Update 按 Time.deltaTime 前进；
    /// Editor 取样（截图 / validation）走同一个 SampleAt(seconds) 入口，
    /// 因此静态截图与真实 Play 的画面来自同一套姿态计算。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LadyLakeTimeline : MonoBehaviour
    {
        [Header("循环时序（秒）")]
        [Tooltip("一个完整动作循环的行动时长（不含 pause）。")]
        public float duration = LadyLakeLabConfig.DefaultDuration;

        [Tooltip("在整个峰值姿态上额外停留的秒数（握住 + 发光）。")]
        public float pause = LadyLakeLabConfig.DefaultPause;

        [Header("强度")]
        [Range(0f, 2f)]
        [Tooltip("剑光 / 光束 / 气泡 / 水流整体强度（不改变几何姿态）。")]
        public float intensity = LadyLakeLabConfig.DefaultIntensity;

        [Header("播放")]
        [Tooltip("Play 后自动循环。")]
        public bool autoPlay = true;

        [Range(0f, 1f)]
        [Tooltip("当前循环进度（只读为主，可手动拖动做单帧检查）。")]
        public float normalizedTime;

        [Header("引用")]
        public LadyLakeLabRoot root;

        private float _elapsed;

        public float TotalDuration
        {
            get { return Mathf.Max(0.5f, duration) + Mathf.Max(0f, pause); }
        }

        public LadyLakePose CurrentPose { get; private set; }

        private void OnEnable()
        {
            // 未 Play 时不推进动画：场景里已经烘焙好 t=0 的姿态，仍有完整的静止画面。
            if (Application.isPlaying)
            {
                SampleAt(_elapsed);
            }
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                SampleAt(_elapsed);
            }
        }

        private void Update()
        {
            if (!Application.isPlaying || !autoPlay)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            if (_elapsed > 1e6f)
            {
                _elapsed = LadyLakeMath.Wrap(_elapsed, TotalDuration);
            }

            SampleAt(_elapsed);
        }

        /// <summary>
        /// 统一采样入口：Editor 截图、validation、Play 全部走这里。
        /// seconds 是**不取模**的时钟：环境水流用连续时间（循环回卷不跳变），
        /// 动作姿态用 pose.normalizedTime（严格循环），发光脉冲用整数周期相位。
        /// </summary>
        public void SampleAt(float seconds)
        {
            LadyLakePose pose = LadyLakeMotion.Evaluate(seconds, duration, pause, intensity);
            CurrentPose = pose;
            normalizedTime = pose.normalizedTime;

            LadyLakeShaderGlobals.Set(seconds, pose.normalizedTime, Mathf.Clamp(intensity, 0f, 2f));

            if (root != null)
            {
                root.ApplyPose(pose);
            }
        }

        /// <summary>按归一化进度采样（便于单帧检查）。</summary>
        public void SampleNormalized(float normalized)
        {
            normalized = Mathf.Clamp01(normalized);
            normalizedTime = normalized;
            SampleAt(normalized * TotalDuration);
        }

        /// <summary>把播放时钟对齐到指定秒数，之后 Update 从该处继续。</summary>
        public void Seek(float seconds)
        {
            _elapsed = seconds;
            SampleAt(_elapsed);
        }
    }
}
