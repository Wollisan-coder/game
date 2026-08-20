using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Экран ВЫБОРА жертвы для ретрита из Death Dungeon (см. project_death_dungeon_concept,
// HeroCollectionManager.ExecuteRetreatSacrifice) — игрок сам набирает RetreatGemCost очков смесью:
// гемы вознесения (клик по плитке героя добавляет/циклит 1 гем ИМЕННО этого героя, до его банка) и
// целиком принесённые в жертву герои-"карты" (герои вне текущего отряда данжа, клик — вкл/выкл целиком).
// Открывается из DeathDungeonMapUI.OnRetreatClicked взамен прежнего тихого авто-списания.
public class DeathDungeonRetreatSelectionUI : MonoBehaviour
{
    private Transform canvasRoot;
    private System.Action onRetreated;

    private GameObject overlayRoot;
    private Transform gridContainer;
    private TMP_Text totalText;
    private Button confirmButton;

    private readonly Dictionary<string, int> gemSelection = new Dictionary<string, int>(); // heroId -> выбрано гемов
    private readonly HashSet<string> cardSelection = new HashSet<string>(); // heroId'ы, выбранные как жертва-карта целиком

    public void Open(Transform canvasRoot, System.Action onRetreated)
    {
        this.canvasRoot = canvasRoot;
        this.onRetreated = onRetreated;

        gemSelection.Clear();
        cardSelection.Clear();

        BuildOverlayIfNeeded();
        overlayRoot.transform.SetAsLastSibling();
        overlayRoot.SetActive(true);
        Populate();
    }

    public void Close()
    {
        if (overlayRoot != null) overlayRoot.SetActive(false);
    }

    private int TotalSelected => gemSelection.Values.Sum() + cardSelection.Count;

    private void BuildOverlayIfNeeded()
    {
        if (overlayRoot != null) return;

        overlayRoot = new GameObject("DeathDungeonRetreatSelectionOverlay", typeof(RectTransform));
        var overlayRect = (RectTransform)overlayRoot.transform;
        overlayRect.SetParent(canvasRoot, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        var dim = overlayRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.8f);

        var windowObj = new GameObject("Window", typeof(RectTransform));
        var windowRect = (RectTransform)windowObj.transform;
        windowRect.SetParent(overlayRect, false);
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(900, 900);
        var windowBg = windowObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsPanel(windowBg);

        // Тап мимо окна = как кнопка Cancel ниже — ничего не коммитится до "Confirm Retreat" (аудит попапов 2026-08-19).
        PopupCloseUtility.AddTapOutsideToClose(overlayRoot, windowRect, Close);

        var titleObj = new GameObject("Title", typeof(RectTransform));
        var titleRect = (RectTransform)titleObj.transform;
        titleRect.SetParent(windowRect, false);
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 50);
        titleRect.anchoredPosition = new Vector2(0, -20);
        var title = titleObj.AddComponent<TextMeshProUGUI>();
        title.text = "Choose Your Sacrifice";
        title.fontSize = 32;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;

        var totalObj = new GameObject("TotalText", typeof(RectTransform));
        var totalRect = (RectTransform)totalObj.transform;
        totalRect.SetParent(windowRect, false);
        totalRect.anchorMin = new Vector2(0, 1);
        totalRect.anchorMax = new Vector2(1, 1);
        totalRect.pivot = new Vector2(0.5f, 1);
        totalRect.sizeDelta = new Vector2(0, 36);
        totalRect.anchoredPosition = new Vector2(0, -70);
        totalText = totalObj.AddComponent<TextMeshProUGUI>();
        totalText.fontSize = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
        totalText.alignment = TextAlignmentOptions.Center;
        totalText.color = new Color(1, 1, 1, 0.85f);

        var scrollObj = new GameObject("Scroll View", typeof(RectTransform));
        var scrollRect = (RectTransform)scrollObj.transform;
        scrollRect.SetParent(windowRect, false);
        scrollRect.anchorMin = new Vector2(0, 0);
        scrollRect.anchorMax = new Vector2(1, 1);
        scrollRect.offsetMin = new Vector2(20, 100);
        scrollRect.offsetMax = new Vector2(-20, -110);

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
        grid.cellSize = new Vector2(190, 230); // увеличено под 28pt-пол в подписях плиток (было 150x200 под 13pt)
        grid.spacing = new Vector2(14, 14);
        grid.padding = new RectOffset(10, 10, 10, 10);

        var fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRect;
        scroll.viewport = scrollRect;
        scroll.horizontal = false;
        scroll.vertical = true;

        gridContainer = contentRect;

        var confirmObj = new GameObject("ConfirmButton", typeof(RectTransform));
        var confirmRect = (RectTransform)confirmObj.transform;
        confirmRect.SetParent(windowRect, false);
        confirmRect.anchorMin = new Vector2(0.3f, 0);
        confirmRect.anchorMax = new Vector2(0.3f, 0);
        confirmRect.pivot = new Vector2(0.5f, 0);
        confirmRect.sizeDelta = new Vector2(280, 70);
        confirmRect.anchoredPosition = new Vector2(0, 20);
        var confirmBg = confirmObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(confirmBg);
        confirmButton = confirmObj.AddComponent<Button>();
        confirmButton.onClick.AddListener(OnConfirmClicked);

        var confirmTextObj = new GameObject("Text", typeof(RectTransform));
        var confirmTextRect = (RectTransform)confirmTextObj.transform;
        confirmTextRect.SetParent(confirmRect, false);
        confirmTextRect.anchorMin = Vector2.zero;
        confirmTextRect.anchorMax = Vector2.one;
        confirmTextRect.offsetMin = Vector2.zero;
        confirmTextRect.offsetMax = Vector2.zero;
        var confirmText = confirmTextObj.AddComponent<TextMeshProUGUI>();
        confirmText.text = "Confirm Retreat";
        confirmText.fontSize = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
        confirmText.fontStyle = FontStyles.Bold;
        confirmText.alignment = TextAlignmentOptions.Center;
        confirmText.color = ConfirmationDialog.ButtonTextColor;

        var cancelObj = new GameObject("CancelButton", typeof(RectTransform));
        var cancelRect = (RectTransform)cancelObj.transform;
        cancelRect.SetParent(windowRect, false);
        cancelRect.anchorMin = new Vector2(0.7f, 0);
        cancelRect.anchorMax = new Vector2(0.7f, 0);
        cancelRect.pivot = new Vector2(0.5f, 0);
        cancelRect.sizeDelta = new Vector2(280, 70);
        cancelRect.anchoredPosition = new Vector2(0, 20);
        var cancelBg = cancelObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(cancelBg);
        var cancelBtn = cancelObj.AddComponent<Button>();
        cancelBtn.onClick.AddListener(Close);

        var cancelTextObj = new GameObject("Text", typeof(RectTransform));
        var cancelTextRect = (RectTransform)cancelTextObj.transform;
        cancelTextRect.SetParent(cancelRect, false);
        cancelTextRect.anchorMin = Vector2.zero;
        cancelTextRect.anchorMax = Vector2.one;
        cancelTextRect.offsetMin = Vector2.zero;
        cancelTextRect.offsetMax = Vector2.zero;
        var cancelText = cancelTextObj.AddComponent<TextMeshProUGUI>();
        cancelText.text = "Cancel";
        cancelText.fontSize = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
        cancelText.alignment = TextAlignmentOptions.Center;
        cancelText.color = ConfirmationDialog.ButtonTextColor;

        overlayRoot.SetActive(false);
    }

    private void Populate()
    {
        foreach (Transform child in gridContainer)
            Destroy(child.gameObject);

        var collectionManager = HeroCollectionManager.Instance;
        if (collectionManager == null) return;

        var squadIds = new HashSet<string>(collectionManager.squad.Where(h => h != null).Select(h => h.heroId));

        foreach (var ownership in collectionManager.ownership.Where(o => o.ascensionGems > 0))
        {
            HeroData hero = collectionManager.allHeroes.FirstOrDefault(h => h != null && h.heroId == ownership.heroId);
            if (hero == null) continue;
            BuildGemTile(hero, ownership);
        }

        foreach (var ownership in collectionManager.ownership.Where(o => o.isUnlocked && !squadIds.Contains(o.heroId)))
        {
            HeroData hero = collectionManager.allHeroes.FirstOrDefault(h => h != null && h.heroId == ownership.heroId);
            if (hero == null) continue;
            BuildCardTile(hero, ownership);
        }

        RefreshTotal();
    }

    private void BuildGemTile(HeroData hero, HeroOwnershipData ownership)
    {
        PickerTileUtility.BuildTile(gridContainer, hero.heroId + "_Gem", new Color(1, 1, 1, 0.06f),
            out Image bg, out Image icon, out TMP_Text label, out Button btn);

        icon.sprite = HeroAscensionUtility.GetDisplayPortrait(hero, ownership);
        Image rarityFrame = null;
        ItemBadgeUtility.ApplyRarityFrame(icon, hero.GetRarityColor(), ref rarityFrame);

        int selected = gemSelection.TryGetValue(hero.heroId, out int sel) ? sel : 0;
        label.text = $"{hero.heroName}\nGem {selected}/{ownership.ascensionGems}";
        label.enableAutoSizing = true;
        label.fontSizeMin = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
        label.fontSizeMax = 34;
        label.color = selected > 0 ? Color.yellow : hero.GetRarityColor();

        bg.color = selected > 0 ? new Color(1f, 0.85f, 0.3f, 0.35f) : new Color(1, 1, 1, 0.06f);

        int available = ownership.ascensionGems;
        string heroId = hero.heroId;
        btn.onClick.AddListener(() => OnGemTileClicked(heroId, available));
    }

    private void BuildCardTile(HeroData hero, HeroOwnershipData ownership)
    {
        PickerTileUtility.BuildTile(gridContainer, hero.heroId + "_Card", new Color(1, 1, 1, 0.06f),
            out Image bg, out Image icon, out TMP_Text label, out Button btn);

        icon.sprite = HeroAscensionUtility.GetDisplayPortrait(hero, ownership);
        Image rarityFrame = null;
        ItemBadgeUtility.ApplyRarityFrame(icon, hero.GetRarityColor(), ref rarityFrame);

        bool selected = cardSelection.Contains(hero.heroId);
        // Убрали инструктивную вторую строку "(tap to sacrifice)" у невыбранного состояния — не влезает даже
        // на жёстком полу 28pt при разумном размере плитки, а сам факт кликабельности плитки + заголовок
        // экрана "Choose Your Sacrifice" уже достаточно объясняют механику; SACRIFICED остаётся, это статус.
        label.text = selected ? $"{hero.heroName}\nSACRIFICED" : hero.heroName;
        label.enableAutoSizing = true;
        label.fontSizeMin = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
        label.fontSizeMax = 34;
        label.color = selected ? new Color(1f, 0.3f, 0.3f) : hero.GetRarityColor();

        bg.color = selected ? new Color(0.9f, 0.2f, 0.2f, 0.4f) : new Color(1, 1, 1, 0.06f);

        string heroId = hero.heroId;
        btn.onClick.AddListener(() => OnCardTileClicked(heroId));
    }

    // Клик циклит выбор для ЭТОГО героя: 0 -> 1 -> ... -> available -> 0. Увеличивать можно только пока
    // общий итог не упёрся в RetreatGemCost — но сброс в 0 (освобождение очков) разрешён всегда.
    private void OnGemTileClicked(string heroId, int available)
    {
        int selected = gemSelection.TryGetValue(heroId, out int sel) ? sel : 0;

        if (selected >= available)
            gemSelection[heroId] = 0;
        else if (TotalSelected < HeroCollectionManager.RetreatGemCost)
            gemSelection[heroId] = selected + 1;

        Populate();
    }

    private void OnCardTileClicked(string heroId)
    {
        if (cardSelection.Contains(heroId))
            cardSelection.Remove(heroId);
        else if (TotalSelected < HeroCollectionManager.RetreatGemCost)
            cardSelection.Add(heroId);

        Populate();
    }

    private void RefreshTotal()
    {
        int total = TotalSelected;
        int need = HeroCollectionManager.RetreatGemCost;
        if (totalText != null) totalText.text = $"Selected: {total} / {need}";
        if (confirmButton != null) confirmButton.interactable = total == need;
    }

    private void OnConfirmClicked()
    {
        var collectionManager = HeroCollectionManager.Instance;
        if (collectionManager == null || DeathDungeonManager.Instance == null) return;

        var squadIds = collectionManager.squad.Where(h => h != null).Select(h => h.heroId);
        List<string> sacrificedHeroIds = collectionManager.ExecuteRetreatSacrifice(
            new Dictionary<string, int>(gemSelection), cardSelection.ToList(), squadIds);

        if (sacrificedHeroIds == null)
        {
            ConfirmationDialog.ShowInfo(canvasRoot, "Your selection is no longer valid — please choose again.");
            Populate();
            return;
        }

        DeathDungeonManager.Instance.lastSacrificedHeroIds = sacrificedHeroIds;
        DeathDungeonManager.Instance.ApplyRetreatConsequence();
        DeathDungeonManager.Instance.AbandonRun();
        MineThreatManager.Instance?.EnqueueThreats(sacrificedHeroIds);

        Close();
        onRetreated?.Invoke();
    }
}
