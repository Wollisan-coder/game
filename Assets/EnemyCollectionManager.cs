using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EnemyCollectionManager : MonoBehaviour
{
    public static EnemyCollectionManager Instance { get; private set; }

    [Header("Все враги игры (пока что один)")]
    public EnemyData[] allEnemies;

    [Header("Состояние владения (заполняется при загрузке сохранения)")]
    public List<EnemyOwnershipData> ownership = new List<EnemyOwnershipData>();

    [Header("Выбранный враг для следующего боя")]
    public EnemyData selectedEnemy;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeOwnershipIfMissing();
        LoadSelectedEnemy();
    }

    // Разблокированы по умолчанию — UnlockEnemy() нигде в проекте не вызывается (нет системы прогрессии
    // коллекции врагов), а WorldMapManager.SelectNode требует IsUnlocked() перед входом в бой — без этого
    // default'а ЛЮБАЯ нода тихо отказывалась запускать бой (SelectNode просто возвращал false без единого
    // сообщения). Если позже появится реальная система разблокировки — поменять на false и звать UnlockEnemy.
    private void InitializeOwnershipIfMissing()
    {
        foreach (var enemy in allEnemies)
        {
            if (!ownership.Any(o => o.enemyId == enemy.enemyId))
            {
                ownership.Add(new EnemyOwnershipData
                {
                    enemyId = enemy.enemyId,
                    isUnlocked = true
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
            : allEnemies.FirstOrDefault(); // если враг один — выбираем его по умолчанию
    }
}