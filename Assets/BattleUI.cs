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

    // Слоты вместо жёсткой привязки HeroData->текст: отряд собирается игроком из
    // произвольных героев, поэтому слот просто занимает i-й герой из activeHeroes,
    // а лишние слоты (отряд меньше, чем слотов) скрываются, как и в HeroInventoryUI.PopulateSkillSelectors.
    [Header("Ресурсы героев")]
    public TMP_Text[] heroResourceSlots;

    [Header("Лог бою (нанесений/отриманий урон)")]
    public TMP_Text battleLogText; // назначить в Inspector — отдельный текстовый блок где-то на экране боя
    private const int MaxLogLines = 8;
    private readonly List<string> logLines = new List<string>();

    private void Start()
    {
        if (battleManager == null)
            battleManager = FindAnyObjectByType<BattleManager>();

        battleManager.OnStateChanged += RefreshUI;
        battleManager.OnBattleLog += AppendLog;
        battleManager.OnEnemyDamaged += HandleEnemyDamaged;
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
            battleManager.OnEnemyDamaged -= HandleEnemyDamaged;
        }
    }

    private void HandleEnemyDamaged(int amount)
    {
        if (enemyPortrait != null)
            FloatingDamageText.Spawn((RectTransform)enemyPortrait.transform, amount, FloatingDamageText.EnemyDamageColor);
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
        playerHPSlider.maxValue = battleManager.playerMaxHP;
        playerHPSlider.value = battleManager.playerHP;
        playerHPText.text = $"{battleManager.playerHP} / {battleManager.playerMaxHP}";
        playerShieldText.text = battleManager.playerShield > 0 ? $"Shield: {battleManager.playerShield}" : "";

        enemyHPSlider.maxValue = battleManager.enemyMaxHP;
        enemyHPSlider.value = battleManager.enemyHP;
        enemyHPText.text = $"{battleManager.enemyHP} / {battleManager.enemyMaxHP}";

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