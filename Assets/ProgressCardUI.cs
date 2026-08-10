using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Общий билдер "плоской" карточки прогресса — переиспользуется AchievementUI и DailyQuestUI, чтобы не
// дублировать вёрстку в двух местах. Карточка растягивается по ширине родителя (anchorMin/Max по X),
// позиционируется по Y от верха через yTop (отрицательное число — отступ вниз от верхнего края parent).
//
// Прогресс-бар — "по уровням": levelFloor/target — это НЕ 0/итоговый порог категории, а границы ИМЕННО
// текущего уровня (levelFloor = порог предыдущего уровня, 0 если это первый), так что бар всегда
// заполняется с нуля на каждом новом уровне, а не "уже наполовину полный" из-за уже пройденных прошлых
// уровней.
public static class ProgressCardUI
{
    public const float CardHeight = 150f;
    public const float CardSpacing = 16f;

    public static void Create(RectTransform parent, float yTop, string title, int current, int levelFloor, int target,
        bool done, bool ready, System.Action onClaim, string rewardIconPath = null, int rewardAmount = 0)
    {
        var cardObj = new GameObject("Card", typeof(RectTransform));
        var cardRect = (RectTransform)cardObj.transform;
        cardRect.SetParent(parent, false);
        cardRect.anchorMin = new Vector2(0, 1);
        cardRect.anchorMax = new Vector2(1, 1);
        cardRect.pivot = new Vector2(0.5f, 1);
        cardRect.sizeDelta = new Vector2(0, CardHeight);
        cardRect.anchoredPosition = new Vector2(0, yTop);

        var bg = cardObj.AddComponent<Image>();
        // Плоская заливка без рамок/орнамента: жёлтая подсветка — готово к клейму, зелёная — уже забрано/MAX.
        bg.color = ready ? new Color(0.45f, 0.4f, 0.1f, 0.6f)
            : done ? new Color(0.15f, 0.35f, 0.15f, 0.55f)
            : new Color(0f, 0f, 0f, 0.35f);

        var titleObj = new GameObject("Title", typeof(RectTransform));
        var titleRect = (RectTransform)titleObj.transform;
        titleRect.SetParent(cardRect, false);
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.offsetMin = new Vector2(16, 0);
        titleRect.offsetMax = new Vector2(-220, 0); // место под Claim-кнопку справа (200 шириной + отступ)
        titleRect.sizeDelta = new Vector2(0, 50);
        titleRect.anchoredPosition = new Vector2(0, -8);
        var titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = title;
        titleText.fontSize = ConfirmationDialog.MinTextFontSize;
        titleText.alignment = TextAlignmentOptions.MidlineLeft;
        titleText.color = Color.white;
        titleText.enableWordWrapping = true;

        // Награда — иконка ресурса + число вместо текстового названия (Wood/PP/Shards и т.п.), в
        // свободной полосе между заголовком и прогресс-баром. Ничего не создаём, если вызывающий не
        // передал rewardIconPath (старое поведение — просто число дописано прямо в title).
        if (!string.IsNullOrEmpty(rewardIconPath) && rewardAmount > 0)
        {
            var rewardRowObj = new GameObject("RewardRow", typeof(RectTransform));
            var rewardRowRect = (RectTransform)rewardRowObj.transform;
            rewardRowRect.SetParent(cardRect, false);
            rewardRowRect.anchorMin = new Vector2(0, 1);
            rewardRowRect.anchorMax = new Vector2(0, 1);
            rewardRowRect.pivot = new Vector2(0, 1);
            rewardRowRect.sizeDelta = new Vector2(160, 40);
            rewardRowRect.anchoredPosition = new Vector2(16, -64);

            ConfirmationDialog.CreateCurrencyIcon(rewardRowRect, rewardIconPath, new Vector2(0, 0), 34);

            var rewardAmountObj = new GameObject("Amount", typeof(RectTransform));
            var rewardAmountRect = (RectTransform)rewardAmountObj.transform;
            rewardAmountRect.SetParent(rewardRowRect, false);
            rewardAmountRect.anchorMin = new Vector2(0, 0.5f);
            rewardAmountRect.anchorMax = new Vector2(0, 0.5f);
            rewardAmountRect.pivot = new Vector2(0, 0.5f);
            rewardAmountRect.sizeDelta = new Vector2(110, 40);
            rewardAmountRect.anchoredPosition = new Vector2(42, 0);
            var rewardAmountText = rewardAmountObj.AddComponent<TextMeshProUGUI>();
            rewardAmountText.text = $"+{rewardAmount}";
            rewardAmountText.fontSize = ConfirmationDialog.MinTextFontSize;
            rewardAmountText.alignment = TextAlignmentOptions.MidlineLeft;
            rewardAmountText.color = Color.white;
        }

        // Claim-кнопка — верхний правый угол карточки. Активна только когда ready; иначе статичная
        // подпись состояния (Claimed/MAX либо просто скрыта, если уровень ещё не набран).
        var claimObj = new GameObject("Claim", typeof(RectTransform));
        var claimRect = (RectTransform)claimObj.transform;
        claimRect.SetParent(cardRect, false);
        claimRect.anchorMin = new Vector2(1, 1);
        claimRect.anchorMax = new Vector2(1, 1);
        claimRect.pivot = new Vector2(1, 1);
        claimRect.sizeDelta = new Vector2(200, 50);
        claimRect.anchoredPosition = new Vector2(-16, -8);

        var claimImg = claimObj.AddComponent<Image>();
        var claimBtn = claimObj.AddComponent<Button>();
        // Рантайм-кнопка не проходит через Selectable.OnValidate (тот авто-назначает targetGraphic, но
        // только если !Application.isPlaying) — без явного назначения здесь Disabled Color из
        // Button.colors не сработает при interactable=false.
        claimBtn.targetGraphic = claimImg;
        var claimTextObj = new GameObject("Text", typeof(RectTransform));
        var claimTextRect = (RectTransform)claimTextObj.transform;
        claimTextRect.SetParent(claimRect, false);
        claimTextRect.anchorMin = Vector2.zero;
        claimTextRect.anchorMax = Vector2.one;
        claimTextRect.offsetMin = Vector2.zero;
        claimTextRect.offsetMax = Vector2.zero;
        var claimText = claimTextObj.AddComponent<TextMeshProUGUI>();
        claimText.fontSize = ConfirmationDialog.MinTextFontSize;
        claimText.alignment = TextAlignmentOptions.Center;

        // Рамка (StyleAsButton) всегда та же, что у активной кнопки, не подменяется плоской серой
        // заливкой — серый некликабельный вид даёт стандартный Disabled Color из Button.colors
        // (тот же приём, что и Collect/Collect All в TerritoryMinesUI).
        ConfirmationDialog.StyleAsButton(claimImg);
        claimBtn.interactable = ready;

        if (ready)
        {
            claimBtn.onClick.AddListener(() => onClaim?.Invoke());
            claimText.text = "Claim!";
            claimText.color = new Color(1f, 0.92f, 0.4f);
        }
        else
        {
            // Обычный дефис вместо em-dash — у используемого TMP-шрифта нет глифа "—", символ рендерился
            // "пустым" и текст на неактивной кнопке визуально пропадал.
            claimText.text = done ? "Done" : "-";
            claimText.color = ConfirmationDialog.ButtonTextColor;
        }

        // Плоский progress bar: тёмный трек + заливка (Image.Type.Filled, Horizontal), число поверх.
        var trackObj = new GameObject("BarTrack", typeof(RectTransform));
        var trackRect = (RectTransform)trackObj.transform;
        trackRect.SetParent(cardRect, false);
        trackRect.anchorMin = new Vector2(0, 0);
        trackRect.anchorMax = new Vector2(1, 0);
        trackRect.pivot = new Vector2(0.5f, 0);
        trackRect.sizeDelta = new Vector2(-32, 42);
        trackRect.anchoredPosition = new Vector2(0, 0);
        var trackImg = trackObj.AddComponent<Image>();
        trackImg.color = new Color(0f, 0f, 0f, 0.5f);

        var fillObj = new GameObject("BarFill", typeof(RectTransform));
        var fillRect = (RectTransform)fillObj.transform;
        fillRect.SetParent(trackRect, false);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fillImg = fillObj.AddComponent<Image>();
        fillImg.color = done ? new Color(0.35f, 0.85f, 0.35f, 0.9f)
            : ready ? new Color(0.9f, 0.8f, 0.3f, 0.9f)
            : new Color(0.3f, 0.55f, 0.9f, 0.9f);
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;

        float levelSpan = target - levelFloor;
        fillImg.fillAmount = done ? 1f : levelSpan > 0 ? Mathf.Clamp01((current - levelFloor) / (float)levelSpan) : 1f;

        var progressTextObj = new GameObject("ProgressText", typeof(RectTransform));
        var progressTextRect = (RectTransform)progressTextObj.transform;
        progressTextRect.SetParent(trackRect, false);
        progressTextRect.anchorMin = Vector2.zero;
        progressTextRect.anchorMax = Vector2.one;
        progressTextRect.offsetMin = Vector2.zero;
        progressTextRect.offsetMax = Vector2.zero;
        var progressText = progressTextObj.AddComponent<TextMeshProUGUI>();
        progressText.text = done ? "MAX" : $"{Mathf.Min(current, target)}/{target}";
        progressText.fontSize = ConfirmationDialog.MinTextFontSize;
        progressText.alignment = TextAlignmentOptions.Center;
        progressText.color = Color.white;
    }
}
