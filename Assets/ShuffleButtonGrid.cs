using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Вішається на будь-який GameObject (наприклад, на сам контейнер сітки 3х3).
// Центральна кнопка по кліку міняє місцями (позиції) решту 8 кнопок.
public class ShuffleButtonGrid : MonoBehaviour
{
    [Header("Центральна кнопка — по кліку перемішує решту")]
    public Button centerButton;

    [Header("Решта 8 кнопок сітки (без центральної)")]
    public RectTransform[] shuffleTargets;

    [Header("Візуальний ефект перемішування")]
    public float shuffleDuration = 0.4f;
    public float squeezeAmount = 0.2f; // наскільки кнопки "стискаються" по дорозі

    private bool isShuffling;

    private void Awake()
    {
        if (centerButton != null)
            centerButton.onClick.AddListener(Shuffle);
    }

    public void Shuffle()
    {
        if (isShuffling) return;
        StartCoroutine(ShuffleRoutine());
    }

    // Показати всі 8 варіантів відкрито — викликати при відкритті панелі, до шаффла
    public void RevealAllOptions()
    {
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
            float scaleMod = 1f - Mathf.Sin(p * Mathf.PI) * squeezeAmount; // легкий "стиск" на півшляху

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

        BlurAllOptions(); // після шаффла позиції секретні — гравець обирає наосліп

        isShuffling = false;
    }
}
