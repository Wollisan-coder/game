public enum CurrencyType
{
    Wood,           // на апгрейд зданий
    Stone,          // на апгрейд зданий
    SummonShards,   // основная валюта призыва (Алтарь и Кузня) — производится зданием + награды за бои/задания
    PremiumGems,    // донатная валюта призыва — микротранзакции + особые условия, с гарантом
    ProgressPoints, // "ОП" — общий прогресс аккаунта, копится за первое прохождение story-уровней/дейлики

    // Добавлены позже, в рамках пересборки гем-экономики (см. project_gem_economy_v2_redesign_pending) —
    // append-only, не переставлять местами (сериализуется как int в PlayerCurrencies/HeroCollectionManager).
    HeroExperience, // единая валюта опыта героя — заменяет собой предметы rarityExperienceGems
    SoulShards,     // переполнение дублей Purple-героя на максимуме вознесения — под будущий магазин обмена
    ArmorShards     // экономика Wondrous Armor (куск 6 Death Dungeon) — распыл/бонус за победу/3->доп. скин
}
