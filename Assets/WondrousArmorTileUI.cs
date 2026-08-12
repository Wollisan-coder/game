using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Клик по плитке открывает попап с описанием и двумя независимыми действиями — Wear/Disenchant (см.
// ConfirmationDialog.ShowActions). Раньше обе кнопки сидели прямо на плитке (см. git-историю/старый
// WondrousArmorTilePrefabBuilder) — перенесены в попап 2026-08-12 по явному запросу, тот же приём, что и
// перенос "гем -> Voucher" с HeroInventoryUI на AscensionGemTile в этом же файле-соседе.
public class WondrousArmorTileUI : MonoBehaviour
{
    public Image icon;
    public TMP_Text label;
    public GameObject quantityBadgeRoot;
    public TMP_Text quantityBadge;
    public Button button;

    [Header("Дивная броня — общий ассет WondrousArmorSkinSet на всю игру")]
    public WondrousArmorSkinSet skinSet;

    public void Setup(HeroData hero, HeroOwnershipData ownership, System.Action onChanged)
    {
        if (hero == null || ownership == null) return;

        // Показываем ИНВЕНТАРНУЮ картинку скина (если для героя она уже нарисована в базе) — не ту же,
        // что накладывается на портрет героя (см. WondrousArmorSkinSet.GetHeroOverlaySprite/Apply). Тут это
        // ещё не надетый инстанс, поэтому не зависит от wondrousArmorWorn. Если скина в базе нет — как
        // раньше, портрет героя как заглушка.
        if (icon != null)
        {
            Sprite skinSprite = skinSet != null ? skinSet.GetInventorySprite(hero.heroId) : null;
            icon.sprite = skinSprite != null ? skinSprite : HeroAscensionUtility.GetDisplayPortrait(hero, ownership);
        }

        if (label != null)
        {
            label.text = hero.heroName;
            label.color = hero.GetRarityColor();
        }

        if (quantityBadgeRoot != null) quantityBadgeRoot.SetActive(ownership.wondrousArmorUnwornCount > 1);
        if (quantityBadge != null) quantityBadge.text = $"x{ownership.wondrousArmorUnwornCount}";

        if (button != null)
        {
            button.interactable = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnClicked(hero, ownership, onChanged));
        }
    }

    private void OnClicked(HeroData hero, HeroOwnershipData ownership, System.Action onChanged)
    {
        if (hero == null || ownership == null) return;

        var canvas = GetComponentInParent<Canvas>();
        Transform canvasRoot = canvas != null ? canvas.transform : transform;

        bool alreadyWorn = ownership.wondrousArmorWorn;
        int unworn = ownership.wondrousArmorUnwornCount;

        string body = $"{hero.heroName}\n{unworn} unworn Wondrous Armor skin{(unworn == 1 ? "" : "s")}" +
            (alreadyWorn ? "\nAlready wearing a skin on this hero — Wear only applies to a hero with none worn yet." : "") +
            "\n\nWear: puts on one skin permanently — squad-wide damage bonus, cannot be undone or swapped.\n" +
            "Disenchant: melts one unworn skin into +2 Armor Shards.";

        var actions = new (string label, bool interactable, System.Action onClick)[]
        {
            ("Wear", !alreadyWorn && unworn > 0, () =>
            {
                HeroCollectionManager.Instance?.WearWondrousArmor(hero.heroId);
                onChanged?.Invoke();
            }),
            ("Disenchant (+2)", unworn > 0, () =>
            {
                HeroCollectionManager.Instance?.DisenchantWondrousArmor(hero.heroId);
                onChanged?.Invoke();
            }),
        };

        ConfirmationDialog.ShowActions(canvasRoot, body, "Wondrous Armor", actions, windowHeight: 1000f);
    }
}
