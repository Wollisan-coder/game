using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Простые рантайм-модалки: подтверждение (Yes/Cancel) и информационное сообщение (Ok).
// Строятся на лету и уничтожаются после выбора. Публичный API (Show/ShowInfo/ButtonColor/ButtonTextColor)
// не менять без проверки — используется в 18+ файлах по всему проекту.
public static class ConfirmationDialog
{
    public static readonly Color ButtonColor = new Color32(0xE8, 0xB8, 0x4B, 0xFF);
    public static readonly Color ButtonTextColor = new Color32(0xF0, 0xF0, 0xF0, 0xFF);

    private const float WindowWidth = 880f;
    private const float MinWindowHeight = 320f;
    private const float HeaderHeight = 90f;

    // Спрайты рамок (тёмно-синий/серебро, ассет-пак пользователя) — Assets/Resources/UI/*.png,
    // нарезаны и настроены под 9-slice через Assets/Editor/UIAssetImportSetup.cs. Если вдруг не
    // загрузились (Resources пуст/файл удалили) — падаем обратно на плоскую заливку, а не падаем совсем.
    private static Sprite windowSprite;
    private static Sprite headerSprite;
    private static bool spritesLoaded;

    private static void EnsureSpritesLoaded()
    {
        if (spritesLoaded) return;
        spritesLoaded = true;
        windowSprite = Resources.Load<Sprite>("UI/DialogWindowFrame");
        headerSprite = Resources.Load<Sprite>("UI/DialogHeaderFrame");
    }

    // Общие хелперы для остального проекта — чтобы кнопки/панели вне ConfirmationDialog (CastleUI,
    // HeroInventoryUI, ItemDetailUI и т.д.) выглядели тем же новым стилем, а не плоской заливкой.
    // Оба безопасно откатываются на старый плоский вид (fallbackColor), если спрайт не загрузился.
    public static void StyleAsButton(Image img, Color? fallbackColor = null)
    {
        EnsureSpritesLoaded();
        if (headerSprite != null)
        {
            img.sprite = headerSprite;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }
        else
        {
            img.color = fallbackColor ?? ButtonColor;
        }
    }

    public static void StyleAsPanel(Image img, Color? fallbackColor = null)
    {
        EnsureSpritesLoaded();
        if (windowSprite != null)
        {
            img.sprite = windowSprite;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }
        else
        {
            img.color = fallbackColor ?? new Color(0.11f, 0.11f, 0.13f, 0.97f);
        }
    }

    public static void Show(Transform parent, string message, System.Action onConfirm, string title = null)
    {
        var (overlay, windowRect) = BuildBase(parent, message, MinWindowHeight, title);

        CreateButton(windowRect, "Yes", new Vector2(0.27f, 0.12f), ButtonColor, () =>
        {
            Object.Destroy(overlay);
            onConfirm?.Invoke();
        });

        CreateButton(windowRect, "Cancel", new Vector2(0.73f, 0.12f), ButtonColor, () =>
        {
            Object.Destroy(overlay);
        });
    }

    // Информационное сообщение с единственной кнопкой "Ok" — без варианта выбора.
    // windowHeight — по умолчанию 170 (старый расчёт под маленькое окно) — теперь это просто минимум,
    // реальная высота окна не бывает меньше MinWindowHeight, так что старые вызовы с 170/190/220
    // по-прежнему работают и просто получают комфортный размер вместо тесного.
    // onClosed — необязательный коллбэк, вызывается после закрытия (например, переход на другую сцену только после Ok).
    // title — необязательный заголовок в отдельной шапке (новым вызовам; старые без него выглядят как раньше, но крупнее).
    public static void ShowInfo(Transform parent, string message, float windowHeight = 170, System.Action onClosed = null, string title = null)
    {
        var (overlay, windowRect) = BuildBase(parent, message, windowHeight, title);

        CreateButton(windowRect, "Ok", new Vector2(0.5f, 0.12f), ButtonColor, () =>
        {
            Object.Destroy(overlay);
            onClosed?.Invoke();
        });
    }

    private static (GameObject overlay, RectTransform windowRect) BuildBase(Transform parent, string message, float windowHeight, string title)
    {
        EnsureSpritesLoaded();

        var overlay = new GameObject("Dialog", typeof(RectTransform));
        var overlayRect = (RectTransform)overlay.transform;
        overlayRect.SetParent(parent, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.SetAsLastSibling();

        var dim = overlay.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);

        var windowObj = new GameObject("Window", typeof(RectTransform));
        var windowRect = (RectTransform)windowObj.transform;
        windowRect.SetParent(overlayRect, false);
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(WindowWidth, Mathf.Max(windowHeight, MinWindowHeight));

        var windowBg = windowObj.AddComponent<Image>();
        if (windowSprite != null)
        {
            windowBg.sprite = windowSprite;
            windowBg.type = Image.Type.Sliced;
            windowBg.color = Color.white; // спрайт уже сине-серебряный сам по себе, тонировать не нужно
        }
        else
        {
            // Фолбэк на плоскую заливку + отдельную рамку, если ассет почему-то не загрузился
            windowBg.color = new Color(0.11f, 0.11f, 0.13f, 0.97f);

            var borderObj = new GameObject("Border", typeof(RectTransform));
            var borderRect = (RectTransform)borderObj.transform;
            borderRect.SetParent(windowRect, false);
            borderRect.SetAsFirstSibling();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-4, -4);
            borderRect.offsetMax = new Vector2(4, 4);
            var borderImg = borderObj.AddComponent<Image>();
            borderImg.color = ButtonColor;
        }

        // Мягкая тень позади окна — визуально отделяет попап от затемнённого фона под ним.
        var shadow = windowObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(0, -8);

        float textTop = 1f;

        if (!string.IsNullOrEmpty(title))
        {
            var headerObj = new GameObject("Header", typeof(RectTransform));
            var headerRect = (RectTransform)headerObj.transform;
            headerRect.SetParent(windowRect, false);
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(0, HeaderHeight);
            headerRect.anchoredPosition = Vector2.zero;

            var headerBg = headerObj.AddComponent<Image>();
            if (headerSprite != null)
            {
                headerBg.sprite = headerSprite;
                headerBg.type = Image.Type.Sliced;
                headerBg.color = Color.white;
            }
            else
            {
                headerBg.color = ButtonColor;
            }

            var titleTextObj = new GameObject("TitleText", typeof(RectTransform));
            var titleTextRect = (RectTransform)titleTextObj.transform;
            titleTextRect.SetParent(headerRect, false);
            titleTextRect.anchorMin = Vector2.zero;
            titleTextRect.anchorMax = Vector2.one;
            titleTextRect.offsetMin = new Vector2(20, 0);
            titleTextRect.offsetMax = new Vector2(-20, 0);
            var titleText = titleTextObj.AddComponent<TextMeshProUGUI>();
            titleText.text = title;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = ButtonTextColor; // тот же светлый текст, что и на всех золотых кнопках по проекту — единый стиль
            titleText.fontStyle = FontStyles.Bold;
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 24;
            titleText.fontSizeMax = 42;

            textTop = 1f - HeaderHeight / windowRect.sizeDelta.y;
        }

        var textObj = new GameObject("Message", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(windowRect, false);
        textRect.anchorMin = new Vector2(0, 0.24f);
        textRect.anchorMax = new Vector2(1, textTop);
        textRect.offsetMin = new Vector2(36, 0);
        textRect.offsetMax = new Vector2(-36, -24);
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = message;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.fontSize = 36;
        text.enableAutoSizing = true; // длинные сообщения (например, результат x10-призыва) сами уменьшают шрифт, чтобы влезть
        text.fontSizeMin = 22;
        text.fontSizeMax = 40;

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
        btnRect.sizeDelta = new Vector2(280, 84);

        var img = btnObj.AddComponent<Image>();
        if (headerSprite != null)
        {
            img.sprite = headerSprite;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }
        else
        {
            img.color = color;
        }
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
        text.fontStyle = FontStyles.Bold;
        text.fontSize = 34;
    }
}
