using System.Linq;
using UnityEngine;

// Добавляет в container плитки AscensionGemTile (Resources/UI/AscensionGemTile.prefab) — по одной на
// героя с ascensionGems > 0. Общий код для DeathDungeonEntryUI (плитки встроены прямо в окно входа в
// данж) и ItemCollectionUI (вкладка Consumables, плитки идут прямо в общую сетку предметов) — один
// источник правды вместо двух копий одного и того же цикла.
//
// НЕ очищает container сам — оба вызывающих места уже чистят свой контейнер перед построением (а в
// ItemCollectionUI контейнер общий с обычными карточками предметов, повторная очистка тут стёрла бы их).
public static class AscensionGemGridUtility
{
    private static GameObject tilePrefab;

    // Возвращает true, если хотя бы одна плитка была создана — вызывающий использует это для показа
    // своего собственного empty-state лейбла (текст лейбла у каждого экрана свой, поэтому наружу).
    public static bool Populate(Transform container, HeroCollectionManager manager)
    {
        if (container == null || manager == null) return false;

        if (tilePrefab == null)
            tilePrefab = Resources.Load<GameObject>("UI/AscensionGemTile");
        if (tilePrefab == null) return false;

        var gemHolders = manager.ownership.Where(o => o.ascensionGems > 0).ToList();

        foreach (var ownership in gemHolders)
        {
            HeroData hero = manager.allHeroes.FirstOrDefault(h => h != null && h.heroId == ownership.heroId);
            if (hero == null) continue;

            GameObject tileObj = Object.Instantiate(tilePrefab, container);
            tileObj.name = hero.heroId + "_AscensionGem";
            tileObj.GetComponent<AscensionGemTileUI>()?.Setup(hero, ownership);
        }

        return gemHolders.Count > 0;
    }
}
