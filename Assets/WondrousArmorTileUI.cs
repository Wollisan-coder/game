using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Компонент готового префаба плитки Дивной брони (Resources/UI/WondrousArmorTile.prefab) — по одной на
// героя с wondrousArmorUnwornCount > 0, показывается в ItemCollectionUI (вкладка Consumables). В отличие
// от AscensionGemTile, тут ДВЕ кнопки прямо на плитке — Wear (надеть, необратимо) и Disenchant (распылить,
// +2 ArmorShards), поэтому отдельный префаб/компонент, а не переиспользование той же плитки.
public class WondrousArmorTileUI : MonoBehaviour
{
    public Image icon;
    public TMP_Text label;
    public GameObject quantityBadgeRoot;
    public TMP_Text quantityBadge;
    public Button wearButton;
    public TMP_Text wearButtonText;
    public Button disenchantButton;

    [Header("Дивная броня — общий ассет WondrousArmorSkinSet на всю игру")]
    public WondrousArmorSkinSet skinSet;

    private string heroId;
    private System.Action onChanged;

    public void Setup(HeroData hero, HeroOwnershipData ownership, System.Action onChanged)
    {
        if (hero == null || ownership == null) return;

        heroId = hero.heroId;
        this.onChanged = onChanged;

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

        bool alreadyWorn = ownership.wondrousArmorWorn;
        if (wearButton != null)
        {
            wearButton.interactable = !alreadyWorn;
            wearButton.onClick.RemoveAllListeners();
            wearButton.onClick.AddListener(OnWearClicked);
        }
        if (wearButtonText != null)
            wearButtonText.text = alreadyWorn ? "Already Worn" : "Wear";

        if (disenchantButton != null)
        {
            disenchantButton.onClick.RemoveAllListeners();
            disenchantButton.onClick.AddListener(OnDisenchantClicked);
        }
    }

    private void OnWearClicked()
    {
        HeroCollectionManager.Instance?.WearWondrousArmor(heroId);
        onChanged?.Invoke();
    }

    private void OnDisenchantClicked()
    {
        HeroCollectionManager.Instance?.DisenchantWondrousArmor(heroId);
        onChanged?.Invoke();
    }
}
