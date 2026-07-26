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

    public void Setup(ItemData item, bool owned, System.Action onClick)
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
    }
}
