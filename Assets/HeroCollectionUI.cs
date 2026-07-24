using UnityEngine;

public class HeroCollectionUI : MonoBehaviour
{
    public HeroCollectionManager collectionManager;
    public Transform gridContainer;
    public GameObject heroCardUIPrefab;

    private void Start()
    {
        PopulateGrid();
    }

            private void PopulateGrid()
        {
            Debug.Log($"Кількість героїв у allHeroes: {collectionManager.allHeroes.Length}");

            foreach (Transform child in gridContainer)
                Destroy(child.gameObject);

            foreach (var hero in collectionManager.allHeroes)
            {
                GameObject cardObj = Instantiate(heroCardUIPrefab, gridContainer);
                var card = cardObj.GetComponent<HeroCollectionCardUI>();
                card.Setup(hero, collectionManager);
            }
        }
}