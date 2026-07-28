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
    public int weight = 1;              // вес карточки — сумма весов героев в отряде ограничена HeroCollectionManager.MaxSquadWeight
    public SkillData[] skills;          // количество умений = длина массива, отдельное поле не нужно

    [Header("Визуал")]
    public Sprite portrait;
    public Color themeColor = Color.white;
    public ResourceType resourceType;

    [Header("Редкость (влияет на шанс выпадения в Алтаре)")]
    public Rarity rarity = Rarity.White;

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