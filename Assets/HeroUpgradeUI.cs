using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Прокачка уровня героя за CurrencyType.HeroExperience — тот же приём "отложенный превью + подтверждение",
// что и у ItemSacrificeUI, только тут ДВА источника оплаты (баланс валюты + предметы HeroExperience, см.
// ComputeSpendPlan), а не N донор-предметов. Отдельный файл-попап, которым владеет HeroInventoryUI (тот же
// принцип "владелец = AddComponent на себя", что уже у ItemDetailUI.sacrificeUI/heroVoucherRedeemUI).
public class HeroUpgradeUI : MonoBehaviour
{
    private Transform canvasRoot;
    private RectTransform windowRect;

    private HeroData hero;
    private HeroOwnershipData ownership;
    private int levelCap;
    private int stagedLevel;

    private System.Action onBeforeApply;
    private System.Action onApplied;

    private GameObject overlayRoot;
    private TMP_Text titleText;
    private TMP_Text levelText;
    private TMP_Text statsText;
    private TMP_Text costText;

    private Image confirmBg;
    private Button confirmButton;
    private TMP_Text confirmButtonText;
    private Image closeBg;
    private TMP_Text closeButtonText;

    // -10 -1 +1 +10, симметрично вокруг центра; Max — отдельная кнопка по центру той же строки.
    private static readonly int[] StepDeltas = { -10, -1, 1, 10 };
    private static readonly string[] StepLabels = { "-10", "-1", "+1", "+10" };
    private readonly Button[] stepButtons = new Button[StepDeltas.Length];
    private readonly Image[] stepBgs = new Image[StepDeltas.Length];
    private readonly TMP_Text[] stepTexts = new TMP_Text[StepDeltas.Length];
    private Image maxBg;
    private Button maxButton;
    private TMP_Text maxText;

    public void Open(HeroData hero, HeroOwnershipData ownership, System.Action onBeforeApply, System.Action onApplied)
    {
        if (hero == null || ownership == null || HeroCollectionManager.Instance == null) return;

        if (canvasRoot == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            canvasRoot = canvas != null ? canvas.transform : transform;
        }

        this.hero = hero;
        this.ownership = ownership;
        this.onBeforeApply = onBeforeApply;
        this.onApplied = onApplied;
        levelCap = HeroAscensionUtility.GetLevelCap(hero.rarity, ownership.ascensionLevel);
        stagedLevel = ownership.level;

        BuildOverlayIfNeeded();
        RefreshTheme();
        if (titleText != null) titleText.text = $"Upgrade {hero.heroName}";
        overlayRoot.transform.SetAsLastSibling();
        overlayRoot.SetActive(true);
        RefreshDisplay();
    }

    public void Close()
    {
        if (overlayRoot != null) overlayRoot.SetActive(false);
    }

    private void RefreshTheme()
    {
        if (closeBg != null) ConfirmationDialog.StyleAsButton(closeBg);
        if (closeButtonText != null) closeButtonText.color = ConfirmationDialog.ButtonTextColor;
        if (confirmBg != null) ConfirmationDialog.StyleAsButton(confirmBg);
        if (confirmButtonText != null) confirmButtonText.color = ConfirmationDialog.ButtonTextColor;

        for (int i = 0; i < stepBgs.Length; i++)
            if (stepBgs[i] != null) ConfirmationDialog.StyleAsStepperButton(stepBgs[i]);
        if (maxBg != null) ConfirmationDialog.StyleAsStepperButton(maxBg);
    }

    private void BuildOverlayIfNeeded()
    {
        if (overlayRoot != null) return;

        overlayRoot = new GameObject("HeroUpgradeOverlay", typeof(RectTransform));
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
        windowRect = (RectTransform)windowObj.transform;
        windowRect.SetParent(overlayRect, false);
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(700, 760);
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
        titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.fontSize = 36;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        // Строка "было -> стало" по уровню
        var levelBgObj = new GameObject("LevelPreview", typeof(RectTransform));
        var levelBgRect = (RectTransform)levelBgObj.transform;
        levelBgRect.SetParent(windowRect, false);
        levelBgRect.anchorMin = new Vector2(0.5f, 1);
        levelBgRect.anchorMax = new Vector2(0.5f, 1);
        levelBgRect.pivot = new Vector2(0.5f, 1);
        levelBgRect.sizeDelta = new Vector2(560, 90);
        levelBgRect.anchoredPosition = new Vector2(0, -120);
        var levelBg = levelBgObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsDescriptionPanel(levelBg);

        var levelTextObj = new GameObject("Text", typeof(RectTransform));
        var levelTextRect = (RectTransform)levelTextObj.transform;
        levelTextRect.SetParent(levelBgRect, false);
        levelTextRect.anchorMin = Vector2.zero;
        levelTextRect.anchorMax = Vector2.one;
        levelTextRect.offsetMin = Vector2.zero;
        levelTextRect.offsetMax = Vector2.zero;
        levelText = levelTextObj.AddComponent<TextMeshProUGUI>();
        levelText.alignment = TextAlignmentOptions.Center;
        levelText.color = Color.white;
        levelText.enableAutoSizing = true;
        levelText.fontSizeMin = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
        levelText.fontSizeMax = 34;

        // Блок "было -> стало" по HP/Mana/Attack/Armor — тот же расчёт, что HeroInventoryUI.ComputeDisplayedStats,
        // но на ОТЛОЖЕННОМ stagedLevel (см. ComputeStatsAtLevel), обновляется на каждый тап степпера.
        var statsBgObj = new GameObject("StatsPreview", typeof(RectTransform));
        var statsBgRect = (RectTransform)statsBgObj.transform;
        statsBgRect.SetParent(windowRect, false);
        statsBgRect.anchorMin = new Vector2(0.5f, 1);
        statsBgRect.anchorMax = new Vector2(0.5f, 1);
        statsBgRect.pivot = new Vector2(0.5f, 1);
        statsBgRect.sizeDelta = new Vector2(560, 150);
        statsBgRect.anchoredPosition = new Vector2(0, -230);
        var statsBg = statsBgObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsDescriptionPanel(statsBg);

        var statsTextObj = new GameObject("Text", typeof(RectTransform));
        var statsTextRect = (RectTransform)statsTextObj.transform;
        statsTextRect.SetParent(statsBgRect, false);
        statsTextRect.anchorMin = Vector2.zero;
        statsTextRect.anchorMax = Vector2.one;
        statsTextRect.offsetMin = Vector2.zero;
        statsTextRect.offsetMax = Vector2.zero;
        statsText = statsTextObj.AddComponent<TextMeshProUGUI>();
        statsText.alignment = TextAlignmentOptions.Center;
        statsText.color = Color.white;
        statsText.enableAutoSizing = true;
        statsText.fontSizeMin = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
        statsText.fontSizeMax = 30;

        // Ряд степпера: -10 -1 +1 +10 симметрично, Max по центру
        const float stepBtnWidth = 100f;
        const float stepRowHeight = 55f;
        float[] stepX = { -270f, -170f, 170f, 270f };
        for (int i = 0; i < StepDeltas.Length; i++)
        {
            int delta = StepDeltas[i]; // локальная копия — безопасна для замыкания кнопки ниже
            var stepObj = new GameObject($"Step_{StepLabels[i]}", typeof(RectTransform));
            var stepRect = (RectTransform)stepObj.transform;
            stepRect.SetParent(windowRect, false);
            stepRect.anchorMin = new Vector2(0.5f, 1);
            stepRect.anchorMax = new Vector2(0.5f, 1);
            stepRect.pivot = new Vector2(0.5f, 1);
            stepRect.sizeDelta = new Vector2(stepBtnWidth, stepRowHeight);
            stepRect.anchoredPosition = new Vector2(stepX[i], -400f);

            stepBgs[i] = stepObj.AddComponent<Image>();
            ConfirmationDialog.StyleAsStepperButton(stepBgs[i]);
            stepButtons[i] = stepObj.AddComponent<Button>();
            stepButtons[i].targetGraphic = stepBgs[i];
            stepButtons[i].onClick.AddListener(() => AdjustStagedLevel(delta));

            var stepTextObj = new GameObject("Text", typeof(RectTransform));
            var stepTextRect = (RectTransform)stepTextObj.transform;
            stepTextRect.SetParent(stepRect, false);
            stepTextRect.anchorMin = Vector2.zero;
            stepTextRect.anchorMax = Vector2.one;
            stepTextRect.offsetMin = Vector2.zero;
            stepTextRect.offsetMax = Vector2.zero;
            stepTexts[i] = stepTextObj.AddComponent<TextMeshProUGUI>();
            stepTexts[i].text = StepLabels[i];
            stepTexts[i].alignment = TextAlignmentOptions.Center;
            stepTexts[i].enableAutoSizing = true;
            stepTexts[i].fontSizeMin = 28;
            stepTexts[i].fontSizeMax = 30;
            stepTexts[i].color = Color.white;
        }

        var maxObj = new GameObject("Step_Max", typeof(RectTransform));
        var maxRect = (RectTransform)maxObj.transform;
        maxRect.SetParent(windowRect, false);
        maxRect.anchorMin = new Vector2(0.5f, 1);
        maxRect.anchorMax = new Vector2(0.5f, 1);
        maxRect.pivot = new Vector2(0.5f, 1);
        maxRect.sizeDelta = new Vector2(stepBtnWidth, stepRowHeight);
        maxRect.anchoredPosition = new Vector2(0, -400f);
        maxBg = maxObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsStepperButton(maxBg);
        maxButton = maxObj.AddComponent<Button>();
        maxButton.targetGraphic = maxBg;
        maxButton.onClick.AddListener(() => SetStagedLevel(levelCap));

        var maxTextObj = new GameObject("Text", typeof(RectTransform));
        var maxTextRect = (RectTransform)maxTextObj.transform;
        maxTextRect.SetParent(maxRect, false);
        maxTextRect.anchorMin = Vector2.zero;
        maxTextRect.anchorMax = Vector2.one;
        maxTextRect.offsetMin = Vector2.zero;
        maxTextRect.offsetMax = Vector2.zero;
        maxText = maxTextObj.AddComponent<TextMeshProUGUI>();
        maxText.text = "Max";
        maxText.alignment = TextAlignmentOptions.Center;
        maxText.enableAutoSizing = true;
        maxText.fontSizeMin = 28;
        maxText.fontSizeMax = 30;
        maxText.color = Color.white;

        // Ресурс: иконка + баланс/нужно (+ сколько добьют предметы опыта, если валюты не хватает)
        var resourceRowObj = new GameObject("ResourceRow", typeof(RectTransform));
        var resourceRowRect = (RectTransform)resourceRowObj.transform;
        resourceRowRect.SetParent(windowRect, false);
        resourceRowRect.anchorMin = new Vector2(0.5f, 1);
        resourceRowRect.anchorMax = new Vector2(0.5f, 1);
        resourceRowRect.pivot = new Vector2(0.5f, 1);
        resourceRowRect.sizeDelta = new Vector2(560, 90);
        resourceRowRect.anchoredPosition = new Vector2(0, -480f);

        ConfirmationDialog.CreateCurrencyIcon(resourceRowRect, ConfirmationDialog.GetCurrencyIconPath(CurrencyType.HeroExperience), new Vector2(0, 0), 56f);

        var costTextObj = new GameObject("Text", typeof(RectTransform));
        var costTextRect = (RectTransform)costTextObj.transform;
        costTextRect.SetParent(resourceRowRect, false);
        costTextRect.anchorMin = new Vector2(0, 0);
        costTextRect.anchorMax = new Vector2(1, 1);
        costTextRect.offsetMin = new Vector2(70, 0); // место под иконку слева
        costTextRect.offsetMax = Vector2.zero;
        costText = costTextObj.AddComponent<TextMeshProUGUI>();
        costText.alignment = TextAlignmentOptions.MidlineLeft;
        costText.color = Color.white;
        costText.enableAutoSizing = true;
        costText.fontSizeMin = 28;
        costText.fontSizeMax = 30;

        // Подтверждение/отмена — тот же анкоринг, что у ItemSacrificeUI
        var confirmObj = new GameObject("ConfirmButton", typeof(RectTransform));
        var confirmRect = (RectTransform)confirmObj.transform;
        confirmRect.SetParent(windowRect, false);
        confirmRect.anchorMin = new Vector2(0.3f, 0);
        confirmRect.anchorMax = new Vector2(0.3f, 0);
        confirmRect.pivot = new Vector2(0.5f, 0);
        confirmRect.sizeDelta = new Vector2(250, 65);
        confirmRect.anchoredPosition = new Vector2(0, 40);
        confirmBg = confirmObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(confirmBg);
        confirmButton = confirmObj.AddComponent<Button>();
        confirmButton.targetGraphic = confirmBg;
        confirmButton.onClick.AddListener(OnConfirmClicked);

        var confirmTextObj = new GameObject("Text", typeof(RectTransform));
        var confirmTextRect = (RectTransform)confirmTextObj.transform;
        confirmTextRect.SetParent(confirmRect, false);
        confirmTextRect.anchorMin = Vector2.zero;
        confirmTextRect.anchorMax = Vector2.one;
        confirmTextRect.offsetMin = Vector2.zero;
        confirmTextRect.offsetMax = Vector2.zero;
        confirmButtonText = confirmTextObj.AddComponent<TextMeshProUGUI>();
        confirmButtonText.text = "Upgrade";
        confirmButtonText.fontStyle = FontStyles.Bold;
        confirmButtonText.alignment = TextAlignmentOptions.Center;

        var closeObj = new GameObject("CloseButton", typeof(RectTransform));
        var closeRect = (RectTransform)closeObj.transform;
        closeRect.SetParent(windowRect, false);
        closeRect.anchorMin = new Vector2(0.7f, 0);
        closeRect.anchorMax = new Vector2(0.7f, 0);
        closeRect.pivot = new Vector2(0.5f, 0);
        closeRect.sizeDelta = new Vector2(250, 65);
        closeRect.anchoredPosition = new Vector2(0, 40);
        closeBg = closeObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(closeBg);
        var closeBtn = closeObj.AddComponent<Button>();
        closeBtn.targetGraphic = closeBg;
        closeBtn.onClick.AddListener(Close);

        var closeTextObj = new GameObject("Text", typeof(RectTransform));
        var closeTextRect = (RectTransform)closeTextObj.transform;
        closeTextRect.SetParent(closeRect, false);
        closeTextRect.anchorMin = Vector2.zero;
        closeTextRect.anchorMax = Vector2.one;
        closeTextRect.offsetMin = Vector2.zero;
        closeTextRect.offsetMax = Vector2.zero;
        closeButtonText = closeTextObj.AddComponent<TextMeshProUGUI>();
        closeButtonText.text = "Cancel";
        closeButtonText.fontStyle = FontStyles.Bold;
        closeButtonText.alignment = TextAlignmentOptions.Center;

        overlayRoot.SetActive(false);
    }

    // levelCap — потолок ТЕКУЩЕЙ ступени вознесения (см. HeroAscensionUtility.GetLevelCap); дальше игрок
    // идёт через Ascend (см. HeroInventoryUI.OpenAscendPopup), это окно его не трогает.
    private void AdjustStagedLevel(int delta) => SetStagedLevel(stagedLevel + delta);

    // -1/-10 откатывают ТОЛЬКО отложенный (ещё не потраченный) выбор назад к реальному текущему уровню
    // героя — не "отменяют" уже купленный уровень, его отменить нельзя. +1/+10/Max могут запросить больше,
    // чем сейчас по карману — тогда откатываем вниз до максимума доступного и явно предупреждаем (тот же
    // принцип, что у ItemSacrificeUI.QuickSelectToLevel).
    private void SetStagedLevel(int targetLevel)
    {
        if (hero == null || ownership == null || HeroCollectionManager.Instance == null) return;

        targetLevel = Mathf.Clamp(targetLevel, ownership.level, levelCap);
        int totalAvailable = GetTotalAvailableExperience();
        int requested = targetLevel;

        while (targetLevel > ownership.level
            && HeroCollectionManager.Instance.GetExperienceNeededForLevel(hero.heroId, targetLevel) > totalAvailable)
            targetLevel--;

        if (targetLevel < requested)
        {
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
                ConfirmationDialog.ShowInfo(canvas.transform,
                    $"Not enough Hero Experience to reach level {requested} — staged the highest level you can currently afford (level {targetLevel}).");
        }

        stagedLevel = targetLevel;
        RefreshDisplay();
    }

    // Баланс валюты + стоимость всех владеемых предметов category==HeroExperience — второй источник оплаты,
    // добавленный по прямой просьбе пользователя вместо отдельной кнопки "Use" на самом предмете.
    private int GetTotalAvailableExperience()
    {
        int currency = PlayerCurrencies.Instance != null ? PlayerCurrencies.Instance.GetBalance(CurrencyType.HeroExperience) : 0;
        return currency + GetOwnedHeroExperienceItemValue();
    }

    private int GetOwnedHeroExperienceItemValue()
    {
        var m = ItemCollectionManager.Instance;
        if (m == null || m.allItems == null) return 0;

        int total = 0;
        foreach (var item in m.allItems)
            if (item != null && item.category == ItemCategory.HeroExperience)
                total += m.GetTotalQuantity(item.itemId) * item.heroExperienceAmount;
        return total;
    }

    // Общий источник правды для превью-текста И реальной траты (см. RefreshDisplay/ApplyUpgrade) — тратит
    // валюту, потом добирает целыми предметами (дешёвый номинал вперёд, см. ItemSacrificeUI.QuickSelectToLevel
    // на тот же приём с донорами). Предметы неделимы — итог может слегка перекрыть neededAmount, это не баг
    // (GrantExperience уже безопасно сжигает лишнее на потолке уровня).
    private void ComputeSpendPlan(int neededAmount, out int currencySpend, out int itemValueSpend, out Dictionary<string, int> itemUnits)
    {
        itemUnits = new Dictionary<string, int>();
        int currencyBalance = PlayerCurrencies.Instance != null ? PlayerCurrencies.Instance.GetBalance(CurrencyType.HeroExperience) : 0;
        currencySpend = Mathf.Min(neededAmount, currencyBalance);
        int remainder = neededAmount - currencySpend;
        itemValueSpend = 0;

        var m = ItemCollectionManager.Instance;
        if (remainder <= 0 || m == null || m.allItems == null) return;

        var candidates = m.allItems
            .Where(i => i != null && i.category == ItemCategory.HeroExperience && i.heroExperienceAmount > 0)
            .OrderBy(i => i.heroExperienceAmount)
            .SelectMany(i => m.GetStacks(i.itemId).Select(s => (item: i, stack: s)));

        foreach (var (item, stack) in candidates)
        {
            if (remainder <= 0) break;

            int take = Mathf.Clamp(Mathf.CeilToInt(remainder / (float)item.heroExperienceAmount), 0, stack.quantity);
            if (take <= 0) continue;

            itemUnits[stack.instanceId] = take;
            itemValueSpend += take * item.heroExperienceAmount;
            remainder -= take * item.heroExperienceAmount;
        }
    }

    // Тот же расчёт, что HeroInventoryUI.ComputeDisplayedStats (тот же HeroStatUtility, чтобы цифры нигде
    // не разъезжались), но параметризован уровнем напрямую — превью здесь идёт по ОТЛОЖЕННОМУ stagedLevel,
    // ownership.level не мутируется до подтверждения (см. ApplyUpgrade).
    private (int hp, int mana, float dmg, int armor, int might) ComputeStatsAtLevel(int level)
    {
        if (hero == null || ownership == null) return (0, 0, 0f, 0, 0);

        var baseStats = HeroStatUtility.CalculateBaseStats(hero, level, ownership.ascensionLevel);
        var bonuses = HeroStatUtility.CalculateEquipmentBonuses(ownership);
        int mana = HeroStatUtility.CalculateMaxMana(hero, ownership, level, ownership.ascensionLevel);
        int might = HeroStatUtility.CalculatePower(hero, ownership, level, ownership.ascensionLevel);

        return (baseStats.health + bonuses.health, mana, baseStats.damageMultiplier + bonuses.damageMultiplier, baseStats.armor + bonuses.armor, might);
    }

    // Красит строку серый->зелёный только там, где стат реально меняется — Attack сравнивается уже в
    // отображаемых ×100-единицах (небольшой сдвиг множителя на 1 уровень может округлиться в то же число).
    private static string FormatStatLine(string label, int before, int after) =>
        before != after ? UpgradeFeedbackUtility.FormatBeforeAfter(label, before.ToString(), after.ToString()) : $"{label}: {after}";

    private void RefreshDisplay()
    {
        if (levelText == null || hero == null || ownership == null || HeroCollectionManager.Instance == null) return;

        levelText.text = stagedLevel > ownership.level
            ? UpgradeFeedbackUtility.FormatBeforeAfter("Lv", ownership.level.ToString(), stagedLevel.ToString()) // без точки — FormatBeforeAfter сам добавляет ": ", "Lv." дало бы двойную пунктуацию
            : $"Lv. {ownership.level}";

        if (statsText != null)
        {
            var before = ComputeStatsAtLevel(ownership.level);
            var after = ComputeStatsAtLevel(stagedLevel);
            int beforeAtk = UpgradeFeedbackUtility.AttackDisplayValue(before.dmg);
            int afterAtk = UpgradeFeedbackUtility.AttackDisplayValue(after.dmg);

            statsText.text = string.Join("\n", new[]
            {
                FormatStatLine("HP", before.hp, after.hp),
                FormatStatLine("Mana", before.mana, after.mana),
                FormatStatLine("Attack", beforeAtk, afterAtk),
                FormatStatLine("Armor", before.armor, after.armor),
                FormatStatLine("Might", before.might, after.might),
            });
        }

        int needed = HeroCollectionManager.Instance.GetExperienceNeededForLevel(hero.heroId, stagedLevel);
        int currencyBalance = PlayerCurrencies.Instance != null ? PlayerCurrencies.Instance.GetBalance(CurrencyType.HeroExperience) : 0;
        ComputeSpendPlan(needed, out _, out int itemValueSpend, out var itemUnits);

        string cost = $"Hero Experience: {currencyBalance} / {needed}";
        if (itemUnits.Count > 0)
            cost += $"\n+ Hero Experience items will cover {itemValueSpend}";
        if (costText != null) costText.text = cost;

        if (confirmButton != null) confirmButton.interactable = stagedLevel > ownership.level;

        bool canGoDown = stagedLevel > ownership.level;
        bool canGoUp = stagedLevel < levelCap
            && HeroCollectionManager.Instance.GetExperienceNeededForLevel(hero.heroId, stagedLevel + 1) <= GetTotalAvailableExperience();

        for (int i = 0; i < StepDeltas.Length; i++)
            if (stepButtons[i] != null)
                stepButtons[i].interactable = StepDeltas[i] < 0 ? canGoDown : canGoUp;
        if (maxButton != null) maxButton.interactable = canGoUp;
    }

    private void OnConfirmClicked()
    {
        if (hero == null || ownership == null || stagedLevel <= ownership.level || HeroCollectionManager.Instance == null) return;

        int needed = HeroCollectionManager.Instance.GetExperienceNeededForLevel(hero.heroId, stagedLevel);
        var canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        ConfirmationDialog.Show(canvas.transform,
            $"Level up {hero.heroName} to level {stagedLevel}?\nCost: {needed} Hero Experience.",
            ApplyUpgrade);
    }

    // onBeforeApply/onApplied — HeroInventoryUI использует их, чтобы снять снимок статов ДО и проиграть уже
    // существующий AnimateStatGain ПОСЛЕ (см. HeroInventoryUI.OnHeroUpgradeWillApply/OnHeroUpgradeApplied) —
    // это окно само НЕ трогает FlashPanel/FloatingDamageText/HapticsUtility/AudioManager, чтобы вспышка
    // никогда не срабатывала дважды.
    private void ApplyUpgrade()
    {
        if (hero == null || ownership == null || stagedLevel <= ownership.level || HeroCollectionManager.Instance == null) return;

        int needed = HeroCollectionManager.Instance.GetExperienceNeededForLevel(hero.heroId, stagedLevel);
        if (needed <= 0) return;

        ComputeSpendPlan(needed, out int currencySpend, out int itemValueSpend, out var itemUnits);
        if (currencySpend + itemValueSpend < needed) return; // подстраховка — SetStagedLevel уже держит stagedLevel по карману

        onBeforeApply?.Invoke();

        if (currencySpend > 0)
            PlayerCurrencies.Instance?.Spend(CurrencyType.HeroExperience, currencySpend);

        var m = ItemCollectionManager.Instance;
        if (m != null)
            foreach (var kvp in itemUnits)
                for (int i = 0; i < kvp.Value; i++)
                    m.ConsumeItem(kvp.Key);

        HeroCollectionManager.Instance.GrantExperience(hero.heroId, currencySpend + itemValueSpend); // ОДИН пакетный вызов, не по одному на предмет

        Close();
        onApplied?.Invoke();
    }
}
