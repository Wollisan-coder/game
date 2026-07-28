using UnityEditor;
using UnityEngine;

// Генерирует все 32 SkillData (8 рас x 4 уровня) по таблице пользователя.
// Меню: Battle -> Generate Race Skill Table.
// Значения cost/effectValue/и т.д. — плейсхолдеры для баланса, легко поправить прямо в инспекторе
// каждого сгенерированного ассета. Название и описание — на английском (видны в игре игроку),
// комментарии в этом файле — на русском (для разработки).
public static class SkillTableGenerator
{
    private const string OutputFolder = "Assets/SkillsHero/Generated";

    private struct Entry
    {
        public string race;
        public int tier;
        public string name;
        public SkillEffectType effectType;
        public int cost;
        public int effectValue;
        public int buffDurationTurns;
        public float damageMultiplier;
        public float shieldPercentOfMaxHP;
        public int hitCount;

        public Entry(string race, int tier, string name, SkillEffectType effectType, int cost,
            int effectValue = 0, int buffDurationTurns = 0, float damageMultiplier = 1f,
            float shieldPercentOfMaxHP = 0f, int hitCount = 0)
        {
            this.race = race;
            this.tier = tier;
            this.name = name;
            this.effectType = effectType;
            this.cost = cost;
            this.effectValue = effectValue;
            this.buffDurationTurns = buffDurationTurns;
            this.damageMultiplier = damageMultiplier;
            this.shieldPercentOfMaxHP = shieldPercentOfMaxHP;
            this.hitCount = hitCount;
        }
    }

    [MenuItem("Battle/Generate Race Skill Table")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder("Assets/SkillsHero"))
            AssetDatabase.CreateFolder("Assets", "SkillsHero");
        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder("Assets/SkillsHero", "Generated");

        // Названия скиллов — на английском (это игровой текст, видимый игроку)
        Entry[] entries =
        {
            new Entry("Elves", 1, "Gem Shatter", SkillEffectType.DestroyRandomGems, 25, effectValue: 3),
            new Entry("Elves", 2, "Purge Corruption", SkillEffectType.DestroyHarmfulTile, 40),
            new Entry("Elves", 3, "Joker's Gift", SkillEffectType.ConvertCellToJoker, 55),
            new Entry("Elves", 4, "Will of the Forest", SkillEffectType.FavorableReshuffle, 75),

            new Entry("Dwarves", 1, "Stone Shield", SkillEffectType.Shield, 25, effectValue: 20),
            new Entry("Dwarves", 2, "Armor Breach", SkillEffectType.ReduceEnemyArmor, 40, buffDurationTurns: 3, shieldPercentOfMaxHP: 0.3f),
            new Entry("Dwarves", 3, "Dwarven Resilience", SkillEffectType.Invulnerability, 55),
            new Entry("Dwarves", 4, "Wall and Echo", SkillEffectType.ShieldAndReflect, 75, effectValue: 25, buffDurationTurns: 2, shieldPercentOfMaxHP: 0.3f),

            new Entry("Orcs", 1, "Axe Strike", SkillEffectType.Damage, 25, effectValue: 15),
            new Entry("Orcs", 2, "Blood Fury", SkillEffectType.MultiHit, 40, effectValue: 8, hitCount: 3),
            new Entry("Orcs", 3, "Finishing Blow", SkillEffectType.DamagePercentCurrentEnemyHP, 55, shieldPercentOfMaxHP: 0.15f),
            new Entry("Orcs", 4, "Blood Ritual", SkillEffectType.SacrificeForDamage, 75, effectValue: 20, damageMultiplier: 3f, shieldPercentOfMaxHP: 0.2f),

            new Entry("Beastfolk", 1, "Feral Dash", SkillEffectType.ExtraTurn, 25),
            new Entry("Beastfolk", 2, "Terrifying Roar", SkillEffectType.StunEnemy, 40),
            new Entry("Beastfolk", 3, "Hunter's Thrill", SkillEffectType.DamageScalingWithMatches, 55, effectValue: 4),
            new Entry("Beastfolk", 4, "Pack on the Hunt", SkillEffectType.DoubleFreeTurn, 75),

            new Entry("Dragonkin", 1, "Fire Breath", SkillEffectType.Damage, 25, effectValue: 18),
            new Entry("Dragonkin", 2, "Dragon's Brand", SkillEffectType.DelayedDamageMark, 40, effectValue: 30, buffDurationTurns: 2),
            new Entry("Dragonkin", 3, "Dragon's Grip", SkillEffectType.DamagePercentMaxEnemyHP, 55, shieldPercentOfMaxHP: 0.12f),
            new Entry("Dragonkin", 4, "Wrath of Ancestors", SkillEffectType.CleanseDebuffsAndDamage, 75, effectValue: 20),

            new Entry("Demons", 1, "Delusion", SkillEffectType.ReduceEnemyAccuracy, 25, buffDurationTurns: 2, shieldPercentOfMaxHP: 0.25f),
            new Entry("Demons", 2, "Essence Theft", SkillEffectType.StealEnemyShield, 40),
            new Entry("Demons", 3, "Mark of Weakness", SkillEffectType.WeaknessMarkNextHit, 55, damageMultiplier: 1.5f),
            new Entry("Demons", 4, "Pact of Pain", SkillEffectType.TransferDebuffToEnemy, 75),

            new Entry("Angels", 1, "Blessing", SkillEffectType.Heal, 25, effectValue: 25),
            new Entry("Angels", 2, "Wellspring of Power", SkillEffectType.FullManaRefill, 40),
            new Entry("Angels", 3, "Heavenly Veil", SkillEffectType.TeamDebuffImmunity, 55, buffDurationTurns: 3),
            new Entry("Angels", 4, "Second Wind", SkillEffectType.ReviveHero, 75, shieldPercentOfMaxHP: 0.5f),

            new Entry("Humans", 1, "Unity", SkillEffectType.ManaTransfer, 25, effectValue: 10),
            new Entry("Humans", 2, "Mentorship", SkillEffectType.ReduceAllyNextSkillCost, 40, shieldPercentOfMaxHP: 0.5f),
            new Entry("Humans", 3, "Mimicry", SkillEffectType.CopyAllyLastSkill, 55),
            new Entry("Humans", 4, "Resonance of Souls", SkillEffectType.BorrowAllyLegendarySkill, 75, buffDurationTurns: 3),
        };

        int created = 0;
        foreach (var e in entries)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillName = e.name;
            skill.description = $"{e.race}, tier {e.tier}: {e.effectType}";
            skill.costType = GetRaceColor(e.race);
            skill.cost = e.cost;
            skill.effectType = e.effectType;
            skill.effectValue = e.effectValue;
            skill.buffDurationTurns = e.buffDurationTurns;
            skill.damageMultiplier = e.damageMultiplier;
            skill.shieldPercentOfMaxHP = e.shieldPercentOfMaxHP;
            skill.hitCount = e.hitCount;

            string fileName = $"{e.race}_T{e.tier}_{e.effectType}.asset";
            string path = AssetDatabase.GenerateUniqueAssetPath($"{OutputFolder}/{fileName}");
            AssetDatabase.CreateAsset(skill, path);
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Создано {created} SkillData в {OutputFolder}");
    }

    // Elves-Green, Dwarves/Fairies-Yellow, Orcs-Red, Beastfolk-Blue заданы пользователем;
    // Dragonkin/Demons-Violet и Angels/Humans-Pink назначены по смыслу (Pink уже даёт лечение вместо урона).
    private static ResourceType GetRaceColor(string race)
    {
        switch (race)
        {
            case "Elves": return ResourceType.Green;
            case "Dwarves": return ResourceType.Yellow;
            case "Orcs": return ResourceType.Red;
            case "Beastfolk": return ResourceType.Blue;
            case "Dragonkin": return ResourceType.Violet;
            case "Demons": return ResourceType.Violet;
            case "Angels": return ResourceType.Pink;
            case "Humans": return ResourceType.Pink;
            default: return ResourceType.Red;
        }
    }
}
