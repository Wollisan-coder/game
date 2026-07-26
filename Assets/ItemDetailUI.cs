using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemDetailUI : MonoBehaviour
{
    [Header("UI елементи")]
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text slotTypeText;
    public TMP_Text descriptionText;
    public TMP_Text statsText;
    public TMP_Text ownedStatusText;

    public Button closeButton;

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        gameObject.SetActive(false);
    }

    public void Open(ItemData item, bool owned)
    {
        if (item == null) return;

        gameObject.SetActive(true);

        if (icon != null) icon.sprite = item.icon;
        if (nameText != null) nameText.text = item.itemName;
        if (slotTypeText != null) slotTypeText.text = item.slotType.ToString();
        if (descriptionText != null) descriptionText.text = item.description;

        if (statsText != null)
        {
            statsText.text =
                $"HP: +{item.bonusHealth}\n" +
                $"Мана: +{item.bonusMana}\n" +
                $"Множник урону: +{item.bonusDamageMultiplier:0.##}";
        }

        if (ownedStatusText != null)
            ownedStatusText.text = owned ? "Отримано" : "Не отримано";
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}
