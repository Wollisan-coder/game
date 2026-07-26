using UnityEngine;

[CreateAssetMenu(fileName = "NewHero", menuName = "Battle/Hero")]
public class HeroData : ScriptableObject
{
    [Header("Ідентифікатор (не змінювати після релізу!)")]
    public string heroId; // унікальний, стабільний ID — не залежить від імені файлу

    [Header("Основні параметри (все в одному блоці)")]
    public string heroName;
    public int maxHealth = 100;         // індивідуальне здоров'я героя (HeroRuntimeState.currentHealth)
    public int maxResource = 50;        // максимальна манна
    public float damageMultiplier = 1f; // особистий множник урону цього героя
    public int weight = 1;              // вага картки — сума ваг героїв у загоні обмежена HeroCollectionManager.MaxSquadWeight
    public SkillData[] skills;          // кількість умінь = довжина масиву, окреме поле не потрібне

    [Header("Візуал")]
    public Sprite portrait;
    public Color themeColor = Color.white;
    public ResourceType resourceType;

    [Header("Рідкість (впливає на шанс випадіння в Алтарі)")]
    public Rarity rarity = Rarity.White;

    [Header("Опис")]
    [TextArea(3, 6)] public string description;

    // Автоматично підставляє heroId = імені файлу при першому створенні,
    // якщо поле ще порожнє — щоб не треба було вручну заповнювати для існуючих героїв
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(heroId))
            heroId = name;
    }

    public Color GetRarityColor() => RarityUtility.GetColor(rarity);
}