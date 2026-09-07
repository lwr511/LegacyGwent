using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Script.DynamicCards
{
    public sealed class DynamicCardDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public DynamicCardView View;
        public void OnBeginDrag(PointerEventData eventData) { if (View != null) View.OnBeginDrag(eventData); }
        public void OnDrag(PointerEventData eventData) { if (View != null) View.OnDrag(eventData); }
        public void OnEndDrag(PointerEventData eventData) { if (View != null) View.OnEndDrag(eventData); }
    }
}
