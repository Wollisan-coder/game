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
    private MineThreatMapHotspots mineThreatHotspots; // строится программно — см. EnsureMineThreatHotspots()
    private HeroCollectionUI heroCollectionUI; // ищется сам в Start() — см. ниже, без ручной привязки в Inspector

    private void Start()
    {
        if (collectionPanel != null)
            heroCollectionUI = collectionPanel.GetComponentInChildren<HeroCollectionUI>(true);

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

        // Тот же приём — после узла гаунтлета Death Dungeon возвращаемся в Замок и сразу переоткрываем
        // карту данжа (если забег ещё активен), а не сбрасываем на Collection по умолчанию.
        bool returningFromDeathDungeon = AccountManager.Instance != null && AccountManager.Instance.returningFromDeathDungeon;
        if (AccountManager.Instance != null)
            AccountManager.Instance.returningFromDeathDungeon = false;

        // Тот же приём — после боя за шахты (см. MineThreatManager) возвращаемся именно в ту городскую
        // панель, откуда кликнули хотспот угрозы (lastPanelName выставляется хотспотом вручную — этот бой
        // НЕ идёт через WorldMapManager.SelectNode/currentNodeId, поэтому returningFromMapBattle его не ловит).
        bool returningFromMineDefense = AccountManager.Instance != null && AccountManager.Instance.returningFromMineDefense;
        if (AccountManager.Instance != null)
            AccountManager.Instance.returningFromMineDefense = false;

        if (returningFromDeathDungeon)
        {
            ShowCastle();
            castleUI.ReopenDeathDungeonMapIfActive();
        }
        else if (returningFromBossTraining)
        {
            ShowCastle();
        }
        else if (returningFromMineDefense)
        {
            ShowWorldMap();
            OpenCityPanelByName(lastPanelName);
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

    // Отмена "режима выбора" Коллекции (пустой слот отряда / Boss Training), если игрок ушёл с экрана
    // через нав-бар вместо того, чтобы выбрать героя — иначе флаг остаётся "залипшим" и угоняет следующий
    // обычный клик по карточке героя (ошибочный запуск боя или подстановка не в тот слот отряда).
    // Не вызывается из ShowCollection() — туда всегда переходят С УЖЕ выставленным флагом (клик по пустому
    // слоту/Boss Training сначала ставит флаг, потом сам вызывает ShowCollection), это легитимный кейс.
    private void CancelHeroPickingIfActive()
    {
        if (HeroCollectionManager.Instance == null) return;
        HeroCollectionManager.Instance.slotBeingEdited = -1;
        HeroCollectionManager.Instance.pickingForBossTraining = false;
    }

    public void ShowCollection()
    {
        collectionPanel.SetActive(true);
        squadPanel.SetActive(false);
        if (enemyCollectionPanel != null) enemyCollectionPanel.SetActive(false);
        if (itemCollectionPanel != null) itemCollectionPanel.SetActive(false);
        HideAllMapPanels();
        castleUI?.Hide();

        // Обновляем грид при каждом открытии (тот же паттерн, что и squadUI.RefreshSlots() ниже в
        // ShowSquad) — раньше грид строился только один раз из HeroCollectionUI.Start(), и уровни/
        // лок-статус/кнопка Summon "замерзали" после первого захода (найдено 2026-08-10).
        heroCollectionUI?.PopulateGrid();
    }

    public void ShowSquad()
    {
        CancelHeroPickingIfActive();
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
        CancelHeroPickingIfActive();
        collectionPanel.SetActive(false);
        squadPanel.SetActive(false);
        if (enemyCollectionPanel != null) enemyCollectionPanel.SetActive(true);
        if (itemCollectionPanel != null) itemCollectionPanel.SetActive(false);
        HideAllMapPanels();
        castleUI?.Hide();
    }

    public void ShowItemCollection()
    {
        CancelHeroPickingIfActive();
        collectionPanel.SetActive(false);
        squadPanel.SetActive(false);
        if (enemyCollectionPanel != null) enemyCollectionPanel.SetActive(false);
        if (itemCollectionPanel != null) itemCollectionPanel.SetActive(true);
        HideAllMapPanels();
        castleUI?.Hide();
    }

    public void ShowCastle()
    {
        CancelHeroPickingIfActive();
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
        CancelHeroPickingIfActive();
        collectionPanel.SetActive(false);
        squadPanel.SetActive(false);
        if (enemyCollectionPanel != null) enemyCollectionPanel.SetActive(false);
        if (itemCollectionPanel != null) itemCollectionPanel.SetActive(false);
        HideAllMapPanels();
        if (worldMapPanel != null) worldMapPanel.SetActive(true);
        castleUI?.Hide();

        EnsureMineThreatHotspots();
        mineThreatHotspots.RefreshAll(this);
    }

    private void EnsureCastleUI()
    {
        if (castleUI != null) return;
        castleUI = gameObject.AddComponent<CastleUI>();
    }

    private void EnsureMineThreatHotspots()
    {
        if (mineThreatHotspots != null) return;
        mineThreatHotspots = gameObject.AddComponent<MineThreatMapHotspots>();
    }

    public void StartBattle()
    {
        if (AccountManager.Instance != null && !AccountManager.Instance.SpendEnergy(battleEnergyCost))
        {
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
                ConfirmationDialog.ShowInfo(canvas.transform, "Not enough energy to start a battle.", iconPath: "UI/Currency/Energy");
            return;
        }

        SceneManager.LoadScene(battleSceneName);
    }
}
