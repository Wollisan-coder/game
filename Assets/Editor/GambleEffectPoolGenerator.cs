using UnityEditor;
using UnityEngine;

// Создаёт BoardEffectPoolData с эффектами гемблинг-колеса по таблице пользователя.
// Меню: Battle -> Generate Gamble Effect Pool.
public static class GambleEffectPoolGenerator
{
    private const string OutputPath = "Assets/SkillsHero/Generated/GambleEffectPool.asset";

    [MenuItem("Battle/Generate Gamble Effect Pool")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder("Assets/SkillsHero"))
            AssetDatabase.CreateFolder("Assets", "SkillsHero");
        if (!AssetDatabase.IsValidFolder("Assets/SkillsHero/Generated"))
            AssetDatabase.CreateFolder("Assets/SkillsHero", "Generated");

        var pool = ScriptableObject.CreateInstance<BoardEffectPoolData>();

        pool.positiveEffects = new[]
        {
            new BoardEffectDefinition { effectType = BoardEffectType.FullManaAllHeroes },
            new BoardEffectDefinition { effectType = BoardEffectType.FreeHitOnEnemy, amount = 40 },
            new BoardEffectDefinition { effectType = BoardEffectType.HealPercentMaxHP, multiplier = 0.5f },
            new BoardEffectDefinition { effectType = BoardEffectType.ShieldTeamTurns, amount = 20, turns = 2 },
            new BoardEffectDefinition { effectType = BoardEffectType.ExtraTurnsFree, amount = 3 },
        };

        pool.negativeEffects = new[]
        {
            new BoardEffectDefinition { effectType = BoardEffectType.FreezeRandomRowOrColumn, turns = 2 },
            new BoardEffectDefinition { effectType = BoardEffectType.WeakenHeroes, multiplier = 0.7f, turns = 2 },
            new BoardEffectDefinition { effectType = BoardEffectType.StunRandomHero, turns = 2 },
            new BoardEffectDefinition { effectType = BoardEffectType.EnemyDamageBuff, multiplier = 1.3f, turns = 2 },
            new BoardEffectDefinition { effectType = BoardEffectType.BlockHeroSkill, turns = 2 },
        };

        string path = AssetDatabase.GenerateUniqueAssetPath(OutputPath);
        AssetDatabase.CreateAsset(pool, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Создан BoardEffectPoolData: {path}");
    }
}
