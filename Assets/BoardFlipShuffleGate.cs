using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Связывает кнопку переворота доски с панелью шаффла:
// 1-й клик по кнопке — доска переворачивается, из пула случайно берётся 4 позитивных +
// 4 негативных эффекта и раскидывается по 8 кнопкам-вариантам (открыто, с иконками),
// кнопка переворота временно блокируется на время выбора.
// Пока кнопки открыты, клик по любой из них показывает попап с описанием (не выбирает).
// Игрок жмёт шаффл (доступен один раз на эту раскладку — см. ShuffleButtonGrid) — позиции
// перемешиваются и кнопки блюрятся. Клик по заблюренной кнопке — блайнд-выбор: эффект
// применяется к бою, выбранная кнопка открывается обратно (чтобы было видно, что выпало),
// а панель остаётся на экране — кнопка переворота снова становится доступна.
// 2-й клик по кнопке переворота — только теперь панель закрывается, доска переворачивается
// назад к 3-в-ряд, и кнопка блокируется навсегда до конца боя.
public class BoardFlipShuffleGate : MonoBehaviour
{
    [Header("Кнопка переворота доски")]
    public Button flipButton;

    [Header("Панель с шаффлом (появляется по клику на flipButton)")]
    public GameObject shufflePanel;

    [Header("Сетка вариантов (чтобы достать все 8 кнопок-вариантов)")]
    public ShuffleButtonGrid shuffleGrid;

    [Header("Пул баффов/дебаффов (позитивные и негативные списки, по 4 берётся случайно)")]
    public BoardEffectPoolData effectPool;

    [Header("Бой — для лимита 'раз за бой' и применения эффекта")]
    public BattleManager battleManager;

    [Header("Модель доски — для переворота туда и обратно")]
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
        if (battleManager != null && battleManager.boardFlipUsedThisBattle) return; // уже полностью использовано этот бой

        if (!hasFlippedOnce)
        {
            hasFlippedOnce = true;

            if (flippableModel != null)
                flippableModel.Flip();

            AssignRandomEffects();

            if (shufflePanel != null)
                shufflePanel.SetActive(true);

            if (flipButton != null)
                flipButton.interactable = false; // заблокировано на время выбора варианта
        }
        else if (awaitingFlipBack)
        {
            awaitingFlipBack = false;

            if (shufflePanel != null)
                shufflePanel.SetActive(false); // прячем панель только теперь, когда доска реально переворачивается назад

            if (flippableModel != null)
                flippableModel.Flip(); // назад к 3-в-ряд

            if (battleManager != null)
                battleManager.boardFlipUsedThisBattle = true;

            if (flipButton != null)
                flipButton.interactable = false; // навсегда до конца боя
        }
    }

    // Берёт 4 случайных уникальных позитивных + 4 случайных уникальных негативных эффекта
    // из раздельных списков пула, перемешивает их вместе и раскидывает по 8 кнопкам-вариантам
    private void AssignRandomEffects()
    {
        if (effectPool == null || shuffleGrid == null || shuffleGrid.shuffleTargets == null)
            return;

        List<BoardEffectDefinition> chosen = new List<BoardEffectDefinition>();
        chosen.AddRange(PickRandomUnique(effectPool.positiveEffects, 4));
        chosen.AddRange(PickRandomUnique(effectPool.negativeEffects, 4));

        // Перемешиваем вместе, чтобы позитив/негатив не шли предсказуемым порядком при показе
        for (int i = chosen.Count - 1; i > 0; i--)
        {
            int r = Random.Range(0, i + 1);
            (chosen[i], chosen[r]) = (chosen[r], chosen[i]);
        }

        int slotCount = Mathf.Min(shuffleGrid.shuffleTargets.Length, chosen.Count);
        for (int i = 0; i < slotCount; i++)
        {
            var option = shuffleGrid.shuffleTargets[i] != null ? shuffleGrid.shuffleTargets[i].GetComponent<BoardEffectOption>() : null;
            if (option == null) continue;

            var def = chosen[i];
            option.effectType = def.effectType;
            option.icon = def.icon;
            option.description = def.description;
            option.amount = def.amount;
            option.turns = def.turns;
            option.multiplier = def.multiplier;
            option.ShowRevealed();
        }
    }

    private List<BoardEffectDefinition> PickRandomUnique(BoardEffectDefinition[] source, int count)
    {
        if (source == null || source.Length == 0) return new List<BoardEffectDefinition>();

        List<BoardEffectDefinition> pool = new List<BoardEffectDefinition>(source);
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int r = Random.Range(0, i + 1);
            (pool[i], pool[r]) = (pool[r], pool[i]);
        }

        int actualCount = Mathf.Min(count, pool.Count);
        return pool.GetRange(0, actualCount);
    }

    // Клик по кнопке: если она открыта (не заблюрена) — просто показать попап с описанием
    // (работает и до шаффла, и повторно на уже выбранной кнопке). Если заблюрена — это блайнд-выбор.
    private void OnVariantSelected(BoardEffectOption option)
    {
        if (option == null) return;

        if (!option.IsBlurred)
        {
            ShowDescriptionPopup(option);
            return;
        }

        if (awaitingFlipBack) return; // вариант уже выбран этим переворотом — остальные заблюренные недоступны

        option.Apply(battleManager);
        option.ShowRevealed(); // открываем именно выбранную кнопку, остальные остаются заблюренными

        awaitingFlipBack = true;

        if (flipButton != null)
            flipButton.interactable = true; // теперь можно нажать ещё раз, чтобы перевернуть доску назад (и закрыть панель)
    }

    private void ShowDescriptionPopup(BoardEffectOption option)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        Transform root = canvas != null ? canvas.transform : transform;
        ConfirmationDialog.ShowInfo(root, option.GetDisplayText());
    }
}
