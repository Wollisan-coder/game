using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemCollectionCardUI : MonoBehaviour
{
    public Image icon;
    public Image lockOverlay;
    public Button selectButton;

    private ItemData itemData;
    private ItemCollectionManager collectionManager;
    private ItemDetailUI detailUI;

    private Image rarityFrame;
    private TMP_Text levelText;
    private TMP_Text quantityText;

    public void Setup(ItemData data, ItemCollectionManager manager, ItemDetailUI detail)
    {
        itemData = data;
        collectionManager = manager;
        detailUI = detail;

        if (icon != null) icon.sprite = data.icon;

        bool owned = manager.IsOwned(data);
        if (lockOverlay != null) lockOverlay.gameObject.SetActive(!owned);

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnSelected);
        }

        ItemBadgeUtility.ApplyRarityFrame(icon, data.GetRarityColor(), ref rarityFrame);
        ItemBadgeUtility.ApplyLevelBadge(icon != null ? icon.rectTransform : null, owned ? manager.GetLevel(data.itemId) : 0, ref levelText);
        ItemBadgeUtility.ApplyQuantityBadge(icon != null ? icon.rectTransform : null, owned ? manager.GetQuantity(data.itemId) : 0, ref quantityText);
    }

    private void OnSelected()
    {
        if (detailUI != null)
            detailUI.Open(itemData, collectionManager.IsOwned(itemData));
    }
}
