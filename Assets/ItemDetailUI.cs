using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemDetailUI : MonoBehaviour
{
    [Header("UI елементи")]
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text slotTypeText;
    public TMP_Text descriptionText;
    public TMP_Text statsText;
    public TMP_Text ownedStatusText;

    public Button closeButton;

    private ItemData currentItem;
    private Image rarityFrame;
    private TMP_Text infoText;      // Rarity + Lvl + прогрес Exp (або кількість — для витратних предметів)

    private Button actionButton;    // "Upgrade" (Equipment) або "Use" (HeroExperience) — залежно від категорії
    private Image actionBg;
    private TMP_Text actionText;

    private ItemSacrificeUI sacrificeUI;
    private HeroExperienceUseUI heroExperienceUseUI;

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        EnsureExtraUI();

        gameObject.SetActive(false);
    }

    public void Open(ItemData item, bool owned)
    {
        if (item == null) return;

        currentItem = item;
        gameObject.SetActive(true);
        Refresh(owned);
    }

    private void Refresh(bool owned)
    {
        var item = currentItem;
        bool isHeroXpItem = item.category == ItemCategory.HeroExperience;

        if (icon != null) icon.sprite = item.icon;
        if (nameText != null) nameText.text = item.itemName;
        if (slotTypeText != null) slotTypeText.text = isHeroXpItem ? "Consumable" : item.slotType.ToString();
        if (descriptionText != null) descriptionText.text = item.description;

        if (statsText != null)
        {
            statsText.text = isHeroXpItem
                ? $"Hero XP: +{item.heroExperienceValue}"
                : $"HP: +{item.bonusHealth}\n" +
                  $"Мана: +{item.bonusMana}\n" +
                  $"Множник урону: +{item.bonusDamageMultiplier:0.##}";
        }

        if (ownedStatusText != null)
            ownedStatusText.text = owned ? "Отримано" : "Не отримано";

        ItemBadgeUtility.ApplyRarityFrame(icon, item.GetRarityColor(), ref rarityFrame);

        var manager = ItemCollectionManager.Instance;
        var ownership = owned && manager != null ? manager.GetOwnership(item.itemId) : null;
        int maxLevel = item.GetMaxLevel();

        if (infoText != null)
        {
            infoText.color = item.GetRarityColor();

            if (isHeroXpItem)
            {
                int qty = ownership != null ? ownership.quantity : 0;
                infoText.text = owned ? $"Owned: x{qty}" : "";
            }
            else if (ownership == null)
            {
                infoText.text = "";
            }
            else
            {
                string expLine = ownership.level >= maxLevel
                    ? "MAX"
                    : $"{ownership.experience}/{manager.ExperienceToNextLevel(ownership.level)} Exp";

                infoText.text =
                    $"Lvl {ownership.level}/{maxLevel}\n" +
                    expLine;
            }
        }

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();

            if (isHeroXpItem)
            {
                bool canUse = owned && ownership != null && ownership.quantity > 0;
                actionButton.gameObject.SetActive(canUse);
                if (actionText != null) actionText.text = "Use";
                actionButton.onClick.AddListener(OnUseClicked);
            }
            else
            {
                bool canSacrifice = owned && ownership != null && ownership.level < maxLevel
                    && manager != null && manager.ownership.Any(o => o.itemId != item.itemId);

                actionButton.gameObject.SetActive(canSacrifice);
                if (actionText != null) actionText.text = "Upgrade";
                actionButton.onClick.AddListener(OnSacrificeClicked);
            }
        }

        if (actionBg != null) actionBg.color = ConfirmationDialog.ButtonColor;
        if (actionText != null) actionText.color = ConfirmationDialog.ButtonTextColor;
    }

    private void EnsureExtraUI()
    {
        if (infoText != null) return;

        var panelRect = (RectTransform)transform;

        var infoObj = new GameObject("InfoText", typeof(RectTransform));
        var infoRect = (RectTransform)infoObj.transform;
        infoRect.SetParent(panelRect, false);
        infoRect.anchorMin = new Vector2(1, 1);
        infoRect.anchorMax = new Vector2(1, 1);
        infoRect.pivot = new Vector2(1, 1);
        infoRect.sizeDelta = new Vector2(360, 120);
        infoRect.anchoredPosition = new Vector2(-622, -284);

        infoText = infoObj.AddComponent<TextMeshProUGUI>();
        infoText.fontSize = 36;
        infoText.alignment = TextAlignmentOptions.TopRight;

        var actionObj = new GameObject("ActionButton", typeof(RectTransform));
        var actionRect = (RectTransform)actionObj.transform;
        actionRect.SetParent(panelRect, false);
        actionRect.anchorMin = new Vector2(0, 0);
        actionRect.anchorMax = new Vector2(0, 0);
        actionRect.pivot = new Vector2(0, 0);
        actionRect.sizeDelta = new Vector2(240, 80);
        actionRect.anchoredPosition = new Vector2(-745, -382);

        actionBg = actionObj.AddComponent<Image>();
        actionBg.color = ConfirmationDialog.ButtonColor;
        actionButton = actionObj.AddComponent<Button>();

        var actionTextObj = new GameObject("Text", typeof(RectTransform));
        var actionTextRect = (RectTransform)actionTextObj.transform;
        actionTextRect.SetParent(actionRect, false);
        actionTextRect.anchorMin = Vector2.zero;
        actionTextRect.anchorMax = Vector2.one;
        actionTextRect.offsetMin = Vector2.zero;
        actionTextRect.offsetMax = Vector2.zero;
        actionText = actionTextObj.AddComponent<TextMeshProUGUI>();
        actionText.text = "Upgrade";
        actionText.alignment = TextAlignmentOptions.Center;
        actionText.color = ConfirmationDialog.ButtonTextColor;

        actionButton.gameObject.SetActive(false);

        sacrificeUI = gameObject.AddComponent<ItemSacrificeUI>();
        heroExperienceUseUI = gameObject.AddComponent<HeroExperienceUseUI>();
    }

    private void OnSacrificeClicked()
    {
        if (sacrificeUI == null || currentItem == null) return;

        string itemId = currentItem.itemId;
        sacrificeUI.Open(itemId, () =>
        {
            var refreshedData = ItemCollectionManager.Instance != null ? ItemCollectionManager.Instance.GetItemById(itemId) : null;
            if (refreshedData != null) Open(refreshedData, true);
        });
    }

    private void OnUseClicked()
    {
        if (heroExperienceUseUI == null || currentItem == null) return;

        string itemId = currentItem.itemId;
        heroExperienceUseUI.Open(itemId, () =>
        {
            var manager = ItemCollectionManager.Instance;
            var refreshedData = manager != null ? manager.GetItemById(itemId) : null;
            if (refreshedData == null) return;

            bool stillOwned = manager.IsOwned(refreshedData);
            if (stillOwned)
                Open(refreshedData, true);
            else
                Close(); // останню копію витрачено
        });
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}
