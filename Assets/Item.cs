using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class Item : MonoBehaviour
{
    public int x;
    public int y;
    public int type;

    [Header("Состояние фишки (для скиллов рас)")]
    public bool isHarmful; // заготовка под будущую систему дебаффов поля — DestroyHarmfulTile ищет именно такие фишки
    public bool isJoker;   // джокер (ConvertCellToJoker) — матчится с фишкой любого цвета

    [Header("Заморозка (гемблинг-колесо: FreezeRandomRowOrColumn)")]
    public bool isFrozen;
    public int frozenTurnsRemaining;
    private Color frozenOriginalColor;
    private bool hasFrozenOriginalColor;

    // Замораживает фишку на turns ходов — её нельзя свайпать/менять местами, пока не разморозится
    public void Freeze(int turns)
    {
        isFrozen = true;
        frozenTurnsRemaining = turns;

        var rend = GetComponentInChildren<Renderer>();
        if (rend == null) return;

        rend.material = new Material(rend.material);

        if (!hasFrozenOriginalColor)
        {
            frozenOriginalColor = GetTintColor(rend.material);
            hasFrozenOriginalColor = true;
        }

        SetTintColor(rend.material, new Color(0.6f, 0.85f, 1f));
    }

    public void Unfreeze()
    {
        isFrozen = false;
        frozenTurnsRemaining = 0;

        if (!hasFrozenOriginalColor) return;

        var rend = GetComponentInChildren<Renderer>();
        if (rend == null) return;

        rend.material = new Material(rend.material);
        SetTintColor(rend.material, frozenOriginalColor);
    }

    // У разных шейдеров (в т.ч. glTF Shader Graph без стандартных _BaseColor/_Color)
    // может не быть ни одного из привычных цветовых свойств — не падаем в этом случае, а просто игнорируем тонирование
    private static Color GetTintColor(Material mat)
    {
        if (mat.HasProperty("_BaseColor")) return mat.GetColor("_BaseColor");
        if (mat.HasProperty("_Color")) return mat.color;
        return Color.white;
    }

    private static void SetTintColor(Material mat, Color color)
    {
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else if (mat.HasProperty("_Color")) mat.color = color;
    }

    // Простая визуальная отметка джокера — золотистая подсветка поверх цвета фишки
    public void MarkAsJoker()
    {
        isJoker = true;

        var rend = GetComponentInChildren<Renderer>();
        if (rend == null) return;

        rend.material = new Material(rend.material);
        if (rend.material.HasProperty("_BaseColor"))
            rend.material.SetColor("_BaseColor", new Color(1f, 0.85f, 0.2f));
        else if (rend.material.HasProperty("_Color"))
            rend.material.color = new Color(1f, 0.85f, 0.2f);
    }

    [Header("Эффекты")]
    public GameObject destroyEffectPrefab; // назначить в Inspector на каждом префабе гема

    private GridManager gridManager;
    private static Item firstSelected;
    private Coroutine selectAnimCoroutine;
    private Vector3 baseScale;

    private void Awake()
    {
        gridManager = FindAnyObjectByType<GridManager>();
        baseScale = transform.localScale;
    }

    [Header("Свайп")]
    public float swipeThresholdPixels = 50f; // меньшее движение мыши/пальца — считается тапом, не свайпом

    private Vector3 mouseDownScreenPos;

    private void OnMouseDown()
    {
        gridManager?.ResetIdleTimer(); // любой клик сбрасывает таймер подсказки и прячет её

        if (gridManager != null && gridManager.isBusy) return; // поле ещё анимируется — игнорируем клик

        mouseDownScreenPos = Mouse.current.position.ReadValue();
    }

    private void OnMouseUp()
    {
        if (gridManager != null && gridManager.isBusy) return;

        Vector3 delta = (Vector3)Mouse.current.position.ReadValue() - mouseDownScreenPos;

        if (delta.magnitude >= swipeThresholdPixels)
            HandleSwipe(delta);
        else
            HandleTap();
    }

    // Свайп напрямую двигает фишку к соседу в сторону свайпа, минуя систему тап-тап-выбора
    private void HandleSwipe(Vector3 screenDelta)
    {
        if (firstSelected != null)
        {
            firstSelected.SetSelected(false);
            firstSelected = null;
        }

        int dx = 0, dy = 0;
        if (Mathf.Abs(screenDelta.x) > Mathf.Abs(screenDelta.y))
            dx = screenDelta.x > 0 ? 1 : -1;
        else
            dy = screenDelta.y > 0 ? 1 : -1;

        Item target = gridManager != null ? gridManager.GetItemAt(x + dx, y + dy) : null;
        if (target != null)
            gridManager.StartCoroutine(gridManager.SwapItems(this, target));
    }

    private void HandleTap()
    {
        if (firstSelected == null)
        {
            firstSelected = this;
            SetSelected(true);
        }
        else
        {
            firstSelected.SetSelected(false); // снимаем подсветку с предыдущей

            if (IsNeighbor(firstSelected, this))
            {
                gridManager.StartCoroutine(
                    gridManager.SwapItems(firstSelected, this)
                );
            }

            firstSelected = null;
        }
    }

    private void SetSelected(bool isSelected)
    {
        if (selectAnimCoroutine != null) StopCoroutine(selectAnimCoroutine);

        if (isSelected)
            selectAnimCoroutine = StartCoroutine(PulseRoutine());
        else
            transform.localScale = baseScale;
    }

    private IEnumerator PulseRoutine()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime * 4f;
            float scaleMod = 1f + Mathf.Sin(t) * 0.1f; // пульсация ±10%
            transform.localScale = baseScale * scaleMod;
            yield return null;
        }
    }

    private Coroutine hintAnimCoroutine;

    // Вызывается из GridManager, когда эта фишка — подсказка возможного хода
    public void SetHighlighted(bool isHighlighted)
    {
        if (hintAnimCoroutine != null) StopCoroutine(hintAnimCoroutine);

        if (isHighlighted)
            hintAnimCoroutine = StartCoroutine(HintPulseRoutine());
        else
            transform.localScale = baseScale;
    }

    private IEnumerator HintPulseRoutine()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime * 3f;
            float scaleMod = 1f + Mathf.Sin(t) * 0.15f; // более заметная пульсация, чем обычное выделение
            transform.localScale = baseScale * scaleMod;
            yield return null;
        }
    }

    private bool IsNeighbor(Item a, Item b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return (dx == 1 && dy == 0) || (dx == 0 && dy == 1);
    }

    public void MoveTo(Vector3 targetPosition, float duration = 0.2f)
    {
        StopAllCoroutines();
        StartCoroutine(MoveRoutine(targetPosition, duration));
    }

    private IEnumerator MoveRoutine(Vector3 target, float duration)
    {
        Vector3 start = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(start, target, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = target;
    }

    public IEnumerator PlayDestroyAnimation()
    {
        // Запускаем частицы перед исчезновением
        if (destroyEffectPrefab != null)
{
    GameObject effect = Instantiate(destroyEffectPrefab, transform.position, Quaternion.identity);

    var renderer = GetComponentInChildren<Renderer>();
    var ps = effect.GetComponent<ParticleSystem>();
    if (renderer != null && ps != null)
    {
        Color gemColor = Color.white;

        if (renderer.material.HasProperty("_Color"))
            gemColor = renderer.material.color;
        else if (renderer.material.HasProperty("_BaseColor"))
            gemColor = renderer.material.GetColor("_BaseColor");

        var main = ps.main;
        main.startColor = gemColor;

        ps.Play(); // явно запускаем систему частиц
    }

    Destroy(effect, 1f);
}

        float duration = 0.25f;
        float t = 0f;
        Vector3 startScale = transform.localScale;

        while (t < duration)
        {
            t += Time.deltaTime;
            float p = t / duration;
            float scaleMod = Mathf.Lerp(1f, 0f, p) + Mathf.Sin(p * Mathf.PI) * 0.3f;
            transform.localScale = startScale * Mathf.Max(scaleMod, 0f);
            transform.Rotate(Vector3.up, 360f * Time.deltaTime);
            yield return null;
        }

        transform.localScale = Vector3.zero;
    }
}
