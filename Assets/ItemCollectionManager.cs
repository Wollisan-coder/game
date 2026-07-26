using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ItemCollectionManager : MonoBehaviour
{
    public static ItemCollectionManager Instance { get; private set; }

    [Header("Всі предмети гри (каталог)")]
    public ItemData[] allItems;

    [Header("Стан володіння (рівень і досвід кожного отриманого предмета)")]
    public List<ItemOwnershipData> ownership = new List<ItemOwnershipData>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadOwnedItems();

        foreach (var item in allItems)
            UnlockItem(item); // ВРЕМЕННО для тесту, як і герої/вороги
    }

    public bool IsOwned(ItemData item) => item != null && ownership.Any(o => o.itemId == item.itemId);

    public ItemOwnershipData GetOwnership(string itemId)
    {
        return ownership.FirstOrDefault(o => o.itemId == itemId);
    }

    public int GetLevel(string itemId)
    {
        var data = GetOwnership(itemId);
        return data != null ? data.level : 0;
    }

    public int GetQuantity(string itemId)
    {
        var data = GetOwnership(itemId);
        return data != null ? data.quantity : 0;
    }

    // Гарантує, що предмет отриманий (є хоча б 1 копія). Повторні виклики нічого не додають —
    // безпечно викликати щоразу при старті (наприклад, у "розблокувати всі предмети для тесту").
    public void UnlockItem(ItemData item)
    {
        if (item == null || IsOwned(item)) return;

        ownership.Add(new ItemOwnershipData { itemId = item.itemId, level = 1, experience = 0, quantity = 1 });
        SaveOwnedItems();
    }

    // Додає ще одну(і) копію(ї) предмета до вже наявного стеку (або створює новий стек, якщо предмета ще немає).
    // Використовувати для реальної видачі предметів (нагороди, магазин тощо) — на відміну від UnlockItem, завжди збільшує кількість.
    public void AddItemCopy(ItemData item, int count = 1)
    {
        if (item == null || count <= 0) return;

        var existing = GetOwnership(item.itemId);
        if (existing != null)
        {
            existing.quantity += count;
        }
        else
        {
            ownership.Add(new ItemOwnershipData { itemId = item.itemId, level = 1, experience = 0, quantity = count });
        }

        SaveOwnedItems();
    }

    public ItemData GetItemById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return allItems.FirstOrDefault(i => i.itemId == id);
    }

    // Всі предмети-екіпіровка певного типу слота — і отримані, і ще ні (для показу "наявності").
    // Витратні предмети (category == HeroExperience тощо) сюди не потрапляють — їх не можна екіпірувати.
    // Відсортовано за рідкістю (White -> Orange), в межах однієї рідкості — за назвою.
    public List<ItemData> GetItemsOfType(EquipmentSlotType slotType)
    {
        return allItems.Where(i => i.category == ItemCategory.Equipment && i.slotType == slotType)
            .OrderBy(i => (int)i.rarity).ThenBy(i => i.itemName)
            .ToList();
    }

    // Знімає одну одиницю з кількості предмета (видаляє стек, коли доходить до 0).
    // Повертає true, якщо предмет був у володінні і одиницю вдалося списати.
    public bool ConsumeItem(string itemId)
    {
        var data = GetOwnership(itemId);
        if (data == null) return false;

        data.quantity--;
        if (data.quantity <= 0)
            ownership.Remove(data);

        SaveOwnedItems();
        return true;
    }

    // Скільки досвіду потрібно назбирати на вказаному рівні, щоб піднятись на наступний
    public int ExperienceToNextLevel(int level) => level * 50;

    // Множник бонусів предмета від його поточного рівня (+10% за кожен рівень понад 1-й)
    public float GetLevelMultiplier(string itemId)
    {
        int level = GetLevel(itemId);
        if (level <= 0) level = 1;
        return 1f + 0.1f * (level - 1);
    }

    // Жертвуємо ОДНУ копію предмета fuelItemId, щоб підняти рівень предмета targetItemId.
    // Знімається лише 1 одиниця з кількості донора (quantity--); стек видаляється з володіння, тільки коли кількість сягає 0.
    // Максимальний рівень цілі обмежений її рідкістю (ItemData.GetMaxLevel).
    // wastedExperience — скільки досвіду "згоріло" понад поріг максимального рівня (якщо предмет саме зараз досяг максимуму).
    public bool SacrificeItem(string fuelItemId, string targetItemId, out int wastedExperience)
    {
        wastedExperience = 0;

        if (string.IsNullOrEmpty(fuelItemId) || string.IsNullOrEmpty(targetItemId) || fuelItemId == targetItemId)
            return false;

        var fuelData = GetItemById(fuelItemId);
        var targetData = GetItemById(targetItemId);
        var fuelOwnership = GetOwnership(fuelItemId);
        var targetOwnership = GetOwnership(targetItemId);

        if (fuelData == null || targetData == null || fuelOwnership == null || targetOwnership == null) return false;

        int maxLevel = targetData.GetMaxLevel();
        if (targetOwnership.level >= maxLevel) return false;

        int gainedExperience = fuelData.sacrificeExperience * fuelOwnership.level;

        fuelOwnership.quantity--;
        if (fuelOwnership.quantity <= 0)
            ownership.Remove(fuelOwnership);

        targetOwnership.experience += gainedExperience;

        while (targetOwnership.level < maxLevel && targetOwnership.experience >= ExperienceToNextLevel(targetOwnership.level))
        {
            targetOwnership.experience -= ExperienceToNextLevel(targetOwnership.level);
            targetOwnership.level++;
        }

        if (targetOwnership.level >= maxLevel && targetOwnership.experience > 0)
        {
            wastedExperience = targetOwnership.experience;
            targetOwnership.experience = 0;
        }

        SaveOwnedItems();
        return true;
    }

    // Прогноз результату (без застосування): який буде рівень/досвід/втрачений досвід, якщо додати totalGainedXp до вказаного предмета
    public (int level, int experience, int wastedExperience) SimulateExperienceGain(int baseLevel, int baseExperience, int totalGainedXp, int maxLevel)
    {
        int level = baseLevel;
        int experience = baseExperience + totalGainedXp;

        while (level < maxLevel && experience >= ExperienceToNextLevel(level))
        {
            experience -= ExperienceToNextLevel(level);
            level++;
        }

        int wasted = 0;
        if (level >= maxLevel && experience > 0)
        {
            wasted = experience;
            experience = 0;
        }

        return (level, experience, wasted);
    }

    private void SaveOwnedItems()
    {
        string serialized = string.Join(";", ownership.Select(o => $"{o.itemId}:{o.level}:{o.experience}:{o.quantity}"));
        PlayerPrefs.SetString("item_ownership", serialized);
        PlayerPrefs.Save();
    }

    private void LoadOwnedItems()
    {
        ownership.Clear();

        string saved = PlayerPrefs.GetString("item_ownership", "");
        if (string.IsNullOrEmpty(saved)) return;

        foreach (var entry in saved.Split(';'))
        {
            string[] parts = entry.Split(':');
            if (parts.Length < 3) continue;

            ownership.Add(new ItemOwnershipData
            {
                itemId = parts[0],
                level = int.Parse(parts[1]),
                experience = int.Parse(parts[2]),
                quantity = parts.Length >= 4 ? int.Parse(parts[3]) : 1 // сумісність зі старими збереженнями без кількості
            });
        }
    }
}
