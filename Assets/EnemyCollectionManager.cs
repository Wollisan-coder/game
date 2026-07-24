using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EnemyCollectionManager : MonoBehaviour
{
    public static EnemyCollectionManager Instance { get; private set; }

    [Header("Всі вороги гри (поки що один)")]
    public EnemyData[] allEnemies;

    [Header("Стан володіння (заповнюється при завантаженні збереження)")]
    public List<EnemyOwnershipData> ownership = new List<EnemyOwnershipData>();

    [Header("Обраний ворог для наступного бою")]
    public EnemyData selectedEnemy;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeOwnershipIfMissing();
        LoadSelectedEnemy();

        foreach (var enemy in allEnemies)
            UnlockEnemy(enemy); // ВРЕМЕННО для тесту, как и у героїв
    }

    private void InitializeOwnershipIfMissing()
    {
        foreach (var enemy in allEnemies)
        {
            if (!ownership.Any(o => o.enemyId == enemy.enemyId))
            {
                ownership.Add(new EnemyOwnershipData
                {
                    enemyId = enemy.enemyId,
                    isUnlocked = false
                });
            }
        }
    }

    public bool IsUnlocked(EnemyData enemy)
    {
        var data = ownership.FirstOrDefault(o => o.enemyId == enemy.enemyId);
        return data != null && data.isUnlocked;
    }

    public void UnlockEnemy(EnemyData enemy)
    {
        var data = ownership.FirstOrDefault(o => o.enemyId == enemy.enemyId);
        if (data != null) data.isUnlocked = true;
    }

    public bool SelectEnemy(EnemyData enemy)
    {
        if (!IsUnlocked(enemy)) return false;

        selectedEnemy = enemy;
        PlayerPrefs.SetString("selected_enemy_id", enemy.enemyId);
        PlayerPrefs.Save();
        return true;
    }

    private void LoadSelectedEnemy()
    {
        string savedId = PlayerPrefs.GetString("selected_enemy_id", "");

        selectedEnemy = !string.IsNullOrEmpty(savedId)
            ? allEnemies.FirstOrDefault(e => e.enemyId == savedId)
            : allEnemies.FirstOrDefault(); // якщо ворог один — обираємо його за замовчуванням
    }
}