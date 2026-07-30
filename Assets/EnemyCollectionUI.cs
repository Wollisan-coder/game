using UnityEngine;

public class EnemyCollectionUI : MonoBehaviour
{
    public Transform gridContainer;
    public GameObject enemyCardUIPrefab;

    private void Start()
    {
        PopulateGrid();
    }

    private void PopulateGrid()
    {
        var collectionManager = EnemyCollectionManager.Instance;
        if (collectionManager == null) return;

        foreach (Transform child in gridContainer)
            Destroy(child.gameObject);

        foreach (var enemy in collectionManager.allEnemies)
        {
            GameObject cardObj = Instantiate(enemyCardUIPrefab, gridContainer);
            var card = cardObj.GetComponent<EnemyCollectionCardUI>();
            card.Setup(enemy, collectionManager);
        }
    }
}