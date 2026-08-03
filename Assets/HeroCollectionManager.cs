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

    // Индекс = (int)Rarity. Гем опыта героя ИМЕННО этого цвета — выдаётся вместо гема вознесения,
    // когда дубликат выпадает на герое, уже стоящем на максимуме вознесения своей редкости.
    [Header("Гемы опыта героя по редкости — награда за дубликат на максимуме вознесения")]
    public ItemData[] rarityExperienceGems;

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

    // Collection-экран как пикер героя для Boss Training (тот же приём, что и slotBeingEdited для отряда) —
    // см. CastleUI.OnBossTrainingClicked / HeroCollectionCardUI.OnSelected.
    public bool pickingForBossTraining;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadOwnership();
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
        if (data == null || data.isUnlocked) return;

        data.isUnlocked = true;
        SaveOwnership();
    }

    // Вызывается SummonService, когда призыв выпал на УЖЕ разблокированного героя (дубликат).
    // Даёт "гем" вознесения этому герою — либо, если герой уже на максимуме вознесения своей редкости
    // (гему больше некуда деваться), выдаёт N гемов опыта героя того же цвета редкости (см. rarityExperienceGems).
    public void HandleDuplicatePull(HeroData hero)
    {
        if (hero == null) return;

        var data = ownership.FirstOrDefault(o => o.heroId == hero.heroId);
        if (data == null) return;

        int maxAscension = HeroAscensionUtility.GetMaxAscension(hero.rarity);
        if (data.ascensionLevel >= maxAscension)
        {
            ItemData gem = GetRarityExperienceGem(hero.rarity);
            int count = HeroAscensionUtility.GetOverflowGemCount(hero.rarity);
            if (gem != null)
                ItemCollectionManager.Instance?.AddItemCopy(gem, count);
        }
        else
        {
            data.ascensionGems++;
        }

        SaveOwnership();
    }

    private ItemData GetRarityExperienceGem(Rarity rarity)
    {
        int index = (int)rarity;
        return rarityExperienceGems != null && index >= 0 && index < rarityExperienceGems.Length
            ? rarityExperienceGems[index] : null;
    }

    // Тратит гемы этого героя, чтобы поднять ступень вознесения на 1 (и вместе с ней — потолок уровня).
    // Возвращает false, если герой уже на максимуме вознесения своей редкости, либо не хватает гемов.
    public bool AscendHero(string heroId)
    {
        var hero = allHeroes.FirstOrDefault(h => h.heroId == heroId);
        var data = ownership.FirstOrDefault(o => o.heroId == heroId);
        if (hero == null || data == null) return false;

        int maxAscension = HeroAscensionUtility.GetMaxAscension(hero.rarity);
        if (data.ascensionLevel >= maxAscension) return false;
        if (data.ascensionGems < HeroAscensionUtility.GemsPerAscension) return false;

        data.ascensionGems -= HeroAscensionUtility.GemsPerAscension;
        data.ascensionLevel++;

        SaveOwnership();
        return true;
    }

    // Сколько опыта нужно набрать герою на указанном уровне, чтобы подняться на следующий
    public int ExperienceToNextLevel(int level) => level * 100;

    // Добавляет герою опыт (например, от расходного предмета) и поднимает уровень, пока опыта хватает —
    // но не выше потолка текущей ступени вознесения (см. HeroAscensionUtility.GetLevelCap).
    // Возвращает true, если герой найден и опыт применён (даже если он уже упёрся в потолок).
    public bool GrantExperience(string heroId, int amount)
    {
        var hero = allHeroes.FirstOrDefault(h => h.heroId == heroId);
        var data = ownership.FirstOrDefault(o => o.heroId == heroId);
        if (hero == null || data == null || amount <= 0) return false;

        int levelCap = HeroAscensionUtility.GetLevelCap(hero.rarity, data.ascensionLevel);
        if (data.level >= levelCap) return false; // уже на потолке — опыт девать некуда, пока не вознесётся дальше

        data.experience += amount;

        while (data.level < levelCap && data.experience >= ExperienceToNextLevel(data.level))
        {
            data.experience -= ExperienceToNextLevel(data.level);
            data.level++;
        }

        if (data.level >= levelCap)
            data.experience = 0; // упёрлись в потолок — остаток сгорает, а не копится "про запас"

        SaveOwnership();
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

    // Публичный — вызывать после любой прямой мутации HeroOwnershipData снаружи (экипировка/выбор скиллов
    // в HeroInventoryUI), поскольку это обычные C#-объекты, которые UI меняет напрямую в обход менеджера.
    public void SaveOwnership()
    {
        string serialized = string.Join(";", ownership.Select(SerializeHeroOwnership));
        PlayerPrefs.SetString("hero_ownership", serialized);
        PlayerPrefs.Save();
    }

    private static string SerializeHeroOwnership(HeroOwnershipData data)
    {
        string equipped = string.Join("|", data.equippedItems
            .Where(e => !string.IsNullOrEmpty(e.itemInstanceId))
            .Select(e => $"{(int)e.slotType},{e.itemInstanceId}"));

        return $"{data.heroId}:{(data.isUnlocked ? 1 : 0)}:{data.level}:{data.experience}:{data.activeSkillIndex}:{data.passiveSkillIndex}:{equipped}" +
               $":{data.ascensionGems}:{data.ascensionLevel}";
    }

    private void LoadOwnership()
    {
        ownership.Clear();

        string saved = PlayerPrefs.GetString("hero_ownership", "");
        if (string.IsNullOrEmpty(saved)) return;

        foreach (var entry in saved.Split(';'))
        {
            string[] parts = entry.Split(':');
            if (parts.Length < 7) continue;

            var data = new HeroOwnershipData
            {
                heroId = parts[0],
                isUnlocked = parts[1] == "1",
                level = int.Parse(parts[2]),
                experience = int.Parse(parts[3]),
                activeSkillIndex = int.Parse(parts[4]),
                passiveSkillIndex = int.Parse(parts[5]),
                // Добавлено позже вознесения ради — старые сохранения (7 частей, без этих двух) просто получат 0/0
                ascensionGems = parts.Length > 7 ? int.Parse(parts[7]) : 0,
                ascensionLevel = parts.Length > 8 ? int.Parse(parts[8]) : 0
            };

            string equippedBlock = parts[6];
            if (!string.IsNullOrEmpty(equippedBlock))
            {
                foreach (var itemEntry in equippedBlock.Split('|'))
                {
                    string[] itemParts = itemEntry.Split(',');
                    if (itemParts.Length != 2) continue;

                    data.equippedItems.Add(new EquippedItem
                    {
                        slotType = (EquipmentSlotType)int.Parse(itemParts[0]),
                        itemInstanceId = itemParts[1]
                    });
                }
            }

            ownership.Add(data);
        }
    }
}
