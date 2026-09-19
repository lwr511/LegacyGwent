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
        private righclickLogic detailsOwner;
        private CardStatus current;
        private bool busy, opened;
        private bool catalogAvailable;
        private string error;
        private string pendingRequestId, pendingCardId, pendingAccountId;
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
            actions = Rect("PremiumCraftActions", owner.EditorUI.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(380, 100));
            status = Label(actions, "Ownership", new Vector2(0, 45), new Vector2(355, 32), 18);
            craft = Button(actions, "CraftPremium", Vector2.zero, new Vector2(380, 60), out craftLabel);
            Icon(craft.transform, "Powder", new Vector2(-143, 0), new Vector2(25, 32));
            craft.onClick.AddListener(CraftClicked);
            select = Button(actions, "SelectCardVersion", new Vector2(0, -41), new Vector2(350, 32), out selectLabel);
            selectLabel.fontSize = 17;
            select.onClick.AddListener(SelectClicked);
            RefreshLabels();
        }

        public void AttachDetails(RectTransform parent, righclickLogic owner)
        {
            detailsOwner=owner;
            actions.SetParent(parent,false);
            actions.anchorMin=actions.anchorMax=new Vector2(.5f,.5f);
            actions.anchoredPosition=new Vector2(407,-367);
            RefreshLabels();
        }
        public void DetachDetails(righclickLogic owner)
        {
            if(detailsOwner!=owner)return;
            detailsOwner=null;
            if(actions!=null && editor!=null) { actions.SetParent(editor.EditorUI.transform,false);actions.gameObject.SetActive(false); }
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
            var row = Rect("CardVersionFilters", sr.parent, sr.anchorMin, pos + new Vector2(195, 12), new Vector2(340, 76));
            row.anchorMax = sr.anchorMax;
            var labels = new[] { Text("普通", "Standard"), Text("闪卡", "Premium"), Text("全部", "All"), Text("已拥有", "Owned") };
            var icons = new[] { "db_filter_premium_standard", "db_filter_premium_premium", "db_filter_premium_all", "db_filter_owned_owned" };
            var result = new Button[4];
            for (int i = 0; i < result.Length; i++)
            {
                int filter = i;
                Text label;
                result[i] = Button(row, "Filter" + i, new Vector2((i - 1.5f) * 82, 0), new Vector2(50, 50), out label);
                StyleButton(result[i],"btn_square_idle","btn_square_hovered","btn_square_down");
                var selected=Rect("Selected",result[i].transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(50,50)).gameObject.AddComponent<Image>();
                selected.sprite=Resources.Load<Sprite>("PremiumCrafting/btn_square_toggle_frame");selected.raycastTarget=false;
                label.fontSize = 14; label.rectTransform.sizeDelta = new Vector2(80, 20); label.rectTransform.anchoredPosition = new Vector2(0, -34);
                label.text = labels[i];
                Icon((RectTransform)result[i].transform, icons[i], Vector2.zero, new Vector2(28, 32));
                result[i].onClick.AddListener(() => { if (busy) return; current = null; if(filter==3)editor.SetOwnedFilter(!editor.OnlyOwned);else editor.SetPremiumFilter(filter); RefreshLabels(); });
            }
            return result;
        }

        private void RefreshLabels()
        {
            if (wallet == null) return;
            wallet.text = Text("陨星粉尘  ", "Meteorite Powder  ") + (PremiumCollectionClient.Ready ? PremiumCollectionClient.Account.MeteoritePowder.ToString("N0") : "—");
            actions.gameObject.SetActive(detailsOwner != null && current?.IsPremium == true &&
                (editor.EditorStatus == EditorStatus.ShowCards || editor.EditorStatus == EditorStatus.EditorDeck));
            foreach (var group in new[] { showFilters, deckFilters })
                if (group != null) for (int i = 0; i < group.Length; i++)
                { group[i].transform.Find("Selected").gameObject.SetActive(i == 3 ? editor.OnlyOwned : i == editor.PremiumFilter); group[i].interactable = !busy; }
            if (current == null) return;
            int count = PremiumCollectionClient.Count(current.CardId, true);
            int limit = CardInventory.Limit(current.CardId);
            int cost; bool available = PremiumCollectionClient.Costs.TryGetValue(current.CardId, out cost);
            bool full = count >= limit;
            status.text = error ?? (PremiumCollectionClient.Ready
                ? Text("普通 ×", "Standard ×") + CardInventory.Limit(current.CardId) + Text("  ·  闪卡 ", "  ·  Premium ") + count + "/" + limit
                : Text("正在同步收藏…", "Synchronizing collection…"));
            craftLabel.text = busy ? Text("合成中…", "Crafting…") : full ? Text("闪卡已集齐", "All copies crafted") :
                pendingRequestId != null && pendingCardId == current.CardId ? Text("重试上次合成", "Retry last craft") :
                Text("合成 1 张闪卡   ", "Craft 1 premium   ") + (available ? cost.ToString() : "—");
            craft.interactable = !busy && !full && available && PremiumCollectionClient.Ready &&
                (PremiumCollectionClient.Account.MeteoritePowder >= cost || pendingRequestId != null);
            craft.gameObject.SetActive(current.IsPremium == true);
            if (!available && PremiumCollectionClient.Ready && error == null)
                status.text = Text("此卡暂无闪卡版本", "No premium version available");
            else if (!full && available && PremiumCollectionClient.Ready && PremiumCollectionClient.Account.MeteoritePowder < cost && error == null)
                status.text += Text(" · 还需 ", " · Need ") + (cost - PremiumCollectionClient.Account.MeteoritePowder) + Text(" 粉尘", " powder");
            select.gameObject.SetActive(false);
        }

        public async void CraftClicked()
        {
            if (busy || detailsOwner==null || current?.IsPremium != true || !craft.interactable) return;
            string card = current.CardId;
            string accountId = PremiumCollectionClient.Account.Id;
            if (pendingCardId != card || pendingAccountId != accountId)
            { pendingRequestId = null; pendingCardId = card; pendingAccountId = accountId; }
            if (pendingRequestId == null) pendingRequestId = Guid.NewGuid().ToString("N");
            busy = true; RefreshLabels();
            try
            {
                if (this == null || !isActiveAndEnabled || PremiumCollectionClient.Account?.Id != accountId) return;
                var result = await PremiumCollectionClient.Craft(card, pendingRequestId);
                pendingRequestId = null;
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
                    var detail=detailsOwner;
                    if(detail!=null && detail.DisplayID==card)targets.Add(detail.CardBorder.rectTransform);
                    await effect.Play(targets, () => {
                        if (this != null && PremiumCollectionClient.Account?.Id == accountId)
                        {
                            editor.RefreshPremiumCards(card);
                            if(detail!=null && detail.DisplayID==card)detail.RefreshCardVisual();
                        }
                    });
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

        private string ErrorText(string code) => code == "already_owned" ? Text("该闪卡数量已满，没有重复扣费", "All copies owned; no additional powder spent") :
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
            var image = r.gameObject.AddComponent<Image>();
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = image;
            StyleButton(b,"btn_wide_idle_300","btn_wide_hovered_300","btn_wide_down_300","btn_wide_inactive_300");
            label = Label(r, "Label", Vector2.zero, size - new Vector2(8, 4), 21);
            return b;
        }
        private static void StyleButton(Button button,string normal,string hover,string down,string disabled=null)
        {
            button.image.sprite=Resources.Load<Sprite>("PremiumCrafting/"+normal);
            button.image.color=Color.white;
            button.transition=Selectable.Transition.SpriteSwap;
            button.spriteState=new SpriteState { highlightedSprite=Resources.Load<Sprite>("PremiumCrafting/"+hover),
                pressedSprite=Resources.Load<Sprite>("PremiumCrafting/"+down),
                disabledSprite=disabled==null?button.image.sprite:Resources.Load<Sprite>("PremiumCrafting/"+disabled) };
        }
        private static void Icon(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var image = Rect(name, parent, new Vector2(.5f, .5f), pos, size).gameObject.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>("PremiumCrafting/" + name); image.preserveAspect = true; image.raycastTarget = false;
        }
    }
}
