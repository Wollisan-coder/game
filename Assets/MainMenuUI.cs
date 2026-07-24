using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    public GameObject collectionPanel;
    public GameObject squadPanel;
    public GameObject enemyCollectionPanel; // новая панель врагов
    public SquadUI squadUI;

    [Header("Назва бойової сцени")]
    public string battleSceneName = "SampleScene";

    private void Start()
    {
        ShowCollection();
    }

    public void ShowCollection()
    {
        collectionPanel.SetActive(true);
        squadPanel.SetActive(false);
        if (enemyCollectionPanel != null) enemyCollectionPanel.SetActive(false);
    }

    public void ShowSquad()
    {
        collectionPanel.SetActive(false);
        squadPanel.SetActive(true);
        if (enemyCollectionPanel != null) enemyCollectionPanel.SetActive(false);

        if (squadUI != null)
            squadUI.RefreshSlots(); // оновлюємо слоти при кожному відкритті
    }

    public void ShowEnemyCollection()
    {
        collectionPanel.SetActive(false);
        squadPanel.SetActive(false);
        if (enemyCollectionPanel != null) enemyCollectionPanel.SetActive(true);
    }

    public void StartBattle()
    {
        SceneManager.LoadScene(battleSceneName);
    }
}