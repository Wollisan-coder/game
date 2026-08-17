using UnityEngine;
using UnityEngine.UI;

// Мягкая тень + тёмный скос по периметру — "физический объём" для карточек-плиток по всей игре
// (см. UX-правку 2026-08-18: "карточки должны отделиться от фона"). Единая точка входа —
// ApplyCardDepth(cardBackground) — вызывается один раз на карточку в момент её постройки.
//
// Скос переиспользует ItemBadgeUtility.ApplyRarityFrame как есть (тот же приём "копия трансформа,
// чуть больше, полая рамка, сиблинг позади"), просто тёмным цветом вместо цвета редкости — это уже
// ровно "тонкий тёмный скос по периметру", ничего нового изобретать не пришлось. Тень — новая
// процедурная 9-slice текстура (мягкое затухание альфы от края внутрь), тот же квадратичный спад,
// что у HeroInventoryUI/CastleUI.GetRadialGlowSprite, только не круглый, а вдоль края прямоугольника.
//
// Проект явно и дважды отказался от штатного UI.Outline для эффектов "по силуэту" (см. комментарии
// в HeroInventoryUI.cs и CastleUI.cs — Outline дублирует спрайт со сдвигом, а не обводит контур),
// поэтому тут тоже не используется — только процедурные текстуры, как и остальной проект.
public static class CardDepthUtility
{
    private const int ShadowTexSize = 64;
    private const int ShadowFeatherPx = 24; // толщина мягкой каймы тени, не зависит от размера карточки (9-slice)

    private static readonly Vector2 ShadowOffset = new Vector2(10f, -10f); // вниз-вправо (Y отрицательный = вниз в UI) — подобрано пользователем вживую в Inspector 2026-08-18
    private const float ShadowOutset = 10f; // насколько тень "высовывается" за периметр карточки
    private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 1f); // alpha 255 — подобрано пользователем вживую 2026-08-18; мягкость даёт градиент текстуры (GetSoftShadowSprite), не альфа-множитель
    private static readonly Color BevelColor = new Color(0f, 0f, 0f, 0.55f);

    private static Sprite softShadowSprite;

    // Мягкая тень — 9-slice текстура: альфа спадает от края текстуры (прозрачно) к границе
    // краевой полосы (непрозрачно), квадратичный спад — плотнее у самой карточки, мягкий длинный
    // хвост наружу. При растяжении под любой размер карточки толщина каймы остаётся постоянной.
    private static Sprite GetSoftShadowSprite()
    {
        if (softShadowSprite != null) return softShadowSprite;

        var tex = new Texture2D(ShadowTexSize, ShadowTexSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[ShadowTexSize * ShadowTexSize];
        for (int y = 0; y < ShadowTexSize; y++)
        {
            for (int x = 0; x < ShadowTexSize; x++)
            {
                int distToEdge = Mathf.Min(Mathf.Min(x, ShadowTexSize - 1 - x), Mathf.Min(y, ShadowTexSize - 1 - y));
                float a = Mathf.Clamp01(distToEdge / (float)ShadowFeatherPx);
                a *= a; // квадратичный спад, тот же приём, что GetRadialGlowSprite
                pixels[y * ShadowTexSize + x] = new Color32(0, 0, 0, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        softShadowSprite = Sprite.Create(tex, new Rect(0, 0, ShadowTexSize, ShadowTexSize), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect, new Vector4(ShadowFeatherPx, ShadowFeatherPx, ShadowFeatherPx, ShadowFeatherPx));
        return softShadowSprite;
    }

    // Добавляет тень (новый сиблинг сразу позади карточки) и скос (см. ItemBadgeUtility.ApplyRarityFrame)
    // тёмным цветом. Порядок вызовов внутри важен: тень вставляется ПЕРВОЙ (сразу позади cardRect) —
    // ApplyRarityFrame ниже вставит свою рамку позади уже сдвинутого cardBackground и окажется МЕЖДУ
    // тенью и карточкой, итоговый порядок рендера: тень (дальше всех) -> скос -> сама карточка.
    public static void ApplyCardDepth(Image cardBackground)
    {
        if (cardBackground == null) return;

        var cardRect = cardBackground.rectTransform;
        var parentRect = cardRect.parent as RectTransform;
        if (parentRect == null) return;

        // Карточка может лежать под LayoutGroup (Grid/Horizontal/Vertical) — тот выставляет реальные
        // anchoredPosition/sizeDelta только на отложенном проходе в конце кадра. Форсируем проход
        // сейчас же, иначе тень скопирует ещё не выставленную (нулевую/дефолтную) геометрию.
        LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);

        var shadowObj = new GameObject("CardShadow", typeof(RectTransform), typeof(Image));
        var shadowRect = (RectTransform)shadowObj.transform;
        shadowRect.SetParent(parentRect, false);
        shadowRect.SetSiblingIndex(cardRect.GetSiblingIndex()); // позади карточки

        shadowRect.anchorMin = cardRect.anchorMin;
        shadowRect.anchorMax = cardRect.anchorMax;
        shadowRect.pivot = cardRect.pivot;
        shadowRect.anchoredPosition = cardRect.anchoredPosition + ShadowOffset;
        shadowRect.sizeDelta = cardRect.sizeDelta + new Vector2(ShadowOutset * 2f, ShadowOutset * 2f);
        shadowRect.localScale = cardRect.localScale; // напр. HeroMiniCardUI в Collection-сетке уменьшен localScale=0.5

        var shadowImg = shadowObj.GetComponent<Image>();
        shadowImg.sprite = GetSoftShadowSprite();
        shadowImg.type = Image.Type.Sliced;
        shadowImg.color = ShadowColor;
        shadowImg.raycastTarget = false; // тень не должна перехватывать клики по карточке

        // КРИТИЧНО: если parentRect — GridLayoutGroup (или Horizontal/VerticalLayoutGroup), она считает
        // КАЖДОГО прямого ребёнка отдельной ячейкой — новый сиблинг-тень без этой пометки ломает всю
        // раскладку (сдвигает/дублирует ячейки — баг 2026-08-18, воспроизведён на Squad/Collection/
        // Inventory). LayoutElement.ignoreLayout исключает объект из расчёта сетки, при этом его
        // anchoredPosition/sizeDelta остаются полностью под нашим контролем (уже выставлены выше).
        shadowObj.AddComponent<LayoutElement>().ignoreLayout = true;

        Image bevelFrame = null;
        ItemBadgeUtility.ApplyRarityFrame(cardBackground, BevelColor, ref bevelFrame);
        if (bevelFrame != null)
        {
            bevelFrame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true; // та же причина, что и у тени выше
            bevelFrame.rectTransform.localScale = cardRect.localScale; // ApplyRarityFrame сам scale не копирует
        }
    }
}
