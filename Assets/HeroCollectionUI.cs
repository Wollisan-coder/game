using UnityEngine;

public class HeroCollectionUI : MonoBehaviour
{
    public HeroCollectionManager collectionManager;
    public Transform gridContainer;
    public GameObject heroCardUIPrefab;
    public HeroInventoryUI inventoryUI; // общий попап на всю сцену, назначить в Inspector

    private void Start()
    {
        PopulateGrid();
    }

    private void PopulateGrid()
    {
        Debug.Log($"Количество героев в allHeroes: {collectionManager.allHeroes.Length}");

        foreach (Transform child in gridContainer)
            Destroy(child.gameObject);

        foreach (var hero in collectionManager.allHeroes)
        {
            Debug.Log($"[HeroCollectionUI] Перед Instantiate для {hero?.heroName}");
            GameObject cardObj = Instantiate(heroCardUIPrefab, gridContainer);
            Debug.Log($"[HeroCollectionUI] Instantiate готово, викликаю Setup для {hero?.heroName}");
            var card = cardObj.GetComponent<HeroCollectionCardUI>();
            card.Setup(hero, collectionManager, inventoryUI);
            Debug.Log($"[HeroCollectionUI] Setup готово для {hero?.heroName}");
        }
        Debug.Log("[HeroCollectionUI] PopulateGrid() полностью завершён");
    }
}