using UnityEngine;
using UnityEngine.UI;

// Общий каталог картинок скинов Дивной брони — один ассет на всю игру (Collection card, Squad slot,
// Hero inventory, плитка в инвентаре предметов), тот же приём, что RarityFrameSet/RaceEmblemSet/
// AscensionOverlaySet. Один герой = один скин: heroId и есть id скина, отдельного счётчика/идентификатора
// не нужно — дубликаты одного героя просто увеличивают HeroOwnershipData.wondrousArmorUnwornCount.
//
// У каждого скина ДВА разных изображения — плитка предмета в инвентаре (inventorySprite, WondrousArmorTileUI)
// и оверлей поверх портрета героя (heroOverlaySprite, применяется на Collection/Squad/Inventory только
// когда скин НАДЕТ, см. Apply) — разные ракурсы/композиция, поэтому не одна картинка на оба места.
[CreateAssetMenu(fileName = "WondrousArmorSkinSet", menuName = "UI/Wondrous Armor Skin Set")]
public class WondrousArmorSkinSet : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public string heroId;
        public Sprite inventorySprite;   // плитка неношеного скина в инвентаре предметов (Consumables)
        public Sprite heroOverlaySprite; // оверлей на портрете героя, когда скин надет
    }

    public Entry[] skins;

    private Entry GetEntry(string heroId)
    {
        if (string.IsNullOrEmpty(heroId) || skins == null) return null;

        foreach (var entry in skins)
        {
            if (entry != null && entry.heroId == heroId)
                return entry;
        }
        return null;
    }

    public Sprite GetInventorySprite(string heroId) => GetEntry(heroId)?.inventorySprite;
    public Sprite GetHeroOverlaySprite(string heroId) => GetEntry(heroId)?.heroOverlaySprite;

    // Показывает оверлей героя, только пока скин НАДЕТ (worn) — тот же контракт, что у RarityUtility.ApplyFrame/
    // HeroAscensionUtility.ApplyOverlay: если картинки нет (герой не надевал скин, или для него скин ещё
    // не нарисован), просто скрывает Image, а не оставляет старую картинку висеть.
    public static void Apply(Image overlayImage, WondrousArmorSkinSet skinSet, string heroId, bool worn)
    {
        if (overlayImage == null) return;

        Sprite sprite = (worn && skinSet != null) ? skinSet.GetHeroOverlaySprite(heroId) : null;
        overlayImage.sprite = sprite;
        overlayImage.enabled = sprite != null;
    }
}
