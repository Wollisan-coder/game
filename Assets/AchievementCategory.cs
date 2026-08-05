// Категории пожизненных (не сбрасываются) многоуровневых ачивок — см. AchievementManager.
// AccountLevel/LoginStreak — особый случай: прогресс не суммируется, а хранит МАКСИМУМ когда-либо
// достигнутого значения (см. AchievementManager.ReportValue), остальные — обычные накопительные счётчики.
public enum AchievementCategory
{
    DefeatEnemies,
    CompleteQuests,
    ClearMapNodes,
    CollectHeroes,
    UseSkills,
    CompleteSummons,
    Ascension,
    BuildingUpgrades,
    BossTrainingTotal,
    TerritoriesCleared,
    AccountLevel,
    GambleSpins,
    LoginStreak,
    FarmDungeonsCompleted
}
