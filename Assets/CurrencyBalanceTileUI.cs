using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Компонент готового префаба плитки "иконка + баланс валюты" (Resources/UI/CurrencyBalanceTile.prefab)
// — сейчас используется в ItemCollectionUI (вкладка Consumables) для CurrencyType.HeroExperience.
// Отдельный от AscensionGemTile префаб — тому не нужна рамка редкости/бедж "x-количества" под героя,
// только картинка + заголовок + число, опционально кликабельная плитка целиком.
public class CurrencyBalanceTileUI : MonoBehaviour
{
    public Image icon; // спрайт задаётся прямо в префабе (Inspector) — код его не трогает
    public TMP_Text label;
    public TMP_Text balanceText;
    public Button button;

    // onClick необязателен — если передан, кнопка плитки становится кликабельной; без него плитка чисто витрина.
    public void Setup(string title, int balance, System.Action onClick = null)
    {
        if (label != null) label.text = title;
        if (balanceText != null) balanceText.text = balance.ToString();

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = onClick != null;
            if (onClick != null) button.onClick.AddListener(() => onClick());
        }
    }
}
