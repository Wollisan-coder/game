using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    public GameObject collectionPanel;
    public GameObject squadPanel;
    public GameObject enemyCollectionPanel; // новая панель врагов
    public GameObject itemCollectionPanel;  // каталог предметов
    public GameObject worldMapPanel;        // карта мира (узлы MapNodeUI)
    public GameObject[] cityMapPanels;      // все городские подкарты (CityMap_Elfs, CityMap_Fairies, ...)
    public SquadUI squadUI;

    [Header("Название боевой сцены")]
    public string battleSceneName = "SampleScene";

    [Header("Энергия на бой")]
    public int battleEnergyCost = 1;

    private CastleUI castleUI; // строится программно — см. EnsureCastleUI()

    private void Start()
    {
        // Если бой был запущен с ноды карты (currentNodeId выставляется в WorldMapManager.SelectNode),
        // значит мы вернулись именно с боя — открываем карту, а не сбрасываем на Collection по умолчанию.
        // currentNodeId сразу же гасим — иначе флаг остался бы "залипшим" навсегда после первого же боя с карты,
        // даже если следующий бой был запущен другим путём (например, старой кнопкой StartBattle()).
        bool returningFromMapBattle = WorldMapManager.Instance != null &&
                                       !string.IsNullOrEmpty(WorldMapManager.Instance.currentNodeId);

        string lastPanelName = WorldMapManager.Instance != null ? WorldMapManager.Instance.lastActiveMapPanelName : null;

        if (WorldMapManager.Instance != null)
        {
            WorldMapManager.Instance.currentNodeId = null;
            WorldMapManager.Instance.lastActiveMapPanelName = null;
        }

        // Тренировка запускается только из замка, поэтому по её завершении логично вернуться именно туда,
        // а не сбрасывать на Collection по умолчанию (см. BattleManager.EndBossTraining).
        bool returningFromBossTraining = AccountManager.Instance != null && AccountManager.Instance.returningFromBossTraining;
        if (AccountManager.Instance != null)
            AccountManager.Instance.returningFromBossTraining = false;

        if (returningFromBossTraining)
        {
            ShowCastle();
        }
        else if (returningFromMapBattle)
        {
            ShowWorldMap();
            OpenCityPanelByName(lastPanelName); // если бой был начат внутри города — переключаемся именно туда
        }
        else
        {
            ShowCollection();
        }
    }

    // Ищет среди cityMapPanels панель с таким именем и показывает её вместо общей мировой карты.
    // Если имя не найдено (пусто, либо это была сама мировая карта) — остаёмся на worldMapPanel как есть.
    private void OpenCityPanelByName(string panelName)
    {
        if (string.IsNullOrEmpty(panelName) || cityMapPanels == null) return;

        foreach (var cityPanel in cityMapPanels)
        {
            if (cityPanel == null || cityPanel.name != panelName) continue;

            if (worldMapPanel != null) worldMapPanel.SetActive(false);
            cityPanel.SetActive(true);
            return;
        }
    }

    // Прячет мировую карту И все городские подкарты разом — вызывается из каждого ShowXxx(), иначе
    // случайно оставленная активной городская панель (после возврата с боя внутри города) перехватывает
    // клики поверх любого другого экрана меню, даже если сама невидима под ним.
    private void HideAllMapPanels()
    {
        if (worldMapPanel != null) worldMapPanel.SetActive(false);

        if (cityMapPanels == null) return;
        foreach (var cityPanel in cityMapPanels)
            if (cityPanel != null) cityPanel.SetActive(false);
    }

    public void ShowCollection()
    {
        collectionPanel.SetActive(true);
        squadPanel.SetActive(false);
        if (enemyCollectionPanel != null) enemyCollectionPanel.SetActive(false);
        if (itemCollectionPanel != null) itemCollectionPanel.SetActive(false);
        HideAllMapPanels();
        castleUI?.Hide();
    }

    public void ShowSquad()
    {
        collectionPanel.SetActive(false);
        squadPanel.SetActive(true);
        if (enemyCollectionPanel != null) enemyCollectionPanel.SetActive(false);
        if (itemCollectionPanel != null) itemCollectionPanel.SetActive(false);
        HideAllMapPanels();
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
        HideAllMapPanels();
        castleUI?.Hide();
    }

    public void ShowItemCollection()
    {
        collectionPanel.SetActive(false);
        squadPanel.SetActive(false);
        if (enemyCollectionPanel != null) enemyCollectionPanel.SetActive(false);
        if (itemCollectionPanel != null) itemCollectionPanel.SetActive(true);
        HideAllMapPanels();
        castleUI?.Hide();
    }

    public void ShowCastle()
    {
        collectionPanel.SetActive(false);
        squadPanel.SetActive(false);
        if (enemyCollectionPanel != null) enemyCollectionPanel.SetActive(false);
        if (itemCollectionPanel != null) itemCollectionPanel.SetActive(false);
        HideAllMapPanels();

        EnsureCastleUI();
        castleUI.Open(this);
    }

    public void ShowWorldMap()
    {
        collectionPanel.SetActive(false);
        squadPanel.SetActive(false);
        if (enemyCollectionPanel != null) enemyCollectionPanel.SetActive(false);
        if (itemCollectionPanel != null) itemCollectionPanel.SetActive(false);
        HideAllMapPanels();
        if (worldMapPanel != null) worldMapPanel.SetActive(true);
        castleUI?.Hide();
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
