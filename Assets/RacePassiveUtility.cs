// Текстовые описания фиксированных расовых пассивок для UI (HeroInventoryUI.PassiveInfo) — сама логика
// эффектов живёт в BattleManager (ApplyRacePassivesPerTurn/ResolvePlayerTurn/TryUseSkill), см.
// project_race_skill_system memory. Держим тексты здесь одним местом, чтобы не дублировать числа
// по коду, если понадобится показать их где-то ещё (например, в Collection).
public static class RacePassiveUtility
{
    // Фиксированная стоимость включения пассивки — вычитается из maxResource героя, как раньше это
    // делал passiveSkill.cost для старого механизма "4й слот = пассивка" (см. BattleManager).
    public const int ManaCost = 10;

    public static string GetDescription(Race race)
    {
        return race switch
        {
            Race.Elves => "Passive: 10% chance to stun the enemy on any match.",
            Race.Fairy => "Passive: each turn, gain a shield equal to 15% of a random ally's max HP.",
            Race.Orcs => "Passive: 10% chance to triple the damage of a match.",
            Race.Beastfolk => "Passive: 10% chance per turn to not consume the turn.",
            Race.Dragonkin => "Passive: each turn, deal damage equal to 1% of the enemy's max HP.",
            Race.Demons => "Passive: each turn, reduce the enemy's resistance by 2% (stacks).",
            Race.Angels => "Passive: each turn, heal the whole team for 5% of max HP.",
            Race.Humans => "Passive: using any skill refunds 15% of its mana cost to another Human ally.",
            _ => "",
        };
    }
}
