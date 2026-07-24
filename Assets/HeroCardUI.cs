using UnityEngine;
using UnityEngine.UI;

public class HeroCardUI : MonoBehaviour
{
    [Header("Дані персонажа")]
    public HeroData heroData;

    [Header("Посилання")]
    public BattleManager battleManager;

    [Header("UI елементи")]
    public Image portraitImage;
    public Image fillImage;
    public Button activateButton; // якщо в героя одна основна навичка (skills[0])
    public Image buttonOverlay;

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
        UpdateButtonPulse();
        UpdateManaFill();
    }

    private void UpdateButtonPulse()
    {
        if (buttonOverlay == null || heroState == null) return;

        bool isManaFull = heroState.currentResource >= heroState.data.maxResource;

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

        float fillRatio = heroState.data.maxResource > 0
            ? (float)heroState.currentResource / heroState.data.maxResource
            : 0f;

        // Без миготіння: альфа лінійно росте разом із заповненням мани,
        // від manaMinAlpha (порожньо) до manaMaxAlpha (повна мана)
        float alpha = Mathf.Lerp(manaMinAlpha, manaMaxAlpha, fillRatio);

        Color c = fillImage.color;
        c.a = alpha;
        fillImage.color = c;
    }

    private void ApplyHeroData()
    {
        if (heroData == null) 
        {
            Debug.LogWarning($"HeroCardUI на {gameObject.name}: HeroData не призначено!");
            return;
        }

        heroState = battleManager.GetHeroState(heroData);
        primarySkill = (heroData.skills != null && heroData.skills.Length > 0) ? heroData.skills[0] : null;

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
        if (fillImage == null || heroState == null) return;

        fillImage.fillAmount = heroState.data.maxResource > 0
            ? (float)heroState.currentResource / heroState.data.maxResource
            : 0f;

        if (activateButton != null && primarySkill != null)
            activateButton.interactable = heroState.currentResource >= primarySkill.cost;
    }

    private void OnActivateClicked()
    {
        if (primarySkill == null || heroState == null || battleManager == null) return;

        bool success = battleManager.TryUseSkill(heroState, primarySkill);
        if (!success)
            Debug.Log($"Недостатньо ресурсу для навички {primarySkill.skillName}");
    }
}