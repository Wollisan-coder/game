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

    private void Start()
    {
        if (battleManager == null)
            battleManager = FindAnyObjectByType<BattleManager>();

        battleManager.OnStateChanged += RefreshUI;

        RefreshUI();
    }

    private void OnDestroy()
    {
        if (battleManager != null)
            battleManager.OnStateChanged -= RefreshUI;
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
                entry.amountText.text = $"{state.currentResource} / {state.data.maxResource}";
        }
    }
}