using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class CurrencyBalance
{
    public CurrencyType type;
    public int amount;
}

public class PlayerCurrencies : MonoBehaviour
{
    public static PlayerCurrencies Instance { get; private set; }

    [Header("Текущие балансы (заполняется при загрузке сохранения)")]
    public List<CurrencyBalance> balances = new List<CurrencyBalance>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
    }

    public int GetBalance(CurrencyType type)
    {
        var entry = balances.FirstOrDefault(b => b.type == type);
        return entry != null ? entry.amount : 0;
    }

    public void SetBalance(CurrencyType type, int amount)
    {
        var entry = balances.FirstOrDefault(b => b.type == type);
        if (entry != null)
            entry.amount = amount;
        else
            balances.Add(new CurrencyBalance { type = type, amount = amount });

        Save();
    }

    public void Add(CurrencyType type, int amount)
    {
        if (amount <= 0) return;

        var entry = balances.FirstOrDefault(b => b.type == type);
        if (entry != null)
            entry.amount += amount;
        else
            balances.Add(new CurrencyBalance { type = type, amount = amount });

        Save();
    }

    // Списывает сумму, если хватает средств. Возвращает false и ничего не меняет, если недостаточно.
    public bool Spend(CurrencyType type, int amount)
    {
        if (amount <= 0) return true;

        var entry = balances.FirstOrDefault(b => b.type == type);
        if (entry == null || entry.amount < amount) return false;

        entry.amount -= amount;
        Save();
        return true;
    }

    private void Save()
    {
        string serialized = string.Join(";", balances.Select(b => $"{(int)b.type}:{b.amount}"));
        PlayerPrefs.SetString("player_currencies", serialized);
        PlayerPrefs.Save();
    }

    private void Load()
    {
        balances.Clear();

        string saved = PlayerPrefs.GetString("player_currencies", "");
        if (string.IsNullOrEmpty(saved)) return;

        foreach (var entry in saved.Split(';'))
        {
            string[] parts = entry.Split(':');
            if (parts.Length != 2) continue;

            balances.Add(new CurrencyBalance
            {
                type = (CurrencyType)int.Parse(parts[0]),
                amount = int.Parse(parts[1])
            });
        }
    }
}
