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

    // Клик теперь открывает попап с описанием + (если у героя voucherConversionUnlocked, т.е. хоть раз
    // дошёл до 3-го вознесения) кнопку "гем -> Voucher" — раньше эта кнопка жила только в HeroInventoryUI
    // (см. её CreateGemConversionUIIfNeeded), перенесена сюда 2026-08-12, чтобы действие было прямо на
    // самой витрине банка гемов, а не спрятано в отдельном экране инвентаря героя. Призыв запертого героя
    // по-прежнему НЕ отсюда — так и остаётся только в HeroCollectionUI (см. GrantGemToHero).
    public void Setup(HeroData hero, HeroOwnershipData ownership, System.Action onChanged)
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

        int gems = ownership.ascensionGems;
        string plural = gems == 1 ? "" : "s";
        string body = ownership.isUnlocked
            ? $"{hero.heroName}\n{gems} Ascension Gem{plural}\n\n" +
              "Earned from duplicate pulls of this hero after you already own it. Spend gems on Ascend " +
              "(Hero Inventory — raises the level cap) or, once this hero has reached max ascension, " +
              "convert them into a Hero Voucher of the same rarity."
            : $"{hero.heroName} (locked)\n{gems} Ascension Gem{plural}\n\n" +
              "This hero isn't unlocked yet — the gem bank carries over once you summon them.";

        if (ownership.voucherConversionUnlocked && gems >= 1)
        {
            // ShowActions (не ShowChoice) — та же кнопка-примитив, что и у WondrousArmorTileUI.OnClicked,
            // даёт контролируемую ширину кнопки (не обрезает "Convert 1 Gem -> Voucher") и вдвое более
            // высокое окно (windowHeight: 1000) — жалоба пользователя 2026-08-12 была именно на это у
            // соседнего попапа Wondrous Armor, тот же фикс применён и здесь для единообразия.
            var actions = new (string label, bool interactable, System.Action onClick)[]
            {
                ("Convert 1 Gem -> Voucher", true, () =>
                {
                    HeroCollectionManager.Instance?.ConvertGemToVoucher(hero.heroId);
                    onChanged?.Invoke();
                }),
            };
            ConfirmationDialog.ShowActions(canvasRoot, body, "Ascension Gems", actions, windowHeight: 1000f);
        }
        else
        {
            ConfirmationDialog.ShowInfo(canvasRoot, body, title: "Ascension Gems");
        }
    }
}
