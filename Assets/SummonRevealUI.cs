using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Экран результата призыва ГЕРОЯ (только Алтарь — предметы Кузни остаются на старом текстовом
// CastleSummonUI.ShowResult, у предметов нет "разблокировки"/портрета в том же смысле). Три уровня,
// по прямой просьбе пользователя 2026-08-18:
// 1. База — сетка портрет+имя(цвет редкости) на КАЖДЫЙ пул, всегда, все герои разом (не по одному с тапом).
// 2. Катсцена первого получения — герой попал на аккаунт ВПЕРВЫЕ и он Orange, один большой экран, по одной на героя.
// 3. Катсцена супер-сумона (джекпот) — 2 гарантированных Orange, один большой экран на пару.
// Катсцены проигрываются ПЕРЕД базовой сеткой; джекпот приоритетнее личной "первой Orange" для ТЕХ ЖЕ
// героев (см. Show) — если джекпотный герой это ещё и его первая Orange, показываем только джекпот-катсцену,
// не обе подряд. Другие герои того же x10, не входящие в джекпотную пару, всё равно получают свою
// катсцену первой Orange, если applicable.
// Самосоздаётся на CastleSummonUI (тот же приём, что HeroUpgradeUI на HeroInventoryUI).
public class SummonRevealUI : MonoBehaviour
{
    private Transform canvasRoot;
    private GameObject overlayRoot;
    private RectTransform windowRect;
    private TMP_Text titleText;
    private RectTransform contentContainer;
    private Button continueButton;
    private TMP_Text continueButtonText;

    private List<(HeroData hero, bool wasNewUnlock)> pendingResults;
    private int requestedCount;
    private System.Action onDone;
    private Queue<HeroData> cutsceneQueue;

    // requestedCount — сколько ИЗНАЧАЛЬНО просили (x1/x10); может быть больше pendingResults.Count, если
    // валюты не хватило на весь пул призывов (см. SummonService.PullHeroMultiple) — сетка внизу это отметит.
    public void Show(Transform canvasRoot, List<(HeroData hero, bool wasNewUnlock)> results, int requestedCount, bool isJackpot, System.Action onDone)
    {
        this.canvasRoot = canvasRoot;
        this.requestedCount = requestedCount;
        this.onDone = onDone;
        pendingResults = results ?? new List<(HeroData hero, bool wasNewUnlock)>();

        // Гарантированная пара джекпота — ВСЕГДА первые 2 элемента pendingResults, когда isJackpot (см.
        // SummonService.TryPullJackpotBatch — кладёт их в начало списка перед обычными роллами). Не
        // передеривать по rarity==Orange заново: один из обычных 8 роллов того же x10 тоже МОЖЕТ случайно
        // выдать Orange, и его нельзя путать с гарантированной парой.
        var jackpotHeroes = isJackpot && pendingResults.Count >= 2
            ? new List<HeroData> { pendingResults[0].hero, pendingResults[1].hero }
            : new List<HeroData>();
        var jackpotSet = new HashSet<HeroData>(jackpotHeroes);

        cutsceneQueue = new Queue<HeroData>(pendingResults
            .Where(r => r.wasNewUnlock && r.hero != null && r.hero.rarity == Rarity.Orange && !jackpotSet.Contains(r.hero))
            .Select(r => r.hero));

        EnsureOverlayIfNeeded();
        overlayRoot.transform.SetAsLastSibling();
        overlayRoot.SetActive(true);

        if (jackpotHeroes.Count > 0)
            ShowJackpotCutscene(jackpotHeroes);
        else
            PlayNextCutsceneOrShowGrid();
    }

    private void PlayNextCutsceneOrShowGrid()
    {
        if (cutsceneQueue.Count > 0)
            ShowFirstOrangeCutscene(cutsceneQueue.Dequeue());
        else
            ShowBaselineGrid();
    }

    private void EnsureOverlayIfNeeded()
    {
        if (overlayRoot != null) return;

        overlayRoot = new GameObject("SummonRevealOverlay", typeof(RectTransform));
        var overlayRect = (RectTransform)overlayRoot.transform;
        overlayRect.SetParent(canvasRoot, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        var dim = overlayRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.88f);
        // Намеренно без клика-мимо-закрытия — только через ContinueButton, иначе случайный тап под
        // катсценой героя пропускает шаг очереди без подтверждения.

        var windowObj = new GameObject("Window", typeof(RectTransform));
        windowRect = (RectTransform)windowObj.transform;
        windowRect.SetParent(overlayRect, false);
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(1040, 900);
        var windowBg = windowObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsPanel(windowBg);

        var titleObj = new GameObject("Title", typeof(RectTransform));
        var titleRect = (RectTransform)titleObj.transform;
        titleRect.SetParent(windowRect, false);
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 130); // с запасом на 2 строки — "Ran out of currency..." подпись может уйти на вторую
        titleRect.anchoredPosition = new Vector2(0, -30);
        titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 32;
        titleText.fontSizeMax = 52;

        var contentObj = new GameObject("Content", typeof(RectTransform));
        contentContainer = (RectTransform)contentObj.transform;
        contentContainer.SetParent(windowRect, false);
        contentContainer.anchorMin = new Vector2(0, 0);
        contentContainer.anchorMax = new Vector2(1, 1);
        contentContainer.offsetMin = new Vector2(40, 140);
        contentContainer.offsetMax = new Vector2(-40, -170); // ниже titleRect (30 сверху + 130 высоты), без нахлёста

        var continueObj = new GameObject("ContinueButton", typeof(RectTransform));
        var continueRect = (RectTransform)continueObj.transform;
        continueRect.SetParent(windowRect, false);
        continueRect.anchorMin = new Vector2(0.5f, 0);
        continueRect.anchorMax = new Vector2(0.5f, 0);
        continueRect.pivot = new Vector2(0.5f, 0);
        continueRect.sizeDelta = new Vector2(340, 76);
        continueRect.anchoredPosition = new Vector2(0, 30);
        var continueBg = continueObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(continueBg);
        continueButton = continueObj.AddComponent<Button>();
        continueButton.targetGraphic = continueBg;

        var continueTextObj = new GameObject("Text", typeof(RectTransform));
        var continueTextRect = (RectTransform)continueTextObj.transform;
        continueTextRect.SetParent(continueRect, false);
        continueTextRect.anchorMin = Vector2.zero;
        continueTextRect.anchorMax = Vector2.one;
        continueTextRect.offsetMin = Vector2.zero;
        continueTextRect.offsetMax = Vector2.zero;
        continueButtonText = continueTextObj.AddComponent<TextMeshProUGUI>();
        continueButtonText.fontStyle = FontStyles.Bold;
        continueButtonText.alignment = TextAlignmentOptions.Center;
        continueButtonText.color = ConfirmationDialog.ButtonTextColor;
        continueButtonText.enableAutoSizing = true;
        continueButtonText.fontSizeMin = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
        continueButtonText.fontSizeMax = 32;

        overlayRoot.SetActive(false);
    }

    private void ClearContent()
    {
        foreach (Transform child in contentContainer)
            Destroy(child.gameObject);
    }

    private void ShowJackpotCutscene(List<HeroData> heroes)
    {
        ClearContent();
        titleText.text = "JACKPOT! Legendary Summon!";

        var rowObj = new GameObject("JackpotRow", typeof(RectTransform));
        var rowRect = (RectTransform)rowObj.transform;
        rowRect.SetParent(contentContainer, false);
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.sizeDelta = new Vector2(heroes.Count * 340 + (heroes.Count - 1) * 40, 560);
        var rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 40;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childControlWidth = true; // без этого карточки не получат размер из LayoutElement.preferredWidth/Height
        rowLayout.childControlHeight = true;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;

        foreach (var hero in heroes)
            BuildHeroCard(rowRect, hero, 340, 560, "NEW ORANGE HERO!");

        continueButtonText.text = "Continue";
        continueButton.onClick.RemoveAllListeners();
        continueButton.onClick.AddListener(PlayNextCutsceneOrShowGrid);
    }

    private void ShowFirstOrangeCutscene(HeroData hero)
    {
        ClearContent();
        titleText.text = "New Orange Hero Acquired!";

        var cardHolder = new GameObject("FirstOrangeCard", typeof(RectTransform));
        var holderRect = (RectTransform)cardHolder.transform;
        holderRect.SetParent(contentContainer, false);
        holderRect.anchorMin = new Vector2(0.5f, 0.5f);
        holderRect.anchorMax = new Vector2(0.5f, 0.5f);
        holderRect.pivot = new Vector2(0.5f, 0.5f);
        holderRect.sizeDelta = new Vector2(380, 620);

        BuildHeroCard(holderRect, hero, 380, 620, null);

        continueButtonText.text = "Continue";
        continueButton.onClick.RemoveAllListeners();
        continueButton.onClick.AddListener(PlayNextCutsceneOrShowGrid);
    }

    // База — все герои этого пула призыва разом, одной сеткой (не по одному с тапом, см. UX-правку 2026-08-18).
    private void ShowBaselineGrid()
    {
        ClearContent();
        titleText.text = pendingResults.Count == 1 ? "You got:" : $"You got {pendingResults.Count} heroes:";
        if (pendingResults.Count < requestedCount)
            titleText.text += $"\n(Ran out of currency after {pendingResults.Count}/{requestedCount} pulls)";

        var gridObj = new GameObject("Grid", typeof(RectTransform));
        var gridRect = (RectTransform)gridObj.transform;
        gridRect.SetParent(contentContainer, false);
        gridRect.anchorMin = new Vector2(0.5f, 0.5f);
        gridRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridRect.pivot = new Vector2(0.5f, 0.5f);
        int columns = Mathf.Clamp(pendingResults.Count, 1, 5);
        int rows = Mathf.CeilToInt(pendingResults.Count / (float)columns);
        gridRect.sizeDelta = new Vector2(columns * 180 + (columns - 1) * 16, rows * 260 + (rows - 1) * 16);

        var grid = gridObj.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(180, 260);
        grid.spacing = new Vector2(16, 16);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.childAlignment = TextAnchor.MiddleCenter;

        foreach (var (hero, _) in pendingResults)
            BuildHeroCard(gridRect, hero, 180, 260, null);

        continueButtonText.text = "Continue";
        continueButton.onClick.RemoveAllListeners();
        continueButton.onClick.AddListener(Finish);
    }

    // Общая карточка для всех трёх режимов — портрет (full-bleed сверху) + имя, окрашенное в цвет
    // редкости (тот же приём, что WondrousArmorChoiceUI.BuildOption). tag — необязательная строка-плашка
    // над именем ("NEW ORANGE HERO!"), используется только катсценами.
    private void BuildHeroCard(Transform parent, HeroData hero, float width, float height, string tag)
    {
        if (hero == null) return;

        var cardObj = new GameObject(hero.heroName, typeof(RectTransform));
        var cardRect = (RectTransform)cardObj.transform;
        cardRect.SetParent(parent, false);
        // Растягивается на весь parent по умолчанию — актуально для катсцены "первая Orange" (parent уже
        // сам явно нужного размера, никакого LayoutGroup над ним нет). Grid/HorizontalLayoutGroup ниже сами
        // перезапишут эти анкоры своими, LayoutElement им и указывает нужный размер.
        cardRect.anchorMin = Vector2.zero;
        cardRect.anchorMax = Vector2.one;
        cardRect.offsetMin = Vector2.zero;
        cardRect.offsetMax = Vector2.zero;
        var le = cardObj.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = height;

        float nameHeight = height <= 260 ? 50 : 60;
        float tagHeight = string.IsNullOrEmpty(tag) ? 0 : 40;

        var portraitObj = new GameObject("Portrait", typeof(RectTransform));
        var portraitRect = (RectTransform)portraitObj.transform;
        portraitRect.SetParent(cardRect, false);
        portraitRect.anchorMin = new Vector2(0, 0);
        portraitRect.anchorMax = new Vector2(1, 1);
        portraitRect.offsetMin = new Vector2(0, nameHeight);
        portraitRect.offsetMax = new Vector2(0, -tagHeight);
        var portraitImg = portraitObj.AddComponent<Image>();
        portraitImg.sprite = hero.portrait;
        portraitImg.preserveAspect = true;

        // Свой Image на cardObj — рендерится позади его же детей (Unity всегда рисует graphic узла ДО
        // рекурсии в его children) автоматически, без ручной перестановки sibling-порядка.
        var frameImg = cardObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsPanel(frameImg);
        CardDepthUtility.ApplyCardDepth(frameImg); // тень + тёмный скос, см. UX-правку 2026-08-18

        if (!string.IsNullOrEmpty(tag))
        {
            var tagObj = new GameObject("Tag", typeof(RectTransform));
            var tagRect = (RectTransform)tagObj.transform;
            tagRect.SetParent(cardRect, false);
            tagRect.anchorMin = new Vector2(0, 1);
            tagRect.anchorMax = new Vector2(1, 1);
            tagRect.pivot = new Vector2(0.5f, 1);
            tagRect.sizeDelta = new Vector2(0, tagHeight);
            var tagText = tagObj.AddComponent<TextMeshProUGUI>();
            tagText.text = tag;
            tagText.fontStyle = FontStyles.Bold;
            tagText.alignment = TextAlignmentOptions.Center;
            tagText.color = FloatingDamageText.PositiveGainColor;
            tagText.enableAutoSizing = true;
            tagText.fontSizeMin = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
            tagText.fontSizeMax = 30;
        }

        var nameObj = new GameObject("Name", typeof(RectTransform));
        var nameRect = (RectTransform)nameObj.transform;
        nameRect.SetParent(cardRect, false);
        nameRect.anchorMin = new Vector2(0, 0);
        nameRect.anchorMax = new Vector2(1, 0);
        nameRect.pivot = new Vector2(0.5f, 0);
        nameRect.sizeDelta = new Vector2(0, nameHeight);
        var nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.text = hero.heroName;
        nameText.fontStyle = FontStyles.Bold;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = hero.GetRarityColor();
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
        nameText.fontSizeMax = height <= 260 ? 30 : 36;
    }

    private void Finish()
    {
        overlayRoot.SetActive(false);
        onDone?.Invoke();
    }
}
