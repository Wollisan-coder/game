using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HeroCollectionManager : MonoBehaviour
{
    public static HeroCollectionManager Instance { get; private set; }

    [Header("Всі герої гри (весь пул для генерації колекції)")]
    public HeroData[] allHeroes;

    [Header("Стан володіння (заповнюється при завантаженні збереження)")]
    public List<HeroOwnershipData> ownership = new List<HeroOwnershipData>();

    [Header("Обраний загін")]
    public List<HeroData> squad = new List<HeroData>();
    public const int BaseSquadSize = 4;

    // Базова місткість + бонус від будівлі SquadCapacity (якщо збудована)
    public int MaxSquadSize => BaseSquadSize + GetSquadCapacityBonus();

    private int GetSquadCapacityBonus()
    {
        if (BuildingManager.Instance == null) return 0;

        var building = BuildingManager.Instance.allBuildings
            .FirstOrDefault(b => b != null && b.buildingType == BuildingType.SquadCapacity);
        if (building == null) return 0;

        var ownership = BuildingManager.Instance.GetOwnership(building.buildingId);
        if (ownership == null || !ownership.isBuilt) return 0;

        return building.GetSquadCapacityBonus(ownership.level);
    }

    // Індекс слота, який зараз редагується (-1 = не в режимі вибору)
    public int slotBeingEdited = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeOwnershipIfMissing();
        LoadSquad();

        foreach (var hero in allHeroes)
            UnlockHero(hero); // ВРЕМЕННО для тесту
    }

    private void InitializeOwnershipIfMissing()
    {
        foreach (var hero in allHeroes)
        {
            if (!ownership.Any(o => o.heroId == hero.heroId))
            {
                ownership.Add(new HeroOwnershipData
                {
                    heroId = hero.heroId,
                    isUnlocked = false
                });
            }
        }
    }

    public bool IsUnlocked(HeroData hero)
    {
        var data = ownership.FirstOrDefault(o => o.heroId == hero.heroId);
        return data != null && data.isUnlocked;
    }

    public void UnlockHero(HeroData hero)
    {
        var data = ownership.FirstOrDefault(o => o.heroId == hero.heroId);
        if (data != null) data.isUnlocked = true;
    }

    // Скільки досвіду потрібно назбирати герою на вказаному рівні, щоб піднятись на наступний
    public int ExperienceToNextLevel(int level) => level * 100;

    // Додає герою досвід (наприклад, від витратного предмета) і піднімає рівень, поки досвіду вистачає.
    // Повертає true, якщо герой знайдений і досвід застосовано.
    public bool GrantExperience(string heroId, int amount)
    {
        var data = ownership.FirstOrDefault(o => o.heroId == heroId);
        if (data == null || amount <= 0) return false;

        data.experience += amount;

        while (data.experience >= ExperienceToNextLevel(data.level))
        {
            data.experience -= ExperienceToNextLevel(data.level);
            data.level++;
        }

        return true;
    }

    // Знімає предмет з усіх героїв, на яких він зараз екіпірований (крім exceptHeroId, якщо вказано).
    // Використовується при екіпіровці, щоб предмет "переносився" між героями, а не дублювався.
    public void UnequipItemFromAllHeroes(string itemId, string exceptHeroId = null)
    {
        if (string.IsNullOrEmpty(itemId)) return;

        foreach (var heroOwnership in ownership)
        {
            if (heroOwnership.heroId == exceptHeroId) continue;

            var entry = heroOwnership.equippedItems.Find(e => e.itemId == itemId);
            if (entry != null) entry.itemId = null;
        }
    }

    // Викликається, коли гравець натискає на слот у загоні — запускає режим вибору
    public void StartEditingSlot(int slotIndex)
    {
        slotBeingEdited = slotIndex;
    }

    // Гарантує, що в списку squad є "місце" під потрібний індекс (заповнює null, якщо треба)
    private void EnsureSquadSize()
    {
        while (squad.Count < MaxSquadSize)
            squad.Add(null);
    }

    // Викликається при виборі героя в колекції, коли активний режим вибору слота
    public bool AssignToSlot(HeroData hero)
    {
        if (slotBeingEdited < 0 || slotBeingEdited >= MaxSquadSize) return false;
        if (!IsUnlocked(hero)) return false;

        EnsureSquadSize();

        // Якщо цей герой вже в іншому слоті — прибираємо його звідти (без дублів у загоні)
        int existingIndex = squad.FindIndex(h => h != null && h.heroId == hero.heroId);
        if (existingIndex >= 0 && existingIndex != slotBeingEdited)
            squad[existingIndex] = null;

        squad[slotBeingEdited] = hero;
        slotBeingEdited = -1;

        SaveSquad();
        return true;
    }

    public void RemoveFromSquad(int slotIndex)
    {
        EnsureSquadSize();
        if (slotIndex >= 0 && slotIndex < squad.Count)
            squad[slotIndex] = null;

        SaveSquad();
    }

    private void SaveSquad()
    {
        EnsureSquadSize();
        string ids = string.Join(",", squad.Select(h => h != null ? h.heroId : ""));
        PlayerPrefs.SetString("squad_ids", ids);
        PlayerPrefs.Save();
    }

    private void LoadSquad()
    {
        squad.Clear();
        EnsureSquadSize();

        string saved = PlayerPrefs.GetString("squad_ids", "");
        if (string.IsNullOrEmpty(saved)) return;

        string[] ids = saved.Split(',');
        for (int i = 0; i < ids.Length && i < MaxSquadSize; i++)
        {
            if (string.IsNullOrEmpty(ids[i])) continue;
            var hero = allHeroes.FirstOrDefault(h => h.heroId == ids[i]);
            if (hero != null) squad[i] = hero;
        }
    }
}