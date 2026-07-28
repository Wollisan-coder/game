using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Рантайм-генерация рамки редкости и беджа уровня для иконки предмета.
// Создаётся программно (а не через префаб), чтобы не редактировать вручную каждый UI-префаб предметов.
public static class ItemBadgeUtility
{
    private const float FrameOutset = 4f;

    // Цветная рамка редкости позади иконки. Копирует трансформ иконки и немного "раздувает" её,
    // поэтому работает одинаково независимо от того, растянута ли иконка на всю ячейку или имеет фиксированный размер.
    public static void ApplyRarityFrame(Image icon, Color rarityColor, ref Image frame)
    {
        if (icon == null) return;
        var iconRect = icon.rectTransform;

        if (frame == null)
        {
            var frameObj = new GameObject("RarityFrame", typeof(RectTransform), typeof(Image));
            var frameRect = (RectTransform)frameObj.transform;
            frameRect.SetParent(iconRect.parent, false);
            frameRect.SetSiblingIndex(iconRect.GetSiblingIndex()); // позади иконки

            frameRect.anchorMin = iconRect.anchorMin;
            frameRect.anchorMax = iconRect.anchorMax;
            frameRect.pivot = iconRect.pivot;
            frameRect.anchoredPosition = iconRect.anchoredPosition;
            frameRect.sizeDelta = iconRect.sizeDelta + new Vector2(FrameOutset * 2f, FrameOutset * 2f);

            frame = frameObj.GetComponent<Image>();
        }

        frame.color = rarityColor;
    }

    // Маленький бедж с номером уровня в правом нижнем углу переданного элемента. level <= 0 — бедж скрывается.
    public static void ApplyLevelBadge(RectTransform anchorTo, int level, ref TMP_Text text)
    {
        if (anchorTo == null) return;

        if (text == null)
        {
            var badgeObj = new GameObject("LevelBadge", typeof(RectTransform));
            var badgeRect = (RectTransform)badgeObj.transform;
            badgeRect.SetParent(anchorTo.parent != null ? anchorTo.parent : anchorTo, false);
            badgeRect.SetAsLastSibling();
            badgeRect.anchorMin = new Vector2(1, 0);
            badgeRect.anchorMax = new Vector2(1, 0);
            badgeRect.pivot = new Vector2(1, 0);
            badgeRect.sizeDelta = new Vector2(28, 18);
            badgeRect.anchoredPosition = new Vector2(-2, 2);

            var bg = badgeObj.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.65f);

            var textObj = new GameObject("Text", typeof(RectTransform));
            var textRect = (RectTransform)textObj.transform;
            textRect.SetParent(badgeRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            text = textObj.AddComponent<TextMeshProUGUI>();
            text.fontSize = 13;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
        }

        text.transform.parent.gameObject.SetActive(level > 0);
        if (level > 0) text.text = level.ToString();
    }

    // Бедж количества "xN" в левом верхнем углу — показывается только когда есть несколько одинаковых копий (quantity > 1)
    public static void ApplyQuantityBadge(RectTransform anchorTo, int quantity, ref TMP_Text text)
    {
        if (anchorTo == null) return;

        if (text == null)
        {
            var badgeObj = new GameObject("QuantityBadge", typeof(RectTransform));
            var badgeRect = (RectTransform)badgeObj.transform;
            badgeRect.SetParent(anchorTo.parent != null ? anchorTo.parent : anchorTo, false);
            badgeRect.SetAsLastSibling();
            badgeRect.anchorMin = new Vector2(0, 1);
            badgeRect.anchorMax = new Vector2(0, 1);
            badgeRect.pivot = new Vector2(0, 1);
            badgeRect.sizeDelta = new Vector2(32, 18);
            badgeRect.anchoredPosition = new Vector2(2, -2);

            var bg = badgeObj.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.65f);

            var textObj = new GameObject("Text", typeof(RectTransform));
            var textRect = (RectTransform)textObj.transform;
            textRect.SetParent(badgeRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            text = textObj.AddComponent<TextMeshProUGUI>();
            text.fontSize = 13;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
        }

        text.transform.parent.gameObject.SetActive(quantity > 1);
        if (quantity > 1) text.text = $"x{quantity}";
    }
}
