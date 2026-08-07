using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Дивная броня (куск 6 Death Dungeon, полностью пересобрана — см. project_gem_economy_v2_redesign_pending)
// — отдельный экран (тот же принцип, что раньше был у Soul Essence): скин это не ItemData-предмет (иначе
// пришлось бы городить по ассету на каждого героя), а number-инстансы прямо на HeroOwnershipData
// (wondrousArmorWorn/wondrousArmorUnwornCount). Тут игрок надевает/распыляет неношеные копии + тратит
// ArmorShards на случайный доп.скин. НИКАКОЙ кнопки "Ставка в PvP" — самой PvP-системы в игре ещё нет,
// кнопку осознанно скрыли целиком, а не оставили заглушкой "coming soon".
public class WondrousArmorScreenUI : MonoBehaviour
{
    private const int RandomSkinCost = 3; // = HeroCollectionManager.TryRedeemArmorShardsForRandomSkin, дублируем только в тексте кнопки

    private Transform canvasRoot;
    private GameObject overlayRoot;
    private Transform tileContainer;
    private GameObject emptyLabel;
    private TMP_Text shardBalanceText;
    private TMP_Text squadBonusText;
    private Button randomSkinButton;

    public void Open(Transform canvasRoot)
    {
        this.canvasRoot = canvasRoot;
        BuildOverlayIfNeeded();
        overlayRoot.transform.SetAsLastSibling();
        overlayRoot.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        if (overlayRoot != null) overlayRoot.SetActive(false);
    }

    private void BuildOverlayIfNeeded()
    {
        if (overlayRoot != null) return;

        overlayRoot = new GameObject("WondrousArmorScreenOverlay", typeof(RectTransform));
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
        windowRect.sizeDelta = new Vector2(1000, 1100);
        var windowBg = windowObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsPanel(windowBg);

        var titleObj = new GameObject("Title", typeof(RectTransform));
        var titleRect = (RectTransform)titleObj.transform;
        titleRect.SetParent(windowRect, false);
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 60);
        titleRect.anchoredPosition = new Vector2(0, -30);
        var title = titleObj.AddComponent<TextMeshProUGUI>();
        title.text = "Wondrous Armor";
        title.fontSize = 40;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;

        var shardObj = new GameObject("ShardBalance", typeof(RectTransform));
        var shardRect = (RectTransform)shardObj.transform;
        shardRect.SetParent(windowRect, false);
        shardRect.anchorMin = new Vector2(0, 1);
        shardRect.anchorMax = new Vector2(1, 1);
        shardRect.pivot = new Vector2(0.5f, 1);
        shardRect.sizeDelta = new Vector2(0, 32);
        shardRect.anchoredPosition = new Vector2(0, -90);
        shardBalanceText = shardObj.AddComponent<TextMeshProUGUI>();
        shardBalanceText.fontSize = 22;
        shardBalanceText.alignment = TextAlignmentOptions.Center;
        shardBalanceText.color = new Color(1, 1, 1, 0.85f);

        var squadBonusObj = new GameObject("SquadBonus", typeof(RectTransform));
        var squadBonusRect = (RectTransform)squadBonusObj.transform;
        squadBonusRect.SetParent(windowRect, false);
        squadBonusRect.anchorMin = new Vector2(0, 1);
        squadBonusRect.anchorMax = new Vector2(1, 1);
        squadBonusRect.pivot = new Vector2(0.5f, 1);
        squadBonusRect.sizeDelta = new Vector2(0, 28);
        squadBonusRect.anchoredPosition = new Vector2(0, -122);
        squadBonusText = squadBonusObj.AddComponent<TextMeshProUGUI>();
        squadBonusText.fontSize = 18;
        squadBonusText.alignment = TextAlignmentOptions.Center;
        squadBonusText.color = new Color(0.6f, 1f, 0.6f, 0.9f);

        // "Random Skin" — 3 ArmorShards -> 1 доп. инстанс случайному разблокированному герою (любому, см.
        // HeroCollectionManager.TryRedeemArmorShardsForRandomSkin).
        var randomObj = new GameObject("RandomSkinButton", typeof(RectTransform));
        var randomRect = (RectTransform)randomObj.transform;
        randomRect.SetParent(windowRect, false);
        randomRect.anchorMin = new Vector2(0.5f, 1);
        randomRect.anchorMax = new Vector2(0.5f, 1);
        randomRect.pivot = new Vector2(0.5f, 1);
        randomRect.sizeDelta = new Vector2(320, 50);
        randomRect.anchoredPosition = new Vector2(0, -164);
        var randomImg = randomObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(randomImg);
        randomSkinButton = randomObj.AddComponent<Button>();
        randomSkinButton.onClick.AddListener(OnRandomSkinClicked);

        var randomTextObj = new GameObject("Text", typeof(RectTransform));
        var randomTextRect = (RectTransform)randomTextObj.transform;
        randomTextRect.SetParent(randomRect, false);
        randomTextRect.anchorMin = Vector2.zero;
        randomTextRect.anchorMax = Vector2.one;
        randomTextRect.offsetMin = Vector2.zero;
        randomTextRect.offsetMax = Vector2.zero;
        var randomText = randomTextObj.AddComponent<TextMeshProUGUI>();
        randomText.text = $"Random Skin ({RandomSkinCost} Shards)";
        randomText.fontSize = 20;
        randomText.alignment = TextAlignmentOptions.Center;
        randomText.color = ConfirmationDialog.ButtonTextColor;

        var scrollObj = new GameObject("Scroll", typeof(RectTransform));
        var scrollRect = (RectTransform)scrollObj.transform;
        scrollRect.SetParent(windowRect, false);
        scrollRect.anchorMin = new Vector2(0, 0);
        scrollRect.anchorMax = new Vector2(1, 1);
        scrollRect.offsetMin = new Vector2(30, 110);
        scrollRect.offsetMax = new Vector2(-30, -224);

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

        var grid = contentObj.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(170, 300);
        grid.spacing = new Vector2(14, 14);
        grid.padding = new RectOffset(10, 10, 10, 10);

        var fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRect;
        scroll.viewport = scrollRect;
        scroll.horizontal = false;
        scroll.vertical = true;

        tileContainer = contentRect;

        var emptyObj = new GameObject("EmptyLabel", typeof(RectTransform));
        var emptyRect = (RectTransform)emptyObj.transform;
        emptyRect.SetParent(windowRect, false);
        emptyRect.anchorMin = new Vector2(0, 0.3f);
        emptyRect.anchorMax = new Vector2(1, 0.55f);
        emptyRect.offsetMin = new Vector2(16, 0);
        emptyRect.offsetMax = new Vector2(-16, 0);
        var emptyText = emptyObj.AddComponent<TextMeshProUGUI>();
        emptyText.text = "No unworn Wondrous Armor yet.\nClear a Death Dungeon season to earn your first skin.";
        emptyText.alignment = TextAlignmentOptions.Center;
        emptyText.color = new Color(1, 1, 1, 0.6f);
        emptyText.fontSize = 18;
        emptyObj.SetActive(false);
        emptyLabel = emptyObj;

        var closeBtnObj = new GameObject("CloseButton", typeof(RectTransform));
        var closeBtnRect = (RectTransform)closeBtnObj.transform;
        closeBtnRect.SetParent(windowRect, false);
        closeBtnRect.anchorMin = new Vector2(0.5f, 0);
        closeBtnRect.anchorMax = new Vector2(0.5f, 0);
        closeBtnRect.pivot = new Vector2(0.5f, 0);
        closeBtnRect.sizeDelta = new Vector2(250, 60);
        closeBtnRect.anchoredPosition = new Vector2(0, 30);
        var closeBtnImg = closeBtnObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(closeBtnImg);
        var closeBtn = closeBtnObj.AddComponent<Button>();
        closeBtn.onClick.AddListener(Close);

        var closeTextObj = new GameObject("Text", typeof(RectTransform));
        var closeTextRect = (RectTransform)closeTextObj.transform;
        closeTextRect.SetParent(closeBtnRect, false);
        closeTextRect.anchorMin = Vector2.zero;
        closeTextRect.anchorMax = Vector2.one;
        closeTextRect.offsetMin = Vector2.zero;
        closeTextRect.offsetMax = Vector2.zero;
        var closeText = closeTextObj.AddComponent<TextMeshProUGUI>();
        closeText.text = "Close";
        closeText.fontSize = 26;
        closeText.alignment = TextAlignmentOptions.Center;
        closeText.color = ConfirmationDialog.ButtonTextColor;

        overlayRoot.SetActive(false);
    }

    private void Refresh()
    {
        var manager = HeroCollectionManager.Instance;
        if (manager == null) return;

        int shardBalance = PlayerCurrencies.Instance != null ? PlayerCurrencies.Instance.GetBalance(CurrencyType.ArmorShards) : 0;
        shardBalanceText.text = $"Armor Shards: {shardBalance}";
        if (randomSkinButton != null) randomSkinButton.interactable = shardBalance >= RandomSkinCost;

        bool squadFull = manager.squad.Count >= manager.MaxSquadSize && manager.squad.All(h => h != null);
        int wornCount = manager.squad.Count(h => h != null
            && manager.ownership.FirstOrDefault(o => o.heroId == h.heroId)?.wondrousArmorWorn == true);

        squadBonusText.text = manager.GetWondrousArmorSquadBonus() > 0f
            ? $"Squad Bonus Active: +{HeroCollectionManager.WondrousArmorSquadBonus * 100:0}% damage"
            : squadFull
                ? $"{wornCount}/4 squad heroes wearing Wondrous Armor"
                : "Fill your squad (4/4) with worn skins for a damage bonus";

        foreach (Transform child in tileContainer)
            Destroy(child.gameObject);

        var holders = manager.ownership.Where(o => o.wondrousArmorUnwornCount > 0).ToList();

        if (emptyLabel != null) emptyLabel.SetActive(holders.Count == 0);

        foreach (var ownership in holders)
        {
            HeroData hero = manager.allHeroes.FirstOrDefault(h => h != null && h.heroId == ownership.heroId);
            if (hero == null) continue;
            BuildTile(hero, ownership);
        }
    }

    // Плитка построена вручную (не через PickerTileUtility) — той плитке не хватает места под 2 доп.
    // кнопки (Wear/Disenchant) при её фиксированных пропорциях иконка/подпись.
    private void BuildTile(HeroData hero, HeroOwnershipData ownership)
    {
        var cardObj = new GameObject(hero.heroId + "_WondrousArmor", typeof(RectTransform));
        var cardRect = (RectTransform)cardObj.transform;
        cardRect.SetParent(tileContainer, false);
        var cardBg = cardObj.AddComponent<Image>();
        cardBg.color = new Color(1, 1, 1, 0.06f);

        var iconObj = new GameObject("Icon", typeof(RectTransform));
        var iconRect = (RectTransform)iconObj.transform;
        iconRect.SetParent(cardRect, false);
        iconRect.anchorMin = new Vector2(0, 0.52f);
        iconRect.anchorMax = new Vector2(1, 1);
        iconRect.offsetMin = new Vector2(6, 0);
        iconRect.offsetMax = new Vector2(-6, -6);
        var icon = iconObj.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.sprite = HeroAscensionUtility.GetDisplayPortrait(hero, ownership);

        Image rarityFrame = null;
        ItemBadgeUtility.ApplyRarityFrame(icon, hero.GetRarityColor(), ref rarityFrame);

        TMP_Text quantityBadge = null;
        ItemBadgeUtility.ApplyQuantityBadge(icon.rectTransform, ownership.wondrousArmorUnwornCount, ref quantityBadge);

        var labelObj = new GameObject("Label", typeof(RectTransform));
        var labelRect = (RectTransform)labelObj.transform;
        labelRect.SetParent(cardRect, false);
        labelRect.anchorMin = new Vector2(0, 0.40f);
        labelRect.anchorMax = new Vector2(1, 0.52f);
        labelRect.offsetMin = new Vector2(4, 0);
        labelRect.offsetMax = new Vector2(-4, 0);
        var label = labelObj.AddComponent<TextMeshProUGUI>();
        label.text = hero.heroName;
        label.fontSize = 14;
        label.alignment = TextAlignmentOptions.Center;
        label.color = hero.GetRarityColor();
        label.enableAutoSizing = true;
        label.fontSizeMin = 10;
        label.fontSizeMax = 14;

        bool alreadyWorn = ownership.wondrousArmorWorn;

        var wearObj = new GameObject("WearButton", typeof(RectTransform));
        var wearRect = (RectTransform)wearObj.transform;
        wearRect.SetParent(cardRect, false);
        wearRect.anchorMin = new Vector2(0, 0.20f);
        wearRect.anchorMax = new Vector2(1, 0.38f);
        wearRect.offsetMin = new Vector2(4, 0);
        wearRect.offsetMax = new Vector2(-4, 0);
        var wearImg = wearObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(wearImg);
        var wearBtn = wearObj.AddComponent<Button>();
        wearBtn.interactable = !alreadyWorn;
        wearBtn.onClick.AddListener(() => OnWearClicked(hero.heroId));

        var wearTextObj = new GameObject("Text", typeof(RectTransform));
        var wearTextRect = (RectTransform)wearTextObj.transform;
        wearTextRect.SetParent(wearRect, false);
        wearTextRect.anchorMin = Vector2.zero;
        wearTextRect.anchorMax = Vector2.one;
        wearTextRect.offsetMin = Vector2.zero;
        wearTextRect.offsetMax = Vector2.zero;
        var wearText = wearTextObj.AddComponent<TextMeshProUGUI>();
        wearText.text = alreadyWorn ? "Already Worn" : "Wear";
        wearText.fontSize = 13;
        wearText.alignment = TextAlignmentOptions.Center;
        wearText.color = ConfirmationDialog.ButtonTextColor;

        var disObj = new GameObject("DisenchantButton", typeof(RectTransform));
        var disRect = (RectTransform)disObj.transform;
        disRect.SetParent(cardRect, false);
        disRect.anchorMin = new Vector2(0, 0.02f);
        disRect.anchorMax = new Vector2(1, 0.19f);
        disRect.offsetMin = new Vector2(4, 0);
        disRect.offsetMax = new Vector2(-4, 0);
        var disImg = disObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(disImg);
        var disBtn = disObj.AddComponent<Button>();
        disBtn.onClick.AddListener(() => OnDisenchantClicked(hero.heroId));

        var disTextObj = new GameObject("Text", typeof(RectTransform));
        var disTextRect = (RectTransform)disTextObj.transform;
        disTextRect.SetParent(disRect, false);
        disTextRect.anchorMin = Vector2.zero;
        disTextRect.anchorMax = Vector2.one;
        disTextRect.offsetMin = Vector2.zero;
        disTextRect.offsetMax = Vector2.zero;
        var disText = disTextObj.AddComponent<TextMeshProUGUI>();
        disText.text = "Disenchant (+2)";
        disText.fontSize = 12;
        disText.alignment = TextAlignmentOptions.Center;
        disText.color = ConfirmationDialog.ButtonTextColor;
    }

    private void OnWearClicked(string heroId)
    {
        HeroCollectionManager.Instance?.WearWondrousArmor(heroId);
        Refresh();
    }

    private void OnDisenchantClicked(string heroId)
    {
        HeroCollectionManager.Instance?.DisenchantWondrousArmor(heroId);
        Refresh();
    }

    private void OnRandomSkinClicked()
    {
        var hero = HeroCollectionManager.Instance?.TryRedeemArmorShardsForRandomSkin();
        if (hero != null)
            ConfirmationDialog.ShowInfo(canvasRoot, $"{hero.heroName} received a new Wondrous Armor skin!");
        else
            ConfirmationDialog.ShowInfo(canvasRoot, "Not enough Armor Shards (needs 3).");
        Refresh();
    }
}
