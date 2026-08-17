using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemPickerEntryUI : MonoBehaviour
{
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text descriptionText;
    public GameObject lockOverlay; // показывается, если предмет ещё не получен
    public Button selectButton;

    private TMP_Text levelText;
    private TMP_Text quantityText;

    public void Setup(ItemData item, bool owned, int level, int quantity, System.Action onClick)
    {
        if (iconImage != null) iconImage.sprite = item.icon;
        if (nameText != null)
        {
            nameText.text = item.itemName;
            nameText.color = item.GetRarityColor();
        }
        if (descriptionText != null) descriptionText.text = item.description;
        if (lockOverlay != null) lockOverlay.SetActive(!owned);

        if (selectButton != null)
        {
            selectButton.interactable = owned; // вставити в слот можна лише отриманий предмет
            selectButton.onClick.RemoveAllListeners();

            if (owned)
                selectButton.onClick.AddListener(() => onClick());
        }

        ItemBadgeUtility.ApplyLevelBadge(iconImage != null ? iconImage.rectTransform : null, owned ? level : 0, ref levelText);
        ItemBadgeUtility.ApplyQuantityBadge(iconImage != null ? iconImage.rectTransform : null, owned ? quantity : 0, ref quantityText);
    }
}
