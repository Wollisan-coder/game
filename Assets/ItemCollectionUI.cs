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

        foreach (var item in collectionManager.allItems)
        {
            GameObject cardObj = Instantiate(itemCardPrefab, gridContainer);
            var card = cardObj.GetComponent<ItemCollectionCardUI>();
            card.Setup(item, collectionManager, detailUI);
        }
    }
}
