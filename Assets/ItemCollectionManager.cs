using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ItemCollectionManager : MonoBehaviour
{
    public static ItemCollectionManager Instance { get; private set; }

    [Header("Все предметы игры (каталог)")]
    public ItemData[] allItems;

    [Header("Состояние владения (отдельный стек на каждый itemId+уровень)")]
    public List<ItemOwnershipData> ownership = new List<ItemOwnershipData>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadOwnedItems();

        foreach (var item in allItems)
            UnlockItem(item); // ВРЕМЕННО для теста, как и герои/враги
    }

    public bool IsOwned(ItemData item) => item != null && ownership.Any(o => o.itemId == item.itemId);

    // Все стеки конкретного предмета (может быть несколько — по одному на каждый уникальный уровень/опыт)
    public List<ItemOwnershipData> GetStacks(string itemId)
    {
        return ownership.Where(o => o.itemId == itemId).ToList();
    }

    // Конкретный стек по его уникальному instanceId (используется для экипировки/жертвоприношения/списания)
    public ItemOwnershipData GetStackByInstanceId(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId)) return null;
        return ownership.FirstOrDefault(o => o.instanceId == instanceId);
    }

    // Суммарное количество копий предмета по всем стекам/уровням (для беджа в каталоге-браузере)
    public int GetTotalQuantity(string itemId)
    {
        return ownership.Where(o => o.itemId == itemId).Sum(o => o.quantity);
    }

    // Гарантирует, что предмет получен (есть хотя бы 1 стек). Повторные вызовы ничего не добавляют —
    // безопасно вызывать каждый раз при старте (например, в "разблокировать все предметы для теста").
    public void UnlockItem(ItemData item)
    {
        if (item == null || IsOwned(item)) return;

        CreateStack(item.itemId, level: 1, experience: 0, quantity: 1);
        SaveOwnedItems();
    }

    // Добавляет ещё одну(и) копию(и) предмета. Только что полученные копии всегда уровня 1 — присоединяются к уже
    // имеющемуся стеку уровня 1 (если есть) или образуют новый. Стеки более высокого уровня (прокачанные) не занимает.
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

    // Все предметы-экипировка определённого типа слота — и полученные, и ещё нет (для показа "наличия").
    // Расходные предметы (category == HeroExperience и т.п.) сюда не попадают — их нельзя экипировать.
    // Отсортировано по редкости (White -> Orange), в пределах одной редкости — по названию.
    public List<ItemData> GetItemsOfType(EquipmentSlotType slotType)
    {
        return allItems.Where(i => i.category == ItemCategory.Equipment && i.slotType == slotType)
            .OrderBy(i => (int)i.rarity).ThenBy(i => i.itemName)
            .ToList();
    }

    // Готовит ОДНУ копию предмета для экипировки: если у указанного стека quantity > 1, отделяет от него
    // 1 единицу в новый отдельный стек (тот же itemId/уровень/опыт, quantity=1) и возвращает ЕГО instanceId —
    // именно его и нужно экипировать, а остальные копии остаются свободными в инвентаре. Если quantity уже == 1,
    // возвращает тот же instanceId без изменений (нет нужды делить единственную копию).
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

    // Снимает одну единицу с количества КОНКРЕТНОГО стека (по instanceId). Стек удаляется, когда доходит до 0.
    // Возвращает true, если стек существовал и единицу удалось списать.
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

    // Сколько опыта нужно набрать на указанном уровне, чтобы подняться на следующий
    public int ExperienceToNextLevel(int level) => level * 50;

    // Суммарный опыт, вложенный в предмет, чтобы он достиг указанного уровня (с нуля) — сумма
    // ExperienceToNextLevel(1..level-1). Используется в "мердже" редкости, см. CalculateSacrificeGain.
    private int CumulativeExperience(int level) => 25 * level * Mathf.Max(0, level - 1);

    // Сколько опыта цель получит от ОДНОЙ единицы донора — общий расчёт для SacrificeItem (реальное
    // применение) и ItemSacrificeUI (превью до подтверждения), чтобы они не могли разойтись в цифрах.
    // Мердж редкости (донор НИЖЕ целевой редкости): цель забирает ВЕСЬ опыт, вложенный в донора
    // (накопленный + несожжённый остаток), не плоскую ставку sacrificeExperience — см. project_campaign_difficulty_curve.
    public int CalculateSacrificeGain(ItemData fuelData, int fuelLevel, int fuelExperience, ItemData targetData)
    {
        if (fuelData == null || targetData == null) return 0;

        return fuelData.rarity < targetData.rarity
            ? CumulativeExperience(fuelLevel) + fuelExperience
            : fuelData.sacrificeExperience * fuelLevel;
    }

    // Жертвуем ОДНУ копию из стека fuelInstanceId, чтобы поднять уровень стека targetInstanceId.
    // Если у целевого стека quantity > 1 — уровень получает только ОДНА единица: она отделяется
    // в новый отдельный стек (новая "ячейка"), а остальные (quantity-1) остаются на старом уровне.
    // resultingTargetInstanceId — instanceId стека, который фактически хранит новый уровень (может отличаться
    // от targetInstanceId, если произошло разделение) — используйте его для СЛЕДУЮЩЕГО вызова SacrificeItem
    // в рамках одного пакетного пожертвования, иначе каждый вызов снова будет делить исходный стек.
    // Максимальный уровень цели ограничен её редкостью (ItemData.GetMaxLevel).
    // wastedExperience — сколько опыта "сгорело" сверх порога максимального уровня.
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

        // Раз кап уровня теперь один и тот же для всех редкостей (EquipmentStatCurve.MaxLevel), мердж
        // (см. CalculateSacrificeGain) переносит цель ровно на тот же уровень, что был у донора —
        // без пересчёта диапазонов, которых больше нет.
        int gainedExperience = CalculateSacrificeGain(fuelData, fuelStack.level, fuelStack.experience, targetData);

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
        int levelBefore = leveledStack.level;

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

        if (leveledStack.level > levelBefore)
            DailyQuestManager.Instance?.ReportItemLeveledUp();

        MergeIdenticalStacks(leveledStack);
        resultingTargetInstanceId = leveledStack.instanceId;

        SaveOwnedItems();
        return true;
    }

    // Если после разделения/апгрейда в системе уже есть ДРУГОЙ стек с таким же itemId+уровнем+опытом —
    // объединяем их (суммируем quantity), чтобы фактически одинаковые предметы не плодили лишние ячейки.
    private void MergeIdenticalStacks(ItemOwnershipData stack)
    {
        var twin = ownership.FirstOrDefault(o => o != stack && o.itemId == stack.itemId
            && o.level == stack.level && o.experience == stack.experience);
        if (twin == null) return;

        twin.quantity += stack.quantity;
        ownership.Remove(stack);
    }

    // Прогноз результата (без применения): каким будет уровень/опыт/потерянный опыт, если добавить totalGainedXp к указанному предмету
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

            // Совместимость со старым форматом сохранения (itemId:level:experience:quantity, без instanceId)
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
