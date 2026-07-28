using UnityEngine;
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
    BlockMana
}

// Вішається на кожну з 8 кнопок-варіантів у панелі шаффла.
// Зберігає, який ефект несе ця кнопка, і вміє показати/приховати ("заблюрити") свій напис.
public class BoardEffectOption : MonoBehaviour
{
    [Header("Який ефект несе ця кнопка")]
    public BoardEffectType effectType;
    public int amount = 10;         // урон/лікування/щит — залежно від типу
    public int turns = 2;           // для баффів/дебаффів на N ходів
    public float multiplier = 1.5f; // для damageMultiplier-ефектів

    [Header("Візуал")]
    public TMP_Text label; // підпис з назвою ефекту

    private const string BlurredText = "?";

    private void Awake()
    {
        ShowRevealed();
    }

    public void ShowBlurred()
    {
        if (label != null)
            label.text = BlurredText;
    }

    public void ShowRevealed()
    {
        if (label != null)
            label.text = GetDisplayName();
    }

    public string GetDisplayName()
    {
        switch (effectType)
        {
            case BoardEffectType.HealPlayer: return $"+{amount} HP";
            case BoardEffectType.ShieldPlayer: return $"+{amount} щит";
            case BoardEffectType.DamageBuff: return $"Урон x{multiplier} ({turns} х.)";
            case BoardEffectType.FreeHitOnEnemy: return $"{amount} уроном ворогу";
            case BoardEffectType.DamageRandomHero: return $"-{amount} HP герою";
            case BoardEffectType.EnemyShield: return $"+{amount} щит ворогу";
            case BoardEffectType.WeakenHeroes: return $"Урон героїв x{multiplier} ({turns} х.)";
            case BoardEffectType.BlockMana: return "Блок мани";
            default: return "?";
        }
    }

    public void Apply(BattleManager battleManager)
    {
        if (battleManager == null) return;

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
                battleManager.DamageRandomHero(amount);
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
        }
    }
}
