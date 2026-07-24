using UnityEngine;
using UnityEngine.UI;

public class EnemyCollectionCardUI : MonoBehaviour
{
    public Image portrait;
    public Image lockOverlay;
    public Button selectButton;

    private EnemyData enemyData;
    private EnemyCollectionManager collectionManager;

    public void Setup(EnemyData data, EnemyCollectionManager manager)
    {
        enemyData = data;
        collectionManager = manager;

        portrait.sprite = data.portrait;

        bool unlocked = manager.IsUnlocked(data);
        lockOverlay.gameObject.SetActive(!unlocked);
        selectButton.interactable = unlocked;

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(OnSelected);
    }

    private void OnSelected()
    {
        collectionManager.SelectEnemy(enemyData);
        Debug.Log($"Обрано ворога: {enemyData.enemyName}");
    }
}