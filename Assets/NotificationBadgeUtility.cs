using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Вынесено из CastleUI (CreateAlertBadge/GetRoundBadgeSprite были private-методами конкретно под мины) —
// сам билдер уже был не завязан на Castle-специфику, только на переданные parent/позицию/цвет/лейбл,
// поэтому просто общий переиспользуемый билдер, как ItemBadgeUtility/PickerTileUtility.
public static class NotificationBadgeUtility
{
    // Минимальный самодостаточный маркер — круглый спрайт-заглушка + текст, без внешнего арта.
    // Начинает неактивным (SetActive(false)) — вызывающий сам включает его по своему сигналу.
    public static GameObject CreateAlertBadge(RectTransform parent, Vector2 anchoredPosition, Color color, string label)
    {
        var badgeObj = new GameObject("Badge_" + label, typeof(RectTransform));
        var badgeRect = (RectTransform)badgeObj.transform;
        badgeRect.SetParent(parent, false);
        badgeRect.anchorMin = new Vector2(1, 1);
        badgeRect.anchorMax = new Vector2(1, 1);
        badgeRect.pivot = new Vector2(1, 1);
        badgeRect.sizeDelta = new Vector2(38, 38);
        badgeRect.anchoredPosition = anchoredPosition;

        var bg = badgeObj.AddComponent<Image>();
        bg.sprite = GetRoundBadgeSprite();
        bg.color = color;

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(badgeRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = label.Length > 1 ? 12 : 24;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;

        badgeObj.SetActive(false);
        return badgeObj;
    }

    private static Sprite roundBadgeSprite;

    // Круглый спрайт для бейджа, сгенерированный на лету — в этой версии Unity 6 Resources
    // .GetBuiltinResource для встроенных UI-спрайтов (Knob, Checkmark и т.п.) молча возвращает null при
    // загрузке из кода (см. feedback_unity6_no_builtin_ui_sprites), так что вместо ссылки на builtin
    // рисуем свою текстуру с кругом один раз и кэшируем.
    public static Sprite GetRoundBadgeSprite()
    {
        if (roundBadgeSprite != null) return roundBadgeSprite;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                tex.SetPixel(x, y, dist <= radius ? Color.white : new Color(1f, 1f, 1f, 0f));
            }
        }
        tex.Apply();

        roundBadgeSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return roundBadgeSprite;
    }
}
