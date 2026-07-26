using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemPickerEntryUI : MonoBehaviour
{
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text descriptionText;
    public GameObject lockOverlay; // показується, якщо предмет ще не отриманий
    public Button selectButton;

    private Image rarityFrame;
    private TMP_Text levelText;
    private TMP_Text quantityText;

    public void Setup(ItemData item, bool owned, int level, int quantity, System.Action onClick)
    {
        if (iconImage != null) iconImage.sprite = item.icon;
        if (nameText != null) nameText.text = item.itemName;
        if (descriptionText != null) descriptionText.text = item.description;
        if (lockOverlay != null) lockOverlay.SetActive(!owned);

        if (selectButton != null)
        {
            selectButton.interactable = owned; // вставити в слот можна лише отриманий предмет
            selectButton.onClick.RemoveAllListeners();

            if (owned)
                selectButton.onClick.AddListener(() => onClick());
        }

        ItemBadgeUtility.ApplyRarityFrame(iconImage, item.GetRarityColor(), ref rarityFrame);
        ItemBadgeUtility.ApplyLevelBadge(iconImage != null ? iconImage.rectTransform : null, owned ? level : 0, ref levelText);
        ItemBadgeUtility.ApplyQuantityBadge(iconImage != null ? iconImage.rectTransform : null, owned ? quantity : 0, ref quantityText);
    }
}
