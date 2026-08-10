using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Runtime-built popup показывающий сегодняшние 3 задания (DailyQuestManager) как карточки с плоским
// progress-баром и Claim-кнопкой (см. ProgressCardUI), плюс бонусная карточка и Claim All сверху, плюс
// Daily Reward (AccountManager.ClaimDailyReward) отдельной строкой — раньше была кнопкой в CastleUI,
// перенесена сюда, т.к. концептуально тоже "дневная награда" (свой Claim, не входит в Claim All).
public class DailyQuestUI : MonoBehaviour
{
    private Transform canvasRoot;
    private GameObject overlayRoot;
    private RectTransform content;
    private System.Action onClosed;

    private Button dailyRewardButton;
    private Image dailyRewardButtonBg;
    private TMP_Text dailyRewardButtonText;
    private Image dailyRewardIcon;
    private TMP_Text dailyRewardAmountText;

    // onClosed — чтобы CastleUI мог обновить бейдж-уведомление на кнопке "Quests" сразу после того, как
    // игрок что-то заклеймил внутри и закрыл попап (см. CastleUI.RefreshNotificationBadges).
    public void Open(Transform canvasRoot, System.Action onClosed = null)
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

        overlayRoot = new GameObject("DailyQuestOverlay", typeof(RectTransform));
        var overlayRect = (RectTransform)overlayRoot.transform;
        overlayRect.SetParent(canvasRoot, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        var dim = overlayRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.75f);
        var dimBtn = overlayRoot.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(Close);

        var windowObj = new GameObject("Window", typeof(RectTransform));
        var windowRect = (RectTransform)windowObj.transform;
        windowRect.SetParent(overlayRect, false);
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(950, 1100);
        var windowBg = windowObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsPanel(windowBg);
        var windowBlocker = windowObj.AddComponent<Button>();
        windowBlocker.transition = Selectable.Transition.None;

        var titleObj = new GameObject("Title", typeof(RectTransform));
        var titleRect = (RectTransform)titleObj.transform;
        titleRect.SetParent(windowRect, false);
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 72);
        titleRect.anchoredPosition = new Vector2(0, -30);
        var titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "Daily Quests";
        titleText.fontSize = 44;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        var dailyRewardObj = new GameObject("DailyReward", typeof(RectTransform));
        var dailyRewardRect = (RectTransform)dailyRewardObj.transform;
        dailyRewardRect.SetParent(windowRect, false);
        dailyRewardRect.anchorMin = new Vector2(0.5f, 1);
        dailyRewardRect.anchorMax = new Vector2(0.5f, 1);
        dailyRewardRect.pivot = new Vector2(0.5f, 1);
        dailyRewardRect.sizeDelta = new Vector2(760, 100);
        dailyRewardRect.anchoredPosition = new Vector2(0, -130);

        dailyRewardButtonBg = dailyRewardObj.AddComponent<Image>();
        dailyRewardButton = dailyRewardObj.AddComponent<Button>();
        // Явно назначаем targetGraphic — рантайм-кнопки не проходят через Selectable.OnValidate, без этого
        // interactable=false никак не тонирует фон (см. project_ui_text_size_standard / тот же паттерн,
        // что и claimBtn в ProgressCardUI и collectAllGlobalBtn в TerritoryMinesUI).
        dailyRewardButton.targetGraphic = dailyRewardButtonBg;
        dailyRewardButton.onClick.AddListener(OnDailyRewardClicked);

        var dailyRewardTextObj = new GameObject("Text", typeof(RectTransform));
        var dailyRewardTextRect = (RectTransform)dailyRewardTextObj.transform;
        dailyRewardTextRect.SetParent(dailyRewardRect, false);
        dailyRewardTextRect.anchorMin = Vector2.zero;
        dailyRewardTextRect.anchorMax = Vector2.one;
        dailyRewardTextRect.offsetMin = Vector2.zero;
        dailyRewardTextRect.offsetMax = new Vector2(-190, 0); // место под иконку+число ресурса справа
        dailyRewardButtonText = dailyRewardTextObj.AddComponent<TextMeshProUGUI>();
        dailyRewardButtonText.alignment = TextAlignmentOptions.Center;
        dailyRewardButtonText.fontSize = 30;
        dailyRewardButtonText.color = ConfirmationDialog.ButtonTextColor;

        // Иконка + число вместо текстового названия ресурса ("Progress Points") — сгруппированы в
        // отдельный контейнер, прижатый к правому краю кнопки, чтобы не зависеть от ширины основного текста.
        var dailyRewardGroupObj = new GameObject("RewardGroup", typeof(RectTransform));
        var dailyRewardGroupRect = (RectTransform)dailyRewardGroupObj.transform;
        dailyRewardGroupRect.SetParent(dailyRewardRect, false);
        dailyRewardGroupRect.anchorMin = new Vector2(1, 0.5f);
        dailyRewardGroupRect.anchorMax = new Vector2(1, 0.5f);
        dailyRewardGroupRect.pivot = new Vector2(1, 0.5f);
        dailyRewardGroupRect.sizeDelta = new Vector2(180, 60);
        dailyRewardGroupRect.anchoredPosition = new Vector2(-24, 0);

        dailyRewardIcon = ConfirmationDialog.CreateCurrencyIcon(dailyRewardGroupRect,
            ConfirmationDialog.GetCurrencyIconPath(CurrencyType.ProgressPoints), new Vector2(0, 0), 44);

        var dailyRewardAmountObj = new GameObject("Amount", typeof(RectTransform));
        var dailyRewardAmountRect = (RectTransform)dailyRewardAmountObj.transform;
        dailyRewardAmountRect.SetParent(dailyRewardGroupRect, false);
        dailyRewardAmountRect.anchorMin = new Vector2(0, 0.5f);
        dailyRewardAmountRect.anchorMax = new Vector2(0, 0.5f);
        dailyRewardAmountRect.pivot = new Vector2(0, 0.5f);
        dailyRewardAmountRect.sizeDelta = new Vector2(130, 60);
        dailyRewardAmountRect.anchoredPosition = new Vector2(52, 0);
        dailyRewardAmountText = dailyRewardAmountObj.AddComponent<TextMeshProUGUI>();
        dailyRewardAmountText.alignment = TextAlignmentOptions.MidlineLeft;
        dailyRewardAmountText.fontSize = ConfirmationDialog.MinTextFontSize;
        dailyRewardAmountText.color = ConfirmationDialog.ButtonTextColor;

        var claimAllBtnObj = new GameObject("ClaimAllButton", typeof(RectTransform));
        var claimAllBtnRect = (RectTransform)claimAllBtnObj.transform;
        claimAllBtnRect.SetParent(windowRect, false);
        claimAllBtnRect.anchorMin = new Vector2(0.5f, 1);
        claimAllBtnRect.anchorMax = new Vector2(0.5f, 1);
        claimAllBtnRect.pivot = new Vector2(0.5f, 1);
        claimAllBtnRect.sizeDelta = new Vector2(300, 60);
        claimAllBtnRect.anchoredPosition = new Vector2(0, -230);
        var claimAllBtnImg = claimAllBtnObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(claimAllBtnImg);
        var claimAllBtn = claimAllBtnObj.AddComponent<Button>();
        claimAllBtn.onClick.AddListener(() => { DailyQuestManager.Instance?.ClaimAll(); Refresh(); });

        var claimAllTextObj = new GameObject("Text", typeof(RectTransform));
        var claimAllTextRect = (RectTransform)claimAllTextObj.transform;
        claimAllTextRect.SetParent(claimAllBtnRect, false);
        claimAllTextRect.anchorMin = Vector2.zero;
        claimAllTextRect.anchorMax = Vector2.one;
        claimAllTextRect.offsetMin = Vector2.zero;
        claimAllTextRect.offsetMax = Vector2.zero;
        var claimAllText = claimAllTextObj.AddComponent<TextMeshProUGUI>();
        claimAllText.text = "Claim All";
        claimAllText.fontSize = ConfirmationDialog.MinTextFontSize;
        claimAllText.alignment = TextAlignmentOptions.Center;
        claimAllText.color = ConfirmationDialog.ButtonTextColor;

        // Контейнер карточек (3 задания + 1 бонусная) — фиксированное число, всегда влезает без скролла,
        // в отличие от AchievementUI (14 категорий, там нужен ScrollRect). ContentArea — растянутая
        // область-рамка (задаёт отступы через offsetMin/Max), Content внутри неё — top-anchored,
        // высота пересчитывается в Refresh() по числу карточек (тот же паттерн, что и AchievementUI).
        var contentAreaObj = new GameObject("ContentArea", typeof(RectTransform));
        var contentAreaRect = (RectTransform)contentAreaObj.transform;
        contentAreaRect.SetParent(windowRect, false);
        contentAreaRect.anchorMin = new Vector2(0, 0);
        contentAreaRect.anchorMax = new Vector2(1, 1);
        contentAreaRect.offsetMin = new Vector2(60, 180);
        contentAreaRect.offsetMax = new Vector2(-60, -290);

        var contentObj = new GameObject("Content", typeof(RectTransform));
        content = (RectTransform)contentObj.transform;
        content.SetParent(contentAreaRect, false);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0, 100);

        var closeBtnObj = new GameObject("CloseButton", typeof(RectTransform));
        var closeBtnRect = (RectTransform)closeBtnObj.transform;
        closeBtnRect.SetParent(windowRect, false);
        closeBtnRect.anchorMin = new Vector2(0.5f, 0);
        closeBtnRect.anchorMax = new Vector2(0.5f, 0);
        closeBtnRect.pivot = new Vector2(0.5f, 0);
        closeBtnRect.sizeDelta = new Vector2(320, 90);
        closeBtnRect.anchoredPosition = new Vector2(0, 30);
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
        closeText.fontSize = 32;
        closeText.alignment = TextAlignmentOptions.Center;
        closeText.color = ConfirmationDialog.ButtonTextColor;

        overlayRoot.SetActive(false);
    }

    private void OnDailyRewardClicked()
    {
        if (AccountManager.Instance == null) return;

        int granted = AccountManager.Instance.ClaimDailyReward();
        if (granted > 0 && canvasRoot != null)
            ConfirmationDialog.ShowInfo(canvasRoot, $"Daily reward claimed!\n+{granted} Progress Points");

        Refresh();
    }

    private void RefreshDailyRewardButton()
    {
        if (dailyRewardButton == null || AccountManager.Instance == null) return;

        bool claimed = AccountManager.Instance.HasClaimedDailyRewardToday();
        int amount = AccountManager.Instance.GetDailyRewardAmount();

        dailyRewardButton.interactable = !claimed;
        if (dailyRewardButtonBg != null)
        {
            ConfirmationDialog.StyleAsButton(dailyRewardButtonBg);
            dailyRewardButtonBg.color = claimed ? new Color(0.5f, 0.5f, 0.5f, 0.6f) : Color.white; // тускло-серый поверх той же рамки, пока не получена
        }
        if (dailyRewardButtonText != null)
            dailyRewardButtonText.text = claimed ? "Daily Reward claimed" : "Claim Daily Reward";

        // Иконка+число ресурса только когда реально есть что забрать — после клейма показывать нечего.
        if (dailyRewardIcon != null) dailyRewardIcon.gameObject.SetActive(!claimed);
        if (dailyRewardAmountText != null)
        {
            dailyRewardAmountText.gameObject.SetActive(!claimed);
            dailyRewardAmountText.text = $"+{amount}";
        }
    }

    private void Refresh()
    {
        var manager = DailyQuestManager.Instance;
        RefreshDailyRewardButton();
        if (manager == null || content == null) return;

        foreach (Transform child in content)
            Destroy(child.gameObject);

        string ppIconPath = ConfirmationDialog.GetCurrencyIconPath(CurrencyType.ProgressPoints);
        string shardsIconPath = ConfirmationDialog.GetCurrencyIconPath(CurrencyType.SummonShards);

        float yTop = 0f;
        foreach (var slot in manager.todaysQuests)
        {
            string title = DailyQuestManager.GetDescription(slot.type);
            var capturedType = slot.type;
            ProgressCardUI.Create(content, yTop, title, slot.current, 0, slot.target, slot.claimed, slot.IsReady,
                () => { manager.ClaimQuest(capturedType); Refresh(); }, ppIconPath, DailyQuestManager.PerQuestReward);
            yTop -= ProgressCardUI.CardHeight + ProgressCardUI.CardSpacing;
        }

        string bonusTitle = "Complete all 3 quests";
        int bonusDone = manager.todaysQuests.Count(s => s.claimed);
        ProgressCardUI.Create(content, yTop, bonusTitle, bonusDone, 0, 3, manager.allBonusGranted, manager.IsBonusReady,
            () => { manager.ClaimBonus(); Refresh(); }, shardsIconPath, DailyQuestManager.AllCompleteBonusShards);
        yTop -= ProgressCardUI.CardHeight + ProgressCardUI.CardSpacing;

        content.sizeDelta = new Vector2(0, -yTop);
    }
}
