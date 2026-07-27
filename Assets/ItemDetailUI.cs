using System.Collections.Generic;
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
    private ItemOwnershipData currentStack; // null = предмет не отриманий (locked)
    private Image rarityFrame;
    private TMP_Text infoText;      // Rarity + Lvl + прогрес Exp (або кількість — для витратних предметів)

    private Button actionButton;    // "Upgrade" (Equipment) або "Use" (HeroExperience) — залежно від категорії
    private Image actionBg;
    private TMP_Text actionText;

    private ItemSacrificeUI sacrificeUI;
    private HeroExperienceUseUI heroExperienceUseUI;

    // Викликається при закритті цього попапу — щоб екран-каталог позаду міг перебудувати сітку
    // (наприклад, якщо апгрейд/використання предмета створило/змінило стек, поки попап був відкритий)
    public System.Action OnClosed;

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        EnsureExtraUI();

        // Панель вже збережена вимкненою в сцені (m_IsActive: 0) — не гасимо її тут ще раз:
        // якщо викликати SetActive(false) з Awake(), а Awake() вперше запускається САМЕ під час
        // першого Open()->SetActive(true), цей виклик одразу скасовує щойно виконану активацію.
    }

    // stack — конкретний стек (рівень) предмета, який належить гравцю; null, якщо предмет ще не отриманий
    public void Open(ItemData item, ItemOwnershipData stack)
    {
        if (item == null) return;

        currentItem = item;
        currentStack = stack;
        transform.SetAsLastSibling(); // інакше панель, з якої відкрито (каталог тощо), може перекрити цю зверху
        gameObject.SetActive(true);
        Refresh();
    }

    private void Refresh()
    {
        var item = currentItem;
        var ownership = currentStack;
        bool owned = ownership != null;
        bool isHeroXpItem = item.category == ItemCategory.HeroExperience;

        if (icon != null) icon.sprite = item.icon;
        if (nameText != null)
        {
            nameText.text = item.itemName;
            nameText.color = item.GetRarityColor();
        }
        if (slotTypeText != null) slotTypeText.text = isHeroXpItem ? "Consumable" : item.slotType.ToString();
        if (descriptionText != null) descriptionText.text = item.description;

        if (statsText != null)
        {
            if (isHeroXpItem)
            {
                statsText.text = $"Hero XP: +{item.heroExperienceValue}";
            }
            else
            {
                // Показуємо лише ті характеристики, які предмет реально підвищує (не 0)
                var lines = new List<string>();
                if (item.bonusHealth != 0) lines.Add($"HP: +{item.bonusHealth}");
                if (item.bonusMana != 0) lines.Add($"Мана: +{item.bonusMana}");
                if (item.bonusDamageMultiplier != 0) lines.Add($"Damage: +{item.bonusDamageMultiplier:0.##}");

                statsText.text = string.Join("\n", lines);
            }
        }

        if (ownedStatusText != null)
            ownedStatusText.text = owned ? "Отримано" : "Не отримано";

        ItemBadgeUtility.ApplyRarityFrame(icon, item.GetRarityColor(), ref rarityFrame);

        var manager = ItemCollectionManager.Instance;
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
                    && manager != null && manager.ownership.Any(o => o.instanceId != ownership.instanceId);

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
        if (sacrificeUI == null || currentItem == null || currentStack == null) return;

        var manager = ItemCollectionManager.Instance;
        if (manager == null) return;

        string itemId = currentItem.itemId;
        sacrificeUI.Open(currentStack.instanceId, () =>
        {
            // instanceId цілі міг змінитись (поділ/злиття стеків) — беремо актуальний зі sacrificeUI
            var refreshedStack = manager.GetStackByInstanceId(sacrificeUI.CurrentTargetInstanceId);
            var refreshedData = manager.GetItemById(itemId);
            if (refreshedData != null && refreshedStack != null)
                Open(refreshedData, refreshedStack);
            else
                Close(); // цільовий стек більше не існує (наприклад, витрачено все паливо і донор зник)
        });
    }

    private void OnUseClicked()
    {
        if (heroExperienceUseUI == null || currentItem == null || currentStack == null) return;

        var manager = ItemCollectionManager.Instance;
        if (manager == null) return;

        string itemId = currentItem.itemId;
        string instanceId = currentStack.instanceId;
        heroExperienceUseUI.Open(instanceId, () =>
        {
            var refreshedData = manager.GetItemById(itemId);
            var refreshedStack = manager.GetStackByInstanceId(instanceId);

            if (refreshedData != null && refreshedStack != null)
                Open(refreshedData, refreshedStack);
            else
                Close(); // останню копію витрачено
        });
    }

    public void Close()
    {
        gameObject.SetActive(false);
        OnClosed?.Invoke();
    }
}
