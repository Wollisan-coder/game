using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Вешается на любой GameObject (например, на сам контейнер сетки 3х3).
// Центральная кнопка по клику меняет местами (позиции) остальные 8 кнопок.
public class ShuffleButtonGrid : MonoBehaviour
{
    [Header("Центральная кнопка — по клику перемешивает остальные")]
    public Button centerButton;

    [Header("Остальные 8 кнопок сетки (без центральной)")]
    public RectTransform[] shuffleTargets;

    [Header("Визуальный эффект перемешивания")]
    public float shuffleDuration = 0.4f;
    public float squeezeAmount = 0.2f; // насколько кнопки "сжимаются" по пути

    private bool isShuffling;

    private void Awake()
    {
        if (centerButton != null)
            centerButton.onClick.AddListener(Shuffle);
    }

    // Шаффл можно нажать только один раз на текущую раскладку — сама кнопка блокируется
    // сразу по нажатию и снова разблокируется в RevealAllOptions() при новой раскладке
    public void Shuffle()
    {
        if (isShuffling) return;

        if (centerButton != null)
            centerButton.interactable = false;

        StartCoroutine(ShuffleRoutine());
    }

    // Показать все 8 вариантов открыто и разблокировать шаффл — вызывать при открытии панели, до шаффла
    public void RevealAllOptions()
    {
        if (centerButton != null)
            centerButton.interactable = true;

        foreach (var t in shuffleTargets)
        {
            var option = t != null ? t.GetComponent<BoardEffectOption>() : null;
            if (option != null) option.ShowRevealed();
        }
    }

    private void BlurAllOptions()
    {
        foreach (var t in shuffleTargets)
        {
            var option = t != null ? t.GetComponent<BoardEffectOption>() : null;
            if (option != null) option.ShowBlurred();
        }
    }

    private IEnumerator ShuffleRoutine()
    {
        isShuffling = true;

        List<Vector2> startPositions = new List<Vector2>();
        foreach (var t in shuffleTargets)
            startPositions.Add(t.anchoredPosition);

        List<Vector2> targetPositions = new List<Vector2>(startPositions);
        for (int i = targetPositions.Count - 1; i > 0; i--)
        {
            int r = Random.Range(0, i + 1);
            (targetPositions[i], targetPositions[r]) = (targetPositions[r], targetPositions[i]);
        }

        float elapsed = 0f;
        while (elapsed < shuffleDuration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, elapsed / shuffleDuration);
            float scaleMod = 1f - Mathf.Sin(p * Mathf.PI) * squeezeAmount; // лёгкое "сжатие" на полпути

            for (int i = 0; i < shuffleTargets.Length; i++)
            {
                shuffleTargets[i].anchoredPosition = Vector2.Lerp(startPositions[i], targetPositions[i], p);
                shuffleTargets[i].localScale = Vector3.one * scaleMod;
            }

            yield return null;
        }

        for (int i = 0; i < shuffleTargets.Length; i++)
        {
            shuffleTargets[i].anchoredPosition = targetPositions[i];
            shuffleTargets[i].localScale = Vector3.one;
        }

        BlurAllOptions(); // после шаффла позиции секретны — игрок выбирает вслепую

        isShuffling = false;
    }
}
