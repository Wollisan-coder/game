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
            activateButton.interactable = !isDead && heroState.currentResource >= primarySkill.cost;

        if (portraitImage != null)
            portraitImage.color = isDead ? deadTintColor : Color.white;

        RefreshStatusIcons();
    }

    // Личный статус героя (оглушение) + командные статусы, действующие сейчас на всю команду
    private void RefreshStatusIcons()
    {
        if (battleManager == null) return;

        bool isStunned = heroState.stunnedTurnsRemaining > 0;
        bool isShielded = battleManager.playerShield > 0;
        bool isDamageBuffed = battleManager.damageMultiplierTurnsRemaining > 0 && battleManager.damageMultiplier > 1f;
        bool isDamageDebuffed = battleManager.heroDamageMultiplierTurnsRemaining > 0 && battleManager.heroDamageMultiplier < 1f;
        bool isInvulnerable = battleManager.playerInvulnerableNextEnemyTurn || battleManager.teamDebuffImmuneTurnsRemaining > 0;

        SetStatusIconActive(stunStatusIcon, isStunned);
        SetStatusIconActive(shieldStatusIcon, isShielded);
        SetStatusIconActive(damageBuffStatusIcon, isDamageBuffed);
        SetStatusIconActive(damageDebuffStatusIcon, isDamageDebuffed);
        SetStatusIconActive(invulnerabilityStatusIcon, isInvulnerable);

        // Всплывающая иконка + звук — только в момент, когда эффект только что стал активным (было false, стало true)
        if (isStunned && !wasStunned) FloatingStatusIcon.Spawn((RectTransform)transform, stunPopupSprite, stunSound, stunPopupSize);
        if (isShielded && !wasShielded) FloatingStatusIcon.Spawn((RectTransform)transform, shieldPopupSprite, shieldSound, shieldPopupSize);
        if (isDamageBuffed && !wasDamageBuffed) FloatingStatusIcon.Spawn((RectTransform)transform, damageBuffPopupSprite, damageBuffSound, damageBuffPopupSize);
        if (isDamageDebuffed && !wasDamageDebuffed) FloatingStatusIcon.Spawn((RectTransform)transform, damageDebuffPopupSprite, damageDebuffSound, damageDebuffPopupSize);
        if (isInvulnerable && !wasInvulnerable) FloatingStatusIcon.Spawn((RectTransform)transform, invulnerabilityPopupSprite, invulnerabilitySound, invulnerabilityPopupSize);

        wasStunned = isStunned;
        wasShielded = isShielded;
        wasDamageBuffed = isDamageBuffed;
        wasDamageDebuffed = isDamageDebuffed;
        wasInvulnerable = isInvulnerable;
    }

    private static void SetStatusIconActive(Image icon, bool active)
    {
        if (icon != null) icon.gameObject.SetActive(active);
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
