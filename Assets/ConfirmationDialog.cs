using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Простые рантайм-модалки: подтверждение (Yes/Cancel) и информационное сообщение (Ok).
// Строятся на лету и уничтожаются после выбора.
public static class ConfirmationDialog
{
    public static readonly Color ButtonColor = new Color32(0xE8, 0xB8, 0x4B, 0xFF);
    public static readonly Color ButtonTextColor = new Color32(0xF0, 0xF0, 0xF0, 0xFF);

    public static void Show(Transform parent, string message, System.Action onConfirm)
    {
        var (overlay, windowRect) = BuildBase(parent, message, 190);

        CreateButton(windowRect, "Yes", new Vector2(0.26f, 0.14f), ButtonColor, () =>
        {
            Object.Destroy(overlay);
            onConfirm?.Invoke();
        });

        CreateButton(windowRect, "Cancel", new Vector2(0.74f, 0.14f), ButtonColor, () =>
        {
            Object.Destroy(overlay);
        });
    }

    // Информационное сообщение с единственной кнопкой "Ok" — без варианта выбора.
    // windowHeight — по умолчанию 170, увеличивайте для более длинных/многострочных сообщений (например, результат x10-призыва).
    public static void ShowInfo(Transform parent, string message, float windowHeight = 170)
    {
        var (overlay, windowRect) = BuildBase(parent, message, windowHeight);

        CreateButton(windowRect, "Ok", new Vector2(0.5f, 0.14f), ButtonColor, () =>
        {
            Object.Destroy(overlay);
        });
    }

    private static (GameObject overlay, RectTransform windowRect) BuildBase(Transform parent, string message, float windowHeight)
    {
        var overlay = new GameObject("Dialog", typeof(RectTransform));
        var overlayRect = (RectTransform)overlay.transform;
        overlayRect.SetParent(parent, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.SetAsLastSibling();

        var dim = overlay.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.6f);

        var windowObj = new GameObject("Window", typeof(RectTransform));
        var windowRect = (RectTransform)windowObj.transform;
        windowRect.SetParent(overlayRect, false);
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(380, windowHeight);
        var windowBg = windowObj.AddComponent<Image>();
        windowBg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        var textObj = new GameObject("Message", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(windowRect, false);
        textRect.anchorMin = new Vector2(0, 0.35f);
        textRect.anchorMax = new Vector2(1, 1);
        textRect.offsetMin = new Vector2(16, 0);
        textRect.offsetMax = new Vector2(-16, -16);
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = message;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.fontSize = 20;
        text.enableAutoSizing = true; // длинные сообщения (например, результат x10-призыва) сами уменьшают шрифт, чтобы влезть
        text.fontSizeMin = 12;
        text.fontSizeMax = 20;

        return (overlay, windowRect);
    }

    private static void CreateButton(RectTransform parent, string label, Vector2 anchorPos, Color color, System.Action onClick)
    {
        var btnObj = new GameObject(label, typeof(RectTransform));
        var btnRect = (RectTransform)btnObj.transform;
        btnRect.SetParent(parent, false);
        btnRect.anchorMin = anchorPos;
        btnRect.anchorMax = anchorPos;
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.sizeDelta = new Vector2(150, 44);

        var img = btnObj.AddComponent<Image>();
        img.color = color;
        var btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(btnRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.color = ButtonTextColor;
    }
}
