using UnityEngine;

[CreateAssetMenu(fileName = "NewHero", menuName = "Battle/Hero")]
public class HeroData : ScriptableObject
{
    [Header("Ідентифікатор (не змінювати після релізу!)")]
    public string heroId; // унікальний, стабільний ID — не залежить від імені файлу

    [Header("Основна інформація")]
    public string heroName;
    public ResourceType resourceType;

    [Header("Візуал")]
    public Sprite portrait;
    public Color themeColor = Color.white;

    [Header("Ресурс героя")]
    public int maxResource = 50;

    [Header("Умінна")]
    public SkillData[] skills;

    // Автоматично підставляє heroId = імені файлу при першому створенні,
    // якщо поле ще порожнє — щоб не треба було вручну заповнювати для існуючих героїв
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(heroId))
            heroId = name;
    }
}