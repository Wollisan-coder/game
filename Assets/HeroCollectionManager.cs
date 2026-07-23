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

    [Header("Обраний загін (максимум 4)")]
    public List<HeroData> squad = new List<HeroData>();
    public const int MaxSquadSize = 4;

        private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeOwnershipIfMissing();
        LoadSquad();

        // ВРЕМЕННО для тесту — розблокувати всіх героїв
        foreach (var hero in allHeroes)
            UnlockHero(hero);
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

    public bool AddToSquad(HeroData hero)
    {
        if (squad.Count >= MaxSquadSize) return false;
        if (squad.Contains(hero)) return false;
        if (!IsUnlocked(hero)) return false;

        squad.Add(hero);
        SaveSquad();
        return true;
    }

    public void RemoveFromSquad(HeroData hero)
    {
        squad.Remove(hero);
        SaveSquad();
    }

    private void SaveSquad()
    {
        string ids = string.Join(",", squad.Select(h => h.heroId));
        PlayerPrefs.SetString("squad_ids", ids);
        PlayerPrefs.Save();
    }

    private void LoadSquad()
    {
        squad.Clear();
        string saved = PlayerPrefs.GetString("squad_ids", "");
        if (string.IsNullOrEmpty(saved)) return;

        foreach (var id in saved.Split(','))
        {
            var hero = allHeroes.FirstOrDefault(h => h.heroId == id);
            if (hero != null) squad.Add(hero);
        }
    }

    


}