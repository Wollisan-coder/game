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
    }

    public bool IsOwned(ItemData item) => item != null && ownership.Any(o => o.itemId == item.itemId);

    // Есть ли реальный кандидат на жертву для апгрейда стека excludeInstanceId — та же фильтрация, что
    // использует ItemSacrificeUI.GetCandidates (исключаем сам стек, экипированные где-либо предметы и
    // не-Equipment категории). Общий источник правды для любой кнопки "Upgrade", чтобы она не включалась,
    // когда ItemSacrificeUI на самом деле откроется на пустой экран.
    public bool HasSacrificeCandidates(string excludeInstanceId)
    {
        var heroManager = HeroCollectionManager.Instance;
        // Тот же набор категорий, что ItemSacrificeUI.GetCandidates() — иначе кнопка Upgrade осталась бы
        // скрытой для игрока, у которого есть только предметы ItemExperience и нет обычной экипировки-донора.
        return ownership.Any(o => o.instanceId != excludeInstanceId
            && (heroManager == null || !heroManager.IsItemEquippedAnywhere(o.instanceId))
            && (GetItemById(o.itemId)?.category == ItemCategory.Equipment || GetItemById(o.itemId)?.category == ItemCategory.ItemExperience));
    }

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

    // Дебажный грант — создаёт новый стек указанного предмета сразу на нужном уровне (level 1..GetMaxLevel()),
    // опыт = 0. Используется DebugConstructorUI, чтобы не гриндить обычный путь "получи копию -> прокачай
    // жертвоприношением" ради теста конкретного уровня экипировки. Возвращает instanceId нового стека.
    public string GrantItemAtLevel(ItemData item, int level)
    {
        if (item == null) return null;

        int clamped = Mathf.Clamp(level, 1, item.GetMaxLevel());
        var stack = CreateStack(item.itemId, clamped, 0, 1);
        SaveOwnedItems();
        return stack.instanceId;
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

    // "Сдача" вместо сжигаемого излишка при прокачке предмета жертвоприношением (см. SacrificeItem.wastedExperience,
    // вызывается из ItemSacrificeUI.ApplySacrifice суммой totalXp за весь пакет). Разбивает totalXp на минимальный
    // набор ItemData категории ItemExperience — КРУПНЫЙ номинал вперёд (в отличие от HeroUpgradeUI.ComputeSpendPlan,
    // где мелкий вперёд при ТРАТЕ — здесь наоборот, при ВЫДАЧЕ меньше предметов лучше). Список номиналов полностью
    // на данных (allItems), новый номинал не требует правок кода. Остаток МЕНЬШЕ самого мелкого номинала
    // округляется ВВЕРХ до одного целого предмета — по прямой просьбе пользователя 2026-08-20 игрок никогда
    // не теряет опыт на округлении, только выигрывает.
    // Возвращает false, если номиналы ItemExperience ещё не заведены в allItems (см. комментарий у ItemCategory.
    // ItemExperience) — вызывающий (ItemSacrificeUI) использует это, чтобы не обещать игроку конвертацию,
    // которая на самом деле не произошла.
    public bool GrantExperienceAsItems(int totalXp)
    {
        if (totalXp <= 0) return false;

        var denominations = allItems
            .Where(i => i != null && i.category == ItemCategory.ItemExperience && i.sacrificeExperience > 0)
            .OrderByDescending(i => i.sacrificeExperience)
            .ToList();
        if (denominations.Count == 0) return false; // номиналы ещё не заведены в allItems — отдавать нечем

        int remaining = totalXp;
        for (int i = 0; i < denominations.Count; i++)
        {
            var item = denominations[i];
            bool isSmallestDenomination = i == denominations.Count - 1;

            int count = remaining / item.sacrificeExperience;
            if (isSmallestDenomination && remaining % item.sacrificeExperience > 0)
                count += 1; // самый мелкий номинал — округляем нецелый остаток вверх, не теряем опыт

            if (count <= 0) continue;

            AddItemCopy(item, count);
            remaining -= count * item.sacrificeExperience;
        }

        return true;
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

    // Сколько опыта не хватает предмету, чтобы дойти РОВНО до targetLevel (без остатка) — используется
    // кнопками быстрого выбора +1/+10/Max в ItemSacrificeUI (см. QuickSelectToLevel), чтобы посчитать,
    // сколько донор-предметов набрать под конкретный шаг, не только "сколько дало бы текущее выделение".
    public int ExperienceNeededForLevel(int fromLevel, int fromExperience, int targetLevel) =>
        targetLevel <= fromLevel ? 0 : Mathf.Max(0, CumulativeExperience(targetLevel) - CumulativeExperience(fromLevel) - fromExperience);

    // Сколько опыта цель получит от ОДНОЙ единицы донора — общий расчёт для SacrificeItem (реальное
    // применение) и ItemSacrificeUI (превью до подтверждения), чтобы они не могли разойтись в цифрах.
    // Мердж редкости (донор НИЖЕ целевой редкости): цель забирает ВЕСЬ опыт, вложенный в донора
    // (накопленный + несожжённый остаток), не плоскую ставку sacrificeExperience — см. project_campaign_difficulty_curve.
    public int CalculateSacrificeGain(ItemData fuelData, int fuelLevel, int fuelExperience, ItemData targetData)
    {
        if (fuelData == null || targetData == null) return 0;

        // ItemExperience — фиксированная валюта (см. GrantExperienceAsItems), не "донор с вложенным опытом
        // ниже редкости цели". Она стоит White (0), поэтому мердж-ветка ниже приняла бы её почти за любую
        // цель и посчитала бы CumulativeExperience(fuelLevel=1)+0 = 0 вместо реальной цены — обходим её
        // явно, всегда плоская ставка sacrificeExperience (найдено при реализации 2026-08-20).
        if (fuelData.category == ItemCategory.ItemExperience)
            return fuelData.sacrificeExperience;

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

        resultingTargetInstanceId = MergeIdenticalStacks(leveledStack);

        SaveOwnedItems();
        return true;
    }

    // Если после разделения/апгрейда в системе уже есть ДРУГОЙ стек с таким же itemId+уровнем+опытом —
    // объединяем их (суммируем quantity), чтобы фактически одинаковые предметы не плодили лишние ячейки.
    // Возвращает instanceId стека, который реально пережил слияние (twin, если слияние произошло, иначе
    // сам stack) — вызывающая сторона (SacrificeItem) использует его как resultingTargetInstanceId для
    // следующего шага пакетного пожертвования; раньше всегда возвращался instanceId только что удалённого
    // stack, и следующий вызов SacrificeItem бил в несуществующий стек.
    private string MergeIdenticalStacks(ItemOwnershipData stack)
    {
        var twin = ownership.FirstOrDefault(o => o != stack && o.itemId == stack.itemId
            && o.level == stack.level && o.experience == stack.experience);
        if (twin == null) return stack.instanceId;

        twin.quantity += stack.quantity;
        ownership.Remove(stack);
        return twin.instanceId;
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
