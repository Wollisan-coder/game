using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum BoardEffectType
{
    HealPlayer,
    ShieldPlayer,
    DamageBuff,
    FreeHitOnEnemy,
    DamageRandomHero,
    EnemyShield,
    WeakenHeroes,
    BlockMana,

    // --- Гемблинг-колесо: пользовательский список позитив/негатив ---
    FullManaAllHeroes,      // позитив: полная мана всем героям сразу
    HealPercentMaxHP,       // позитив: лечение команды на % от максимального HP (multiplier = процент)
    ShieldTeamTurns,        // позитив: щит на всю команду, не сбрасывается N ходов
    ExtraTurnsFree,         // позитив: N дополнительных ходов без затрат (amount = кол-во ходов)
    FreezeRandomRowOrColumn,// негатив: заморозка случайной строки/столбца на N ходов
    StunRandomHero,         // негатив: случайный герой "пропускает" N ходов (его цвет не работает)
    EnemyDamageBuff,        // негатив: враг получает бафф урона на N ходов
    BlockHeroSkill          // негатив: случайному герою блокируется скилл на N ходов
}

// Вешается на каждую из 8 кнопок-вариантов в панели шаффла.
// Хранит, какой эффект несёт эта кнопка, и умеет показать/скрыть ("заблюрить") свою надпись.
public class BoardEffectOption : MonoBehaviour
{
    [Header("Какой эффект несёт эта кнопка")]
    public BoardEffectType effectType;
    public Sprite icon;              // назначается из BoardEffectDefinition при раздаче эффектов
    public string description;       // если пусто — используется автотекст GetDisplayName()
    public int amount = 10;          // урон/лечение/щит/кол-во ходов — в зависимости от типа
    public int turns = 2;            // для баффов/дебаффов на N ходов
    public float multiplier = 1.5f;  // для damageMultiplier- и процентных эффектов (0.5 = 50%)

    [Header("Визуал")]
    public TMP_Text label;   // подпись с названием/описанием эффекта
    public Image iconImage;  // иконка эффекта (необязательно)

    private const string BlurredText = "?";

    // true, пока кнопка заблюрена ("?") — используется BoardFlipShuffleGate, чтобы решить,
    // открыть попап с описанием (не заблюрено) или засчитать блайнд-выбор (заблюрено)
    public bool IsBlurred { get; private set; } = true;

    private void Awake()
    {
        ShowRevealed();
    }

    // Прячет и текст, и иконку — чтобы не подсказывать, какой эффект под какой кнопкой
    public void ShowBlurred()
    {
        IsBlurred = true;

        if (label != null)
            label.text = BlurredText;

        if (iconImage != null)
            iconImage.enabled = false;
    }

    // Показывает только иконку — описание теперь не висит постоянным текстом на кнопке,
    // а всплывает попапом по клику (см. BoardFlipShuffleGate.ShowDescriptionPopup)
    public void ShowRevealed()
    {
        IsBlurred = false;

        if (label != null)
            label.text = string.Empty;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }
    }

    public string GetDisplayText() => !string.IsNullOrEmpty(description) ? description : GetDisplayName();

    public string GetDisplayName()
    {
        switch (effectType)
        {
            case BoardEffectType.HealPlayer: return $"+{amount} HP";
            case BoardEffectType.ShieldPlayer: return $"+{amount} Shield";
            case BoardEffectType.DamageBuff: return $"Damage x{multiplier} ({turns} turns)";
            case BoardEffectType.FreeHitOnEnemy: return $"{amount} damage to enemy";
            case BoardEffectType.DamageRandomHero: return $"-{amount} HP to a hero";
            case BoardEffectType.EnemyShield: return $"+{amount} Shield to enemy";
            case BoardEffectType.WeakenHeroes: return $"Heroes' damage x{multiplier} ({turns} turns)";
            case BoardEffectType.BlockMana: return "Mana block";

            case BoardEffectType.FullManaAllHeroes: return "Full mana to all heroes";
            case BoardEffectType.HealPercentMaxHP: return $"Heal team {multiplier * 100:0}% HP";
            case BoardEffectType.ShieldTeamTurns: return $"+{amount} Shield to team ({turns} turns)";
            case BoardEffectType.ExtraTurnsFree: return $"+{amount} free turns";
            case BoardEffectType.FreezeRandomRowOrColumn: return $"Freeze a row/column ({turns} turns)";
            case BoardEffectType.StunRandomHero: return $"A hero is stunned ({turns} turns)";
            case BoardEffectType.EnemyDamageBuff: return $"Enemy damage x{multiplier} ({turns} turns)";
            case BoardEffectType.BlockHeroSkill: return $"A hero's skill is blocked ({turns} turns)";

            default: return "?";
        }
    }

    public void Apply(BattleManager battleManager)
    {
        if (battleManager == null) return;

        HeroRuntimeState targetedHero = null; // назначается только для эффектов, бьющих в конкретного героя

        switch (effectType)
        {
            case BoardEffectType.HealPlayer:
                battleManager.Heal(amount);
                break;
            case BoardEffectType.ShieldPlayer:
                battleManager.AddShield(amount);
                break;
            case BoardEffectType.DamageBuff:
                battleManager.ApplyDamageBuff(multiplier, turns);
                break;
            case BoardEffectType.FreeHitOnEnemy:
                battleManager.DealDamageToEnemy(amount);
                break;
            case BoardEffectType.DamageRandomHero:
                targetedHero = battleManager.DamageRandomHero(amount);
                break;
            case BoardEffectType.EnemyShield:
                battleManager.AddEnemyShield(amount);
                break;
            case BoardEffectType.WeakenHeroes:
                battleManager.ApplyWeakenHeroes(multiplier, turns);
                break;
            case BoardEffectType.BlockMana:
                battleManager.BlockManaForAllHeroes();
                break;

            case BoardEffectType.FullManaAllHeroes:
                battleManager.FullManaAllHeroes();
                break;
            case BoardEffectType.HealPercentMaxHP:
                battleManager.HealTeamPercent(multiplier);
                break;
            case BoardEffectType.ShieldTeamTurns:
                battleManager.ApplyTeamShieldForTurns(amount, turns);
                break;
            case BoardEffectType.ExtraTurnsFree:
                battleManager.freeExtraTurnsRemaining += amount;
                break;
            case BoardEffectType.FreezeRandomRowOrColumn:
                if (battleManager.gridManager != null)
                    battleManager.gridManager.FreezeRandomRowOrColumn(turns);
                break;
            case BoardEffectType.StunRandomHero:
                targetedHero = battleManager.StunRandomHero(turns);
                break;
            case BoardEffectType.EnemyDamageBuff:
                battleManager.ApplyEnemyDamageBuff(multiplier, turns);
                break;
            case BoardEffectType.BlockHeroSkill:
                targetedHero = battleManager.BlockRandomHeroSkill(turns);
                break;
        }

        // Единая строка лога для любого эффекта колеса — какой эффект и (если бил в конкретного героя) на кого
        string targetSuffix = targetedHero != null ? $" → {targetedHero.data.heroName}" : string.Empty;
        battleManager.OnBattleLog?.Invoke($"Board effect: {GetDisplayName()}{targetSuffix}");
    }
}
