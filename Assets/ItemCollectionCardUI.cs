using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemCollectionCardUI : MonoBehaviour
{
    public Image icon;
    public Image lockOverlay;
    public Button selectButton;

    private ItemData itemData;
    private ItemOwnershipData stack; // null = предмет ще не отриманий (locked)
    private ItemDetailUI detailUI;

    private TMP_Text levelText;
    private TMP_Text quantityText;

    // Фоновая Image карточки (та же, что selectButton.targetGraphic) — не публичное Inspector-поле,
    // читается напрямую с корневого GameObject, на котором сидит и этот компонент. Start(), не Awake() —
    // тот же порядок, что у HeroMiniCardUI (см. её комментарий): если когда-нибудь эта карточка тоже
    // начнёт масштабироваться вызывающим кодом после Instantiate(), Start() уже готов это подхватить.
    private void Start()
    {
        CardDepthUtility.ApplyCardDepth(GetComponent<Image>());
    }

    // stack — конкретный стек (уровень) этого предмета; null, если ни одной копии не получено (карточка заблокирована)
    public void Setup(ItemData data, ItemOwnershipData ownedStack, ItemDetailUI detail)
    {
        itemData = data;
        stack = ownedStack;
        detailUI = detail;

        if (icon != null) icon.sprite = data.icon;

        bool owned = stack != null;
        if (lockOverlay != null) lockOverlay.gameObject.SetActive(!owned);

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnSelected);
        }

        // Фунгибельный расходник (HeroExperience/ItemExperience) — "уровня" у него нет, бедж бы врал (см. UX-правку 2026-08-17).
        bool isFungibleExperienceItem = data.category == ItemCategory.HeroExperience || data.category == ItemCategory.ItemExperience;
        ItemBadgeUtility.ApplyLevelBadge(icon != null ? icon.rectTransform : null, isFungibleExperienceItem ? 0 : (owned ? stack.level : 0), ref levelText);
        ItemBadgeUtility.ApplyQuantityBadge(icon != null ? icon.rectTransform : null, owned ? stack.quantity : 0, ref quantityText);
    }

    private void OnSelected()
    {
        if (detailUI != null)
            detailUI.Open(itemData, stack);
    }
}
