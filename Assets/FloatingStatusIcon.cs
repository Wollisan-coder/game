using UnityEngine;
using UnityEngine.UI;

// Всплывающая иконка статус-эффекта — спавнится снизу карточки (герой/враг) в момент,
// когда эффект только что стал активным, едет вверх до верхнего края карточки с затуханием
// и сама себя уничтожает. Тот же паттерн, что и FloatingDamageText.
public class FloatingStatusIcon : MonoBehaviour
{
    private const float Lifetime = 2f;
    private const float DefaultIconSize = 48f;

    public static void Spawn(RectTransform cardRect, Sprite icon, AudioClip sound = null, float size = DefaultIconSize)
    {
        if (cardRect == null || icon == null) return;

        var obj = new GameObject("FloatingStatusIcon", typeof(RectTransform));
        var rect = (RectTransform)obj.transform;
        rect.SetParent(cardRect, false);
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = Vector2.zero; // низ карточки

        var image = obj.AddComponent<Image>();
        image.sprite = icon;
        image.preserveAspect = true;
        image.raycastTarget = false;

        float riseDistance = cardRect.rect.height > 0 ? cardRect.rect.height : 100f;
        obj.AddComponent<FloatingStatusIcon>().Init(rect, image, riseDistance);

        if (sound != null)
            AudioSource.PlayClipAtPoint(sound, Vector3.zero);
    }

    private RectTransform rect;
    private Image image;
    private float riseDistance;
    private float elapsed;
    private Color startColor;

    private void Init(RectTransform r, Image i, float rise)
    {
        rect = r;
        image = i;
        riseDistance = rise;
        startColor = i.color;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / Lifetime);

        rect.anchoredPosition = new Vector2(0, riseDistance * t);

        Color c = startColor;
        c.a = startColor.a * (1f - t);
        image.color = c;

        if (elapsed >= Lifetime)
            Destroy(gameObject);
    }
}
