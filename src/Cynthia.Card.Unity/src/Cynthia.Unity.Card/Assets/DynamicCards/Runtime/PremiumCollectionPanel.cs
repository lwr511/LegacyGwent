using System;
using System.Collections;
using System.Linq;
using Autofac;
using Assets.Script.Localization;
using Cynthia.Card;
using Cynthia.Card.Client;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.DynamicCards
{
    public sealed class PremiumCollectionPanel : MonoBehaviour
    {
        private EditorInfo editor;
        private Font font;
        private Text wallet, status, craftLabel, selectLabel;
        private Button craft, select;
        private RectTransform actions;
        private CardStatus current;
        private bool busy, opened;
        private bool catalogAvailable;
        private string error;
        private Button[] showFilters, deckFilters;
        private LocalizationService translator;
        public bool Busy => busy;
        public string CurrentCardId => current?.CardId;
        private bool Chinese => translator.TextLocalization.ChosenLanguage.Filename == "cn" ||
            translator.TextLocalization.ChosenLanguage.Filename.StartsWith("cn.");
        internal string Text(string cn, string en) => Chinese ? cn : en;

        public void Initialize(EditorInfo owner)
        {
            editor = owner;
            translator = DependencyResolver.Container.Resolve<LocalizationService>();
            font = owner.ShowSearch.textComponent.font;
            catalogAvailable = PremiumCollectionClient.Ready;
            var currency = Rect("MeteoritePowder", owner.EditorUI.transform, new Vector2(1, 1), new Vector2(-225, -66), new Vector2(370, 65));
            Icon(currency, "Powder", new Vector2(-128, 0), new Vector2(46, 52));
            wallet = Label(currency, "PowderBalance", new Vector2(35, 0), new Vector2(280, 60), 24);
            showFilters = Filters(owner.ShowSearch);
            deckFilters = Filters(owner.EditorSearch);
            actions = Rect("PremiumCraftActions", owner.EditorUI.transform, new Vector2(1, 0), new Vector2(-240, 180), new Vector2(355, 130));
            status = Label(actions, "Ownership", new Vector2(0, 45), new Vector2(355, 32), 18);
            craft = Button(actions, "CraftPremium", Vector2.zero, new Vector2(350, 44), out craftLabel);
            Icon(craft.transform, "Powder", new Vector2(-143, 0), new Vector2(25, 32));
            craft.onClick.AddListener(CraftClicked);
            select = Button(actions, "SelectCardVersion", new Vector2(0, -41), new Vector2(350, 32), out selectLabel);
            selectLabel.fontSize = 17;
            select.onClick.AddListener(SelectClicked);
            RefreshLabels();
        }

        private void OnEnable() { PremiumCollectionClient.Changed += AccountChanged; }
        private void OnDisable() { PremiumCollectionClient.Changed -= AccountChanged; opened = false; }
        public async void Open()
        {
            opened = true; current = null; error = null;
            RefreshLabels();
            try { await PremiumCollectionClient.Refresh(); }
            catch (Exception e) { error = Text("粉尘同步失败，重新进入收藏重试", "Collection unavailable. Reopen to retry."); Debug.LogWarning(e.Message); }
            if (this != null) RefreshLabels();
        }

        public void Preview(CardStatus card)
        {
            if (busy) return;
            if (current?.CardId != card.CardId) error = null;
            current = card;
            RefreshLabels();
        }

        private void AccountChanged()
        {
            if (editor == null) return;
            RefreshLabels();
            if (!opened || busy) return;
            if (!catalogAvailable && PremiumCollectionClient.Ready)
            {
                catalogAvailable = true;
                if (editor.EditorStatus == EditorStatus.ShowCards) editor.AutoSetShowCards();
                else if (editor.EditorStatus == EditorStatus.EditorDeck) editor.AutoSetEditorCards();
            }
            else editor.RefreshPremiumCards();
        }

        private Button[] Filters(InputField search)
        {
            var sr = (RectTransform)search.transform;
            var pos = sr.anchoredPosition;
            sr.sizeDelta = new Vector2(340, sr.sizeDelta.y);
            sr.anchoredPosition = pos + new Vector2(-170, 0);
            var row = Rect("CardVersionFilters", sr.parent, sr.anchorMin, pos + new Vector2(195, 0), new Vector2(340, 40));
            row.anchorMax = sr.anchorMax;
            var labels = new[] { Text("普通卡", "Standard"), Text("闪卡", "Premium"), Text("全部", "All") };
            var icons = new[] { "db_filter_premium_standard", "db_filter_premium_premium", "db_filter_premium_all" };
            var result = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                int filter = i;
                Text label;
                result[i] = Button(row, "Filter" + i, new Vector2((i - 1) * 114, 0), new Vector2(110, 38), out label);
                label.fontSize = 16; label.rectTransform.sizeDelta = new Vector2(76, 34); label.rectTransform.anchoredPosition = new Vector2(13, 0);
                label.text = labels[i];
                Icon((RectTransform)result[i].transform, icons[i], new Vector2(-38, 0), new Vector2(25, 28));
                result[i].onClick.AddListener(() => { if (busy) return; current = null; editor.SetPremiumFilter(filter); RefreshLabels(); });
            }
            return result;
        }

        private void RefreshLabels()
        {
            if (wallet == null) return;
            wallet.text = Text("陨星粉尘  ", "Meteorite Powder  ") + (PremiumCollectionClient.Ready ? PremiumCollectionClient.Account.MeteoritePowder.ToString("N0") : "—");
            actions.gameObject.SetActive(current?.IsPremium == true && (!PremiumCollectionClient.Owns(current.CardId) || error != null) &&
                (editor.EditorStatus == EditorStatus.ShowCards || editor.EditorStatus == EditorStatus.EditorDeck));
            foreach (var group in new[] { showFilters, deckFilters })
                if (group != null) for (int i = 0; i < group.Length; i++)
                { group[i].image.color = i == editor.PremiumFilter ? new Color(.48f, .36f, .16f) : new Color(.18f, .15f, .12f); group[i].interactable = !busy; }
            if (current == null) return;
            bool owns = PremiumCollectionClient.Owns(current.CardId);
            int cost; bool available = PremiumCollectionClient.Costs.TryGetValue(current.CardId, out cost);
            status.text = error ?? (owns ? Text("闪卡已合成 · 永久拥有", "Premium unlocked permanently") :
                available ? Text("闪卡尚未合成", "Premium not crafted") :
                PremiumCollectionClient.Ready ? Text("此卡暂无闪卡版本", "No premium version available") : Text("正在同步收藏…", "Synchronizing collection…"));
            craftLabel.text = busy ? Text("合成中…", "Transmuting…") : owns ? Text("已合成", "Crafted") :
                Text("合成闪卡   ", "Transmute   ") + (available ? cost.ToString() : "—");
            craft.interactable = !busy && !owns && available && PremiumCollectionClient.Ready && PremiumCollectionClient.Account.MeteoritePowder >= cost;
            craft.gameObject.SetActive(current.IsPremium == true && !owns);
            if (!owns && available && PremiumCollectionClient.Ready && PremiumCollectionClient.Account.MeteoritePowder < cost && error == null)
                status.text = Text("未合成 · 还需 ", "Not crafted · Need ") + (cost - PremiumCollectionClient.Account.MeteoritePowder) + Text(" 粉尘", " powder");
            bool selected = PremiumCollectionClient.Selected(current.CardId);
            selectLabel.text = selected ? Text("已选用闪卡 · 切换普通卡", "Premium equipped · Use standard") : Text("选用闪卡", "Use premium");
            select.gameObject.SetActive(false); // An unlock applies to all copies; no per-copy equip step.
            select.interactable = owns && !busy;
        }

        public async void CraftClicked()
        {
            if (busy || current?.IsPremium != true || !craft.interactable) return;
            string card = current.CardId;
            string accountId = PremiumCollectionClient.Account.Id;
            busy = true; RefreshLabels();
            try
            {
                if (this == null || !isActiveAndEnabled || PremiumCollectionClient.Account?.Id != accountId) return;
                var result = await PremiumCollectionClient.Craft(card);
                if (this == null || !isActiveAndEnabled || PremiumCollectionClient.Account?.Id != accountId) return;
                if (result.Success)
                {
                    var container = editor.EditorStatus == EditorStatus.EditorDeck ? editor.EditorCardsContext : editor.ShowCardsContent;
                    var targets = container.GetComponentsInChildren<CardShowInfo>()
                        .Where(x => x.CurrentCore.CardId == card && x.CurrentCore.IsPremium == true)
                        .Select(x => x.CardBorder.rectTransform).ToList();
                    var preview = editor.EditorStatus == EditorStatus.EditorDeck ? editor.EditorArtCard : editor.ShowArtCard;
                    if (preview.gameObject.activeInHierarchy && preview.CurrentCore?.CardId == card && preview.CurrentCore.IsPremium == true)
                        targets.Add(preview.CardBorder.rectTransform);
                    var effect = gameObject.AddComponent<PremiumCraftEffect>();
                    await effect.Play(targets, () => { if (this != null && PremiumCollectionClient.Account?.Id == accountId) editor.RefreshPremiumCards(card); });
                    if (this != null) { current = new CardStatus(card) { IsPremium = true }; error = null; }
                }
                else error = ErrorText(result.Status);
            }
            catch (Exception e)
            {
                error = Text("未确认合成结果，正在刷新余额", "Result uncertain; refreshing collection"); Debug.LogWarning(e.Message);
                try { await PremiumCollectionClient.Refresh(); } catch { }
            }
            finally
            {
                busy = false;
                if (this != null) { AccountChanged(); RefreshLabels(); }
            }
        }

        public async void SelectClicked()
        {
            if (busy || current == null || !PremiumCollectionClient.Owns(current.CardId)) return;
            string card = current.CardId;
            busy = true; RefreshLabels();
            try
            {
                var result = await PremiumCollectionClient.Select(card, !PremiumCollectionClient.Selected(card));
                if (this != null)
                {
                    error = result.Success ? null : ErrorText(result.Status);
                    current = new CardStatus(card) { IsPremium = PremiumCollectionClient.Selected(card) };
                    var preview = editor.EditorStatus == EditorStatus.EditorDeck ? editor.EditorArtCard : editor.ShowArtCard;
                    preview.CurrentCore = current;
                }
            }
            catch (Exception e) { error = Text("选用失败，请重试", "Could not equip. Please retry."); Debug.LogWarning(e.Message); }
            finally { busy = false; if (this != null) AccountChanged(); }
        }

        private string ErrorText(string code) => code == "already_owned" ? Text("该闪卡已合成，没有重复扣费", "Already crafted; no additional powder spent") :
            code == "insufficient_powder" ? Text("粉尘不足", "Insufficient powder") : Text("操作失败，请重新进入收藏重试", "Operation failed. Reopen collection to retry.");

        public static void MarkCard(CardShowInfo view, CardStatus card)
        {
            PremiumCardAppearance.Apply(view.CardImg, card, view.CardBorder, view.FactionIcon);
        }

        internal static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false); r.anchorMin = r.anchorMax = anchor; r.anchoredPosition = position; r.sizeDelta = size;
            return r;
        }
        private Text Label(Transform parent, string name, Vector2 pos, Vector2 size, int fontSize)
        {
            var t = Rect(name, parent, new Vector2(.5f, .5f), pos, size).gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = fontSize; t.alignment = TextAnchor.MiddleCenter; t.color = new Color(.94f, .87f, .66f); t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; return t;
        }
        private Button Button(Transform parent, string name, Vector2 pos, Vector2 size, out Text label)
        {
            var r = Rect(name, parent, new Vector2(.5f, .5f), pos, size);
            var image = r.gameObject.AddComponent<Image>(); image.color = new Color(.22f, .17f, .11f);
            var outline = r.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.62f, .48f, .26f); outline.effectDistance = new Vector2(1, -1);
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = image;
            label = Label(r, "Label", Vector2.zero, size - new Vector2(8, 4), 21);
            return b;
        }
        private static void Icon(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var image = Rect(name, parent, new Vector2(.5f, .5f), pos, size).gameObject.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>("PremiumCrafting/" + name); image.preserveAspect = true; image.raycastTarget = false;
        }
    }
}
