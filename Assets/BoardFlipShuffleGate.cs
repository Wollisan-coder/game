using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Пов'язує кнопку перевороту дошки з панеллю шаффла:
// 1-й клік по кнопці — дошка перевертається, з пулу випадково береться 8 унікальних
// баффів/дебаффів і розкидається по кнопках-варіантах, кнопка перевороту тимчасово
// блокується на час вибору.
// Гравець тисне шаффл (ховає варіанти) і обирає один наосліп — ефект застосовується
// до бою, панель закривається, а кнопка перевороту знову стає доступна.
// 2-й клік по кнопці — дошка перевертається назад до 3-в-ряд, і кнопка блокується
// назавжди до кінця бою.
public class BoardFlipShuffleGate : MonoBehaviour
{
    [Header("Кнопка перевороту дошки")]
    public Button flipButton;

    [Header("Панель з шаффлом (з'являється по кліку на flipButton)")]
    public GameObject shufflePanel;

    [Header("Сітка варіантів (щоб дістати всі 8 кнопок-варіантів)")]
    public ShuffleButtonGrid shuffleGrid;

    [Header("Пул баффів/дебаффів (може містити більше 8 варіантів)")]
    public BoardEffectPoolData effectPool;

    [Header("Бій — для ліміту 'раз за бій' і застосування ефекту")]
    public BattleManager battleManager;

    [Header("Модель дошки — для перевороту туди й назад")]
    public FlippableModel flippableModel;

    private bool hasFlippedOnce;
    private bool awaitingFlipBack;

    private void Awake()
    {
        if (flipButton != null)
            flipButton.onClick.AddListener(OnFlipPressed);

        if (shufflePanel != null)
            shufflePanel.SetActive(false);

        if (battleManager != null && battleManager.boardFlipUsedThisBattle && flipButton != null)
            flipButton.interactable = false;

        if (shuffleGrid != null && shuffleGrid.shuffleTargets != null)
        {
            foreach (var target in shuffleGrid.shuffleTargets)
            {
                if (target == null) continue;

                Button optionButton = target.GetComponent<Button>();
                BoardEffectOption option = target.GetComponent<BoardEffectOption>();
                if (optionButton != null)
                    optionButton.onClick.AddListener(() => OnVariantSelected(option));
            }
        }
    }

    private void OnFlipPressed()
    {
        if (battleManager != null && battleManager.boardFlipUsedThisBattle) return; // вже повністю використано цей бій

        if (!hasFlippedOnce)
        {
            hasFlippedOnce = true;

            if (flippableModel != null)
                flippableModel.Flip();

            AssignRandomEffects();

            if (shufflePanel != null)
                shufflePanel.SetActive(true);

            if (flipButton != null)
                flipButton.interactable = false; // заблоковано на час вибору варіанту
        }
        else if (awaitingFlipBack)
        {
            awaitingFlipBack = false;

            if (flippableModel != null)
                flippableModel.Flip(); // назад до 3-в-ряд

            if (battleManager != null)
                battleManager.boardFlipUsedThisBattle = true;

            if (flipButton != null)
                flipButton.interactable = false; // назавжди до кінця бою
        }
    }

    // Випадково бере 8 унікальних ефектів із пулу і розкидає їх по кнопках-варіантах
    private void AssignRandomEffects()
    {
        if (effectPool == null || effectPool.effects == null || shuffleGrid == null || shuffleGrid.shuffleTargets == null)
            return;

        List<BoardEffectDefinition> pool = new List<BoardEffectDefinition>(effectPool.effects);
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int r = Random.Range(0, i + 1);
            (pool[i], pool[r]) = (pool[r], pool[i]);
        }

        int slotCount = Mathf.Min(shuffleGrid.shuffleTargets.Length, pool.Count);
        for (int i = 0; i < slotCount; i++)
        {
            var option = shuffleGrid.shuffleTargets[i] != null ? shuffleGrid.shuffleTargets[i].GetComponent<BoardEffectOption>() : null;
            if (option == null) continue;

            var def = pool[i];
            option.effectType = def.effectType;
            option.amount = def.amount;
            option.turns = def.turns;
            option.multiplier = def.multiplier;
            option.ShowRevealed();
        }
    }

    private void OnVariantSelected(BoardEffectOption option)
    {
        if (option != null)
            option.Apply(battleManager);

        if (shufflePanel != null)
            shufflePanel.SetActive(false);

        awaitingFlipBack = true;

        if (flipButton != null)
            flipButton.interactable = true; // тепер можна натиснути ще раз, щоб перевернути назад
    }
}
