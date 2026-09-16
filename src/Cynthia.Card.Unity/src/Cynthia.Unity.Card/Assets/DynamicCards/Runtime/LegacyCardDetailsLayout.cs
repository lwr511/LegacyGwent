using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Autofac;
using Assets.Script.Localization;
using Cynthia.Card;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.DynamicCards
{
    // Layout data comes from the 2018 UICardPreviewPrefab, at its original 1600 x 900 size.
    public sealed class LegacyCardDetailsLayout : MonoBehaviour
    {
        private righclickLogic owner;
        private RectTransform design, details, linkedViewport;
        private Text strengthValue;
        private GameObject creature;
        private Font fallback, heading, body;
        private bool chinese;
        private readonly Dictionary<string, Text> labels = new Dictionary<string, Text>();
        private readonly List<CardStatus> navigation = new List<CardStatus>();
        private Button previous, next;
        private CanvasGroup fade;
        private bool closing;
        private int navigationIndex = -1;
        private string navigationId;
        private bool? navigationPremium;
        private PremiumCollectionPanel collection;
        public bool IsPremium => PremiumFor(owner.DisplayID) ?? (owner.DisplayID==EditorInfo.RightClickedCardID
            ? EditorInfo.RightClickedPremium : PremiumCollectionClient.Selected(owner.DisplayID));
        public bool? PremiumFor(string id) => id == navigationId ? navigationPremium : null;

        public void Initialize(righclickLogic controller)
        {
            owner = controller;
            var translator = DependencyResolver.Container.Resolve<LocalizationService>();
            chinese = translator.TextLocalization.ChosenLanguage.Filename.StartsWith("cn");
            fallback = owner.NameDisplay.font;
            heading = chinese ? fallback : Resources.Load<Font>("FountInfo/HalisGR-Medium");
            body = chinese ? fallback : Resources.Load<Font>("FountInfo/PFDinTextCondPro-Regular");
            var canvas = owner.CardImg.canvas;
            fade = canvas.GetComponent<CanvasGroup>();
            if (fade == null) fade = canvas.gameObject.AddComponent<CanvasGroup>();
            fade.alpha = 0; fade.DOFade(1, .3f).SetUpdate(true);
            var card = DynamicCardView.FindCardRoot(owner.CardImg.transform, owner.CardBorder.transform);
            // Preserve the existing card, its bindings and this scene's controller; replace the page around it.
            foreach (var graphic in canvas.GetComponentsInChildren<Graphic>(true))
                if (!graphic.transform.IsChildOf(card)) graphic.enabled = false;
            foreach (var button in canvas.GetComponentsInChildren<Button>(true)) button.interactable = false;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            var backdrop = Image(canvas.transform, "LegacyBackground", "challenges_background");
            Stretch(backdrop.rectTransform); backdrop.raycastTarget = true;
            design = Rect("LegacyCardPreview", canvas.transform, Vector2.zero, new Vector2(1600, 900));
            details = Build(JObject.Parse(Resources.Load<TextAsset>("LegacyCardPreview/DetailsLayout").text), design, "");
            owner.NameDisplay = labels["CardDetails/CardName/Layout/Name"];
            owner.TagsDisplay = labels["CardDetails/CardName/Layout/Categories"];
            owner.FlavourDisplay = labels["CardDetails/Description/FlavorText"];
            owner.AbilityDisplay = labels["CardDetails/CardDetails/Ability/AbilityFlavorText"];
            strengthValue = labels["CardDetails/CardDetails/CreatureFolder/Strength/Value"];
            creature = strengthValue.transform.parent.parent.gameObject;
            labels["CardDetails/CardDetails/CreatureFolder/Strength/Label"].text = chinese ? "战力" : "STRENGTH";
            owner.layoutGroup = details;
            PlaceCard(card, owner.CardBorder.rectTransform, design, new Vector2(407, -16), 380);
            // Only the visible portrait receives a drag, never the padded 1024-square artwork.
            owner.CardImg.raycastTarget = false; owner.CardBorder.raycastTarget = true;
            BuildRelated();
            var close = TextButton(design, "Close", new Vector2(0, -400), new Vector2(240, 45), chinese ? "[Esc]  关闭" : "[Esc]  CLOSE", owner.Closerightclick);
            owner.ExitButtonText = close.GetComponentInChildren<Text>();
            var back = TextButton(design, "HistoryBack", new Vector2(-245, -400), new Vector2(200, 45), chinese ? "返回" : "BACK", owner.BackButton);
            owner.BackButtonText = back.GetComponentInChildren<Text>();
            owner.SoundButton = TextButton(design, "CardVoice", new Vector2(407, -355), new Vector2(200, 38), chinese ? "▷  语音" : "▷  VOICE", owner.PlaySound).gameObject;
            previous = Arrow("PreviousCard", new Vector2(-715, 2), true, () => Navigate(-1));
            next = Arrow("NextCard", new Vector2(640, 2), false, () => Navigate(1));
            var editor = Resources.FindObjectsOfTypeAll<EditorInfo>().FirstOrDefault(x => x.gameObject.scene.IsValid() && x.EditorUI.activeInHierarchy);
            if (editor != null)
            {
                collection=editor.PremiumPanel;
                collection.AttachDetails(design,owner);
                owner.SoundButton.GetComponent<RectTransform>().anchoredPosition=new Vector2(407,300);
                var context = editor.EditorStatus == EditorStatus.EditorDeck ? editor.EditorCardsContext : editor.ShowCardsContent;
                navigation.AddRange(context.GetComponentsInChildren<CardShowInfo>().Select(x => x.CurrentCore));
            }
        }

        public void Refresh()
        {
            creature.SetActive(owner.CardInfo.CardType != CardType.Special);
            strengthValue.text = owner.CardInfo.Strength.ToString();
            linkedViewport.gameObject.SetActive(owner.CardInfo.LinkedCards.Count != 0);
            owner.BackButtonText.transform.parent.gameObject.SetActive(owner.History.Count > 1);
            owner.ExitButtonText.text = chinese ? "[Esc]  关闭" : "[Esc]  CLOSE";
            owner.BackButtonText.text = chinese ? "返回" : "BACK";
            if (navigationIndex < 0 || navigation[navigationIndex].CardId != owner.DisplayID)
                navigationIndex = navigation.FindIndex(x => x.CardId == owner.DisplayID &&
                    (owner.DisplayID != EditorInfo.RightClickedCardID || x.IsPremium == EditorInfo.RightClickedPremium));
            previous.gameObject.SetActive(navigationIndex > 0); next.gameObject.SetActive(navigationIndex >= 0 && navigationIndex < navigation.Count - 1);
            LayoutRebuilder.ForceRebuildLayoutImmediate(details);
            if(collection!=null)collection.Preview(new CardStatus(owner.DisplayID){IsPremium=IsPremium});
        }

        private void Navigate(int step)
        {
            if(collection!=null && collection.Busy)return;
            int index = navigationIndex + step;
            if (index < 0 || index >= navigation.Count) return;
            navigationIndex = index; navigationId = navigation[index].CardId; navigationPremium = navigation[index].IsPremium;
            owner.UpdateCard(navigationId);
        }
        private void Update() { if (Input.GetKeyDown(KeyCode.Escape)) owner.Closerightclick(); }
        public void Close(UnityEngine.Events.UnityAction completed)
        {
            if (closing) return; closing = true;
            if(collection!=null)collection.DetachDetails(owner);
            fade.DOKill(); fade.interactable = false;
            fade.DOFade(0, .3f).SetUpdate(true).OnComplete(() => completed());
        }
        private void OnDestroy() { if(collection!=null)collection.DetachDetails(owner);if (fade != null) fade.DOKill(); }

        private void BuildRelated()
        {
            linkedViewport = Rect("ConnectedCardsPreview", details, Vector2.zero, new Vector2(640, 240));
            var sizing = linkedViewport.gameObject.AddComponent<LayoutElement>(); sizing.minHeight = 0; sizing.preferredHeight = 240; sizing.flexibleHeight = 1; sizing.flexibleWidth = 1;
            var viewport = Rect("Viewport", linkedViewport, Vector2.zero, Vector2.zero); Stretch(viewport);
            viewport.offsetMax = new Vector2(-14, 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            var input = viewport.gameObject.AddComponent<Image>(); input.color = Color.clear;
            var content = Rect("ConnectedCards", viewport, Vector2.zero, new Vector2(0, 0));
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 16; layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false; layout.padding = new RectOffset(15, 8, 18, 8);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = linkedViewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            var bar = Image(linkedViewport, "Scrollbar", "scrollbar_middle");
            bar.rectTransform.anchorMin = new Vector2(1, 0); bar.rectTransform.anchorMax = Vector2.one;
            bar.rectTransform.sizeDelta = new Vector2(5, 0); bar.rectTransform.anchoredPosition = new Vector2(-4, 0);
            var handle = Image(bar.transform, "Handle", "scroll_bar_sellector"); Stretch(handle.rectTransform);
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>(); scrollbar.handleRect = handle.rectTransform; scrollbar.targetGraphic = handle; scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            owner.ScrollContent = content; owner.Slider = bar.gameObject;
        }

        public void StyleLinked(LinkedCard linked)
        {
            var root = (RectTransform)linked.transform;
            var card = DynamicCardView.FindCardRoot(linked.CardImg.transform, linked.CardBorder.transform);
            foreach (var layout in root.GetComponentsInChildren<LayoutGroup>(true)) layout.enabled = false;
            foreach (var fit in root.GetComponentsInChildren<ContentSizeFitter>(true)) fit.enabled = false;
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true)) if (!graphic.transform.IsChildOf(card)) graphic.enabled = false;
            foreach (var button in root.GetComponentsInChildren<Button>(true)) button.interactable = false;
            root.localScale = Vector3.one;
            var size = root.GetComponent<LayoutElement>();
            if (size == null) size = root.gameObject.AddComponent<LayoutElement>();
            size.minHeight = 120; size.preferredHeight = 120; size.flexibleWidth = 1;
            PlaceCard(card, linked.CardBorder.rectTransform, root, new Vector2(-255, 0), 84);
            linked.NameDisplay = TextLabel(root, "Name", new Vector2(55, 44), new Vector2(470, 25), 18, heading);
            linked.TagsDisplay = TextLabel(root, "Categories", new Vector2(55, 17), new Vector2(470, 22), 18, body);
            linked.TagsDisplay.color = new Color(.90f, .89f, .88f);
            linked.AbilityDisplay = TextLabel(root, "Ability", new Vector2(55, -30), new Vector2(470, 66), 20, body);
            var click = root.gameObject.AddComponent<Image>(); click.color = Color.clear;
            var action = root.GetComponent<Button>();
            if (action == null) action = root.gameObject.AddComponent<Button>();
            action.interactable = true; action.targetGraphic = click; action.onClick.RemoveAllListeners(); action.onClick.AddListener(linked.UpdateOnButtonClick);
        }

        private RectTransform Build(JObject node, Transform parent, string path)
        {
            string name = (string)node["name"]; path = path.Length == 0 ? name : path + "/" + name;
            var r = Rect(name, parent, Vector2.zero, Vector2.zero); var data = node["rect"];
            r.anchorMin = Vector(data["m_AnchorMin"]); r.anchorMax = Vector(data["m_AnchorMax"]); r.pivot = Vector(data["m_Pivot"]);
            r.sizeDelta = Vector(data["m_SizeDelta"]); r.anchoredPosition = Vector(data["m_AnchoredPosition"]);
            var s = data["m_LocalScale"]; r.localScale = new Vector3((float)s["x"], (float)s["y"], (float)s["z"]);
            foreach (var component in node["components"])
            {
                string kind = (string)component["kind"]; var d = component["data"];
                if (kind == "Text")
                {
                    long fontId = (long)d["m_fontAsset"]["m_PathID"];
                    var text = r.gameObject.AddComponent<Text>(); text.font = (fontId == -6437917728163946042 ? Resources.Load<Font>("FountInfo/hinted-GWENT-ExtraBold") : fontId == 5804311606960194540 ? heading : body) ?? fallback;
                    text.fontSize = Mathf.RoundToInt((float)d["m_fontSize"]); text.alignment = TextAnchor.UpperLeft;
                    text.color = ColorValue(d["m_fontColor"]); text.raycastTarget = false; text.supportRichText = true;
                    text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
                    if (((int)d["m_fontStyle"] & 2) != 0) text.fontStyle = FontStyle.Italic;
                    else if (((int)d["m_fontStyle"] & 1) != 0) text.fontStyle = FontStyle.Bold;
                    if (fontId == -6437917728163946042 || name == "Name")
                    { text.resizeTextForBestFit = true; text.resizeTextMinSize = 16; text.resizeTextMaxSize = text.fontSize; text.verticalOverflow = VerticalWrapMode.Truncate; }
                    labels[path] = text;
                }
                else if (kind == "Image")
                { var image = r.gameObject.AddComponent<Image>(); image.sprite = Resources.Load<Sprite>("LegacyCardPreview/" + (string)d["sprite"]); image.color = ColorValue(d["color"]); image.raycastTarget = false; }
                else if (kind == "LayoutElement")
                {
                    var e = r.gameObject.AddComponent<LayoutElement>(); e.ignoreLayout = (int)d["m_IgnoreLayout"] != 0;
                    e.minWidth = (float)d["m_MinWidth"]; e.minHeight = (float)d["m_MinHeight"]; e.preferredWidth = (float)d["m_PreferredWidth"]; e.preferredHeight = (float)d["m_PreferredHeight"];
                    e.flexibleWidth = (float)d["m_FlexibleWidth"]; e.flexibleHeight = (float)d["m_FlexibleHeight"];
                }
                else if (kind == "VerticalLayoutGroup" || kind == "HorizontalLayoutGroup")
                {
                    HorizontalOrVerticalLayoutGroup g = kind == "VerticalLayoutGroup" ? (HorizontalOrVerticalLayoutGroup)r.gameObject.AddComponent<VerticalLayoutGroup>() : r.gameObject.AddComponent<HorizontalLayoutGroup>();
                    var p = d["m_Padding"]; g.padding = new RectOffset((int)p["m_Left"], (int)p["m_Right"], (int)p["m_Top"], (int)p["m_Bottom"]);
                    g.spacing = (float)d["m_Spacing"]; g.childAlignment = (TextAnchor)(int)d["m_ChildAlignment"];
                    g.childControlWidth = (int)d["m_ChildControlWidth"] != 0; g.childControlHeight = (int)d["m_ChildControlHeight"] != 0;
                    g.childForceExpandWidth = (int)d["m_ChildForceExpandWidth"] != 0; g.childForceExpandHeight = (int)d["m_ChildForceExpandHeight"] != 0;
                }
            }
            foreach (JObject child in node["children"]) Build(child, r, path);
            return r;
        }

        private Button Arrow(string name, Vector2 pos, bool left, UnityEngine.Events.UnityAction action)
        {
            var image = Image(design, name, "preview_arrow_inactive"); image.rectTransform.anchoredPosition = pos; image.rectTransform.sizeDelta = new Vector2(150, 150);
            image.preserveAspect = true; if (left) image.rectTransform.localScale = new Vector3(-1, 1, 1);
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action); return button;
        }
        private Button TextButton(Transform parent, string name, Vector2 pos, Vector2 size, string caption, UnityEngine.Events.UnityAction action)
        {
            var r = Rect(name, parent, pos, size); var image = r.gameObject.AddComponent<Image>(); image.color = Color.clear;
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = image; b.onClick.AddListener(action);
            var t = TextLabel(r, "Label", Vector2.zero, size, 22, heading); t.text = caption; t.alignment = TextAnchor.MiddleCenter; return b;
        }
        private Text TextLabel(Transform parent, string name, Vector2 pos, Vector2 size, int fontSize, Font font)
        {
            var t = Rect(name, parent, pos, size).gameObject.AddComponent<Text>(); t.font = font ?? fallback; t.fontSize = fontSize; t.color = Color.white;
            t.alignment = TextAnchor.UpperLeft; t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap; return t;
        }
        private static void PlaceCard(RectTransform card, RectTransform border, Transform parent, Vector2 center, float width)
        {
            card.SetParent(parent, false); card.anchorMin = card.anchorMax = new Vector2(.5f, .5f); card.localRotation = Quaternion.identity; card.localScale = Vector3.one;
            float span = card.InverseTransformVector(border.TransformVector(Vector3.right * border.rect.width)).magnitude;
            card.localScale = Vector3.one * (width / Mathf.Max(1, span));
            var actual = parent.InverseTransformPoint(border.TransformPoint(border.rect.center));
            card.localPosition += new Vector3(center.x, center.y, 0) - actual;
        }
        private static Vector2 Vector(JToken d) => new Vector2((float)d["x"], (float)d["y"]);
        private static Color ColorValue(JToken d) => new Color((float)d["r"], (float)d["g"], (float)d["b"], (float)d["a"]);
        private static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size) => PremiumCollectionPanel.Rect(name, parent, new Vector2(.5f, .5f), pos, size);
        private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        private static Image Image(Transform parent, string name, string sprite)
        { var image = Rect(name, parent, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>(); image.sprite = Resources.Load<Sprite>("LegacyCardPreview/" + sprite); return image; }
    }
}
