using UnityEngine;

// Кривая бонуса экипировки по (тип слота, редкость, уровень) — линейная интерполяция между значением
// на 1 уровне и значением на кап-уровне, как задумана диковинка в match3-economy.md, а не компаундинг-процент
// (GetLevelMultiplierForLevel) — тот на длинных диапазонах улетал далеко за пределы задуманных чисел.
// Полностью заменяет ItemData.bonusHealth/Mana/Damage/Armor — значение считается из редкости+уровня,
// авторить на ассете больше ничего не нужно.
//
// Кап уровня ОДИН И ТОТ ЖЕ для всех редкостей (MaxLevel=50, см. ItemData.GetMaxLevel) — редкость задаёт
// только стартовое/финишное ЗНАЧЕНИЕ, не диапазон уровней (раньше был разный диапазон на редкость + понятие
// "минимального уровня" предмета, от него отказались — см. project_campaign_difficulty_curve). По конвенции
// доки один слот = один стат: Weapon->Damage, Armor->HP, Accessory->Defense/броня, Trinket->Mana.
public static class EquipmentStatCurve
{
    public const int MaxLevel = 50;

    private struct Anchor
    {
        public float startValue, endValue;
    }

    private static Anchor A(float startValue, float endValue) =>
        new Anchor { startValue = startValue, endValue = endValue };

    private static Anchor? GetAnchor(EquipmentSlotType slot, Rarity rarity)
    {
        switch (slot)
        {
            case EquipmentSlotType.Armor: // HP
                return rarity switch
                {
                    Rarity.Green => A(5, 25),
                    Rarity.Blue => A(25, 50),
                    Rarity.Purple => A(50, 100),
                    Rarity.Orange => A(100, 200),
                    _ => (Anchor?)null,
                };
            case EquipmentSlotType.Accessory: // Defense/броня
                return rarity switch
                {
                    Rarity.Green => A(2, 8),
                    Rarity.Blue => A(8, 16),
                    Rarity.Purple => A(16, 30),
                    Rarity.Orange => A(30, 55),
                    _ => (Anchor?)null,
                };
            case EquipmentSlotType.Weapon: // Damage (доля к damageMultiplier)
                return rarity switch
                {
                    Rarity.Green => A(0.05f, 0.15f),
                    Rarity.Blue => A(0.15f, 0.35f),
                    Rarity.Purple => A(0.35f, 0.70f),
                    Rarity.Orange => A(0.70f, 1.20f),
                    _ => (Anchor?)null,
                };
            case EquipmentSlotType.Trinket: // Mana
                return rarity switch
                {
                    Rarity.Green => A(1, 10),
                    Rarity.Blue => A(10, 20),
                    // Purple НЕ наследует доковский старт "+10" — та цифра была рассчитана на модель с
                    // минимальным уровнем предмета (Purple физически недоступна ниже 20 ур.), без гейта
                    // это уравняло бы Purple 1 уровня с Blue 1 уровня. Стартует с потолка Blue (20) вместо.
                    Rarity.Purple => A(20, 35),
                    Rarity.Orange => A(35, 60),
                    _ => (Anchor?)null,
                };
            default:
                return null;
        }
    }

    // Значение стата, который даёт ЭТОТ слот на данной редкости/уровне (0, если для White/неизвестной
    // комбинации кривой нет — White не выдаётся игрокам, см. RarityUtility/ItemData).
    public static float GetValue(EquipmentSlotType slot, Rarity rarity, int level)
    {
        var anchor = GetAnchor(slot, rarity);
        if (anchor == null) return 0f;

        var a = anchor.Value;
        int clampedLevel = Mathf.Clamp(level, 1, MaxLevel);
        float t = (clampedLevel - 1) / (float)(MaxLevel - 1);
        return Mathf.Lerp(a.startValue, a.endValue, t);
    }
}
