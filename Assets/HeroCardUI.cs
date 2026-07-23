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

    [Header("Налаштування мигання")]
    public float minAlpha = 0.05f;
    public float maxAlpha = 0.35f;
    public float pulseSpeed = 2f;

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
        if (buttonOverlay == null) return;

        bool canActivate = activateButton == null || activateButton.interactable;
        float effectiveMax = canActivate ? maxAlpha : minAlpha * 0.5f;

        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;
        float alpha = Mathf.Lerp(minAlpha, effectiveMax, t);

        Color c = buttonOverlay.color;
        c.a = alpha;
        buttonOverlay.color = c;
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
            fillImage.color = heroData.themeColor;

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