using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class BattleUI : MonoBehaviour
{
    [Header("Посилання")]
    public BattleManager battleManager;

    [Header("HP гравця")]
    public Slider playerHPSlider;
    public TMP_Text playerHPText;
    public TMP_Text playerShieldText;

    [Header("Портрет ворога")]
    public Image enemyPortrait;

    [Header("HP ворога")]
    public Slider enemyHPSlider;
    public TMP_Text enemyHPText;
    public TMP_Text enemyShieldText; // назначить в Inspector, по аналогии с playerShieldText — раньше поля не было вообще, enemyShield (в Boss Training это щит босса) нигде не отображался

    // Слоты вместо жёсткой привязки HeroData->текст: отряд собирается игроком из
    // произвольных героев, поэтому слот просто занимает i-й герой из activeHeroes,
    // а лишние слоты (отряд меньше, чем слотов) скрываются, как и в HeroInventoryUI.PopulateSkillSelectors.
    [Header("Ресурсы героев")]
    public TMP_Text[] heroResourceSlots;

    [Header("Лог бою (нанесений/отриманий урон)")]
    public TMP_Text battleLogText; // назначить в Inspector — отдельный текстовый блок где-то на экране боя
    private const int MaxLogLines = 8;
    private readonly List<string> logLines = new List<string>();

    // Счётчик оставшихся кликов на кнопках скиллов Boss Training (правый верхний угол кнопки) — см. BuildBossTrainingSkillBar/RefreshBossTrainingSkillCounters.
    private readonly Dictionary<BossTrainingSkillData, TMP_Text> bossTrainingSkillUsesTexts = new Dictionary<BossTrainingSkillData, TMP_Text>();

    private void Start()
    {
        if (battleManager == null)
            battleManager = FindAnyObjectByType<BattleManager>();

        battleManager.OnStateChanged += RefreshUI;
        battleManager.OnBattleLog += AppendLog;
        battleManager.OnEnemyDamaged += HandleEnemyDamaged;
if (enemyPortrait != null && battleManager.currentEnemy != null && battleManager.currentEnemy.portrait != null)
    enemyPortrait.sprite = battleManager.currentEnemy.portrait;

        EnsureEnemyShieldText();

        if (battleManager.isBossTraining)
            BuildBossTrainingSkillBar();

        RefreshUI();
    }

    // Текст щита врага/босса — раньше такого поля не было вообще нигде в проекте (playerShieldText есть,
    // а для enemyShield — нет), хотя сам enemyShield полноценно работает в BattleManager.DealDamageToEnemy
    // (в т.ч. в Boss Training, где "враг" — это HP-пул игрока-босса). Строим рядом с enemyHPText, если не
    // назначено вручную в Inspector — тот же EnsureExtraUI-паттерн, что и у панели скиллов босса.
    private void EnsureEnemyShieldText()
    {
        if (enemyShieldText != null || enemyHPText == null) return;

        var srcRect = (RectTransform)enemyHPText.transform;
        var obj = new GameObject("EnemyShieldText", typeof(RectTransform));
        var rect = (RectTransform)obj.transform;
        rect.SetParent(srcRect.parent, false);
        rect.anchorMin = srcRect.anchorMin;
        rect.anchorMax = srcRect.anchorMax;
        rect.pivot = srcRect.pivot;
        rect.sizeDelta = srcRect.sizeDelta;
        rect.anchoredPosition = srcRect.anchoredPosition + new Vector2(0, -srcRect.sizeDelta.y - 4);

        var text = obj.AddComponent<TextMeshProUGUI>();
        text.fontSize = enemyHPText.fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = enemyHPText.color;

        enemyShieldText = text;
    }

    // Панель кнопок скиллов босса — только в Boss Training (см. BattleManager.isBossTraining/UseBossTrainingSkill).
    // Строится на лету, как и остальной рантайм-UI в проекте (EnsureExtraUI-паттерн ItemDetailUI и т.п.).
    private void BuildBossTrainingSkillBar()
    {
        if (battleManager.bossTrainingSkillKit == null || battleManager.bossTrainingSkillKit.Length == 0)
        {
            Debug.LogWarning("BattleUI: isBossTraining=true, но BattleManager.bossTrainingSkillKit пуст — " +
                "назначь 5 ассетов из Assets/BossTraining в Inspector на BattleManager. Без этого игрок не может " +
                "использовать скиллы босса, бой идёт как автобой.");
            return;
        }

        // Ни GetComponentInParent (BattleUIController — сиблинг Canvas, не потомок), ни голый
        // FindAnyObjectByType<Canvas>() (в сцене может быть больше одного Canvas — нашёл не тот, отсюда
        // была неправильная позиция) не годятся. Берём Canvas от enemyPortrait — он гарантированно тот самый,
        // раз сам корректно отображается.
        var canvas = enemyPortrait != null ? enemyPortrait.GetComponentInParent<Canvas>() : null;
        Transform root = canvas != null ? canvas.transform : transform;

        var barObj = new GameObject("BossTrainingSkillBar", typeof(RectTransform));
        var barRect = (RectTransform)barObj.transform;
        barRect.SetParent(root, false);
        barRect.anchorMin = new Vector2(0.5f, 0.5f);
        barRect.anchorMax = new Vector2(0.5f, 0.5f);
        barRect.pivot = new Vector2(0.5f, 0.5f);
        barRect.sizeDelta = new Vector2(battleManager.bossTrainingSkillKit.Length * 212, 160);
        barRect.anchoredPosition = new Vector2(-275, -660);

        var layout = barObj.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        foreach (var skill in battleManager.bossTrainingSkillKit)
        {
            if (skill == null) continue;

            var btnObj = new GameObject(skill.skillName, typeof(RectTransform));
            var btnRect = (RectTransform)btnObj.transform;
            btnRect.SetParent(barRect, false);
            var le = btnObj.AddComponent<LayoutElement>();
            le.preferredWidth = 200;
            le.preferredHeight = 140;

            var bg = btnObj.AddComponent<Image>();
            ConfirmationDialog.StyleAsButton(bg);
            var btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(() => battleManager.UseBossTrainingSkill(skill));

            var textObj = new GameObject("Text", typeof(RectTransform));
            var textRect = (RectTransform)textObj.transform;
            textRect.SetParent(btnRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = skill.skillName;
            text.fontSize = 36;
            text.alignment = TextAlignmentOptions.Center;
            text.color = ConfirmationDialog.ButtonTextColor;

            if (skill.usesPerTraining > 0)
            {
                var counterObj = new GameObject("UsesCounter", typeof(RectTransform));
                var counterRect = (RectTransform)counterObj.transform;
                counterRect.SetParent(btnRect, false);
                counterRect.anchorMin = new Vector2(1, 1);
                counterRect.anchorMax = new Vector2(1, 1);
                counterRect.pivot = new Vector2(1, 1);
                counterRect.sizeDelta = new Vector2(40, 40);
                counterRect.anchoredPosition = new Vector2(-2, -2);

                var counterBg = counterObj.AddComponent<Image>();
                counterBg.color = new Color(0f, 0f, 0f, 0.65f);

                var counterTextObj = new GameObject("Text", typeof(RectTransform));
                var counterTextRect = (RectTransform)counterTextObj.transform;
                counterTextRect.SetParent(counterRect, false);
                counterTextRect.anchorMin = Vector2.zero;
                counterTextRect.anchorMax = Vector2.one;
                counterTextRect.offsetMin = Vector2.zero;
                counterTextRect.offsetMax = Vector2.zero;
                var counterText = counterTextObj.AddComponent<TextMeshProUGUI>();
                counterText.fontSize = 36;
                counterText.alignment = TextAlignmentOptions.Center;
                counterText.color = ConfirmationDialog.ButtonTextColor;
                counterText.text = skill.usesPerTraining.ToString();

                bossTrainingSkillUsesTexts[skill] = counterText;
            }
        }
    }

    private void RefreshBossTrainingSkillCounters()
    {
        foreach (var kvp in bossTrainingSkillUsesTexts)
        {
            int remaining = battleManager.GetBossTrainingSkillUsesRemaining(kvp.Key);
            if (remaining >= 0)
                kvp.Value.text = remaining.ToString();
        }
    }

    private void OnDestroy()
    {
        if (battleManager != null)
        {
            battleManager.OnStateChanged -= RefreshUI;
            battleManager.OnBattleLog -= AppendLog;
            battleManager.OnEnemyDamaged -= HandleEnemyDamaged;
        }
    }

    private void HandleEnemyDamaged(int amount)
    {
        if (enemyPortrait != null)
            FloatingDamageText.Spawn((RectTransform)enemyPortrait.transform, amount, FloatingDamageText.EnemyDamageColor);
    }

    // Новая строка добавляется сверху, старые за пределами лимита отбрасываются снизу
    private void AppendLog(string message)
    {
        logLines.Insert(0, message);
        if (logLines.Count > MaxLogLines)
            logLines.RemoveAt(logLines.Count - 1);

        if (battleLogText != null)
            battleLogText.text = string.Join("\n", logLines);
    }

    private void RefreshUI()
    {
        playerHPSlider.maxValue = battleManager.playerMaxHP;
        playerHPSlider.value = battleManager.playerHP;
        playerHPText.text = $"{battleManager.playerHP} / {battleManager.playerMaxHP}";
        playerShieldText.text = battleManager.playerShield > 0 ? $"Shield: {battleManager.playerShield}" : "";

        enemyHPSlider.maxValue = battleManager.enemyMaxHP;
        enemyHPSlider.value = battleManager.enemyHP;
        enemyHPText.text = $"{battleManager.enemyHP} / {battleManager.enemyMaxHP}";
        if (enemyShieldText != null)
            enemyShieldText.text = battleManager.enemyShield > 0 ? $"Shield: {battleManager.enemyShield}" : "";

        if (battleManager.isBossTraining)
            RefreshBossTrainingSkillCounters();

        for (int i = 0; i < heroResourceSlots.Length; i++)
        {
            if (heroResourceSlots[i] == null) continue;

            bool hasHero = i < battleManager.activeHeroes.Count;
            heroResourceSlots[i].gameObject.SetActive(hasHero);
            if (hasHero)
            {
                HeroRuntimeState state = battleManager.activeHeroes[i];
                heroResourceSlots[i].text = $"{state.currentResource} / {state.maxResource}";
            }
        }
    }
}