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
    private EnemySpriteAnimator enemyAnimator;

    [Header("HP ворога")]
    public Slider enemyHPSlider;
    public TMP_Text enemyHPText;
    public TMP_Text enemyShieldText; // назначить в Inspector, по аналогии с playerShieldText — раньше поля не было вообще, enemyShield (в Boss Training это щит босса) нигде не отображался

    [Header("Статус-иконки врага (правый нижний угол портрета)")]
    public Sprite enemyStunIconSprite;
    public Sprite enemyShieldIconSprite;
    public Sprite enemyDamageBuffIconSprite;
    public Sprite enemyDamageDebuffIconSprite;
    private Image enemyStunIcon;
    private Image enemyShieldIcon;
    private Image enemyDamageBuffIcon;
    private Image enemyDamageDebuffIcon;

    [Header("Картинки для всплывающей анимации врага (отдельные от угловых иконок)")]
    public Sprite enemyStunPopupSprite;
    public Sprite enemyShieldPopupSprite;
    public Sprite enemyDamageBuffPopupSprite;
    public Sprite enemyDamageDebuffPopupSprite;

    [Header("Размер всплывающей картинки врага (px)")]
    public float enemyStunPopupSize = 48f;
    public float enemyShieldPopupSize = 48f;
    public float enemyDamageBuffPopupSize = 48f;
    public float enemyDamageDebuffPopupSize = 48f;

    [Header("Звук статус-эффектов врага (необязательно, на каждый свой)")]
    public AudioClip enemyStunSound;
    public AudioClip enemyShieldSound;
    public AudioClip enemyDamageBuffSound;
    public AudioClip enemyDamageDebuffSound;

    private bool wasEnemyStunned;
    private bool wasEnemyShielded;
    private bool wasEnemyDamageBuffed;
    private bool wasEnemyDamageDebuffed;
    private bool wasEnemyMarkedForDamage;

    // Delayed Damage Mark (Драконы T2, SkillEffectType.DelayedDamageMark) — иконка status_dot.png,
    // грузится Resources.Load (не Inspector-поле, как остальные 4 — см. EnsureEnemyStatusIcons), потому
    // что это новая иконка добавляется чисто кодом, без ручной правки сцены.
    private Image enemyDelayedDamageIcon;

    // Таймер-бейджи поверх всех 5 иконок врага — тот же паттерн, что и у HeroCardUI (см. StatusBadge там),
    // размер/шрифт считаются от размера самой иконки (см. CreateBadge).
    private class StatusBadge
    {
        public GameObject root;
        public TMP_Text text;
    }

    private StatusBadge enemyStunBadge, enemyShieldBadge, enemyDamageBuffBadge, enemyDamageDebuffBadge, enemyDelayedDamageBadge;

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
        battleManager.OnEnemyAttackAnimation += HandleEnemyAttackAnimation;

        if (enemyPortrait != null)
        {
            enemyAnimator = enemyPortrait.gameObject.AddComponent<EnemySpriteAnimator>();
            enemyAnimator.target = enemyPortrait;
            enemyAnimator.SetEnemy(battleManager.currentEnemy);
        }

        EnsureEnemyShieldText();
        EnsureEnemyStatusIcons();

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

    // Ряд статус-иконок в правом нижнем углу портрета врага — тот же EnsureExtraUI-паттерн, что и у enemyShieldText,
    // потому что в сцене этот элемент никогда не размещался вручную.
    private void EnsureEnemyStatusIcons()
    {
        if (enemyPortrait == null) return;

        var container = new GameObject("EnemyStatusIcons", typeof(RectTransform));
        var containerRect = (RectTransform)container.transform;
        containerRect.SetParent(enemyPortrait.transform, false);
        containerRect.anchorMin = new Vector2(1, 0);
        containerRect.anchorMax = new Vector2(1, 0);
        containerRect.pivot = new Vector2(1, 0);
        containerRect.anchoredPosition = new Vector2(-4, 4);
        containerRect.sizeDelta = new Vector2(100, 18);

        var layout = container.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.spacing = 2;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        enemyStunIcon = CreateStatusIcon(containerRect, "StunIcon", enemyStunIconSprite);
        enemyShieldIcon = CreateStatusIcon(containerRect, "ShieldIcon", enemyShieldIconSprite);
        enemyDamageBuffIcon = CreateStatusIcon(containerRect, "DamageBuffIcon", enemyDamageBuffIconSprite);
        enemyDamageDebuffIcon = CreateStatusIcon(containerRect, "DamageDebuffIcon", enemyDamageDebuffIconSprite);
        // Resources.Load, а не Inspector-поле как остальные 4 — добавлена чисто кодом, без правки сцены.
        enemyDelayedDamageIcon = CreateStatusIcon(containerRect, "DelayedDamageIcon", Resources.Load<Sprite>("UI/StatusIcons/status_dot"));

        enemyStunBadge = CreateBadge(enemyStunIcon);
        enemyShieldBadge = CreateBadge(enemyShieldIcon);
        enemyDamageBuffBadge = CreateBadge(enemyDamageBuffIcon);
        enemyDamageDebuffBadge = CreateBadge(enemyDamageDebuffIcon);
        enemyDelayedDamageBadge = CreateBadge(enemyDelayedDamageIcon);
    }

    private static Image CreateStatusIcon(RectTransform parent, string name, Sprite sprite)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(18, 18);

        var image = obj.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;

        return image;
    }

    // Маленький тёмный кружок-бейдж с числом в углу иконки — сидит НАД углом, а не внутри (иконки врага
    // всего 18x18, число внутри физически не влезло бы). Тот же паттерн, что и HeroCardUI.CreateBadge.
    // Правый ВЕРХНИЙ угол (было — нижний), размер/шрифт от реального размера иконки — тот же приём,
    // что и в HeroCardUI.CreateBadge.
    private static StatusBadge CreateBadge(Image icon)
    {
        if (icon == null) return null;

        float iconSize = Mathf.Max(icon.rectTransform.sizeDelta.x, icon.rectTransform.sizeDelta.y);
        float badgeSize = Mathf.Max(22f, iconSize * 0.55f);

        var rootObj = new GameObject("Badge", typeof(RectTransform));
        var rootRect = (RectTransform)rootObj.transform;
        rootRect.SetParent(icon.transform, false);
        rootRect.anchorMin = new Vector2(1, 1);
        rootRect.anchorMax = new Vector2(1, 1);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(badgeSize, badgeSize);
        rootRect.anchoredPosition = Vector2.zero;

        var bg = rootObj.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.8f);
        bg.raycastTarget = false;

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(rootRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = badgeSize * 0.65f;
        text.color = Color.white;
        text.raycastTarget = false;

        rootObj.SetActive(false);
        return new StatusBadge { root = rootObj, text = text };
    }

    private static void SetStatusBadge(StatusBadge badge, int value)
    {
        if (badge?.root == null) return;

        bool show = value > 0;
        badge.root.SetActive(show);
        if (show) badge.text.text = value.ToString();
    }

    private void RefreshEnemyStatusIcons()
    {
        if (battleManager == null) return;

        bool isStunned = battleManager.enemyStunnedNextTurn;
        bool isShielded = battleManager.enemyShield > 0;
        bool isDamageBuffed = battleManager.enemyDamageMultiplierTurnsRemaining > 0 && battleManager.enemyDamageMultiplier > 1f;
        bool isDamageDebuffed = battleManager.enemyDamageMultiplierTurnsRemaining > 0 && battleManager.enemyDamageMultiplier < 1f;
        int soonestMarkTurns = battleManager.GetSoonestPendingDamageTurns();
        bool isMarkedForDamage = soonestMarkTurns > 0;

        if (enemyStunIcon != null) enemyStunIcon.gameObject.SetActive(isStunned);
        if (enemyShieldIcon != null) enemyShieldIcon.gameObject.SetActive(isShielded);
        if (enemyDamageBuffIcon != null) enemyDamageBuffIcon.gameObject.SetActive(isDamageBuffed);
        if (enemyDamageDebuffIcon != null) enemyDamageDebuffIcon.gameObject.SetActive(isDamageDebuffed);
        if (enemyDelayedDamageIcon != null) enemyDelayedDamageIcon.gameObject.SetActive(isMarkedForDamage);

        // Бейджи — у Stun своего счётчика нет (enemyStunnedNextTurn — bool, ровно 1 ход), у Shield нет
        // фиксированной длительности (показываем объём щита), у Delayed Damage Mark — ходы до ближайшего взрыва.
        SetStatusBadge(enemyStunBadge, isStunned ? 1 : 0);
        SetStatusBadge(enemyShieldBadge, battleManager.enemyShield);
        SetStatusBadge(enemyDamageBuffBadge, isDamageBuffed ? battleManager.enemyDamageMultiplierTurnsRemaining : 0);
        SetStatusBadge(enemyDamageDebuffBadge, isDamageDebuffed ? battleManager.enemyDamageMultiplierTurnsRemaining : 0);
        SetStatusBadge(enemyDelayedDamageBadge, soonestMarkTurns);

        // Всплывающая иконка + звук — только в момент, когда эффект только что стал активным
        if (isStunned && !wasEnemyStunned) SpawnEnemyStatusPopup(enemyStunPopupSprite, enemyStunSound, enemyStunPopupSize);
        if (isShielded && !wasEnemyShielded) SpawnEnemyStatusPopup(enemyShieldPopupSprite, enemyShieldSound, enemyShieldPopupSize);
        if (isDamageBuffed && !wasEnemyDamageBuffed) SpawnEnemyStatusPopup(enemyDamageBuffPopupSprite, enemyDamageBuffSound, enemyDamageBuffPopupSize);
        if (isDamageDebuffed && !wasEnemyDamageDebuffed) SpawnEnemyStatusPopup(enemyDamageDebuffPopupSprite, enemyDamageDebuffSound, enemyDamageDebuffPopupSize);
        if (isMarkedForDamage && !wasEnemyMarkedForDamage && enemyDelayedDamageIcon != null)
            SpawnEnemyStatusPopup(enemyDelayedDamageIcon.sprite, null, enemyDelayedDamageIcon.rectTransform.sizeDelta.x);

        wasEnemyStunned = isStunned;
        wasEnemyShielded = isShielded;
        wasEnemyDamageBuffed = isDamageBuffed;
        wasEnemyDamageDebuffed = isDamageDebuffed;
        wasEnemyMarkedForDamage = isMarkedForDamage;
    }

    private void SpawnEnemyStatusPopup(Sprite sprite, AudioClip sound, float size)
    {
        if (enemyPortrait == null) return;
        FloatingStatusIcon.Spawn((RectTransform)enemyPortrait.transform, sprite, sound, size);
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

        enemyAnimator?.PlayHitReaction();
    }

    private void HandleEnemyAttackAnimation()
    {
        enemyAnimator?.PlayAttack();
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
        playerHPSlider.maxValue = battleManager.TotalHeroMaxHealth;
        playerHPSlider.value = battleManager.TotalHeroHealth;
        playerHPText.text = $"{battleManager.TotalHeroHealth} / {battleManager.TotalHeroMaxHealth}";
        playerShieldText.text = battleManager.playerShield > 0 ? $"Shield: {battleManager.playerShield}" : "";

        enemyHPSlider.maxValue = battleManager.enemyMaxHP;
        enemyHPSlider.value = battleManager.enemyHP;
        enemyHPText.text = $"{battleManager.enemyHP} / {battleManager.enemyMaxHP}";
        if (enemyShieldText != null)
            enemyShieldText.text = battleManager.enemyShield > 0 ? $"Shield: {battleManager.enemyShield}" : "";

        RefreshEnemyStatusIcons();

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