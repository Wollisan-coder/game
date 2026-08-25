using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HeroCardUI : MonoBehaviour
{
    [Header("Данные персонажа")]
    public HeroData heroData;

    [Header("Ссылки")]
    public BattleManager battleManager;

    [Header("UI элементы")]
    public Image portraitImage;
    public Image fillImage;       // мана
    public Image healthFillImage; // HP — отдельно от маны
    public Image shieldFillImage; // щит — % от максимального HP героя
    public TMP_Text healthText;   // числовое значение "текущее/максимальное" HP поверх полоски
    public Button activateButton; // если у героя один основной навык (skills[0])
    public Image buttonOverlay;

    [Header("Статус-иконки (правый нижний угол карточки)")]
    public Image stunStatusIcon;
    public Image shieldStatusIcon;
    public Image damageBuffStatusIcon;
    public Image damageDebuffStatusIcon;
    public Image invulnerabilityStatusIcon;

    [Header("Картинки для всплывающей анимации (отдельные от угловых иконок)")]
    public Sprite stunPopupSprite;
    public Sprite shieldPopupSprite;
    public Sprite damageBuffPopupSprite;
    public Sprite damageDebuffPopupSprite;
    public Sprite invulnerabilityPopupSprite;

    [Header("Размер всплывающей картинки (px)")]
    public float stunPopupSize = 48f;
    public float shieldPopupSize = 48f;
    public float damageBuffPopupSize = 48f;
    public float damageDebuffPopupSize = 48f;
    public float invulnerabilityPopupSize = 48f;

    [Header("Звук статус-эффектов (необязательно, на каждый свой)")]
    public AudioClip stunSound;
    public AudioClip shieldSound;
    public AudioClip damageBuffSound;
    public AudioClip damageDebuffSound;
    public AudioClip invulnerabilitySound;

    private bool wasStunned;
    private bool wasShielded;
    private bool wasDamageBuffed;
    private bool wasDamageDebuffed;
    private bool wasInvulnerable;
    private bool wasSkillBlocked;

    // Skill-Blocked — 6я статус-иконка, добавлена в рантайме (не в префабе — HorizontalLayoutGroup-контейнер
    // уже есть вокруг stunStatusIcon и т.д., просто дописываем в него ещё один child, см. EnsureStatusExtras).
    private Image skillBlockedStatusIcon;

    // Таймер-бейджи поверх всех 6 иконок — тоже строятся в рантайме, тёмный кружок в правом верхнем углу
    // иконки с числом (ходы для большинства статусов, количество для Shield — у него нет фиксированной
    // длительности, показываем текущий объём щита). Размер/шрифт считаются от размера самой иконки
    // (см. CreateBadge) — если размер иконки в префабе поменяют, бейдж подстроится сам.
    private class StatusBadge
    {
        public GameObject root;
        public TMP_Text text;
    }

    private StatusBadge stunBadge, shieldBadge, damageBuffBadge, damageDebuffBadge, invulnerabilityBadge, skillBlockedBadge;
    private bool statusExtrasBuilt;

    [Header("Состояние гибели")]
    public Color deadTintColor = new Color(0.25f, 0.25f, 0.25f, 1f);

    [Header("Мигание кнопки активации")]
    public float buttonMinAlpha = 0.05f;
    public float buttonMaxAlpha = 0.35f;
    public float buttonPulseSpeed = 2f;

    [Header("Прозрачность маны в зависимости от заполнения")]
    public float manaMinAlpha = 0.15f;   // прозрачность, когда мана пуста
    public float manaMaxAlpha = 0.6f;    // пиковая прозрачность, когда мана полна (без мерцания)

    private HeroRuntimeState heroState;
    private SkillData primarySkill;

    private void Start()
    {
        if (battleManager == null)
            battleManager = FindAnyObjectByType<BattleManager>();

        ApplyHeroData();

        battleManager.OnStateChanged += RefreshCard;
        battleManager.OnHeroDamaged += HandleHeroDamaged;

        if (activateButton != null)
        {
            activateButton.onClick.RemoveAllListeners();
            activateButton.onClick.AddListener(OnActivateClicked);
        }

        RefreshCard();
    }

    private void OnDestroy()
    {
        if (battleManager != null)
        {
            battleManager.OnStateChanged -= RefreshCard;
            battleManager.OnHeroDamaged -= HandleHeroDamaged;
        }
    }

    private void HandleHeroDamaged(HeroRuntimeState damagedHero, int amount)
    {
        if (damagedHero == heroState)
            FloatingDamageText.Spawn((RectTransform)transform, amount, FloatingDamageText.HeroDamageColor);
    }

    private void Update()
    {
        if (heroState != null && heroState.currentHealth <= 0) return; // мёртвый герой — без анимаций

        UpdateButtonPulse();
        UpdateManaFill();
    }

    private void UpdateButtonPulse()
    {
        if (buttonOverlay == null || heroState == null) return;

        bool isManaFull = heroState.currentResource >= heroState.maxResource;

        if (isManaFull)
        {
            // мерцание только когда мана полна
            float t = (Mathf.Sin(Time.time * buttonPulseSpeed) + 1f) / 2f;
            float alpha = Mathf.Lerp(buttonMinAlpha, buttonMaxAlpha, t);

            Color c = buttonOverlay.color;
            c.a = alpha;
            buttonOverlay.color = c;
        }
        else
        {
            // статично, без мерцания, пока мана не полна
            Color c = buttonOverlay.color;
            c.a = buttonMinAlpha;
            buttonOverlay.color = c;
        }
    }

    private void UpdateManaFill()
    {
        if (fillImage == null || heroState == null) return;

        float fillRatio = heroState.maxResource > 0
            ? (float)heroState.currentResource / heroState.maxResource
            : 0f;

        // Без мигания: альфа линейно растёт вместе с заполнением маны,
        // от manaMinAlpha (пусто) до manaMaxAlpha (полная мана)
        float alpha = Mathf.Lerp(manaMinAlpha, manaMaxAlpha, fillRatio);

        Color c = fillImage.color;
        c.a = alpha;
        fillImage.color = c;
    }

    // Берётся навык, выбранный игроком как активный в окне инвентаря (по умолчанию — первый)
    private SkillData ResolveActiveSkill()
    {
        if (heroData.skills == null || heroData.skills.Length == 0) return null;

        // Death Dungeon могла переопределить выбор игрока НА ЭТОТ БОЙ (см. BattleManager.
        // EnsureUsableActiveSkills) — сохранённый ownership.activeSkillIndex ниже тут не трогаем.
        if (heroState != null && heroState.effectiveActiveSkillOverride != null)
            return heroState.effectiveActiveSkillOverride;

        int activeIndex = 0;
        if (HeroCollectionManager.Instance != null)
        {
            var ownership = HeroCollectionManager.Instance.ownership.Find(o => o.heroId == heroData.heroId);
            if (ownership != null)
                activeIndex = Mathf.Clamp(ownership.activeSkillIndex, 0, heroData.skills.Length - 1);
        }

        return heroData.skills[activeIndex];
    }

    private void ApplyHeroData()
    {
        if (heroData == null)
        {
            Debug.LogWarning($"HeroCardUI на {gameObject.name}: HeroData не назначено!");
            return;
        }

        heroState = battleManager.GetHeroState(heroData);
        primarySkill = ResolveActiveSkill();

        if (portraitImage != null && heroData.portrait != null)
            portraitImage.sprite = heroData.portrait;

        if (fillImage != null)
        {
            Color c = heroData.themeColor;
            c.a = manaMinAlpha; // стартовая альфа — сразу едва заметна, а не сплошной цвет
            fillImage.color = c;
        }

        if (buttonOverlay != null)
            buttonOverlay.color = heroData.themeColor;
    }

    private void RefreshCard()
    {
        if (heroState == null) return;

        bool isDead = heroState.currentHealth <= 0;

        if (fillImage != null)
            fillImage.fillAmount = heroState.maxResource > 0
                ? (float)heroState.currentResource / heroState.maxResource
                : 0f;

        if (healthFillImage != null)
            healthFillImage.fillAmount = heroState.maxHealth > 0
                ? (float)heroState.currentHealth / heroState.maxHealth
                : 0f;

        if (healthText != null)
            healthText.text = $"{heroState.currentHealth}/{heroState.maxHealth}";

        if (shieldFillImage != null && battleManager != null)
            shieldFillImage.fillAmount = heroState.maxHealth > 0
                ? Mathf.Clamp01((float)battleManager.playerShield / heroState.maxHealth)
                : 0f;

        if (activateButton != null && primarySkill != null)
        {
            // Реальная стоимость (со скидкой от ReduceAllyNextSkillCost), а не полная — иначе кнопка
            // может остаться недоступной, хотя BattleManager.TryUseSkill уже разрешил бы каст по скидке.
            int actualCost = Mathf.RoundToInt(primarySkill.cost * (1f - heroState.costReductionPercent));
            // Тот же гейт, что и TryUseSkill (skillsCastSinceLastRealTurn) — иначе кнопка выглядит нажимаемой,
            // но тап по уже скастованному в этом окне скиллу молча ничего не делает (найдено на аудите 2026-08-20).
            bool alreadyCastThisWindow = heroState.skillsCastSinceLastRealTurn.Contains(primarySkill);
            activateButton.interactable = !isDead && !alreadyCastThisWindow && heroState.currentResource >= actualCost;
        }

        if (portraitImage != null)
            portraitImage.color = isDead ? deadTintColor : Color.white;

        RefreshStatusIcons();
    }

    // Личный статус героя (оглушение) + командные статусы, действующие сейчас на всю команду
    private void RefreshStatusIcons()
    {
        if (battleManager == null) return;

        EnsureStatusExtras();

        bool isStunned = heroState.stunnedTurnsRemaining > 0;
        bool isShielded = battleManager.playerShield > 0;
        bool isDamageBuffed = battleManager.damageMultiplierTurnsRemaining > 0 && battleManager.damageMultiplier > 1f;
        bool isDamageDebuffed = battleManager.heroDamageMultiplierTurnsRemaining > 0 && battleManager.heroDamageMultiplier < 1f;
        bool isInvulnerable = battleManager.playerInvulnerableNextEnemyTurn || battleManager.teamDebuffImmuneTurnsRemaining > 0;
        bool isSkillBlocked = heroState.skillBlockedTurnsRemaining > 0;

        SetStatusIconActive(stunStatusIcon, isStunned);
        SetStatusIconActive(shieldStatusIcon, isShielded);
        SetStatusIconActive(damageBuffStatusIcon, isDamageBuffed);
        SetStatusIconActive(damageDebuffStatusIcon, isDamageDebuffed);
        SetStatusIconActive(invulnerabilityStatusIcon, isInvulnerable);
        SetStatusIconActive(skillBlockedStatusIcon, isSkillBlocked);

        // Бейджи — turns remaining для большинства; у Shield нет фиксированной длительности, показываем
        // текущий объём щита вместо ходов; у Invulnerability — teamDebuffImmuneTurnsRemaining, если он
        // активен, иначе просто "1" (playerInvulnerableNextEnemyTurn — ровно один следующий вражеский ход).
        SetStatusBadge(stunBadge, heroState.stunnedTurnsRemaining);
        SetStatusBadge(shieldBadge, battleManager.playerShield);
        SetStatusBadge(damageBuffBadge, battleManager.damageMultiplierTurnsRemaining);
        SetStatusBadge(damageDebuffBadge, battleManager.heroDamageMultiplierTurnsRemaining);
        SetStatusBadge(invulnerabilityBadge, battleManager.teamDebuffImmuneTurnsRemaining > 0
            ? battleManager.teamDebuffImmuneTurnsRemaining
            : (battleManager.playerInvulnerableNextEnemyTurn ? 1 : 0));
        SetStatusBadge(skillBlockedBadge, heroState.skillBlockedTurnsRemaining);

        // Всплывающая иконка + звук — только в момент, когда эффект только что стал активным (было false, стало true)
        if (isStunned && !wasStunned) FloatingStatusIcon.Spawn((RectTransform)transform, stunPopupSprite, stunSound, stunPopupSize);
        if (isShielded && !wasShielded) FloatingStatusIcon.Spawn((RectTransform)transform, shieldPopupSprite, shieldSound, shieldPopupSize);
        if (isDamageBuffed && !wasDamageBuffed) FloatingStatusIcon.Spawn((RectTransform)transform, damageBuffPopupSprite, damageBuffSound, damageBuffPopupSize);
        if (isDamageDebuffed && !wasDamageDebuffed) FloatingStatusIcon.Spawn((RectTransform)transform, damageDebuffPopupSprite, damageDebuffSound, damageDebuffPopupSize);
        if (isInvulnerable && !wasInvulnerable) FloatingStatusIcon.Spawn((RectTransform)transform, invulnerabilityPopupSprite, invulnerabilitySound, invulnerabilityPopupSize);
        if (isSkillBlocked && !wasSkillBlocked && skillBlockedStatusIcon != null)
            FloatingStatusIcon.Spawn((RectTransform)transform, skillBlockedStatusIcon.sprite, null, skillBlockedStatusIcon.rectTransform.sizeDelta.x);

        wasStunned = isStunned;
        wasShielded = isShielded;
        wasDamageBuffed = isDamageBuffed;
        wasDamageDebuffed = isDamageDebuffed;
        wasInvulnerable = isInvulnerable;
        wasSkillBlocked = isSkillBlocked;
    }

    private static void SetStatusIconActive(Image icon, bool active)
    {
        if (icon != null) icon.gameObject.SetActive(active);
    }

    private static void SetStatusBadge(StatusBadge badge, int value)
    {
        if (badge?.root == null) return;

        bool show = value > 0;
        badge.root.SetActive(show);
        if (show) badge.text.text = value.ToString();
    }

    // Skill-Blocked иконка + бейджи на всех 6 — строится один раз при первом Refresh, не в префабе.
    // stunStatusIcon.transform.parent — уже существующий HorizontalLayoutGroup-контейнер из префаба
    // (см. HeroBattleCard.prefab), новую иконку просто дописываем туда сайблингом.
    private void EnsureStatusExtras()
    {
        if (statusExtrasBuilt) return;
        statusExtrasBuilt = true;

        if (stunStatusIcon != null)
        {
            var container = stunStatusIcon.transform.parent;
            var sprite = Resources.Load<Sprite>("UI/StatusIcons/skill_blocked_icon");
            skillBlockedStatusIcon = CreateRuntimeStatusIcon(container, "SkillBlockedIcon", sprite, stunStatusIcon.rectTransform.sizeDelta);
        }

        stunBadge = CreateBadge(stunStatusIcon);
        shieldBadge = CreateBadge(shieldStatusIcon);
        damageBuffBadge = CreateBadge(damageBuffStatusIcon);
        damageDebuffBadge = CreateBadge(damageDebuffStatusIcon);
        invulnerabilityBadge = CreateBadge(invulnerabilityStatusIcon);
        skillBlockedBadge = CreateBadge(skillBlockedStatusIcon);
    }

    private static Image CreateRuntimeStatusIcon(Transform parent, string name, Sprite sprite, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = size;

        var img = obj.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;

        obj.SetActive(false);
        return img;
    }

    // Тёмный кружок-бейдж с числом в ПРАВОМ ВЕРХНЕМ углу иконки (было — правый нижний), сидит НАД углом,
    // выступая за его границы, а не внутри — размер и шрифт считаются от РЕАЛЬНОГО размера иконки
    // (icon.rectTransform.sizeDelta), а не фиксированной константой: если размер иконки в префабе
    // увеличат, бейдж вырастет вместе с ней, а не останется мелким на фоне уже подросшей иконки.
    private static StatusBadge CreateBadge(Image icon)
    {
        if (icon == null) return null;

        float iconSize = Mathf.Max(icon.rectTransform.sizeDelta.x, icon.rectTransform.sizeDelta.y);
        float badgeSize = Mathf.Max(28f, iconSize * 0.55f);

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

    private void OnActivateClicked()
    {
        if (primarySkill == null || heroState == null || battleManager == null) return;
        if (heroState.currentHealth <= 0) return; // мёртвый герой не может использовать навыки

        bool success = battleManager.TryUseSkill(heroState, primarySkill);
        if (!success)
            Debug.Log($"Недостаточно ресурса для навыка {primarySkill.skillName}");
        else
        {
            DailyQuestManager.Instance?.ReportSkillUsed(); // только реальный клик игрока — не AI-каст тренируемого героя в Boss Training (см. TryUseTrainedHeroSkillIfPossible)
            AchievementManager.Instance?.ReportSkillUsed();
        }
    }
}
