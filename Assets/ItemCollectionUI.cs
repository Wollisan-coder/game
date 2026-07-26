using System.Linq;
using UnityEngine;

public class ItemCollectionUI : MonoBehaviour
{
    public ItemCollectionManager collectionManager;
    public Transform gridContainer;
    public GameObject itemCardPrefab;
    public ItemDetailUI detailUI;

    private void Start()
    {
        PopulateGrid();
    }

    private void PopulateGrid()
    {
        foreach (Transform child in gridContainer)
            Destroy(child.gameObject);

        // Сортуємо за рідкістю (White -> Orange), в межах однієї рідкості — за назвою;
        // предмети з різною рідкістю мають різний itemId, тож завжди займають окремі клітинки
        var sortedItems = collectionManager.allItems.OrderBy(i => (int)i.rarity).ThenBy(i => i.itemName);

        foreach (var item in sortedItems)
        {
            GameObject cardObj = Instantiate(itemCardPrefab, gridContainer);
            var card = cardObj.GetComponent<ItemCollectionCardUI>();
            card.Setup(item, collectionManager, detailUI);
        }
    }
}
