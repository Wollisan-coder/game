using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HeroCardUI : MonoBehaviour
{
    [Header("Дані персонажа")]
    public HeroData heroData;

    [Header("Посилання")]
    public BattleManager battleManager;

    [Header("UI елементи")]
    public Image portraitImage;
    public Image fillImage;       // мана
    public Image healthFillImage; // HP — окремий від мани
    public Image shieldFillImage; // щит — % від максимального HP героя
    public TMP_Text healthText;   // числове значення "поточне/максимальне" HP поверх смужки
    public Button activateButton; // якщо в героя одна основна навичка (skills[0])
    public Image buttonOverlay;

    [Header("Стан загибелі")]
    public Color deadTintColor = new Color(0.25f, 0.25f, 0.25f, 1f);

    [Header("Мигання кнопки активації")]
    public float buttonMinAlpha = 0.05f;
    public float buttonMaxAlpha = 0.35f;
    public float buttonPulseSpeed = 2f;

    [Header("Прозорість мани залежно від заповнення")]
    public float manaMinAlpha = 0.15f;   // прозорість, коли мана порожня
    public float manaMaxAlpha = 0.6f;    // пікова прозорість, коли мана повна (без мерехтіння)

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
        if (heroState != null && heroState.currentHealth <= 0) return; // мертвий герой — без анімацій

        UpdateButtonPulse();
        UpdateManaFill();
    }

    private void UpdateButtonPulse()
    {
        if (buttonOverlay == null || heroState == null) return;

        bool isManaFull = heroState.currentResource >= heroState.maxResource;

        if (isManaFull)
        {
            // мерехтіння тільки коли мана повна
            float t = (Mathf.Sin(Time.time * buttonPulseSpeed) + 1f) / 2f;
            float alpha = Mathf.Lerp(buttonMinAlpha, buttonMaxAlpha, t);

            Color c = buttonOverlay.color;
            c.a = alpha;
            buttonOverlay.color = c;
        }
        else
        {
            // статично, без мерехтіння, поки мана не повна
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

        // Без миготіння: альфа лінійно росте разом із заповненням мани,
        // від manaMinAlpha (порожньо) до manaMaxAlpha (повна мана)
        float alpha = Mathf.Lerp(manaMinAlpha, manaMaxAlpha, fillRatio);

        Color c = fillImage.color;
        c.a = alpha;
        fillImage.color = c;
    }

    // Береться навичка, обрана гравцем як активна у вікні інвентаря (за замовчуванням — перша)
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
            Debug.LogWarning($"HeroCardUI на {gameObject.name}: HeroData не призначено!");
            return;
        }

        heroState = battleManager.GetHeroState(heroData);
        primarySkill = ResolveActiveSkill();

        if (portraitImage != null && heroData.portrait != null)
            portraitImage.sprite = heroData.portrait;

        if (fillImage != null)
        {
            Color c = heroData.themeColor;
            c.a = manaMinAlpha; // стартова альфа — одразу ледь помітна, а не суцільний колір
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
        if (heroState.currentHealth <= 0) return; // мертвий герой не може використовувати навички

        bool success = battleManager.TryUseSkill(heroState, primarySkill);
        if (!success)
            Debug.Log($"Недостатньо ресурсу для навички {primarySkill.skillName}");
    }
}