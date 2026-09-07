using System.Collections.Generic;
using Assets.Script.Localization;
using Autofac;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.DynamicCards
{
    public sealed class DynamicCardSettingRow : MonoBehaviour
    {
        private ChoseValue choice;
        private Text label;
        public static void Install(GameObject template)
        {
            if (template == null) return;
            // QualityPanel points to the value selector, while its parent owns the label and divider.
            var templateRow = template.transform.parent;
            if (templateRow.parent.Find("DynamicCardsOption") != null) return;
            var row = Instantiate(templateRow.gameObject, templateRow.parent);
            row.name = "DynamicCardsOption";
            var choice = row.GetComponentInChildren<ChoseValue>(true);
            // Replace the event object: RemoveAllListeners alone leaves serialized quality callbacks.
            choice.onValueChanged = new ChoseValue.ChoseValueEvent();
            var controller = row.AddComponent<DynamicCardSettingRow>();
            controller.choice = choice;
            foreach (var text in row.GetComponentsInChildren<Text>(true))
                if (!text.transform.IsChildOf(choice.transform) && text.text.Length > 1)
                { controller.label = text; break; }
            choice.onValueChanged.AddListener(index => DynamicCardSettings.Enabled = index == 1);
            var rect = (RectTransform)row.transform;
            if (row.transform.parent.GetComponent<LayoutGroup>() == null)
            {
                float bottom = 0;
                foreach (RectTransform sibling in row.transform.parent)
                    if (sibling != rect) bottom = Mathf.Min(bottom, sibling.anchoredPosition.y - sibling.rect.height * (1 - sibling.pivot.y));
                rect.anchoredPosition = new Vector2(((RectTransform)templateRow).anchoredPosition.x, bottom - rect.rect.height * rect.pivot.y - 8);
                var parent = row.transform.parent as RectTransform;
                if (parent != null) parent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(parent.rect.height, -bottom + rect.rect.height + 24));
            }
            controller.RefreshLabels();
        }
        private void OnEnable() { if (choice != null) RefreshLabels(); }
        private void RefreshLabels()
        {
            var locale = DependencyResolver.Container.Resolve<LocalizationService>().TextLocalization.ChosenLanguage.Filename.ToLowerInvariant();
            bool chinese = locale.Contains("cn") || locale.Contains("zh");
            choice.ChoseList = new List<string> { chinese ? "关闭" : "Off", chinese ? "开启" : "On" };
            if (label != null) label.text = chinese ? "动态卡牌" : "Animated cards";
            choice.Index = DynamicCardSettings.Enabled ? 1 : 0;
        }
    }
}
