using UnityEngine;
using UnityEngine.EventSystems;

namespace LegacyGwent.LadyLakeLab.Presentation
{
    /// <summary>
    /// 卡面指针转发：与生产代码 <c>Assets/DynamicCards/Runtime/DynamicCardDragHandle.cs</c>
    /// 一一对应（同样的 IPointerDown/Up + IBeginDrag/Drag/EndDrag 五件套，同样在 OnDisable 时回正）。
    ///
    /// 与生产版本的唯一差别：指针**必须命中卡面**才开始拖动（任务要求交互只从卡面命中开始）。
    /// 本组件挂在真实收藏卡框 CardBorder 上，而 CardBorder 是 ArtCard.prefab 里唯一
    /// <c>raycastTarget = true</c> 的整卡图元（ArtCard.cs:69-70），因此命中它就是命中卡面；
    /// 这里额外做一次显式校验，避免以后有人把 raycastTarget 挂到别的子物体上。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LadyLakeCardDragHandle : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public LadyLakeCardStage View;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsCardFace(eventData)) return;
            if (View != null) View.OnBeginDrag(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (View != null) View.OnEndDrag(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (View != null) View.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (View != null) View.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (View != null) View.OnEndDrag(eventData);
        }

        private void OnDisable()
        {
            if (View != null) View.ReturnToCenter();
        }

        /// <summary>
        /// 真实 PointerEventData 命中测试：命中卡框自身、其后代或其后代所命中的祖先才算卡面。
        ///
        /// 说明：本组件挂在 CardBorder 上，Unity 的常规指针管线只会把事件交给被命中的图元，
        /// 所以能走到这里本身就已经是卡面命中。两个 raycast 字段按可用性读取：
        /// Unity 自己填 <c>pointerCurrentRaycast</c>；脚本化验收（例如
        /// <c>LegacyGwent.LadyLakeLab.Acceptance.LadyLakePresentationProbe</c>）用
        /// <c>EventSystem.RaycastAll</c> + <c>ExecuteEvents</c> 直接驱动时填的是
        /// <c>pointerPressRaycast</c>。两者都为空时按命中处理，而不是静默吞掉拖动。
        /// </summary>
        public bool IsCardFace(PointerEventData eventData)
        {
            if (eventData == null) return false;
            var hit = eventData.pointerCurrentRaycast.gameObject;
            if (hit == null) hit = eventData.pointerPressRaycast.gameObject;
            if (hit == null) return true;
            return hit == gameObject
                || hit.transform.IsChildOf(transform)
                || transform.IsChildOf(hit.transform);
        }
    }
}
