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
            battleManager.OnStateChanged -= RefreshCard;
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
    }

    private void OnActivateClicked()
    {
        if (primarySkill == null || heroState == null || battleManager == null) return;
        if (heroState.currentHealth <= 0) return; // мёртвый герой не может использовать навыки

        bool success = battleManager.TryUseSkill(heroState, primarySkill);
        if (!success)
            Debug.Log($"Недостаточно ресурса для навыка {primarySkill.skillName}");
    }
}
