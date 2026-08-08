using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Компонент готового префаба плитки "Ascension Gems" (Resources/UI/AscensionGemTile.prefab) — раньше
// эта плитка собиралась целиком в рантайме через PickerTileUtility/ItemBadgeUtility (см.
// DeathDungeonEntryUI.BuildAscensionGemTile), теперь это отдельный .prefab-ассет с полями, доступными
// через Inspector; скрипт только заполняет готовую иерархию данными конкретного героя.
public class AscensionGemTileUI : MonoBehaviour
{
    public Image icon;
    public Image rarityFrame;
    public TMP_Text label;
    public GameObject quantityBadgeRoot; // родитель quantityBadge — включается/выключается целиком (см. ItemBadgeUtility.ApplyQuantityBadge)
    public TMP_Text quantityBadge;
    public Button button;

    // Клика нет даже для запертых героев с гемом (см. HeroCollectionManager.GrantGemToHero) — призыв
    // живёт ТОЛЬКО в HeroCollectionUI, эта плитка чисто витрина.
    public void Setup(HeroData hero, HeroOwnershipData ownership)
    {
        if (hero == null) return;

        if (icon != null)
            icon.sprite = HeroAscensionUtility.GetDisplayPortrait(hero, ownership);

        if (rarityFrame != null)
            rarityFrame.color = hero.GetRarityColor();

        bool locked = ownership == null || !ownership.isUnlocked;
        if (label != null)
        {
            label.text = locked ? $"{hero.heroName}\n(locked)" : hero.heroName;
            label.color = hero.GetRarityColor();
        }

        int gems = ownership != null ? ownership.ascensionGems : 0;
        if (quantityBadgeRoot != null) quantityBadgeRoot.SetActive(gems > 1);
        if (quantityBadge != null) quantityBadge.text = $"x{gems}";

        if (button != null) button.interactable = false;
    }
}
