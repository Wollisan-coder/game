using UnityEngine;

[CreateAssetMenu(fileName = "NewHero", menuName = "Battle/Hero")]
public class HeroData : ScriptableObject
{
    [Header("Идентификатор (не менять после релиза!)")]
    public string heroId; // уникальный, стабильный ID — не зависит от имени файла

    [Header("Основные параметры (всё в одном блоке)")]
    public string heroName;
    public int maxHealth = 100;         // индивидуальное здоровье героя (HeroRuntimeState.currentHealth)
    public int maxResource = 50;        // максимальна манна
    public float damageMultiplier = 1f; // особистий множник урону цього героя
    public int armor = 5;               // базовая броня героя САМОГО ПО СЕБЕ (без экипировки) — см. HeroStatUtility
    public int weight = 1;              // вес карточки — сумма весов героев в отряде ограничена HeroCollectionManager.MaxSquadWeight
    public SkillData[] skills;          // количество умений = длина массива, отдельное поле не нужно

    [Header("Визуал")]
    public Sprite portrait;
    public Color themeColor = Color.white;
    public ResourceType resourceType;

    // Награда за ПОЛНОЕ вознесение (см. HeroAscensionUtility.IsMaxAscension) — не для Green/Blue, у них
    // вознесения нет вообще. Пусто (null) = нет особого арта, показывать обычный portrait как раньше.
    [Header("Арт полного вознесения (опционально — см. project_hero_ascension_system)")]
    public Sprite ascendedPortrait;

    // Апгрейд пассивки на полном вознесении — ТОЛЬКО для Orange (см. HeroAscensionUtility). ПОКА НЕ
    // ПРИМЕНЯЕТСЯ В БОЮ: пассивки сейчас вообще не исполняют effectType, только резервируют ману
    // (см. BattleManager.Awake) — это отдельный, более ранний пробел, не специфичный для вознесения.
    // Поле-заготовка под контент, ждёт, когда пассивки в принципе начнут применять эффект.
    [Header("Апгрейд пассивки на полном вознесении (только Orange — см. комментарий, пока не работает в бою)")]
    public SkillData ascendedPassiveSkill;

    [Header("Раса (для фильтра в коллекции — см. HeroCollectionUI)")]
    public Race race;

    // White исключён из выдачи (Алтарь/Кузня больше не спавнят её) — новые ассеты по умолчанию создаются с Green
    [Header("Редкость (влияет на шанс выпадения в Алтаре)")]
    public Rarity rarity = Rarity.Green;

    [Header("Опис")]
    [TextArea(3, 6)] public string description;

    // Автоматически подставляет heroId = имени файла при первом создании,
    // если поле ещё пустое — чтобы не нужно было вручную заполнять для существующих героев
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(heroId))
            heroId = name;
    }

    public Color GetRarityColor() => RarityUtility.GetColor(rarity);
}