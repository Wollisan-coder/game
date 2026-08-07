using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Runtime-built window that lets the player spend ОП (Progress Points) on CurrencyType.HeroExperience
// (the same unified currency HeroCollectionManager hands out as duplicate-overflow reward for Green/Blue —
// see project_gem_economy_v2_redesign_pending memory). Opened from a "Exchange" nav button in CastleUI.
public class ProgressExchangeUI : MonoBehaviour
{
    // (hero exp granted, cost in ОП). Flat 2 exp-per-1-ОП ratio, four tiers kept from the original
    // per-rarity HeroExpGem prices (200/600/1600/4000 exp for 100/300/800/2000 PP) now that those items
    // are gone and this sells the unified currency directly.
    private static readonly (int expAmount, int cost)[] Offers =
    {
        (200, 100),
        (600, 300),
        (1600, 800),
        (4000, 2000),
    };

    private Transform canvasRoot;
    private System.Action onClosed;

    private GameObject overlayRoot;
    private TMP_Text balanceText;
    private readonly List<TMP_Text> rowLabels = new List<TMP_Text>();

    public void Open(Transform canvasRoot, System.Action onClosed)
    {
        this.canvasRoot = canvasRoot;
        this.onClosed = onClosed;

        EnsureOverlay();
        overlayRoot.transform.SetAsLastSibling();
        overlayRoot.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        if (overlayRoot != null) overlayRoot.SetActive(false);
        onClosed?.Invoke();
    }

    private void EnsureOverlay()
    {
        if (overlayRoot != null) return;

        overlayRoot = new GameObject("ProgressExchangeOverlay", typeof(RectTransform));
        var overlayRect = (RectTransform)overlayRoot.transform;
        overlayRect.SetParent(canvasRoot, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        var dim = overlayRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.75f);

        var closeAreaObj = new GameObject("CloseArea", typeof(RectTransform));
        var closeAreaRect = (RectTransform)closeAreaObj.transform;
        closeAreaRect.SetParent(overlayRect, false);
        closeAreaRect.anchorMin = Vector2.zero;
        closeAreaRect.anchorMax = Vector2.one;
        closeAreaRect.offsetMin = Vector2.zero;
        closeAreaRect.offsetMax = Vector2.zero;
        var closeAreaImg = closeAreaObj.AddComponent<Image>();
        closeAreaImg.color = new Color(0, 0, 0, 0);
        var closeAreaBtn = closeAreaObj.AddComponent<Button>();
        closeAreaBtn.transition = Selectable.Transition.None;
        closeAreaBtn.onClick.AddListener(Close);

        var windowObj = new GameObject("Window", typeof(RectTransform));
        var windowRect = (RectTransform)windowObj.transform;
        windowRect.SetParent(overlayRect, false);
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(520, 600);
        var windowBg = windowObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsPanel(windowBg);

        var titleObj = new GameObject("Title", typeof(RectTransform));
        var titleRect = (RectTransform)titleObj.transform;
        titleRect.SetParent(windowRect, false);
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 45);
        titleRect.anchoredPosition = new Vector2(0, -30);
        var titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "Exchange Progress Points";
        titleText.fontSize = 30;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        var balanceObj = new GameObject("Balance", typeof(RectTransform));
        var balanceRect = (RectTransform)balanceObj.transform;
        balanceRect.SetParent(windowRect, false);
        balanceRect.anchorMin = new Vector2(0, 1);
        balanceRect.anchorMax = new Vector2(1, 1);
        balanceRect.pivot = new Vector2(0.5f, 1);
        balanceRect.sizeDelta = new Vector2(0, 30);
        balanceRect.anchoredPosition = new Vector2(0, -70);
        balanceText = balanceObj.AddComponent<TextMeshProUGUI>();
        balanceText.fontSize = 24;
        balanceText.alignment = TextAlignmentOptions.Center;
        balanceText.color = new Color(1, 1, 1, 0.85f);

        // Four stacked rows, one per HeroExpGem rarity — top row highest y, going down
        float rowY = -104f;
        for (int i = 0; i < Offers.Length; i++)
        {
            int offerIndex = i; // capture for lambda
            CreateRow(windowRect, rowY, () => Buy(offerIndex));
            rowY -= 96f;
        }

        var closeBtnObj = new GameObject("CloseButton", typeof(RectTransform));
        var closeBtnRect = (RectTransform)closeBtnObj.transform;
        closeBtnRect.SetParent(windowRect, false);
        closeBtnRect.anchorMin = new Vector2(0.5f, 0);
        closeBtnRect.anchorMax = new Vector2(0.5f, 0);
        closeBtnRect.pivot = new Vector2(0.5f, 0);
        closeBtnRect.sizeDelta = new Vector2(250, 45);
        closeBtnRect.anchoredPosition = new Vector2(0, 50);
        var closeBtnImg = closeBtnObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(closeBtnImg);
        var closeBtn = closeBtnObj.AddComponent<Button>();
        closeBtn.onClick.AddListener(Close);

        var closeTextObj = new GameObject("Text", typeof(RectTransform));
        var closeTextRect = (RectTransform)closeTextObj.transform;
        closeTextRect.SetParent(closeBtnRect, false);
        closeTextRect.anchorMin = Vector2.zero;
        closeTextRect.anchorMax = Vector2.one;
        closeTextRect.offsetMin = Vector2.zero;
        closeTextRect.offsetMax = Vector2.zero;
        var closeText = closeTextObj.AddComponent<TextMeshProUGUI>();
        closeText.text = "Close";
        closeText.alignment = TextAlignmentOptions.Center;
        closeText.color = ConfirmationDialog.ButtonTextColor; // тёмный текст был под старую светлую заливку, на новой тёмно-синей рамке нужен светлый

        overlayRoot.SetActive(false);
    }

    // One row = label (name + cost, left-aligned) + a "Buy" button (right side)
    private void CreateRow(RectTransform parent, float anchoredY, UnityEngine.Events.UnityAction onClick)
    {
        var rowObj = new GameObject("Row", typeof(RectTransform));
        var rowRect = (RectTransform)rowObj.transform;
        rowRect.SetParent(parent, false);
        rowRect.anchorMin = new Vector2(0, 1);
        rowRect.anchorMax = new Vector2(1, 1);
        rowRect.pivot = new Vector2(0.5f, 1);
        rowRect.sizeDelta = new Vector2(0, 80);
        rowRect.anchoredPosition = new Vector2(0, anchoredY);
        rowRect.offsetMin = new Vector2(30, rowRect.offsetMin.y);  // отступ слева, 16px
        rowRect.offsetMax = new Vector2(-25, rowRect.offsetMax.y); // отступ справа, 16px (обрати 
        
       

        var rowBg = rowObj.AddComponent<Image>();
        rowBg.color = new Color(1, 1, 1, 0.05f);

        var labelObj = new GameObject("Label", typeof(RectTransform));
        var labelRect = (RectTransform)labelObj.transform;
        labelRect.SetParent(rowRect, false);
        labelRect.anchorMin = new Vector2(0, 0);
        labelRect.anchorMax = new Vector2(0.62f, 1);
        labelRect.offsetMin = new Vector2(16, 0);
        labelRect.offsetMax = new Vector2(0, 0);
        var label = labelObj.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.color = Color.white;
        label.enableAutoSizing = true; // 3 строки (имя/эффект/цена) в фиксированной высоте строки — на 22pt впритык налезали на строку ниже
        label.fontSizeMin = 12;
        label.fontSizeMax = 20;
        rowLabels.Add(label);

        var btnObj = new GameObject("BuyButton", typeof(RectTransform));
        var btnRect = (RectTransform)btnObj.transform;
        btnRect.SetParent(rowRect, false);
        btnRect.anchorMin = new Vector2(0.66f, 0.5f);
        btnRect.anchorMax = new Vector2(0.66f, 0.5f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.sizeDelta = new Vector2(150, 50);
        btnRect.anchoredPosition = new Vector2(60, 0);

        var btnImg = btnObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(btnImg);
        var btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        var btnTextObj = new GameObject("Text", typeof(RectTransform));
        var btnTextRect = (RectTransform)btnTextObj.transform;
        btnTextRect.SetParent(btnRect, false);
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;
        var btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
        btnText.text = "Buy";
        btnText.fontSize = 22;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = ConfirmationDialog.ButtonTextColor; // тёмный текст был под старую светлую заливку, на новой тёмно-синей рамке нужен светлый
    }

    private void Refresh()
    {
        int balance = PlayerCurrencies.Instance != null ? PlayerCurrencies.Instance.GetBalance(CurrencyType.ProgressPoints) : 0;
        if (balanceText != null)
            balanceText.text = $"Your balance: {balance} PP";

        for (int i = 0; i < Offers.Length && i < rowLabels.Count; i++)
        {
            var (expAmount, cost) = Offers[i];
            rowLabels[i].text = $"+{expAmount} Hero Exp\nCost: {cost} PP";
        }
    }

    private void Buy(int offerIndex)
    {
        if (offerIndex < 0 || offerIndex >= Offers.Length) return;
        if (PlayerCurrencies.Instance == null) return;

        var (expAmount, cost) = Offers[offerIndex];

        if (!PlayerCurrencies.Instance.Spend(CurrencyType.ProgressPoints, cost))
        {
            ConfirmationDialog.ShowInfo(canvasRoot, $"Not enough Progress Points (needs {cost}).");
            return;
        }

        PlayerCurrencies.Instance.Add(CurrencyType.HeroExperience, expAmount);
        ConfirmationDialog.ShowInfo(canvasRoot, $"Bought +{expAmount} Hero Exp!");

        Refresh();
    }
}
