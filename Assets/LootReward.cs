using UnityEngine;

[System.Serializable]
public class CurrencyLootEntry
{
    public CurrencyType type;
    public int amount = 100;
}

[System.Serializable]
public class ItemLootEntry
{
    public ItemData item;
    public int count = 1;
}

// Добыча за победу над конкретным врагом — назначается в инспекторе EnemyData.
[System.Serializable]
public class LootReward
{
    [Header("Опыт")]
    public int accountExperience = 20;
    public int heroExperience = 20; // каждому герою, участвовавшему в этом бою

    [Header("Прогресс-валюта (ОП) — только за ПЕРВОЕ прохождение этой ноды карты")]
    public int progressPoints = 30; // "1 story level = 30 ОП" из match3-economy.md

    [Header("Ресурсы")]
    public CurrencyLootEntry[] currency;

    [Header("Предметы")]
    public ItemLootEntry[] items;
}
