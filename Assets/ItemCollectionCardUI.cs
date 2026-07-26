using UnityEngine;
using UnityEngine.UI;

public class ItemCollectionCardUI : MonoBehaviour
{
    public Image icon;
    public Image lockOverlay;
    public Button selectButton;

    private ItemData itemData;
    private ItemCollectionManager collectionManager;
    private ItemDetailUI detailUI;

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
    }

    private void OnSelected()
    {
        if (detailUI != null)
            detailUI.Open(itemData, collectionManager.IsOwned(itemData));
    }
}
