using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Runtime-built popup показывающий 14 категорий пожизненных ачивок (AchievementManager) как карточки
// с плоским per-level progress-баром и Claim-кнопкой (см. ProgressCardUI), в скроллящемся списке —
// карточек больше, чем влезает в фиксированное окно. Плюс Claim All сверху — клеймит разом все
// категории, где накопилось несколько готовых уровней.
public class AchievementUI : MonoBehaviour
{
    private Transform canvasRoot;
    private GameObject overlayRoot;
    private RectTransform content;
    private ScrollRect scrollRect;
    private System.Action onClosed;

    // onClosed — чтобы CastleUI мог обновить бейдж-уведомление на кнопке "Achieve" сразу после того, как
    // игрок что-то заклеймил внутри и закрыл попап (см. CastleUI.RefreshNotificationBadges).
    public void Open(Transform canvasRoot, System.Action onClosed = null)
    {
        this.canvasRoot = canvasRoot;
        this.onClosed = onClosed;
        EnsureOverlay();
        overlayRoot.transform.SetAsLastSibling();
        overlayRoot.SetActive(true);
        Refresh(resetScroll: true);
    }

    public void Close()
    {
        if (overlayRoot != null) overlayRoot.SetActive(false);
        onClosed?.Invoke();
    }

    private void EnsureOverlay()
    {
        if (overlayRoot != null) return;

        overlayRoot = new GameObject("AchievementOverlay", typeof(RectTransform));
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
        windowRect.sizeDelta = new Vector2(950, 1500);
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
        titleText.text = "Achievements";
        titleText.fontSize = 44;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        var claimAllBtnObj = new GameObject("ClaimAllButton", typeof(RectTransform));
        var claimAllBtnRect = (RectTransform)claimAllBtnObj.transform;
        claimAllBtnRect.SetParent(windowRect, false);
        claimAllBtnRect.anchorMin = new Vector2(0.5f, 1);
        claimAllBtnRect.anchorMax = new Vector2(0.5f, 1);
        claimAllBtnRect.pivot = new Vector2(0.5f, 1);
        claimAllBtnRect.sizeDelta = new Vector2(340, 64);
        claimAllBtnRect.anchoredPosition = new Vector2(0, -118);
        var claimAllBtnImg = claimAllBtnObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(claimAllBtnImg);
        var claimAllBtn = claimAllBtnObj.AddComponent<Button>();
        claimAllBtn.onClick.AddListener(() => { AchievementManager.Instance?.ClaimAll(); Refresh(); }); // resetScroll: false — сохраняем позицию

        var claimAllTextObj = new GameObject("Text", typeof(RectTransform));
        var claimAllTextRect = (RectTransform)claimAllTextObj.transform;
        claimAllTextRect.SetParent(claimAllBtnRect, false);
        claimAllTextRect.anchorMin = Vector2.zero;
        claimAllTextRect.anchorMax = Vector2.one;
        claimAllTextRect.offsetMin = Vector2.zero;
        claimAllTextRect.offsetMax = Vector2.zero;
        var claimAllText = claimAllTextObj.AddComponent<TextMeshProUGUI>();
        claimAllText.text = "Claim All";
        claimAllText.fontSize = 26;
        claimAllText.alignment = TextAlignmentOptions.Center;
        claimAllText.color = ConfirmationDialog.ButtonTextColor;

        // ScrollView > Viewport(Mask) > Content — стандартный Unity-паттерн, вручную кодом (без префабов),
        // как и весь остальной UI в проекте.
        var scrollViewObj = new GameObject("ScrollView", typeof(RectTransform));
        var scrollViewRect = (RectTransform)scrollViewObj.transform;
        scrollViewRect.SetParent(windowRect, false);
        scrollViewRect.anchorMin = new Vector2(0, 0);
        scrollViewRect.anchorMax = new Vector2(1, 1);
        scrollViewRect.offsetMin = new Vector2(40, 150);
        scrollViewRect.offsetMax = new Vector2(-40, -190);
        scrollRect = scrollViewObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        var viewportObj = new GameObject("Viewport", typeof(RectTransform));
        var viewportRect = (RectTransform)viewportObj.transform;
        viewportRect.SetParent(scrollViewRect, false);
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewportObj.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.001f); // почти прозрачный — нужен как источник маски
        viewportObj.AddComponent<RectMask2D>();

        var contentObj = new GameObject("Content", typeof(RectTransform));
        content = (RectTransform)contentObj.transform;
        content.SetParent(viewportRect, false);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0, 100); // пересчитывается в Refresh()

        scrollRect.viewport = viewportRect;
        scrollRect.content = content;

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

    // resetScroll — только при Open() (свежий заход в попап). Клейм одной категории/Claim All раньше
    // тоже безусловно прыгали в начало списка, сбивая игрока, если он клеймил что-то ближе к концу.
    private void Refresh(bool resetScroll = false)
    {
        var manager = AchievementManager.Instance;
        if (manager == null || content == null) return;

        float previousScroll = scrollRect.verticalNormalizedPosition;

        foreach (Transform child in content)
            Destroy(child.gameObject);

        float yTop = 0f;
        foreach (AchievementCategory category in System.Enum.GetValues(typeof(AchievementCategory)))
        {
            int current = manager.GetProgress(category);
            int tier = manager.GetTierClaimed(category);
            var thresholds = manager.GetThresholds(category);
            bool maxed = tier >= thresholds.Length;
            bool ready = manager.IsReadyToClaim(category);
            int levelFloor = tier == 0 ? 0 : thresholds[tier - 1];
            int target = maxed ? thresholds[thresholds.Length - 1] : thresholds[tier];

            string tierLabel = maxed ? "MAX" : $"Lv.{tier + 1}/{thresholds.Length}";
            string title = $"[{tierLabel}] {AchievementManager.GetDescription(category)}";

            var capturedCategory = category;
            ProgressCardUI.Create(content, yTop, title, current, levelFloor, target, maxed, ready,
                () => { manager.ClaimCategory(capturedCategory); Refresh(); });
            yTop -= ProgressCardUI.CardHeight + ProgressCardUI.CardSpacing;
        }

        content.sizeDelta = new Vector2(0, -yTop);
        scrollRect.verticalNormalizedPosition = resetScroll ? 1f : previousScroll;
    }
}
