using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Экран подготовки перед входом в Death Dungeon (см. project_death_dungeon_concept /
// project_death_dungeon_implementation) — показывает текущий отряд и витрину "Ascension Gems",
// предупреждает о permadeath перед входом. Гемы вознесения — двойного назначения (см.
// HeroOwnershipData.ascensionGems): тратятся либо на вознесение героя, либо как одна из "монет" жертвы для
// ретрита (наравне с целиком принесёнными героями-картами) — см. HeroCollectionManager.ExecuteRetreatSacrifice /
// DeathDungeonRetreatSelectionUI. Тут гемы только отображаются, чтобы игрок видел цену ретрита ДО входа.
// Вход запускает StartNewRun() и открывает карту гаунтлета (DeathDungeonMapUI).
public class DeathDungeonEntryUI : MonoBehaviour
{
    private const float CardScale = 150f / 450f;

    private Transform canvasRoot;
    private MainMenuUI mainMenuUI;
    private GameObject heroCardPrefab;

    private GameObject overlayRoot;
    private Transform squadContainer;
    private Transform essenceContainer;
    private GameObject essenceEmptyLabel;

    public void Open(Transform canvasRoot, MainMenuUI mainMenuUI)
    {
        this.canvasRoot = canvasRoot;
        this.mainMenuUI = mainMenuUI;
        heroCardPrefab = mainMenuUI != null && mainMenuUI.squadUI != null ? mainMenuUI.squadUI.heroCardPrefab : null;

        BuildOverlayIfNeeded();
        overlayRoot.transform.SetAsLastSibling();
        overlayRoot.SetActive(true);
        Populate();
    }

    public void Close()
    {
        if (overlayRoot != null) overlayRoot.SetActive(false);
    }

    private void BuildOverlayIfNeeded()
    {
        if (overlayRoot != null) return;

        overlayRoot = new GameObject("DeathDungeonEntryOverlay", typeof(RectTransform));
        var overlayRect = (RectTransform)overlayRoot.transform;
        overlayRect.SetParent(canvasRoot, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        var dim = overlayRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.75f);

        var closeAreaObj = new GameObject("CloseArea", typeof(RectTransform));
        var closeAreaRect = (RectTransform)closeAreaObj.transform;
        closeAreaRect.SetParent(overlayRect, false);
        closeAreaRect.anchorMin = Vector2.zero;
        closeAreaRect.anchorMax = Vector2.one;
        closeAreaRect.offsetMin = Vector2.zero;
        closeAreaRect.offsetMax = Vector2.zero;
        var closeAreaImg = closeAreaObj.AddComponent<Image>();
        closeAreaImg.color = new Color(0, 0, 0, 0);
        var closeAreaBtn = closeAreaObj.AddComponent<Button>();
        closeAreaBtn.transition = Selectable.Transition.None;
        closeAreaBtn.onClick.AddListener(Close);

        var windowObj = new GameObject("Window", typeof(RectTransform));
        var windowRect = (RectTransform)windowObj.transform;
        windowRect.SetParent(overlayRect, false);
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(1000, 1300);
        var windowBg = windowObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsPanel(windowBg);

        var titleObj = new GameObject("Title", typeof(RectTransform));
        var titleRect = (RectTransform)titleObj.transform;
        titleRect.SetParent(windowRect, false);
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 70);
        titleRect.anchoredPosition = new Vector2(0, -30);
        var title = titleObj.AddComponent<TextMeshProUGUI>();
        title.text = "Death Dungeon";
        title.fontSize = 46;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;

        var squadLabelObj = new GameObject("SquadLabel", typeof(RectTransform));
        var squadLabelRect = (RectTransform)squadLabelObj.transform;
        squadLabelRect.SetParent(windowRect, false);
        squadLabelRect.anchorMin = new Vector2(0, 1);
        squadLabelRect.anchorMax = new Vector2(1, 1);
        squadLabelRect.pivot = new Vector2(0.5f, 1);
        squadLabelRect.sizeDelta = new Vector2(0, 40);
        squadLabelRect.anchoredPosition = new Vector2(0, -110);
        var squadLabel = squadLabelObj.AddComponent<TextMeshProUGUI>();
        squadLabel.text = "Your Squad";
        squadLabel.fontSize = 28;
        squadLabel.alignment = TextAlignmentOptions.Center;
        squadLabel.color = new Color(1, 1, 1, 0.85f);

        var squadRowObj = new GameObject("SquadRow", typeof(RectTransform));
        var squadRowRect = (RectTransform)squadRowObj.transform;
        squadRowRect.SetParent(windowRect, false);
        squadRowRect.anchorMin = new Vector2(0.5f, 1);
        squadRowRect.anchorMax = new Vector2(0.5f, 1);
        squadRowRect.pivot = new Vector2(0.5f, 1);
        squadRowRect.sizeDelta = new Vector2(4 * 150 + 3 * 14, 200);
        squadRowRect.anchoredPosition = new Vector2(0, -160);
        var squadGrid = squadRowObj.AddComponent<GridLayoutGroup>();
        squadGrid.cellSize = new Vector2(150, 200);
        squadGrid.spacing = new Vector2(14, 0);
        squadGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        squadGrid.constraintCount = 4;
        squadContainer = squadRowRect;

        var essencesLabelObj = new GameObject("EssencesLabel", typeof(RectTransform));
        var essencesLabelRect = (RectTransform)essencesLabelObj.transform;
        essencesLabelRect.SetParent(windowRect, false);
        essencesLabelRect.anchorMin = new Vector2(0, 1);
        essencesLabelRect.anchorMax = new Vector2(1, 1);
        essencesLabelRect.pivot = new Vector2(0.5f, 1);
        essencesLabelRect.sizeDelta = new Vector2(0, 40);
        essencesLabelRect.anchoredPosition = new Vector2(0, -390);
        var essencesLabel = essencesLabelObj.AddComponent<TextMeshProUGUI>();
        essencesLabel.text = "Ascension Gems";
        essencesLabel.fontSize = 28;
        essencesLabel.alignment = TextAlignmentOptions.Center;
        essencesLabel.color = new Color(1, 1, 1, 0.85f);

        var scrollObj = new GameObject("EssenceScroll", typeof(RectTransform));
        var scrollRect = (RectTransform)scrollObj.transform;
        scrollRect.SetParent(windowRect, false);
        scrollRect.anchorMin = new Vector2(0, 0);
        scrollRect.anchorMax = new Vector2(1, 1);
        scrollRect.offsetMin = new Vector2(30, 140);
        scrollRect.offsetMax = new Vector2(-30, -430);

        var scroll = scrollObj.AddComponent<ScrollRect>();
        var scrollImage = scrollObj.AddComponent<Image>();
        scrollImage.color = new Color(1, 1, 1, 0.03f);
        var scrollMask = scrollObj.AddComponent<Mask>();
        scrollMask.showMaskGraphic = true;

        var contentObj = new GameObject("Content", typeof(RectTransform));
        var contentRect = (RectTransform)contentObj.transform;
        contentRect.SetParent(scrollRect, false);
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0, 0);

        var essenceGrid = contentObj.AddComponent<GridLayoutGroup>();
        essenceGrid.cellSize = new Vector2(150, 200);
        essenceGrid.spacing = new Vector2(14, 14);
        essenceGrid.padding = new RectOffset(10, 10, 10, 10);

        var fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRect;
        scroll.viewport = scrollRect;
        scroll.horizontal = false;
        scroll.vertical = true;

        essenceContainer = contentRect;

        var essenceEmptyObj = new GameObject("EmptyLabel", typeof(RectTransform));
        var essenceEmptyRect = (RectTransform)essenceEmptyObj.transform;
        essenceEmptyRect.SetParent(windowRect, false);
        essenceEmptyRect.anchorMin = new Vector2(0, 0.3f);
        essenceEmptyRect.anchorMax = new Vector2(1, 0.55f);
        essenceEmptyRect.offsetMin = new Vector2(16, 0);
        essenceEmptyRect.offsetMax = new Vector2(-16, 0);
        var essenceEmptyText = essenceEmptyObj.AddComponent<TextMeshProUGUI>();
        essenceEmptyText.text = "No Ascension Gems banked.\nDuplicate hero pulls from summoning grant them.";
        essenceEmptyText.alignment = TextAlignmentOptions.Center;
        essenceEmptyText.color = new Color(1, 1, 1, 0.6f);
        essenceEmptyText.fontSize = 18;
        essenceEmptyObj.SetActive(false);
        essenceEmptyLabel = essenceEmptyObj;

        var enterObj = new GameObject("EnterButton", typeof(RectTransform));
        var enterRect = (RectTransform)enterObj.transform;
        enterRect.SetParent(windowRect, false);
        enterRect.anchorMin = new Vector2(0.3f, 0);
        enterRect.anchorMax = new Vector2(0.3f, 0);
        enterRect.pivot = new Vector2(0.5f, 0);
        enterRect.sizeDelta = new Vector2(280, 80);
        enterRect.anchoredPosition = new Vector2(0, 40);
        var enterImg = enterObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(enterImg);
        var enterBtn = enterObj.AddComponent<Button>();
        enterBtn.onClick.AddListener(OnEnterClicked);

        var enterTextObj = new GameObject("Text", typeof(RectTransform));
        var enterTextRect = (RectTransform)enterTextObj.transform;
        enterTextRect.SetParent(enterRect, false);
        enterTextRect.anchorMin = Vector2.zero;
        enterTextRect.anchorMax = Vector2.one;
        enterTextRect.offsetMin = Vector2.zero;
        enterTextRect.offsetMax = Vector2.zero;
        var enterText = enterTextObj.AddComponent<TextMeshProUGUI>();
        enterText.text = "Enter";
        enterText.fontSize = 30;
        enterText.fontStyle = FontStyles.Bold;
        enterText.alignment = TextAlignmentOptions.Center;
        enterText.color = ConfirmationDialog.ButtonTextColor;

        var exitObj = new GameObject("ExitButton", typeof(RectTransform));
        var exitRect = (RectTransform)exitObj.transform;
        exitRect.SetParent(windowRect, false);
        exitRect.anchorMin = new Vector2(0.7f, 0);
        exitRect.anchorMax = new Vector2(0.7f, 0);
        exitRect.pivot = new Vector2(0.5f, 0);
        exitRect.sizeDelta = new Vector2(280, 80);
        exitRect.anchoredPosition = new Vector2(0, 40);
        var exitImg = exitObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(exitImg);
        var exitBtn = exitObj.AddComponent<Button>();
        exitBtn.onClick.AddListener(Close);

        var exitTextObj = new GameObject("Text", typeof(RectTransform));
        var exitTextRect = (RectTransform)exitTextObj.transform;
        exitTextRect.SetParent(exitRect, false);
        exitTextRect.anchorMin = Vector2.zero;
        exitTextRect.anchorMax = Vector2.one;
        exitTextRect.offsetMin = Vector2.zero;
        exitTextRect.offsetMax = Vector2.zero;
        var exitText = exitTextObj.AddComponent<TextMeshProUGUI>();
        exitText.text = "Exit";
        exitText.fontSize = 30;
        exitText.fontStyle = FontStyles.Bold;
        exitText.alignment = TextAlignmentOptions.Center;
        exitText.color = ConfirmationDialog.ButtonTextColor;

        overlayRoot.SetActive(false);
    }

    private void Populate()
    {
        var collectionManager = HeroCollectionManager.Instance;
        if (collectionManager == null) return;

        foreach (Transform child in squadContainer)
            Destroy(child.gameObject);

        foreach (var hero in collectionManager.squad)
        {
            if (hero == null) continue;
            BuildCard(squadContainer, hero, collectionManager);
        }

        foreach (Transform child in essenceContainer)
            Destroy(child.gameObject);

        var gemHolders = collectionManager.ownership.Where(o => o.ascensionGems > 0).ToList();

        if (essenceEmptyLabel != null)
            essenceEmptyLabel.SetActive(gemHolders.Count == 0);

        foreach (var ownership in gemHolders)
        {
            HeroData hero = collectionManager.allHeroes.FirstOrDefault(h => h != null && h.heroId == ownership.heroId);
            if (hero == null) continue;
            BuildAscensionGemTile(essenceContainer, hero, ownership);
        }
    }

    // Плитка гема вознесения — портрет героя в рамке цвета его редкости ("кристалл с лицом героя") +
    // бедж количества, тот же визуальный язык, что и у обычных предметов (см. ItemBadgeUtility). Чисто
    // витрина, клика нет — даже для запертых героев с гемом (см. HeroCollectionManager.GrantGemToHero):
    // призыв живёт ТОЛЬКО в HeroCollectionUI (кнопка Summon на самой карточке героя) — этот экран про
    // валюту ретрита, а не про получение новых героев, два входа в одно действие только путали.
    private void BuildAscensionGemTile(Transform parent, HeroData hero, HeroOwnershipData ownership)
    {
        PickerTileUtility.BuildTile(parent, hero.heroId + "_AscensionGem", new Color(1, 1, 1, 0.06f),
            out Image bg, out Image icon, out TMP_Text label, out Button btn);

        icon.sprite = HeroAscensionUtility.GetDisplayPortrait(hero, ownership);

        bool locked = !ownership.isUnlocked;
        label.text = locked ? $"{hero.heroName}\n(locked)" : hero.heroName;
        label.fontSize = 14;
        label.color = hero.GetRarityColor();

        Image rarityFrame = null;
        ItemBadgeUtility.ApplyRarityFrame(icon, hero.GetRarityColor(), ref rarityFrame);

        TMP_Text quantityBadge = null;
        ItemBadgeUtility.ApplyQuantityBadge(icon.rectTransform, ownership.ascensionGems, ref quantityBadge);

        btn.interactable = false;
    }

    // Обёртка — реальный ребёнок GridLayoutGroup (сетка форсирует его RectTransform под cellSize),
    // сама карточка вложена ещё на уровень глубже с localScale — тот же приём, что и у HeroCollectionUI.
    private void BuildCard(Transform parent, HeroData hero, HeroCollectionManager collectionManager)
    {
        if (heroCardPrefab == null) return;

        var wrapperObj = new GameObject(hero.heroName + "_Slot", typeof(RectTransform));
        wrapperObj.transform.SetParent(parent, false);

        GameObject cardObj = Instantiate(heroCardPrefab, wrapperObj.transform);
        var cardRect = (RectTransform)cardObj.transform;
        cardRect.localScale = new Vector3(CardScale, CardScale, 1f);

        var card = cardObj.GetComponent<HeroMiniCardUI>();
        var ownership = collectionManager.ownership.Find(o => o.heroId == hero.heroId);
        card?.Setup(hero, ownership);
    }

    private void OnEnterClicked()
    {
        ConfirmationDialog.Show(
            canvasRoot,
            "If your squad is wiped, those heroes are permanently removed from your collection.\nOnce you enter, you cannot simply leave — the only way out is to retreat.\n\n"
            + "Gear grants no bonuses here — if a hero's chosen skill needs more mana than they can reach without a trinket, it will be automatically swapped for one they can actually cast.\n\n"
            + "Enter the Death Dungeon?",
            StartDungeon);
    }

    // Запускает новый забег и открывает карту гаунтлета вместо прямого входа в бой — энергия теперь
    // списывается за КАЖДЫЙ узел отдельно (см. DeathDungeonMapUI.OnNodeClicked), не один раз здесь.
    // DeathDungeonMapUI — сиблинг-компонент на том же GameObject (оба добавлены в CastleUI.EnsurePanel),
    // достаём его через GetComponent вместо отдельной ссылки.
    private void StartDungeon()
    {
        if (mainMenuUI == null || DeathDungeonManager.Instance == null) return;

        DeathDungeonManager.Instance.StartNewRun();
        Close();

        var mapUI = GetComponent<DeathDungeonMapUI>();
        mapUI?.Open(canvasRoot, mainMenuUI);
    }
}
