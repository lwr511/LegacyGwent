using UnityEngine;

namespace LegacyGwent.LadyLakeLab
{
    /// <summary>
    /// 场景根节点。集中持有各子系统引用，把一次 LadyLakePose 分发给
    /// 人物骨架、前景摆动、剑、水效与摄像机视差。场景层级本身是完整的，
    /// 不依赖运行时创建对象。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LadyLakeLabRoot : MonoBehaviour
    {
        [Header("驱动")]
        public LadyLakeTimeline timeline;

        [Header("子系统")]
        public LadyLakeFigureRig figureRig;
        [Tooltip("握剑手局部上覆网格的骨架（与人物同一套蒙皮，渲染在剑之后，让手指挡住剑柄）。")]
        public LadyLakeFigureRig handOverlayRig;
        public LadyLakeFigureRig[] repairedHandRigs;
        public LadyLakeSwayField foregroundSway;
        public LadyLakeSwordRig swordRig;
        public LadyLakeWaterFx waterFx;
        public LadyLakeCameraRig cameraRig;

        [Header("层级（验收用）")]
        public Transform layerBackdrop;
        public Transform layerBackground;
        public Transform layerCaustics;
        public Transform layerFigure;
        public Transform layerSword;
        public Transform layerForeground;
        public Transform layerEffects;
        public Transform layerUi;

        [Header("摄像机")]
        public Camera targetCamera;

        private void Awake()
        {
            if (timeline == null)
            {
                timeline = GetComponentInChildren<LadyLakeTimeline>(true);
            }
        }

        /// <summary>按统一 SampleAt 路径采样（Editor 截图 / validation 用）。</summary>
        public void SampleAt(float seconds)
        {
            if (timeline != null)
            {
                timeline.SampleAt(seconds);
            }
        }

        internal void ApplyPose(LadyLakePose pose)
        {
            if (figureRig != null) figureRig.ApplyPose(pose);
            if (handOverlayRig != null) handOverlayRig.ApplyPose(pose);
            if (repairedHandRigs != null) foreach (var hand in repairedHandRigs) if (hand != null) hand.ApplyPose(pose);
            if (foregroundSway != null) foregroundSway.ApplyPose(pose);
            if (swordRig != null) swordRig.ApplyPose(pose);
            if (waterFx != null) waterFx.ApplyPose(pose);
            if (cameraRig != null) cameraRig.ApplyPose(pose);
        }
    }
}
