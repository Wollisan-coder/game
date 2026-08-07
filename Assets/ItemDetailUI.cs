using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemDetailUI : MonoBehaviour
{
    [Header("UI элементы")]
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text slotTypeText;
    public TMP_Text descriptionText;
    public TMP_Text statsText;
    public TMP_Text ownedStatusText;

    [Header("Фон под описание — панель под текст, см. ConfirmationDialog.StyleAsDescriptionPanel")]
    public Image descriptionBg;

    public Button closeButton;

    private ItemData currentItem;
    private ItemOwnershipData currentStack; // null = предмет не получен (locked)
    private Image rarityFrame;
    private TMP_Text infoText;      // Rarity + Lvl + прогресс Exp (или количество — для расходных предметов)

    private Button actionButton;    // "Upgrade" (Equipment) или "Use" (HeroExperience) — в зависимости от категории
    private Image actionBg;
    private TMP_Text actionText;

    private ItemSacrificeUI sacrificeUI;
    private HeroExperienceUseUI heroExperienceUseUI;
    private HeroVoucherRedeemUI heroVoucherRedeemUI;

    // Вызывается при закрытии этого попапа — чтобы экран-каталог позади мог перестроить сетку
    // (например, если апгрейд/использование предмета создало/изменило стек, пока попап был открыт)
    public System.Action OnClosed;

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (descriptionBg != null)
            ConfirmationDialog.StyleAsDescriptionPanel(descriptionBg);

        EnsureExtraUI();

        // Панель уже сохранена выключенной в сцене (m_IsActive: 0) — не гасим её здесь ещё раз:
        // если вызвать SetActive(false) из Awake(), а Awake() впервые запускается ИМЕННО во время
        // первого Open()->SetActive(true), этот вызов сразу отменяет только что выполненную активацию.
    }

    // stack — конкретный стек (уровень) предмета, принадлежащий игроку; null, если предмет ещё не получен
    public void Open(ItemData item, ItemOwnershipData stack)
    {
        if (item == null) return;

        currentItem = item;
        currentStack = stack;
        transform.SetAsLastSibling(); // иначе панель, из которой открыто (каталог и т.д.), может перекрыть эту сверху
        gameObject.SetActive(true);
        Refresh();
    }

    private void Refresh()
    {
        var item = currentItem;
        var ownership = currentStack;
        bool owned = ownership != null;
        bool isHeroXpItem = item.category == ItemCategory.HeroExperience;
        bool isHeroVoucherItem = item.category == ItemCategory.HeroVoucher;

        if (icon != null) icon.sprite = item.icon;
        if (nameText != null)
        {
            nameText.text = item.itemName;
            nameText.color = item.GetRarityColor();
        }
        if (slotTypeText != null) slotTypeText.text = isHeroXpItem ? "Consumable" : isHeroVoucherItem ? "Voucher" : item.slotType.ToString();
        if (descriptionText != null) descriptionText.text = item.description;

        if (statsText != null)
        {
            if (isHeroXpItem)
            {
                statsText.text = $"Hero XP: +{item.heroExperienceValue}";
            }
            else if (isHeroVoucherItem)
            {
                int voucherCount = ItemCollectionManager.Instance != null ? ItemCollectionManager.Instance.GetTotalQuantity(item.itemId) : 0;
                statsText.text = $"Owned: {voucherCount} / {HeroCollectionManager.HeroVouchersPerGem} needed to grant a hero an Ascension Gem";
            }
            else
            {
                // Один слот = один стат (EquipmentStatCurve) — получен: точное значение на текущем
                // уровне стека; не получен (каталог/превью): диапазон мин-редкость -> кап-редкость.
                string statLabel = item.slotType switch
                {
                    EquipmentSlotType.Armor => "HP",
                    EquipmentSlotType.Trinket => "Mana",
                    EquipmentSlotType.Weapon => "Damage",
                    EquipmentSlotType.Accessory => "Armor",
                    _ => "",
                };

                if (owned)
                {
                    float value = EquipmentStatCurve.GetValue(item.slotType, item.rarity, ownership.level);
                    statsText.text = $"{statLabel}: +{value:0.##} (Lvl {ownership.level})";
                }
                else
                {
                    float minValue = EquipmentStatCurve.GetValue(item.slotType, item.rarity, 1);
                    float maxValue = EquipmentStatCurve.GetValue(item.slotType, item.rarity, item.GetMaxLevel());
                    statsText.text = $"{statLabel}: +{minValue:0.##} → +{maxValue:0.##}";
                }
            }
        }

        if (ownedStatusText != null)
            ownedStatusText.text = owned ? "Owned" : "Not owned";

        ItemBadgeUtility.ApplyRarityFrame(icon, item.GetRarityColor(), ref rarityFrame);

        var manager = ItemCollectionManager.Instance;
        int maxLevel = item.GetMaxLevel();

        if (infoText != null)
        {
            infoText.color = item.GetRarityColor();
            infoText.text = $"{item.itemName}\n{slotTypeText?.text}\n{statsText?.text}";
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
            else if (isHeroVoucherItem)
            {
                bool canRedeem = owned && manager != null && manager.GetTotalQuantity(item.itemId) >= HeroCollectionManager.HeroVouchersPerGem;
                actionButton.gameObject.SetActive(canRedeem);
                if (actionText != null) actionText.text = "Grant Gem";
                actionButton.onClick.AddListener(OnRedeemClicked);
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

        if (actionBg != null) ConfirmationDialog.StyleAsButton(actionBg);
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
        infoRect.anchoredPosition = new Vector2(-248, -682);

        infoText = infoObj.AddComponent<TextMeshProUGUI>();
        infoText.enableAutoSizing = true; // имя+тип+стата разной длины — фиксированный размер либо мелкий, либо не влезает в рамку
        infoText.fontSizeMin = 14;
        infoText.fontSizeMax = 36;
        infoText.alignment = TextAlignmentOptions.TopRight;

        var actionObj = new GameObject("ActionButton", typeof(RectTransform));
        var actionRect = (RectTransform)actionObj.transform;
        actionRect.SetParent(panelRect, false);
        actionRect.anchorMin = new Vector2(0.5f, 0.5f);
        actionRect.anchorMax = new Vector2(0.5f, 0.5f);
        actionRect.pivot = new Vector2(0.5f, 0.5f);
        actionRect.sizeDelta = new Vector2(300, 90);
        actionRect.anchoredPosition = new Vector2(0, -300);

        actionBg = actionObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(actionBg);
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
        heroVoucherRedeemUI = gameObject.AddComponent<HeroVoucherRedeemUI>();
    }

    private void OnSacrificeClicked()
    {
        if (sacrificeUI == null || currentItem == null || currentStack == null) return;

        var manager = ItemCollectionManager.Instance;
        if (manager == null) return;

        string itemId = currentItem.itemId;
        sacrificeUI.Open(currentStack.instanceId, () =>
        {
            // instanceId цели мог измениться (разделение/слияние стеков) — берём актуальный из sacrificeUI
            var refreshedStack = manager.GetStackByInstanceId(sacrificeUI.CurrentTargetInstanceId);
            var refreshedData = manager.GetItemById(itemId);
            if (refreshedData != null && refreshedStack != null)
                Open(refreshedData, refreshedStack);
            else
                Close(); // целевой стек больше не существует (например, потрачено всё топливо и донор исчез)
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
                Close(); // последняя копия потрачена
        });
    }

    private void OnRedeemClicked()
    {
        if (heroVoucherRedeemUI == null || currentItem == null) return;

        Rarity rarity = currentItem.rarity;
        string itemId = currentItem.itemId;
        heroVoucherRedeemUI.Open(rarity, () =>
        {
            var refreshedData = ItemCollectionManager.Instance?.GetItemById(itemId);
            var refreshedStack = ItemCollectionManager.Instance?.GetStacks(itemId).FirstOrDefault();

            if (refreshedData != null && refreshedStack != null)
                Open(refreshedData, refreshedStack);
            else
                Close(); // последний ваучер потрачен
        });
    }

    public void Close()
    {
        gameObject.SetActive(false);
        OnClosed?.Invoke();
    }
}
