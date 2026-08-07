using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Экран выбора героя-цели, которому Hero Voucher'ы превращаются в 1 ascensionGems (см.
// HeroCollectionManager.GrantGemToHero, project_death_dungeon_concept). Ваучер НЕ привязан к конкретному
// герою — только к редкости, поэтому список показывает ВСЕХ героев этой редкости — и открытых (гем просто
// пополнит их обычный банк), и запертых (гем включит им кнопку призыва, см. UnlockHeroWithGem). Структура
// скопирована с HeroExperienceUseUI (тот же приём "оверлей + скролл-сетка + PickerTileUtility").
public class HeroVoucherRedeemUI : MonoBehaviour
{
    private HeroCollectionManager heroCollectionManager;
    private Transform canvasRoot;

    private GameObject overlayRoot;
    private Transform listContainer;
    private GameObject emptyLabelHolder;
    private TMP_Text titleText;

    private Image closeBg;
    private TMP_Text closeButtonText;

    private Rarity rarity;
    private System.Action onApplied;

    public void Open(Rarity rarity, System.Action onApplied = null)
    {
        heroCollectionManager = HeroCollectionManager.Instance;
        if (heroCollectionManager == null) return;

        if (canvasRoot == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            canvasRoot = canvas != null ? canvas.transform : transform;
        }

        this.rarity = rarity;
        this.onApplied = onApplied;

        BuildOverlayIfNeeded();
        RefreshButtonTheme();
        overlayRoot.transform.SetAsLastSibling();
        overlayRoot.SetActive(true);
        Populate();
    }

    public void Close()
    {
        if (overlayRoot != null) overlayRoot.SetActive(false);
    }

    private void RefreshButtonTheme()
    {
        if (closeBg != null) ConfirmationDialog.StyleAsButton(closeBg);
        if (closeButtonText != null) closeButtonText.color = ConfirmationDialog.ButtonTextColor;
    }

    private void BuildOverlayIfNeeded()
    {
        if (overlayRoot != null) return;

        overlayRoot = new GameObject("HeroVoucherRedeemOverlay", typeof(RectTransform));
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
        windowRect.sizeDelta = new Vector2(560, 440);
        var windowBg = windowObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsPanel(windowBg);

        var titleObj = new GameObject("Title", typeof(RectTransform));
        var titleRect = (RectTransform)titleObj.transform;
        titleRect.SetParent(windowRect, false);
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 40);
        titleRect.anchoredPosition = new Vector2(0, -8);
        titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.fontSize = 24;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        var scrollObj = new GameObject("Scroll View", typeof(RectTransform));
        var scrollRect = (RectTransform)scrollObj.transform;
        scrollRect.SetParent(windowRect, false);
        scrollRect.anchorMin = new Vector2(0, 0);
        scrollRect.anchorMax = new Vector2(1, 1);
        scrollRect.offsetMin = new Vector2(16, 64);
        scrollRect.offsetMax = new Vector2(-16, -56);

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
        contentRect.pivot = new Vector2(0, 1);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0, 0);

        var grid = contentObj.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(100, 120);
        grid.spacing = new Vector2(10, 10);
        grid.padding = new RectOffset(6, 6, 6, 6);

        var fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRect;
        scroll.viewport = scrollRect;
        scroll.horizontal = false;
        scroll.vertical = true;

        listContainer = contentRect;

        var emptyLabelObj = new GameObject("EmptyLabel", typeof(RectTransform));
        var emptyLabelRect = (RectTransform)emptyLabelObj.transform;
        emptyLabelRect.SetParent(windowRect, false);
        emptyLabelRect.anchorMin = new Vector2(0, 0.3f);
        emptyLabelRect.anchorMax = new Vector2(1, 0.7f);
        emptyLabelRect.offsetMin = new Vector2(16, 0);
        emptyLabelRect.offsetMax = new Vector2(-16, 0);
        var emptyLabel = emptyLabelObj.AddComponent<TextMeshProUGUI>();
        emptyLabel.text = "No heroes of this rarity exist";
        emptyLabel.alignment = TextAlignmentOptions.Center;
        emptyLabel.color = new Color(1, 1, 1, 0.6f);
        emptyLabel.fontSize = 18;
        emptyLabelObj.SetActive(false);
        emptyLabelHolder = emptyLabelObj;

        var closeBtnObj = new GameObject("CloseButton", typeof(RectTransform));
        var closeBtnRect = (RectTransform)closeBtnObj.transform;
        closeBtnRect.SetParent(windowRect, false);
        closeBtnRect.anchorMin = new Vector2(0.5f, 0);
        closeBtnRect.anchorMax = new Vector2(0.5f, 0);
        closeBtnRect.pivot = new Vector2(0.5f, 0);
        closeBtnRect.sizeDelta = new Vector2(160, 36);
        closeBtnRect.anchoredPosition = new Vector2(0, 12);
        closeBg = closeBtnObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(closeBg);
        var closeBtn = closeBtnObj.AddComponent<Button>();
        closeBtn.onClick.AddListener(Close);

        var closeTextObj = new GameObject("Text", typeof(RectTransform));
        var closeTextRect = (RectTransform)closeTextObj.transform;
        closeTextRect.SetParent(closeBtnRect, false);
        closeTextRect.anchorMin = Vector2.zero;
        closeTextRect.anchorMax = Vector2.one;
        closeTextRect.offsetMin = Vector2.zero;
        closeTextRect.offsetMax = Vector2.zero;
        closeButtonText = closeTextObj.AddComponent<TextMeshProUGUI>();
        closeButtonText.text = "Cancel";
        closeButtonText.alignment = TextAlignmentOptions.Center;
        closeButtonText.color = ConfirmationDialog.ButtonTextColor;

        overlayRoot.SetActive(false);
    }

    private void Populate()
    {
        if (titleText != null) titleText.text = $"Grant a Gem to a {rarity} hero";

        foreach (Transform child in listContainer)
            Destroy(child.gameObject);

        var heroes = heroCollectionManager.allHeroes
            .Where(h => h != null && h.rarity == rarity)
            .ToList();

        if (emptyLabelHolder != null)
            emptyLabelHolder.SetActive(heroes.Count == 0);

        foreach (var hero in heroes)
            CreateHeroEntry(hero);
    }

    // Любой герой этой редкости — не только недостающий: гем можно направить и уже открытому герою
    // (например, добить недостающий гем до вознесения), см. HeroCollectionManager.GrantGemToHero.
    private void CreateHeroEntry(HeroData hero)
    {
        PickerTileUtility.BuildTile(listContainer, hero.heroId, new Color(1, 1, 1, 0.06f),
            out Image bg, out Image icon, out TMP_Text label, out Button btn);

        icon.sprite = hero.portrait;

        bool unlocked = heroCollectionManager.IsUnlocked(hero);
        label.text = unlocked ? hero.heroName : $"{hero.heroName}\n(locked)";
        label.fontSize = 12;
        label.color = hero.themeColor;

        btn.onClick.AddListener(() => OnHeroSelected(hero));
    }

    private void OnHeroSelected(HeroData hero)
    {
        bool unlocked = heroCollectionManager.IsUnlocked(hero);
        string message = unlocked
            ? $"Grant {hero.heroName} an Ascension Gem using {HeroCollectionManager.HeroVouchersPerGem} Hero Vouchers?"
            : $"Grant {hero.heroName} an Ascension Gem using {HeroCollectionManager.HeroVouchersPerGem} Hero Vouchers?\nThis will let you summon them from the Collection screen.";

        ConfirmationDialog.Show(canvasRoot, message, () => Apply(hero.heroId));
    }

    private void Apply(string heroId)
    {
        if (!heroCollectionManager.GrantGemToHero(heroId)) return;

        onApplied?.Invoke();
        Close();
    }
}
