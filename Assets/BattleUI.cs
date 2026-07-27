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

    [System.Serializable]
    public class HeroResourceUIEntry
    {
        public HeroData heroData;   // який герой відповідає цьому UI-елементу
        public TMP_Text amountText; // текст з поточним значенням ресурсу
    }

    [Header("Ресурси героїв")]
    public HeroResourceUIEntry[] heroResourceEntries;

    [Header("Лог бою (нанесений/отриманий урон)")]
    public TMP_Text battleLogText; // призначити в Inspector — окремий текстовий блок десь на екрані бою
    private const int MaxLogLines = 8;
    private readonly List<string> logLines = new List<string>();

    private void Start()
    {
        if (battleManager == null)
            battleManager = FindAnyObjectByType<BattleManager>();

        battleManager.OnStateChanged += RefreshUI;
        battleManager.OnBattleLog += AppendLog;
if (enemyPortrait != null && battleManager.currentEnemy != null && battleManager.currentEnemy.portrait != null)
    enemyPortrait.sprite = battleManager.currentEnemy.portrait;
        RefreshUI();
    }

    private void OnDestroy()
    {
        if (battleManager != null)
        {
            battleManager.OnStateChanged -= RefreshUI;
            battleManager.OnBattleLog -= AppendLog;
        }
    }

    // Новий рядок додається зверху, старі за межами ліміту відкидаються знизу
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
        playerShieldText.text = battleManager.playerShield > 0 ? $"Щит: {battleManager.playerShield}" : "";

        enemyHPSlider.maxValue = battleManager.enemyMaxHP;
        enemyHPSlider.value = battleManager.enemyHP;
        enemyHPText.text = $"{battleManager.enemyHP} / {battleManager.enemyMaxHP}";

        foreach (var entry in heroResourceEntries)
        {
            if (entry.amountText == null || entry.heroData == null) continue;

            HeroRuntimeState state = battleManager.GetHeroState(entry.heroData);
            if (state != null)
                entry.amountText.text = $"{state.currentResource} / {state.maxResource}";
        }
    }
}