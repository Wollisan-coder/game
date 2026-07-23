using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BattleUI : MonoBehaviour
{
    [Header("Посилання")]
    public BattleManager battleManager;

    [Header("HP гравця")]
    public Slider playerHPSlider;
    public TMP_Text playerHPText;
    public TMP_Text playerShieldText;

    [Header("HP ворога")]
    public Slider enemyHPSlider;
    public TMP_Text enemyHPText;

    [System.Serializable]
    public class ResourceTextEntry
    {
        public ResourceType type;
        public TMP_Text text;
    }

    [Header("Ресурси")]
    public ResourceTextEntry[] resourceEntries;

    [Header("Навички")]
    public SkillData[] availableSkills;
    public Button[] skillButtons;
    public TMP_Text[] skillButtonLabels;

    private void Start()
    {
        if (battleManager == null)
            battleManager = FindAnyObjectByType<BattleManager>();

        battleManager.OnStateChanged += RefreshUI;

        SetupSkillButtons();
        RefreshUI();
    }

    private void OnDestroy()
    {
        if (battleManager != null)
            battleManager.OnStateChanged -= RefreshUI;
    }

    private void SetupSkillButtons()
    {
        for (int i = 0; i < skillButtons.Length; i++)
        {
            if (i >= availableSkills.Length)
            {
                skillButtons[i].gameObject.SetActive(false);
                continue;
            }

            SkillData skill = availableSkills[i];
            skillButtons[i].onClick.RemoveAllListeners();
            skillButtons[i].onClick.AddListener(() => OnSkillButtonClicked(skill));

            if (skillButtonLabels != null && i < skillButtonLabels.Length && skillButtonLabels[i] != null)
                skillButtonLabels[i].text = $"{skill.skillName}\n({skill.cost} {skill.costType})";
        }
    }

    private void OnSkillButtonClicked(SkillData skill)
    {
        bool success = battleManager.TryUseSkill(skill);
        if (!success)
            Debug.Log($"Недостатньо ресурсу для навички {skill.skillName}");
    }

    private void RefreshUI()
    {
        playerHPSlider.maxValue = battleManager.playerMaxHP;
        playerHPSlider.value = battleManager.playerHP;
        playerHPText.text = $"{battleManager.playerHP} / {battleManager.playerMaxHP}";
        playerShieldText.text = battleManager.playerShield > 0 ? $"Щит: {battleManager.playerShield}" : "";

        enemyHPSlider.maxValue = battleManager.enemyMaxHP;
        enemyHPSlider.value = battleManager.enemyHP;
        enemyHPText.text = $"{battleManager.enemyHP} / {battleManager.enemyMaxHP}";

        foreach (var entry in resourceEntries)
        {
            if (entry.text != null)
                entry.text.text = $"{entry.type}: {battleManager.resources[(int)entry.type]}";
        }

        UpdateSkillButtonsInteractable();
    }

    private void UpdateSkillButtonsInteractable()
    {
        for (int i = 0; i < skillButtons.Length && i < availableSkills.Length; i++)
        {
            SkillData skill = availableSkills[i];
            bool canAfford = battleManager.resources[(int)skill.costType] >= skill.cost;
            skillButtons[i].interactable = canAfford;
        }
    }
}