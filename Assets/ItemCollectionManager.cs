using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ItemCollectionManager : MonoBehaviour
{
    public static ItemCollectionManager Instance { get; private set; }

    [Header("Всі предмети гри (каталог)")]
    public ItemData[] allItems;

    [Header("Стан володіння (окремий стек на кожен itemId+рівень)")]
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

    // Всі стеки конкретного предмета (може бути кілька — по одному на кожен унікальний рівень/досвід)
    public List<ItemOwnershipData> GetStacks(string itemId)
    {
        return ownership.Where(o => o.itemId == itemId).ToList();
    }

    // Конкретний стек за його унікальним instanceId (використовується для екіпіровки/жертвоприношення/списання)
    public ItemOwnershipData GetStackByInstanceId(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId)) return null;
        return ownership.FirstOrDefault(o => o.instanceId == instanceId);
    }

    // Сумарна кількість копій предмета по всіх стеках/рівнях (для бейджа в каталозі-браузері)
    public int GetTotalQuantity(string itemId)
    {
        return ownership.Where(o => o.itemId == itemId).Sum(o => o.quantity);
    }

    // Гарантує, що предмет отриманий (є хоча б 1 стек). Повторні виклики нічого не додають —
    // безпечно викликати щоразу при старті (наприклад, у "розблокувати всі предмети для тесту").
    public void UnlockItem(ItemData item)
    {
        if (item == null || IsOwned(item)) return;

        CreateStack(item.itemId, level: 1, experience: 0, quantity: 1);
        SaveOwnedItems();
    }

    // Додає ще одну(і) копію(ї) предмета. Щойно отримані копії завжди рівня 1 — приєднуються до вже
    // наявного стека рівня 1 (якщо є) або утворюють новий. Стеки вищого рівня (прокачані) не займає.
    public void AddItemCopy(ItemData item, int count = 1)
    {
        if (item == null || count <= 0) return;

        var level1Stack = ownership.FirstOrDefault(o => o.itemId == item.itemId && o.level == 1);
        if (level1Stack != null)
            level1Stack.quantity += count;
        else
            CreateStack(item.itemId, level: 1, experience: 0, quantity: count);

        SaveOwnedItems();
    }

    private ItemOwnershipData CreateStack(string itemId, int level, int experience, int quantity)
    {
        var stack = new ItemOwnershipData
        {
            instanceId = System.Guid.NewGuid().ToString("N"),
            itemId = itemId,
            level = level,
            experience = experience,
            quantity = quantity
        };
        ownership.Add(stack);
        return stack;
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

    // Готує ОДНУ копію предмета для екіпіровки: якщо у вказаного стека quantity > 1, відділяє від нього
    // 1 одиницю в новий окремий стек (той самий itemId/рівень/досвід, quantity=1) і повертає ЙОГО instanceId —
    // саме його й треба екіпірувати, а решта копій лишаються вільними в інвентарі. Якщо quantity вже == 1,
    // повертає той самий instanceId без змін (нема потреби ділити єдину копію).
    public string SplitOneForEquip(string instanceId)
    {
        var stack = GetStackByInstanceId(instanceId);
        if (stack == null) return null;

        if (stack.quantity <= 1) return instanceId;

        stack.quantity--;
        var newStack = CreateStack(stack.itemId, stack.level, stack.experience, 1);
        SaveOwnedItems();

        return newStack.instanceId;
    }

    // Знімає одну одиницю з кількості КОНКРЕТНОГО стека (за instanceId). Стек видаляється, коли доходить до 0.
    // Повертає true, якщо стек існував і одиницю вдалося списати.
    public bool ConsumeItem(string instanceId)
    {
        var data = GetStackByInstanceId(instanceId);
        if (data == null) return false;

        data.quantity--;
        if (data.quantity <= 0)
            ownership.Remove(data);

        SaveOwnedItems();
        return true;
    }

    // Скільки досвіду потрібно назбирати на вказаному рівні, щоб піднятись на наступний
    public int ExperienceToNextLevel(int level) => level * 50;

    // Множник бонусів предмета від вказаного рівня (+10% за кожен рівень понад 1-й)
    public float GetLevelMultiplierForLevel(int level)
    {
        if (level <= 0) level = 1;
        return 1f + 0.1f * (level - 1);
    }

    // Жертвуємо ОДНУ копію зі стека fuelInstanceId, щоб підняти рівень стека targetInstanceId.
    // Якщо в цільового стека quantity > 1 — рівень отримує лише ОДНА одиниця: вона відділяється
    // в новий окремий стек (нова "ячейка"), а решта (quantity-1) залишається на старому рівні.
    // resultingTargetInstanceId — instanceId стека, який фактично зберігає новий рівень (може відрізнятись
    // від targetInstanceId, якщо стався поділ) — використовуйте його для НАСТУПНОГО виклику SacrificeItem
    // у межах одного пакетного пожертвування, інакше кожен виклик знову ділитиме вихідний стек.
    // Максимальний рівень цілі обмежений її рідкістю (ItemData.GetMaxLevel).
    // wastedExperience — скільки досвіду "згоріло" понад поріг максимального рівня.
    public bool SacrificeItem(string fuelInstanceId, string targetInstanceId, out int wastedExperience, out string resultingTargetInstanceId)
    {
        wastedExperience = 0;
        resultingTargetInstanceId = targetInstanceId;

        if (string.IsNullOrEmpty(fuelInstanceId) || string.IsNullOrEmpty(targetInstanceId) || fuelInstanceId == targetInstanceId)
            return false;

        var fuelStack = GetStackByInstanceId(fuelInstanceId);
        var targetStack = GetStackByInstanceId(targetInstanceId);
        if (fuelStack == null || targetStack == null) return false;

        var fuelData = GetItemById(fuelStack.itemId);
        var targetData = GetItemById(targetStack.itemId);
        if (fuelData == null || targetData == null) return false;

        int maxLevel = targetData.GetMaxLevel();
        if (targetStack.level >= maxLevel) return false;

        int gainedExperience = fuelData.sacrificeExperience * fuelStack.level;

        fuelStack.quantity--;
        if (fuelStack.quantity <= 0)
            ownership.Remove(fuelStack);

        ItemOwnershipData leveledStack;
        if (targetStack.quantity > 1)
        {
            targetStack.quantity--;
            leveledStack = CreateStack(targetStack.itemId, targetStack.level, targetStack.experience, 1);
        }
        else
        {
            leveledStack = targetStack;
        }

        leveledStack.experience += gainedExperience;

        while (leveledStack.level < maxLevel && leveledStack.experience >= ExperienceToNextLevel(leveledStack.level))
        {
            leveledStack.experience -= ExperienceToNextLevel(leveledStack.level);
            leveledStack.level++;
        }

        if (leveledStack.level >= maxLevel && leveledStack.experience > 0)
        {
            wastedExperience = leveledStack.experience;
            leveledStack.experience = 0;
        }

        MergeIdenticalStacks(leveledStack);
        resultingTargetInstanceId = leveledStack.instanceId;

        SaveOwnedItems();
        return true;
    }

    // Якщо після поділу/апгрейду в системі вже є ІНШИЙ стек з таким самим itemId+рівнем+досвідом —
    // об'єднуємо їх (сумуємо quantity), щоб фактично однакові предмети не плодили зайві ячейки.
    private void MergeIdenticalStacks(ItemOwnershipData stack)
    {
        var twin = ownership.FirstOrDefault(o => o != stack && o.itemId == stack.itemId
            && o.level == stack.level && o.experience == stack.experience);
        if (twin == null) return;

        twin.quantity += stack.quantity;
        ownership.Remove(stack);
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
        string serialized = string.Join(";", ownership.Select(o => $"{o.instanceId}:{o.itemId}:{o.level}:{o.experience}:{o.quantity}"));
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

            // Сумісність зі старим форматом збереження (itemId:level:experience:quantity, без instanceId)
            if (parts.Length == 4)
            {
                ownership.Add(new ItemOwnershipData
                {
                    instanceId = System.Guid.NewGuid().ToString("N"),
                    itemId = parts[0],
                    level = int.Parse(parts[1]),
                    experience = int.Parse(parts[2]),
                    quantity = int.Parse(parts[3])
                });
            }
            else if (parts.Length >= 5)
            {
                ownership.Add(new ItemOwnershipData
                {
                    instanceId = parts[0],
                    itemId = parts[1],
                    level = int.Parse(parts[2]),
                    experience = int.Parse(parts[3]),
                    quantity = int.Parse(parts[4])
                });
            }
        }
    }
}
