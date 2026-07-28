using System.Collections;
using UnityEngine;

// Вешается на модель под сеткой фишек. Камера сцены смотрит строго вниз (world -Y),
// доска лежит горизонтально в плоскости X/Z — поэтому "перевернуть вверх тормашками"
// означает крутить её вокруг горизонтальной мировой оси (лежащей в плоскости стола),
// а не вокруг вертикали (это дало бы просто разворот на месте, незаметный на
// симметричной сетке). Крутим вокруг центра меша (RotateAround), а не вокруг pivot'а
// объекта (у импортированного .obj pivot обычно в углу, и вращение вокруг него
// сдвигало бы всю доску по дуге вместо переворота на месте).
public class FlippableModel : MonoBehaviour
{
    [Header("Скорость анимации переворота")]
    public float flipDuration = 0.6f;

    [Header("Ось переворота в мировых координатах (должна лежать в плоскости стола, не вертикаль!)")]
    public Vector3 worldFlipAxis = Vector3.right;

    private bool isFlipped;
    private Coroutine flipRoutine;
    private Vector3 pivotWorldPoint;

    private void Start()
    {
        var rend = GetComponentInChildren<Renderer>();
        pivotWorldPoint = rend != null ? rend.bounds.center : transform.position;
    }

    // Вызывать из UI Button (OnClick) или из кода при нужном событии
    public void Flip()
    {
        if (flipRoutine != null)
            StopCoroutine(flipRoutine);

        isFlipped = !isFlipped;
        flipRoutine = StartCoroutine(FlipRoutine(isFlipped ? 180f : -180f));
    }

    private IEnumerator FlipRoutine(float totalAngle)
    {
        Vector3 axis = worldFlipAxis.normalized;
        float elapsed = 0f;
        float appliedAngle = 0f;

        while (elapsed < flipDuration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / flipDuration));
            float targetAngle = totalAngle * p;
            transform.RotateAround(pivotWorldPoint, axis, targetAngle - appliedAngle);
            appliedAngle = targetAngle;
            yield return null;
        }

        transform.RotateAround(pivotWorldPoint, axis, totalAngle - appliedAngle);
        flipRoutine = null;
    }
}
