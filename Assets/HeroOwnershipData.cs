using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EquippedItem
{
    public EquipmentSlotType slotType;
    public string itemInstanceId; // уникальный ID конкретного стека предмета (не itemId — тот не уникален, потому что
                                   // один и тот же предмет может существовать несколькими стеками разного уровня)
}

[System.Serializable]
public class HeroOwnershipData
{
    public string heroId;
    public bool isUnlocked;
    public int level = 1;
    public int experience = 0; // накопленный опыт в пределах текущего уровня (от предметов-источников опыта)

    // Гемы этого героя, копятся от дублей при призыве (см. HeroCollectionManager.HandleDuplicatePull).
    // Тройного назначения: тратятся на вознесение (AscendHero), на конвертацию в опыт/Hero Voucher
    // (ConvertGemToExperience/ConvertGemToVoucher), либо выбираются как "монета" жертвы для ретрита из
    // Death Dungeon наравне с целиком принесёнными героями-картами (см.
    // HeroCollectionManager.ExecuteRetreatSacrifice, project_death_dungeon_concept) — сознательно ОДИН и
    // тот же ресурс, а не отдельная "бесплатная" валюта, чтобы ретрит реально стоил игроку прогресса
    // героя. Отображаются на экране "Ascension Gems" (см. DeathDungeonEntryUI). НЕ сбрасывается при
    // PermadeleteSquad/SacrificeHeroForRetreat (см. HeroCollectionManager.ResetHeroToNeverUnlocked) —
    // получены игроком через уже состоявшиеся гача-пуллы, терять их вместе с ушедшим героем неправильно.
    [Header("Вознесение (только Purple/Orange — см. HeroAscensionUtility)")]
    public int ascensionGems;
    public int ascensionLevel; // сколько раз уже вознесён (0 = база)

    // Разблокирует кнопку "гем -> Hero Voucher" (см. HeroCollectionManager.ConvertGemToVoucher) — навсегда
    // true, как только герой хоть раз дошёл до 3-го вознесения (де-факто только Orange, у Purple потолок 2).
    // Как и ascensionGems выше, НЕ сбрасывается ни при PermadeleteSquad, ни при жертве героя на ретрит
    // (см. ResetHeroToNeverUnlocked) — раз разблокировано, остаётся разблокированным навсегда, даже если
    // этот конкретный герой потом погибнет/будет принесён в жертву и открыт заново с нуля.
    public bool voucherConversionUnlocked;

    // Дивная броня (Wondrous Armor, куск 6 Death Dungeon, полностью пересобрана — см.
    // project_gem_economy_v2_redesign_pending). wondrousArmorWorn — навсегда true, как только игрок надел
    // скин на этого героя (см. HeroCollectionManager.WearWondrousArmor) — необратимо, дальше с ним нельзя
    // ничего сделать (ни распылить, ни поставить в PvP). Как voucherConversionUnlocked выше — престиж за
    // годы игры, НЕ сбрасывается ни при PermadeleteSquad, ни при жертве героя на ретрит.
    // wondrousArmorUnwornCount — сколько ещё не надетых копий скина на руках у этого героя (сезонная
    // награда позволяет выбрать УЖЕ заскиненного героя повторно, плюс есть путь 3 осколка -> случайный
    // скин) — можно Wear (если ещё не worn) или Disenchant (-1 инстанс, +2 ArmorShards) каждую копию по
    // отдельности. В отличие от wondrousArmorWorn, ЭТО хероkарта-бонус, а не аккаунт-престиж — сбрасывается
    // вместе с остальным состоянием карточки в ResetHeroToNeverUnlocked.
    public bool wondrousArmorWorn;
    public int wondrousArmorUnwornCount;

    [Header("Выбранные навыки")]
    public int activeSkillIndex = 0;   // навык, используемый кнопкой в бою
    public int passiveSkillIndex = -1; // -1 = не выбран

    [Header("Пассивка расы (см. RacePassiveUtility/BattleManager) — вкл/выкл за фиксированную ману")]
    public bool racePassiveEnabled;

    [Header("Экипированные предметы (по одному на тип слота)")]
    public List<EquippedItem> equippedItems = new List<EquippedItem>();

    public string GetEquippedItemInstanceId(EquipmentSlotType slotType)
    {
        var entry = equippedItems.Find(e => e.slotType == slotType);
        return entry != null ? entry.itemInstanceId : null;
    }

    public void SetEquippedItem(EquipmentSlotType slotType, string itemInstanceId)
    {
        var entry = equippedItems.Find(e => e.slotType == slotType);

        if (entry != null)
            entry.itemInstanceId = itemInstanceId;
        else
            equippedItems.Add(new EquippedItem { slotType = slotType, itemInstanceId = itemInstanceId });
    }
}
