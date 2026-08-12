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

    // Единый стандарт шрифтов/отступов для рантайм-окон (задан 2026-08-09) — раньше каждый экран
    // (Achievement/DailyQuest/TerritoryMines/...) подбирал размеры отдельно, разброс дошёл до title
    // 24-44 и ни одной пары файлов с одинаковыми отступами. Новые окна/переделки должны использовать
    // эти константы вместо своих чисел; старые экраны намеренно НЕ ретрофичены целиком в этот заход
    // (см. project_territory_mines) — только TerritoryMinesUI как образец нового стандарта.
    //
    // MinTextFontSize — жёсткое правило по всему проекту (задано 2026-08-09, см.
    // project_ui_text_size_standard): нигде в игре текст не должен быть мельче 28. HeaderFontSize/
    // BodyFontSize подняты до этого пола; проверяй новый текст на >= MinTextFontSize, даже если не
    // используешь остальные константы этого блока (мелкие подписи бейджей и т.п. — тоже под правило).
    public const float MinTextFontSize = 28f;
    public const float TitleFontSize = 44f;
    public const float HeaderFontSize = 32f;
    public const float BodyFontSize = 28f;
    public const float ButtonFontSize = 28f;
    public const float WindowContentPaddingX = 40f;

    private const float WindowWidth = 1000f;
    private const float MinWindowHeight = 500f;
    private const float HeaderHeight = 90f;

    // Спрайты рамок (тёмно-синий/серебро, ассет-пак пользователя) — Assets/Resources/UI/*.png,
    // нарезаны и настроены под 9-slice через Assets/Editor/UIAssetImportSetup.cs. Если вдруг не
    // загрузились (Resources пуст/файл удалили) — падаем обратно на плоскую заливку, а не падаем совсем.
    private static Sprite windowSprite;
    private static Sprite headerSprite;
    private static Sprite descriptionSprite;
    private static bool spritesLoaded;

    // Реальные border-значения нарезки (см. Assets/Editor/UIAssetImportSetup.cs) — кнопка/панель мельче
    // этого физически не может нормально показать рамку (углы наложатся друг на друга и потекут/размажутся),
    // так что ниже этого порога честнее откатиться на плоскую заливку, чем ломать вид. У кнопки порог
    // асимметричный — измерено по альфа-каналу: орнамент по бокам (вырез угла + "жемчужина") занимает
    // ~35px, а сверху/снизу это просто тонкая линия без выступов — там достаточно ~10px. Поэтому широкие,
    // но невысокие кнопки (тулбар-фильтры, вкладки) теперь проходят порог, не требуя отдельного спрайта.
    private const float MinSpriteButtonWidth = 80f;
    private const float MinSpriteButtonHeight = 30f;
    private const float MinSpritePanelSize = 160f;  // border окна — 70 со всех сторон (студы по всем 4 краям), порог с запасом
    private const float MinSpriteDescriptionSize = 120f; // border панели описания — 55 со всех сторон, порог с запасом

    private static void EnsureSpritesLoaded()
    {
        if (spritesLoaded) return;
        spritesLoaded = true;
        windowSprite = Resources.Load<Sprite>("UI/DialogWindowFrame");
        headerSprite = Resources.Load<Sprite>("UI/DialogHeaderFrame");
        descriptionSprite = Resources.Load<Sprite>("UI/DialogDescriptionPanel");
    }

    // Общие хелперы для остального проекта — чтобы кнопки/панели вне ConfirmationDialog (CastleUI,
    // HeroInventoryUI, ItemDetailUI и т.д.) выглядели тем же новым стилем, а не плоской заливкой.
    // Оба безопасно откатываются на старый плоский вид (fallbackColor), если спрайт не загрузился
    // ИЛИ если сам элемент слишком мелкий для рамки (см. MinSpriteButtonSize/MinSpritePanelSize).
    public static void StyleAsButton(Image img, Color? fallbackColor = null)
    {
        EnsureSpritesLoaded();
        var size = img.rectTransform.rect;
        bool bigEnough = size.width >= MinSpriteButtonWidth && size.height >= MinSpriteButtonHeight;
        if (headerSprite != null && bigEnough)
        {
            img.sprite = headerSprite;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }
        else
        {
            img.sprite = null;
            img.color = fallbackColor ?? ButtonColor;
        }
    }

    public static void StyleAsPanel(Image img, Color? fallbackColor = null)
    {
        EnsureSpritesLoaded();
        var size = img.rectTransform.rect;
        bool bigEnough = size.width >= MinSpritePanelSize && size.height >= MinSpritePanelSize;
        if (windowSprite != null && bigEnough)
        {
            img.sprite = windowSprite;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }
        else
        {
            img.sprite = null;
            img.color = fallbackColor ?? new Color(0.11f, 0.11f, 0.13f, 0.97f);
        }
    }

    // Плоская прямоугольная рамка (без вырезанных углов) — под блоки с текстом описания (скилл-инфо,
    // описание предмета, пассивка расы и т.п.), где раньше сидела просто плоская тёмная заливка.
    public static void StyleAsDescriptionPanel(Image img, Color? fallbackColor = null)
    {
        EnsureSpritesLoaded();
        var size = img.rectTransform.rect;
        bool bigEnough = size.width >= MinSpriteDescriptionSize && size.height >= MinSpriteDescriptionSize;
        if (descriptionSprite != null && bigEnough)
        {
            img.sprite = descriptionSprite;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }
        else
        {
            img.sprite = null;
            img.color = fallbackColor ?? new Color(0.11f, 0.11f, 0.13f, 0.97f);
        }
    }

    // Единая таблица CurrencyType -> путь иконки в Resources — раньше CastleUI.CreateCurrencyBar держала
    // такую же таблицу только для себя, а TerritoryMinesUI.CreateResourceRow дублировал похожую логику
    // ещё раз для 3 ресурсов шахт. null, если под валюту ещё нет иконки — вызывающий сам решает, что
    // делать (обычно просто не показывает иконку).
    public static string GetCurrencyIconPath(CurrencyType type) => type switch
    {
        CurrencyType.Wood => "UI/Currency/Wood",
        CurrencyType.Stone => "UI/Currency/Stone",
        CurrencyType.SummonShards => "UI/Currency/Shards",
        CurrencyType.PremiumGems => "UI/Currency/Gems",
        CurrencyType.ProgressPoints => "UI/Currency/PP",
        CurrencyType.HeroExperience => "UI/Currency/HeroExperience",
        CurrencyType.ArmorShards => "UI/Currency/ArmorShards",
        _ => null,
    };

    // Спавнит один preserveAspect Image с иконкой ресурса по готовому пути (см. GetCurrencyIconPath) —
    // общий примитив, а не готовый виджет: каждый вызывающий сам решает компоновку (ряд, бар, кнопка).
    // anchoredPosition — левый край иконки (anchor/pivot 0,0.5), как в уже существующих CreateCurrencyBar/
    // CreateResourceRow, чтобы соседний текст можно было ставить сразу после неё по X.
    public static Image CreateCurrencyIcon(RectTransform parent, string iconPath, Vector2 anchoredPosition, float size)
    {
        var iconObj = new GameObject("CurrencyIcon", typeof(RectTransform));
        var iconRect = (RectTransform)iconObj.transform;
        iconRect.SetParent(parent, false);
        iconRect.anchorMin = new Vector2(0, 0.5f);
        iconRect.anchorMax = new Vector2(0, 0.5f);
        iconRect.pivot = new Vector2(0, 0.5f);
        iconRect.sizeDelta = new Vector2(size, size);
        iconRect.anchoredPosition = anchoredPosition;

        var img = iconObj.AddComponent<Image>();
        if (!string.IsNullOrEmpty(iconPath))
            img.sprite = Resources.Load<Sprite>(iconPath);
        img.preserveAspect = true;
        return img;
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

    // Как Show(), но с настраиваемым текстом кнопки подтверждения вместо фиксированного "Yes" — для
    // конкретных именованных действий (например "Convert 1 Gem -> Voucher"), а не общего да/нет.
    public static void ShowChoice(Transform parent, string message, string confirmLabel, System.Action onConfirm, string title = null, float windowHeight = MinWindowHeight)
    {
        var (overlay, windowRect) = BuildBase(parent, message, windowHeight, title);

        CreateButton(windowRect, confirmLabel, new Vector2(0.27f, 0.12f), ButtonColor, () =>
        {
            Object.Destroy(overlay);
            onConfirm?.Invoke();
        });

        CreateButton(windowRect, "Cancel", new Vector2(0.73f, 0.12f), ButtonColor, () =>
        {
            Object.Destroy(overlay);
        });
    }

    // Как ShowChoice(), но с ЛЮБЫМ числом именованных НЕЗАВИСИМЫХ действий в один ряд (не да/нет-выбор,
    // где одно действие обязательно отменяет другое) — например Wear/Disenchant на плитке Дивной брони
    // (см. WondrousArmorTileUI.OnClicked), где оба видны сразу и клик по любому просто закрывает попап.
    // action.interactable — например, "Wear" неактивна, если уже надето.
    public static void ShowActions(Transform parent, string message, string title, (string label, bool interactable, System.Action onClick)[] actions, float windowHeight = MinWindowHeight)
    {
        var (overlay, windowRect) = BuildBase(parent, message, windowHeight, title);

        const float btnWidth = 300f;
        const float gap = 16f;
        float totalWidth = actions.Length * btnWidth + (actions.Length - 1) * gap;
        float startX = -totalWidth / 2f + btnWidth / 2f;

        for (int i = 0; i < actions.Length; i++)
        {
            var action = actions[i];
            float x = startX + i * (btnWidth + gap);
            CreateActionButton(windowRect, action.label, x, btnWidth, action.interactable, () =>
            {
                Object.Destroy(overlay);
                action.onClick?.Invoke();
            });
        }

        CreateButton(windowRect, "Close", new Vector2(0.5f, 0.12f), ButtonColor, () =>
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
    // iconPath — необязательная иконка ресурса (см. GetCurrencyIconPath) над текстом сообщения, например
    // для "Not enough energy to start a battle." — чтобы игрок сразу видел, о каком именно ресурсе речь.
    public static void ShowInfo(Transform parent, string message, float windowHeight = 170, System.Action onClosed = null, string title = null, string iconPath = null)
    {
        var (overlay, windowRect) = BuildBase(parent, message, windowHeight, title, iconPath);

        CreateButton(windowRect, "Ok", new Vector2(0.5f, 0.12f), ButtonColor, () =>
        {
            Object.Destroy(overlay);
            onClosed?.Invoke();
        });
    }

    private static (GameObject overlay, RectTransform windowRect) BuildBase(Transform parent, string message, float windowHeight, string title, string iconPath = null)
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

        // Иконка ресурса — маленький preserveAspect-Image по центру над текстом, отодвигает верх текстового
        // блока вниз на свою высоту, чтобы не перекрываться (тот же приём, что заголовок выше).
        if (!string.IsNullOrEmpty(iconPath))
        {
            var iconSprite = Resources.Load<Sprite>(iconPath);
            if (iconSprite != null)
            {
                const float iconSize = 64f;
                var iconObj = new GameObject("Icon", typeof(RectTransform));
                var iconRect = (RectTransform)iconObj.transform;
                iconRect.SetParent(windowRect, false);
                iconRect.anchorMin = new Vector2(0.5f, textTop);
                iconRect.anchorMax = new Vector2(0.5f, textTop);
                iconRect.pivot = new Vector2(0.5f, 1f);
                iconRect.sizeDelta = new Vector2(iconSize, iconSize);
                iconRect.anchoredPosition = new Vector2(0, -16);
                var iconImg = iconObj.AddComponent<Image>();
                iconImg.sprite = iconSprite;
                iconImg.preserveAspect = true;

                textTop -= (iconSize + 24f) / windowRect.sizeDelta.y;
            }
        }

        var textObj = new GameObject("Message", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(windowRect, false);
        textRect.anchorMin = new Vector2(0, 0.24f);
        textRect.anchorMax = new Vector2(1, textTop);
        textRect.offsetMin = new Vector2(48, 0);
        textRect.offsetMax = new Vector2(-48, -48);
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
        StyleAsButton(img, color);
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
        text.enableAutoSizing = true;
        text.fontSizeMin = 18;
        text.fontSizeMax = 34;
    }

    // Как CreateButton(), но с явной шириной (для N кнопок в ряд, см. ShowActions) и управляемой
    // interactable — CreateButton не отдаёт наружу сам Button, тут это нужно.
    private static void CreateActionButton(RectTransform parent, string label, float xOffset, float width, bool interactable, System.Action onClick)
    {
        var btnObj = new GameObject(label, typeof(RectTransform));
        var btnRect = (RectTransform)btnObj.transform;
        btnRect.SetParent(parent, false);
        btnRect.anchorMin = new Vector2(0.5f, 0.28f);
        btnRect.anchorMax = new Vector2(0.5f, 0.28f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.sizeDelta = new Vector2(width, 84);
        btnRect.anchoredPosition = new Vector2(xOffset, 0);

        var img = btnObj.AddComponent<Image>();
        StyleAsButton(img);
        var btn = btnObj.AddComponent<Button>();
        btn.interactable = interactable;
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
        text.enableAutoSizing = true;
        text.fontSizeMin = 14;
        text.fontSizeMax = 30;
    }
}
