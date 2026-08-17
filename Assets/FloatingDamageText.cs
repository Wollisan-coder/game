using UnityEngine;
using TMPro;

// Одна всплывающая цифра урона — спавнится снизу карточки (герой/враг), плавно едет вверх
// до верхнего края той же карточки и растворяется, затем сама себя уничтожает. Полностью
// одноразовая — ничего не сохраняется в сцене/префабе, как и остальной рантайм-UI в проекте.
public class FloatingDamageText : MonoBehaviour
{
    private const float Lifetime = 2f;
    public static readonly Color EnemyDamageColor = new Color(1f, 0.35f, 0.25f); // враг получил урон
    public static readonly Color HeroDamageColor = new Color(1f, 0.15f, 0.15f);  // герой получил урон
    public static readonly Color PositiveGainColor = new Color(0.3f, 0.85f, 0.35f); // прокачка/апгрейд

    public static void Spawn(RectTransform cardRect, int amount, Color color)
    {
        if (amount <= 0) return;
        Spawn(cardRect, $"-{amount}", color);
    }

    // xOffset — чтобы несколько одновременных цифр (например, левелап поднимает сразу 4 стата) не сыпались
    // ровно друг на друга; по умолчанию 0, старые вызовы (урон в бою — всегда одна цифра за раз) не задеты.
    public static void Spawn(RectTransform cardRect, string text, Color color, float xOffset = 0)
    {
        if (cardRect == null || string.IsNullOrEmpty(text)) return;

        var obj = new GameObject("FloatingDamageText", typeof(RectTransform));
        var rect = (RectTransform)obj.transform;
        rect.SetParent(cardRect, false);
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(280, 120);
        rect.anchoredPosition = new Vector2(xOffset, 0); // низ карточки

        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 90;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;

        float riseDistance = cardRect.rect.height > 0 ? cardRect.rect.height : 100f;
        obj.AddComponent<FloatingDamageText>().Init(rect, tmp, riseDistance);
    }

    private RectTransform rect;
    private TextMeshProUGUI text;
    private float riseDistance;
    private float elapsed;
    private Color startColor;

    private void Init(RectTransform r, TextMeshProUGUI t, float rise)
    {
        rect = r;
        text = t;
        riseDistance = rise;
        startColor = t.color;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / Lifetime);

        rect.anchoredPosition = new Vector2(0, riseDistance * t);

        Color c = startColor;
        c.a = startColor.a * (1f - t);
        text.color = c;

        if (elapsed >= Lifetime)
            Destroy(gameObject);
    }
}
