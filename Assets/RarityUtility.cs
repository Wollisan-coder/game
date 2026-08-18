using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Общая логика цвета редкости — используется и предметами, и героями
public static class RarityUtility
{
    public static Color GetColor(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.White: return Color.white;
            case Rarity.Green: return new Color(0.2f, 0.85f, 0.2f);
            case Rarity.Blue: return new Color(0.25f, 0.55f, 1f);
            case Rarity.Purple: return new Color(0.65f, 0.25f, 0.9f);
            case Rarity.Orange: return new Color(1f, 0.55f, 0.1f);
            default: return Color.white;
        }
    }

    // Аббревиатура ранга на ленте редкости — гача-нотация вместо названия цвета (UX-правка 2026-08-18)
    public static string GetTierLabel(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Green: return "R";
            case Rarity.Blue: return "SR";
            case Rarity.Purple: return "SSR";
            case Rarity.Orange: return "LR";
            default: return "";
        }
    }

    private const int PlateTexSize = 32;
    private const int PlateCornerPx = 8;
    private static readonly Color PlateColor = new Color(0.07f, 0.06f, 0.05f); // тёмная кованая плашка, одна на все редкости

    private const int KantTexSize = 32;
    private const float KantRingDist = 7f; // на каком расстоянии от края текстуры светится кант
    private const float KantSpread = 5f;   // ширина размытия кольца в обе стороны от KantRingDist
    private const int KantBorder = 14;     // 9-slice border — перекрывает весь кант (RingDist+Spread)

    private static Sprite plateSprite;
    private static Sprite kantSprite;

    // Тёмная плашка ленты редкости — сплошная заливка со скруглёнными углами, ОДИН цвет для всех редкостей.
    // Раньше плашка сама красилась в цвет редкости (яркий PNG на всю площадь) — читалось слишком "мультяшно"
    // и "дорожной лентой" (см. правку 2026-08-18: пользователь попросил темнее и без камня-иконки). Теперь
    // редкость видна только по канту (GetKantSprite) и аббревиатуре (GetTierLabel).
    private static Sprite GetPlateSprite()
    {
        if (plateSprite != null) return plateSprite;

        var tex = new Texture2D(PlateTexSize, PlateTexSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[PlateTexSize * PlateTexSize];
        for (int y = 0; y < PlateTexSize; y++)
        {
            for (int x = 0; x < PlateTexSize; x++)
            {
                bool inCornerZone = (x < PlateCornerPx || x >= PlateTexSize - PlateCornerPx) && (y < PlateCornerPx || y >= PlateTexSize - PlateCornerPx);
                float a = 1f;
                if (inCornerZone)
                {
                    int cx = x < PlateCornerPx ? PlateCornerPx : PlateTexSize - 1 - PlateCornerPx;
                    int cy = y < PlateCornerPx ? PlateCornerPx : PlateTexSize - 1 - PlateCornerPx;
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    a = dist <= PlateCornerPx ? 1f : 0f;
                }
                pixels[y * PlateTexSize + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        plateSprite = Sprite.Create(tex, new Rect(0, 0, PlateTexSize, PlateTexSize), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect, new Vector4(PlateCornerPx, PlateCornerPx, PlateCornerPx, PlateCornerPx));
        return plateSprite;
    }

    // Светящийся кант — узкое кольцо альфы на фиксированном расстоянии от края (пик на KantRingDist, спад в обе
    // стороны через smoothstep), а не сплошная заливка от края. Красится через Image.color в цвет редкости.
    // 9-slice, поэтому толщина кольца одинакова при любом размере ленты (Hero Detail, карточка Collection/Squad).
    private static Sprite GetKantSprite()
    {
        if (kantSprite != null) return kantSprite;

        var tex = new Texture2D(KantTexSize, KantTexSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[KantTexSize * KantTexSize];
        for (int y = 0; y < KantTexSize; y++)
        {
            for (int x = 0; x < KantTexSize; x++)
            {
                int distToEdge = Mathf.Min(Mathf.Min(x, KantTexSize - 1 - x), Mathf.Min(y, KantTexSize - 1 - y));
                float a = Mathf.Clamp01(1f - Mathf.Abs(distToEdge - KantRingDist) / KantSpread);
                a = a * a * (3f - 2f * a); // smoothstep — мягче линейного спада
                pixels[y * KantTexSize + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        kantSprite = Sprite.Create(tex, new Rect(0, 0, KantTexSize, KantTexSize), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect, new Vector4(KantBorder, KantBorder, KantBorder, KantBorder));
        return kantSprite;
    }

    // Тёмная плашка + светящийся кант цвета редкости + аббревиатура ранга (R/SR/SSR/LR) — заменяет старые
    // PNG-спрайты RarityFrameSet (стоковая дорожно-предупредительная лента, не подходила под тёмное фэнтези —
    // см. UX-правку 2026-08-18). frameImage — тот же Image, что раньше показывал PNG рамки; kantGlow/tierLabel
    // создаются один раз поверх него и кэшируются на стороне вызывающего (тот же приём, что и
    // ItemBadgeUtility.ApplyRarityFrame/ApplyLevelBadge). White больше не выдаётся игрокам — лента, как и
    // раньше, для него целиком скрывается.
    public static void ApplyFrame(Image frameImage, Rarity rarity, ref Image kantGlow, ref TMP_Text tierLabel)
    {
        if (frameImage == null) return;

        bool visible = rarity != Rarity.White;
        frameImage.enabled = visible;
        if (!visible)
        {
            if (kantGlow != null) kantGlow.gameObject.SetActive(false);
            if (tierLabel != null) tierLabel.gameObject.SetActive(false);
            return;
        }

        frameImage.sprite = GetPlateSprite();
        frameImage.type = Image.Type.Sliced;
        frameImage.color = PlateColor;

        if (kantGlow == null)
        {
            var obj = new GameObject("RarityKant", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(frameImage.rectTransform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            kantGlow = obj.GetComponent<Image>(); // typeof(Image) в конструкторе GameObject выше уже добавил компонент
            kantGlow.sprite = GetKantSprite();
            kantGlow.type = Image.Type.Sliced;
            kantGlow.raycastTarget = false;
        }
        kantGlow.gameObject.SetActive(true);
        kantGlow.color = GetColor(rarity);

        if (tierLabel == null)
        {
            var textObj = new GameObject("RarityTierLabel", typeof(RectTransform));
            var textRect = (RectTransform)textObj.transform;
            textRect.SetParent(frameImage.rectTransform, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            tierLabel = textObj.AddComponent<TextMeshProUGUI>();
            tierLabel.alignment = TextAlignmentOptions.Center;
            tierLabel.enableAutoSizing = true; // лента бывает и во весь экран (Hero Detail), и мелкой (Collection-карточка)
            tierLabel.fontSizeMin = 10;
            tierLabel.fontSizeMax = 26;
            tierLabel.fontStyle = FontStyles.Bold;
            tierLabel.color = new Color(0.91f, 0.86f, 0.77f); // тёплый пергамент — читается на тёмной плашке
            tierLabel.raycastTarget = false;
        }
        tierLabel.gameObject.SetActive(true);
        tierLabel.text = GetTierLabel(rarity);
    }
}
