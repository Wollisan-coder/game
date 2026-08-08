using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Компонент готового префаба плитки CurrencyType.ArmorShards (Resources/UI/ArmorShardsTile.prefab) —
// отдельный от CurrencyBalanceTile (тот теперь только под Hero Experience), чтобы у Armor Shards была
// своя независимая карточка: клик открывает подтверждение "3 осколка -> случайный скин герою" (см.
// HeroCollectionManager.TryRedeemArmorShardsForRandomSkin, ItemCollectionUI.OnRedeemArmorShardsClicked).
public class ArmorShardsTileUI : MonoBehaviour
{
    public Image icon; // спрайт задаётся прямо в префабе (Inspector) — код его не трогает
    public TMP_Text label;
    public TMP_Text balanceText;
    public Button button;

    public void Setup(int balance, System.Action onClick)
    {
        if (label != null) label.text = "Armor Shards";
        if (balanceText != null) balanceText.text = balance.ToString();

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = onClick != null;
            if (onClick != null) button.onClick.AddListener(() => onClick());
        }
    }
}
