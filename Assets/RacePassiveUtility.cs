// Текстовые описания фиксированных расовых пассивок для UI (HeroInventoryUI.PassiveInfo) — сама логика
// эффектов живёт в BattleManager (ApplyRacePassivesPerTurn/ResolvePlayerTurn/TryUseSkill), см.
// project_race_skill_system memory. Держим тексты здесь одним местом, чтобы не дублировать числа
// по коду, если понадобится показать их где-то ещё (например, в Collection).
public static class RacePassiveUtility
{
    // Фиксированная стоимость включения пассивки — вычитается из maxResource героя, как раньше это
    // делал passiveSkill.cost для старого механизма "4й слот = пассивка" (см. BattleManager).
    public const int ManaCost = 10;

    // maxAscended — герой на 3й (последней, Orange-only) ступени вознесения: числа расовой пассивки
    // становятся выше (см. BattleManager.HasLivingMaxAscendedHeroOfRace/project_hero_ascension_system).
    // Тот же эффект, просто сильнее — отдельного нового эффекта на вознесении нет.
    public static string GetDescription(Race race, bool maxAscended = false)
    {
        return race switch
        {
            Race.Elves => maxAscended
                ? "Passive: 15% chance to stun the enemy on any match. (max ascension)"
                : "Passive: 10% chance to stun the enemy on any match.",
            Race.Fairy => maxAscended
                ? "Passive: each turn, gain a shield equal to 20% of a random ally's max HP. (max ascension)"
                : "Passive: each turn, gain a shield equal to 15% of a random ally's max HP.",
            Race.Orcs => maxAscended
                ? "Passive: 15% chance to triple the damage of a match. (max ascension)"
                : "Passive: 10% chance to triple the damage of a match.",
            Race.Beastfolk => maxAscended
                ? "Passive: 15% chance per turn to not consume the turn. (max ascension)"
                : "Passive: 10% chance per turn to not consume the turn.",
            Race.Dragonkin => maxAscended
                ? "Passive: each turn, deal damage equal to 1.5% of the enemy's max HP. (max ascension)"
                : "Passive: each turn, deal damage equal to 1% of the enemy's max HP.",
            Race.Demons => maxAscended
                ? "Passive: each turn, reduce the enemy's resistance by 3% (stacks). (max ascension)"
                : "Passive: each turn, reduce the enemy's resistance by 2% (stacks).",
            Race.Angels => maxAscended
                ? "Passive: each turn, heal the whole team for 7.5% of max HP. (max ascension)"
                : "Passive: each turn, heal the whole team for 5% of max HP.",
            Race.Humans => maxAscended
                ? "Passive: using any skill refunds 25% of its mana cost to another Human ally. (max ascension)"
                : "Passive: using any skill refunds 15% of its mana cost to another Human ally.",
            _ => "",
        };
    }
}
