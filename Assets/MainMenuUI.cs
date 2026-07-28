using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    public GameObject collectionPanel;
    public GameObject squadPanel;
    public GameObject enemyCollectionPanel; // новая панель врагов
    public GameObject itemCollectionPanel;  // каталог предметов
    public SquadUI squadUI;

    [Header("Название боевой сцены")]
    public string battleSceneName = "SampleScene";

    [Header("Энергия на бой")]
    public int battleEnergyCost = 1;

    private CastleUI castleUI; // строится программно — см. EnsureCastleUI()

    private void Start()
    {
        ShowCollection();
    }

    public void ShowCollection()
    {
        collectionPanel.SetActive(true);
        squadPanel.SetActive(false);
        if (enemyCollectionPanel != null) enemyCollectionPanel.SetActive(false);
        if (itemCollectionPanel != null) itemCollectionPanel.SetActive(false);
        castleUI?.Hide();
    }

    public void ShowSquad()
    {
        collectionPanel.SetActive(false);
        squadPanel.SetActive(true);
        if (enemyCollectionPanel != null) enemyCollectionPanel.SetActive(false);
        if (itemCollectionPanel != null) itemCollectionPanel.SetActive(false);
        castleUI?.Hide();

        if (squadUI != null)
            squadUI.RefreshSlots(); // обновляем слоты при каждом открытии
    }

    public void ShowEnemyCollection()
    {
        collectionPanel.SetActive(false);
        squadPanel.SetActive(false);
        if (enemyCollectionPanel != null) enemyCollectionPanel.SetActive(true);
        if (itemCollectionPanel != null) itemCollectionPanel.SetActive(false);
        castleUI?.Hide();
    }

    public void ShowItemCollection()
    {
        collectionPanel.SetActive(false);
        squadPanel.SetActive(false);
        if (enemyCollectionPanel != null) enemyCollectionPanel.SetActive(false);
        if (itemCollectionPanel != null) itemCollectionPanel.SetActive(true);
        castleUI?.Hide();
    }

    public void ShowCastle()
    {
        collectionPanel.SetActive(false);
        squadPanel.SetActive(false);
        if (enemyCollectionPanel != null) enemyCollectionPanel.SetActive(false);
        if (itemCollectionPanel != null) itemCollectionPanel.SetActive(false);

        EnsureCastleUI();
        castleUI.Open(this);
    }

    private void EnsureCastleUI()
    {
        if (castleUI != null) return;
        castleUI = gameObject.AddComponent<CastleUI>();
    }

    public void StartBattle()
    {
        if (AccountManager.Instance != null && !AccountManager.Instance.SpendEnergy(battleEnergyCost))
        {
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
                ConfirmationDialog.ShowInfo(canvas.transform, "Not enough energy to start a battle.");
            return;
        }

        SceneManager.LoadScene(battleSceneName);
    }
}
