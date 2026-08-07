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

    // Индекс = (int)Rarity. Гем опыта героя ИМЕННО этого цвета — для White/Green/Blue (вознесения не имеют)
    // выдаётся АВТОМАТОМ за каждый дубликат. Для Purple/Orange больше не выдаётся автоматом (см.
    // HandleDuplicatePull) — доступен только через ручную кнопку "-> Experience" на банке ascensionGems.
    [Header("Гемы опыта героя по редкости")]
    public ItemData[] rarityExperienceGems;

    // Индекс = (int)Rarity, заполнено только для Purple/Orange (у остальных вознесения нет вообще).
    // Ваучер этой редкости — получается конвертацией ascensionGems (см. ConvertGemToVoucher), доступно
    // только если у героя-источника гема voucherConversionUnlocked (см. project_death_dungeon_concept).
    // 3 ваучера тратятся на начисление 1 ascensionGems ЛЮБОМУ герою этой редкости по выбору игрока — если
    // герой заперт, этот гем открывает ему кнопку призыва (см. HeroVoucherRedeemUI/GrantGemToHero/UnlockHeroWithGem).
    [Header("Ваучеры разблокировки героя по редкости (только Purple/Orange)")]
    public ItemData[] heroVoucherItems;

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
    // см. CastleUI.OnBossTrainingClicked / HeroCollectionUI.OnCardSelected.
    public bool pickingForBossTraining;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadOwnership();
        InitializeOwnershipIfMissing();
        LoadSquad();
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
        AchievementManager.Instance?.ReportHeroCollected();
        SaveOwnership();
    }

    // Вызывается SummonService, когда призыв выпал на УЖЕ разблокированного героя (дубликат).
    // White/Green/Blue вознесения не имеют вообще (GetMaxAscension=0) — для них НИЧЕГО не меняется,
    // дубликат как и раньше автоматом даёт N гемов опыта героя того же цвета редкости (rarityExperienceGems).
    // Purple/Orange — гем вознесения (см. HeroOwnershipData.ascensionGems) теперь копится ВСЕГДА, даже
    // после того как герой дошёл до максимума вознесения своей редкости — никакой автоконвертации на
    // потолке больше нет для этих редкостей. Что делать с накопленным гемом (потратить на вознесение,
    // конвертировать в опыт, конвертировать в Hero Voucher), игрок решает сам отдельной кнопкой позже —
    // см. AscendHero/ConvertGemToExperience/ConvertGemToVoucher, project_death_dungeon_concept.
    public void HandleDuplicatePull(HeroData hero)
    {
        if (hero == null) return;

        var data = ownership.FirstOrDefault(o => o.heroId == hero.heroId);
        if (data == null) return;

        int maxAscension = HeroAscensionUtility.GetMaxAscension(hero.rarity);
        if (maxAscension <= 0)
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

    // Тратит 1 гем вознесения этого героя за 1 предмет опыта той же редкости (rarityExperienceGems) —
    // ручная версия того, что раньше выдавалось автоматом на потолке вознесения. Доступна всегда
    // (не требует voucherConversionUnlocked, в отличие от ConvertGemToVoucher ниже).
    public bool ConvertGemToExperience(string heroId)
    {
        var hero = allHeroes.FirstOrDefault(h => h.heroId == heroId);
        var data = ownership.FirstOrDefault(o => o.heroId == heroId);
        if (hero == null || data == null || data.ascensionGems < 1) return false;

        ItemData gem = GetRarityExperienceGem(hero.rarity);
        if (gem == null) return false;

        data.ascensionGems--;
        ItemCollectionManager.Instance?.AddItemCopy(gem, 1);

        SaveOwnership();
        return true;
    }

    // Тратит 1 гем вознесения этого героя за 1 Hero Voucher той же редкости (heroVoucherItems) — доступно
    // ТОЛЬКО если герой хоть раз дошёл до 3-го вознесения (см. HeroOwnershipData.voucherConversionUnlocked,
    // де-факто только Orange). Ваучер уже не привязан к личности героя-источника — см. GrantGemToHero.
    public bool ConvertGemToVoucher(string heroId)
    {
        var hero = allHeroes.FirstOrDefault(h => h.heroId == heroId);
        var data = ownership.FirstOrDefault(o => o.heroId == heroId);
        if (hero == null || data == null || !data.voucherConversionUnlocked || data.ascensionGems < 1) return false;

        ItemData voucher = GetHeroVoucherItem(hero.rarity);
        if (voucher == null) return false;

        data.ascensionGems--;
        ItemCollectionManager.Instance?.AddItemCopy(voucher, 1);

        SaveOwnership();
        return true;
    }

    public const int HeroVouchersPerGem = 3;

    // Тратит HeroVouchersPerGem ваучеров редкости targetHeroId и начисляет ЕМУ 1 ascensionGems — targetHeroId
    // может быть ЛЮБЫМ героем этой редкости, разблокированным или нет (см. HeroVoucherRedeemUI — экран
    // выбора цели). Если герой уже разблокирован, гем просто пополняет его обычный банк (годится и на
    // AscendHero, и на дальнейшую конвертацию). Если ещё заперт — этого гема достаточно, чтобы для него
    // появилась кнопка призыва (см. UnlockHeroWithGem) — и на карточке в коллекции, и на его же плитке на
    // экране Ascension Gems.
    public bool GrantGemToHero(string targetHeroId)
    {
        var hero = allHeroes.FirstOrDefault(h => h.heroId == targetHeroId);
        var data = ownership.FirstOrDefault(o => o.heroId == targetHeroId);
        if (hero == null || data == null) return false;

        ItemData voucher = GetHeroVoucherItem(hero.rarity);
        var manager = ItemCollectionManager.Instance;
        if (voucher == null || manager == null || manager.GetTotalQuantity(voucher.itemId) < HeroVouchersPerGem) return false;

        var stack = manager.GetStacks(voucher.itemId).FirstOrDefault();
        if (stack == null) return false;
        string instanceId = stack.instanceId;
        for (int i = 0; i < HeroVouchersPerGem; i++)
            manager.ConsumeItem(instanceId);

        data.ascensionGems++;
        SaveOwnership();
        return true;
    }

    // Призыв запертого героя за 1 ascensionGems, начисленный через GrantGemToHero (или накопленный любым
    // другим путём, если он вдруг оказался заперт с гемом на руках). Тратит РОВНО 1 гем — остаток (если
    // конвертировали несколько раз подряд) остаётся банком героя на будущее, уже после разблокировки.
    public bool UnlockHeroWithGem(string heroId)
    {
        var data = ownership.FirstOrDefault(o => o.heroId == heroId);
        if (data == null || data.isUnlocked || data.ascensionGems < 1) return false;

        data.ascensionGems--;
        data.isUnlocked = true;
        AchievementManager.Instance?.ReportHeroCollected();
        SaveOwnership();
        return true;
    }

    public const int RetreatGemCost = 4;

    public int GetTotalAscensionGemCount() => ownership.Sum(o => o.ascensionGems);

    // Сколько героев вне переданного текущего отряда доступны как "карта" для жертвы на ретрит
    // (разблокированы, не в отряде) — используется для gate-проверки "есть ли вообще из чего выбирать"
    // до открытия DeathDungeonRetreatSelectionUI.
    public int GetAvailableSacrificeHeroCount(IEnumerable<string> currentSquadHeroIds)
    {
        var squadSet = currentSquadHeroIds != null ? new HashSet<string>(currentSquadHeroIds) : new HashSet<string>();
        return ownership.Count(o => o.isUnlocked && !squadSet.Contains(o.heroId));
    }

    // Исполняет РУЧНОЙ выбор игрока для жертвы на ретрит из Death Dungeon (см.
    // DeathDungeonRetreatSelectionUI, project_death_dungeon_concept) — смесь гемов вознесения (могут быть
    // с разных героев, gemSpendByHero[heroId] = сколько гемов ИМЕННО с него) и целиком принесённых в жертву
    // героев (heroCardSacrifices, каждый должен быть вне currentSquadHeroIds). Сумма ВСЕХ очков (гемы +
    // карты) должна ровно совпасть с RetreatGemCost — иначе (или если что-то из выбора уже невалидно:
    // герой перестал быть открыт, гемов не хватает и т.п.) ничего не тратит и возвращает null (без
    // частичного списания). При успехе возвращает список ВСЕХ причастных heroId (гемы + карты, с повторами
    // для гемов) — кусок 5 позже читает это, чтобы поднять именно ИХ как боссов-нежить.
    public List<string> ExecuteRetreatSacrifice(Dictionary<string, int> gemSpendByHero, List<string> heroCardSacrifices, IEnumerable<string> currentSquadHeroIds)
    {
        gemSpendByHero ??= new Dictionary<string, int>();
        heroCardSacrifices ??= new List<string>();

        int totalPoints = gemSpendByHero.Values.Sum() + heroCardSacrifices.Count;
        if (totalPoints != RetreatGemCost) return null;

        var squadSet = currentSquadHeroIds != null ? new HashSet<string>(currentSquadHeroIds) : new HashSet<string>();

        // Проверяем ВСЁ до того, как начать тратить — частичное списание при невалидном выборе недопустимо.
        foreach (var entry in gemSpendByHero)
        {
            if (entry.Value <= 0) continue;
            var data = ownership.FirstOrDefault(o => o.heroId == entry.Key);
            if (data == null || data.ascensionGems < entry.Value) return null;
        }
        foreach (var heroId in heroCardSacrifices)
        {
            var data = ownership.FirstOrDefault(o => o.heroId == heroId);
            if (data == null || !data.isUnlocked || squadSet.Contains(heroId)) return null;
        }

        var consumed = new List<string>();

        foreach (var entry in gemSpendByHero)
        {
            if (entry.Value <= 0) continue;
            var data = ownership.First(o => o.heroId == entry.Key);
            data.ascensionGems -= entry.Value;
            for (int i = 0; i < entry.Value; i++)
                consumed.Add(entry.Key);
        }

        foreach (var heroId in heroCardSacrifices)
        {
            var data = ownership.First(o => o.heroId == heroId);
            ResetHeroToNeverUnlocked(data);
            consumed.Add(heroId);
        }

        SaveOwnership();
        return consumed;
    }

    private ItemData GetRarityExperienceGem(Rarity rarity)
    {
        int index = (int)rarity;
        return rarityExperienceGems != null && index >= 0 && index < rarityExperienceGems.Length
            ? rarityExperienceGems[index] : null;
    }

    private ItemData GetHeroVoucherItem(Rarity rarity)
    {
        int index = (int)rarity;
        return heroVoucherItems != null && index >= 0 && index < heroVoucherItems.Length
            ? heroVoucherItems[index] : null;
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
        if (data.ascensionLevel >= 3) data.voucherConversionUnlocked = true;
        AchievementManager.Instance?.ReportAscension();

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
        int levelBefore = data.level;

        while (data.level < levelCap && data.experience >= ExperienceToNextLevel(data.level))
        {
            data.experience -= ExperienceToNextLevel(data.level);
            data.level++;
        }

        if (data.level >= levelCap)
            data.experience = 0; // упёрлись в потолок — остаток сгорает, а не копится "про запас"

        if (data.level > levelBefore)
            DailyQuestManager.Instance?.ReportHeroLeveledUp();

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

    // Сбрасывает героя в состояние "никогда не открывался" — общее ядро и для полного поражения без
    // ретрита (PermadeleteSquad), и для добровольной жертвы одним героем на ретрит (SacrificeHeroForRetreat).
    // НЕ трогает voucherConversionUnlocked (см. HeroOwnershipData) — это единственное поле героя,
    // рассчитанное пережить его "смерть" в любой форме.
    // ascensionGems и voucherConversionUnlocked НЕ трогаем — гемы получены игроком через уже состоявшиеся
    // гача-пуллы (а не "прогресс этой конкретной карточки"), терять их вместе с героем неправильно. Если
    // герой позже снова выпадет в гаче, SummonService.GrantHero увидит IsUnlocked()==false и вызовет
    // UnlockHero (не HandleDuplicatePull) — герой откроется заново со ВСЕМ уже накопленным банком гемов
    // (и, если voucherConversionUnlocked уже true, конвертация в ваучер будет доступна сразу).
    private void ResetHeroToNeverUnlocked(HeroOwnershipData data)
    {
        data.isUnlocked = false;
        data.level = 1;
        data.experience = 0;
        data.ascensionLevel = 0;
        data.activeSkillIndex = 0;
        data.passiveSkillIndex = -1;
        data.racePassiveEnabled = false;
        data.equippedItems.Clear();
    }

    // Death Dungeon — поражение БЕЗ ретрита (см. project_death_dungeon_concept). Все 4 героя текущего
    // отряда удаляются из коллекции навсегда — не "ранены", как будто их никогда не открывали. Экипировка
    // с них снимается (сами предметы у игрока остаются, не пропадают вместе с героем). Банк ascensionGems
    // и voucherConversionUnlocked переживают это — см. ResetHeroToNeverUnlocked.
    public void PermadeleteSquad()
    {
        foreach (var hero in squad.Where(h => h != null).ToList())
        {
            var data = ownership.FirstOrDefault(o => o.heroId == hero.heroId);
            if (data == null) continue;

            ResetHeroToNeverUnlocked(data);
        }

        for (int i = 0; i < squad.Count; i++)
            squad[i] = null;

        SaveOwnership();
        SaveSquad();
    }

    // Добровольная жертва ОДНОГО героя как "карты" в ретрите из Death Dungeon (см.
    // project_death_dungeon_concept, DeathDungeonMapUI/RetreatSelectionUI) — тот же необратимый сброс,
    // что и PermadeleteSquad, но точечно на один конкретный банк-героя вне текущего отряда данжа
    // (currentSquadHeroIds передаётся вызывающим кодом, чтобы не завязываться на DeathDungeonManager).
    // Возвращает false, если герой не найден, не разблокирован, или входит в переданный текущий отряд.
    public bool SacrificeHeroForRetreat(string heroId, IEnumerable<string> currentSquadHeroIds)
    {
        var data = ownership.FirstOrDefault(o => o.heroId == heroId);
        if (data == null || !data.isUnlocked) return false;
        if (currentSquadHeroIds != null && currentSquadHeroIds.Contains(heroId)) return false;

        ResetHeroToNeverUnlocked(data);

        SaveOwnership();
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
               $":{data.ascensionGems}:{data.ascensionLevel}:{(data.racePassiveEnabled ? 1 : 0)}:{(data.voucherConversionUnlocked ? 1 : 0)}";
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
                ascensionLevel = parts.Length > 8 ? int.Parse(parts[8]) : 0,
                // Ещё позже добавлена пассивка расы — старые сохранения (9 частей, без неё) получат выключенную по умолчанию
                racePassiveEnabled = parts.Length > 9 && parts[9] == "1",
                // Ваучер-разблокировка добавлена ещё позже — старые сохранения (без части 10) получат false
                voucherConversionUnlocked = parts.Length > 10 && parts[10] == "1"
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
