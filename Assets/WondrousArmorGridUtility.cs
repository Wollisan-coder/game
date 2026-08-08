using System.Linq;
using UnityEngine;

// Добавляет в container плитки WondrousArmorTile (Resources/UI/WondrousArmorTile.prefab) — по одной на
// героя с wondrousArmorUnwornCount > 0. Используется в ItemCollectionUI (вкладка Consumables, плитки
// прямо в общей сетке предметов). НЕ очищает container сам — контейнер общий с обычными карточками
// предметов, вызывающий уже чистит его перед построением (см. AscensionGemGridUtility — тот же приём).
public static class WondrousArmorGridUtility
{
    private static GameObject tilePrefab;

    public static void Populate(Transform container, HeroCollectionManager manager, System.Action onChanged)
    {
        if (container == null || manager == null) return;

        if (tilePrefab == null)
            tilePrefab = Resources.Load<GameObject>("UI/WondrousArmorTile");
        if (tilePrefab == null) return;

        var holders = manager.ownership.Where(o => o.wondrousArmorUnwornCount > 0).ToList();

        foreach (var ownership in holders)
        {
            HeroData hero = manager.allHeroes.FirstOrDefault(h => h != null && h.heroId == ownership.heroId);
            if (hero == null) continue;

            GameObject tileObj = Object.Instantiate(tilePrefab, container);
            tileObj.name = hero.heroId + "_WondrousArmor";
            tileObj.GetComponent<WondrousArmorTileUI>()?.Setup(hero, ownership, onChanged);
        }
    }
}
