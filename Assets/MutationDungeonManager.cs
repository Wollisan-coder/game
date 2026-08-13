using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Один вариант на выбор игрока в MutationDungeonNodeChoiceUI — не структура на диске, чисто рантайм
// (генерируется заново на каждый шаг, ничего не персистится между узлами кроме самого currentNodeIndex/
// carried HP-маны, см. MutationDungeonManager ниже).
public class MutationDungeonChoice
{
    public MutationDungeonNodeType nodeType;
    public BoardMutationType mutationType;   // валиден только для Battle/Elite — см. HasMutation
    public int mutationValue;

    public bool HasMutation => nodeType == MutationDungeonNodeType.Battle || nodeType == MutationDungeonNodeType.Elite;
}

// Постоянный менеджер состояния текущего забега нового roguelike-режима (см. project-future-backlog #7,
// вариант C+A — мутация поля + ветвящийся путь). В отличие от DeathDungeonManager: НЕТ permadeath/retreat-
// экономики (поражение просто заканчивает ран, герои целы) и путь генерируется НА ЛЕТУ шаг за шагом
// (2-3 варианта на выбор), а не фиксированной последовательностью — полноценная карта-граф с визуализацией
// не строится, это сознательное упрощение v1 (см. design-обсуждение в чате 2026-08-12).
public class MutationDungeonManager : MonoBehaviour
{
    public static MutationDungeonManager Instance { get; private set; }

    public const int TotalNodes = 14; // узлов до и включая Boss (12-15 из design-обсуждения, середина диапазона)
    private const int ChoicesPerStep = 3;

    public int currentNodeIndex = -1; // -1 = ран сейчас не активен
    public bool IsRunActive => currentNodeIndex >= 0;

    // Тип/мутация УЖЕ ВЫБРАННОГО текущего узла — то, что реально запускает BattleManager (см. SelectNode).
    public MutationDungeonNodeType CurrentNodeType { get; private set; }
    public BoardMutationType CurrentMutation { get; private set; }
    public int CurrentMutationValue { get; private set; }

    // Варианты, предложенные игроку СЕЙЧАС (до выбора) — хранится, а не перегенерируется каждый раз, когда
    // экран выбора открывается заново (например, после Rest/Treasure, которые не выходят из UI-потока),
    // чтобы варианты не "мигали" новым рандомом при каждом перерисовывании экрана.
    public List<MutationDungeonChoice> PendingChoices { get; private set; } = new List<MutationDungeonChoice>();

    // Уже пройденные узлы этого рана, по порядку — для хлебных крошек (см. MutationDungeonNodeChoiceUI).
    // Пополняется в SelectNode (ДО AdvanceAfterWin++), поэтому в момент, когда что-то реально отрисовывает
    // экран выбора (после StartNewRun, или после AdvanceAfterWin для только что пройденного узла), длина
    // History совпадает с currentNodeIndex — ровно "прошедшие" узлы, без ещё не выбранного текущего.
    // (Между SelectNode и AdvanceAfterWin, пока идёт бой, это временно не так — но экран выбора в этот
    // момент и не показывается, так что несовпадение никогда не рендерится.) НЕ хранит будущие узлы —
    // путь генерируется лениво, будущее реально неизвестно.
    public List<MutationDungeonChoice> History { get; private set; } = new List<MutationDungeonChoice>();

    // HP/мана переносятся между узлами ОДНОГО рана — та же логика, что и у DeathDungeonManager (см. её
    // комментарий), сброс только между ранами (StartNewRun/EndRun).
    private readonly Dictionary<string, int> carriedHeroHP = new Dictionary<string, int>();
    private readonly Dictionary<string, int> carriedHeroMana = new Dictionary<string, int>();

    public void SetCarriedHP(string heroId, int hp) => carriedHeroHP[heroId] = hp;
    public int GetCarriedHP(string heroId, int fallbackFullHP) =>
        carriedHeroHP.TryGetValue(heroId, out int hp) ? hp : fallbackFullHP;

    public void SetCarriedMana(string heroId, int mana) => carriedHeroMana[heroId] = mana;
    public int GetCarriedMana(string heroId, int fallbackFullMana) =>
        carriedHeroMana.TryGetValue(heroId, out int mana) ? mana : fallbackFullMana;

    // Rest-узел (см. MutationDungeonNodeChoiceUI) — полностью лечит отряд между боями. Проще, чем Death
    // Dungeon-овский Heal (30% от maxHealth), т.к. тут нет доступа к посчитанным статам героя вне боя без
    // дублирования HeroStatUtility — просто стираем перенесённые значения, следующий бой стартует с
    // GetCarriedHP/Mana фолбэком на "полный" (см. их сигнатуры выше).
    public void HealSquadFully()
    {
        carriedHeroHP.Clear();
        carriedHeroMana.Clear();
    }

    // Сколько узлов реально пройдено в этом ране (для итогового экрана/наград) — 0, если ран ещё не начат
    // или только что стартовал (currentNodeIndex=0 значит "стоим НА узле 0", ещё не прошли его).
    public int NodesCleared => Mathf.Max(0, currentNodeIndex);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartNewRun()
    {
        currentNodeIndex = -1;
        carriedHeroHP.Clear();
        carriedHeroMana.Clear();
        PendingChoices.Clear();
        History.Clear();

        currentNodeIndex = 0;
        RollNextChoices();
    }

    // Игрок кликнул одну из PendingChoices — фиксирует её как текущий узел (CurrentNodeType/Mutation*) И
    // сразу пишет в History (см. её комментарий про то, почему длина совпадает с currentNodeIndex — этот
    // узел фиксируется здесь, ДО AdvanceAfterWin++, поэтому история из currentNodeIndex записей как раз
    // не включает ЕЩЁ не пройденный текущий). НЕ продвигает currentNodeIndex — это происходит только после
    // реальной победы в бою (AdvanceAfterWin) или сразу же для Rest/Treasure, у которых боя нет вообще
    // (см. вызывающий UI).
    public void SelectNode(MutationDungeonChoice choice)
    {
        CurrentNodeType = choice.nodeType;
        CurrentMutation = choice.mutationType;
        CurrentMutationValue = choice.mutationValue;
        History.Add(choice);
        PendingChoices.Clear();
    }

    // Возвращает true, если это был Boss — ран завершён победой (currentNodeIndex сбрасывается в -1).
    public bool AdvanceAfterWin()
    {
        currentNodeIndex++;
        if (currentNodeIndex >= TotalNodes)
        {
            currentNodeIndex = -1;
            return true;
        }

        RollNextChoices();
        return false;
    }

    // Ран брошен/провален — поражение тут НЕ штрафует героев (см. class-комментарий), просто сброс
    // состояния рана. Тот же метод для явного выхода игроком из рана посередине.
    public void EndRun()
    {
        currentNodeIndex = -1;
        carriedHeroHP.Clear();
        carriedHeroMana.Clear();
        PendingChoices.Clear();
        History.Clear();
    }

    // Генерирует 2-3 варианта следующего шага. Boss форсируется единственным вариантом на последнем
    // индексе (TotalNodes-1) — не подмешивается к обычному пулу. Иначе — случайная смесь Battle/Elite/
    // Rest/Treasure с гарантией хотя бы одного боевого варианта (Battle или Elite), чтобы нельзя было
    // теоретически получить экран из одних Rest/Treasure подряд без возможности прогрессировать боем
    // (сам по себе прогресс это не блокирует — Rest/Treasure тоже продвигают currentNodeIndex, см. UI —
    // но так веер выбора выглядит содержательнее).
    private void RollNextChoices()
    {
        PendingChoices.Clear();

        if (currentNodeIndex == TotalNodes - 1)
        {
            PendingChoices.Add(new MutationDungeonChoice { nodeType = MutationDungeonNodeType.Boss });
            return;
        }

        var pool = new[]
        {
            MutationDungeonNodeType.Battle,
            MutationDungeonNodeType.Battle,
            MutationDungeonNodeType.Elite,
            MutationDungeonNodeType.Rest,
            MutationDungeonNodeType.Treasure,
        };

        var picked = new List<MutationDungeonNodeType>();
        for (int i = 0; i < ChoicesPerStep; i++)
            picked.Add(pool[Random.Range(0, pool.Length)]);

        if (!picked.Contains(MutationDungeonNodeType.Battle) && !picked.Contains(MutationDungeonNodeType.Elite))
            picked[0] = MutationDungeonNodeType.Battle;

        var mutationValues = System.Enum.GetValues(typeof(BoardMutationType)).Cast<BoardMutationType>().ToArray();

        foreach (var nodeType in picked)
        {
            var choice = new MutationDungeonChoice { nodeType = nodeType };
            if (nodeType == MutationDungeonNodeType.Battle || nodeType == MutationDungeonNodeType.Elite)
            {
                choice.mutationType = mutationValues[Random.Range(0, mutationValues.Length)];
                choice.mutationValue = MutationUtility.GetDefaultValue(choice.mutationType);
            }
            PendingChoices.Add(choice);
        }
    }
}
