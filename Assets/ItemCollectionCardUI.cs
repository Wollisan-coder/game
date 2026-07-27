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

    private Image rarityFrame;
    private TMP_Text levelText;
    private TMP_Text quantityText;

    // stack — конкретний стек (рівень) цього предмета; null, якщо жодної копії не отримано (картка заблокована)
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

        ItemBadgeUtility.ApplyRarityFrame(icon, data.GetRarityColor(), ref rarityFrame);
        ItemBadgeUtility.ApplyLevelBadge(icon != null ? icon.rectTransform : null, owned ? stack.level : 0, ref levelText);
        ItemBadgeUtility.ApplyQuantityBadge(icon != null ? icon.rectTransform : null, owned ? stack.quantity : 0, ref quantityText);
    }

    private void OnSelected()
    {
        if (detailUI != null)
            detailUI.Open(itemData, stack);
    }
}
