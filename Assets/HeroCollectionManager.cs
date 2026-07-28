using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HeroCollectionManager : MonoBehaviour
{
    public static HeroCollectionManager Instance { get; private set; }

    [Header("Все герои игры (весь пул для генерации коллекции)")]
    public HeroData[] allHeroes;

    [Header("Состояние владения (заполняется при загрузке сохранения)")]
    public List<HeroOwnershipData> ownership = new List<HeroOwnershipData>();

    [Header("Выбранный отряд")]
    public List<HeroData> squad = new List<HeroData>();
    public const int BaseSquadSize = 4;

    // Количество слотов в отряде всегда фиксировано — Бараки НЕ добавляют слоты, только поднимают лимит веса
    public int MaxSquadSize => BaseSquadSize;

    [Header("Вес отряда")]
    public const int BaseSquadWeight = 4; // базовый лимит — хватает ровно на 4 героев весом 1 без каких-либо построек

    // Базовый лимит + бонус от здания SquadCapacity (Бараки), если построено
    public int MaxSquadWeight => BaseSquadWeight + GetSquadWeightBonus();

    // Суммарный вес героев, которые сейчас реально стоят в отряде
    public int CurrentSquadWeight => squad.Where(h => h != null).Sum(h => h.weight);

    private int GetSquadWeightBonus()
    {
        if (BuildingManager.Instance == null) return 0;

        var building = BuildingManager.Instance.allBuildings
            .FirstOrDefault(b => b != null && b.buildingType == BuildingType.SquadCapacity);
        if (building == null) return 0;

        var ownership = BuildingManager.Instance.GetOwnership(building.buildingId);
        if (ownership == null || !ownership.isBuilt) return 0;

        return building.GetSquadWeightBonus(ownership.level);
    }

    // Индекс слота, который сейчас редактируется (-1 = не в режиме выбора)
    public int slotBeingEdited = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeOwnershipIfMissing();
        LoadSquad();

        foreach (var hero in allHeroes)
            UnlockHero(hero); // ВРЕМЕННО для теста
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

    // Сколько опыта нужно набрать герою на указанном уровне, чтобы подняться на следующий
    public int ExperienceToNextLevel(int level) => level * 100;

    // Добавляет герою опыт (например, от расходного предмета) и поднимает уровень, пока опыта хватает.
    // Возвращает true, если герой найден и опыт применён.
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

    // Экипирован ли этот конкретный стек (по instanceId) хоть на одном герое сейчас.
    // Используется, чтобы скрыть экипированные предметы из каталога и из списка доноров жертвоприношения.
    public bool IsItemEquippedAnywhere(string itemInstanceId)
    {
        if (string.IsNullOrEmpty(itemInstanceId)) return false;
        return ownership.Any(o => o.equippedItems.Any(e => e.itemInstanceId == itemInstanceId));
    }

    // Снимает предмет со всех героев, на которых он сейчас экипирован (кроме exceptHeroId, если указан).
    // Используется при экипировке, чтобы предмет "переносился" между героями, а не дублировался.
    public void UnequipItemFromAllHeroes(string itemInstanceId, string exceptHeroId = null)
    {
        if (string.IsNullOrEmpty(itemInstanceId)) return;

        foreach (var heroOwnership in ownership)
        {
            if (heroOwnership.heroId == exceptHeroId) continue;

            var entry = heroOwnership.equippedItems.Find(e => e.itemInstanceId == itemInstanceId);
            if (entry != null) entry.itemInstanceId = null;
        }
    }

    // Вызывается, когда игрок нажимает на слот в отряде — запускает режим выбора
    public void StartEditingSlot(int slotIndex)
    {
        slotBeingEdited = slotIndex;
    }

    // Гарантирует, что в списке squad есть "место" под нужный индекс (заполняет null, если нужно)
    private void EnsureSquadSize()
    {
        while (squad.Count < MaxSquadSize)
            squad.Add(null);
    }

    // Сколько будет весить отряд, если именно сейчас назначить hero в slotBeingEdited (с учётом переноса героя из другого слота)
    public int GetProjectedSquadWeight(HeroData hero)
    {
        int existingIndex = squad.FindIndex(h => h != null && h.heroId == hero.heroId);

        int weight = hero.weight;
        for (int i = 0; i < squad.Count; i++)
        {
            if (i == slotBeingEdited || i == existingIndex) continue;
            if (squad[i] != null) weight += squad[i].weight;
        }

        return weight;
    }

    // Вызывается при выборе героя в коллекции, когда активен режим выбора слота
    public bool AssignToSlot(HeroData hero)
    {
        if (slotBeingEdited < 0 || slotBeingEdited >= MaxSquadSize) return false;
        if (!IsUnlocked(hero)) return false;

        EnsureSquadSize();

        if (GetProjectedSquadWeight(hero) > MaxSquadWeight) return false;

        // Если этот герой уже в другом слоте — убираем его оттуда (без дублей в отряде)
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
