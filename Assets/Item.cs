using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class Item : MonoBehaviour
{
    public int x;
    public int y;
    public int type;

    [Header("Состояние фишки (для скиллов рас)")]
    public bool isJoker;   // джокер (ConvertCellToJoker) — матчится с фишкой любого цвета

    // Создаются матчем 4+/5+ в ряд — см. GridManager.ProcessMatches/FindStraightRuns.
    // Row/Column — какую линию сетки фишка сносит при активации (не направление матча, которым её создали).
    public enum SpecialType { None, LineClearRow, LineClearColumn, ColorBomb }

    [Header("Спец-фишки (созданы матчем 4/5 в ряд)")]
    public SpecialType specialType = SpecialType.None;

    // Не перекрашивает фишку целиком (в отличие от MarkAsJoker) — цвет должен остаться читаемым,
    // чтобы игрок понимал, каким обычным матчем её можно активировать. Вместо этого — форма (растянута
    // вдоль линии, которую снесёт) + лёгкое осветление тона.
    public void MarkAsSpecial(SpecialType type)
    {
        specialType = type;

        switch (type)
        {
            case SpecialType.LineClearRow:
                transform.localScale = new Vector3(baseScale.x * 1.6f, baseScale.y, baseScale.z * 0.6f);
                break;
            case SpecialType.LineClearColumn:
                transform.localScale = new Vector3(baseScale.x * 0.6f, baseScale.y, baseScale.z * 1.6f);
                break;
            case SpecialType.ColorBomb:
                transform.localScale = baseScale * 1.3f;
                break;
        }

        var rend = GetComponentInChildren<Renderer>();
        if (rend == null) return;

        rend.material = new Material(rend.material);
        Color blended = Color.Lerp(GetTintColor(rend.material), Color.white, 0.35f);
        SetTintColor(rend.material, blended);
    }

    [Header("Вредные фишки поля (per-enemy дебаффы, см. HarmfulTileSpawnRule)")]
    public HarmfulTileType harmfulType = HarmfulTileType.None;
    public int harmfulValue;      // Spike/Trap: урон герою; Anchor: сколько матчей по соседству нужно, чтобы снять
    public int anchorMatchStreak; // Anchor: сколько таких матчей уже пережила подряд

    [Header("Заморозка (гемблинг-колесо: FreezeRandomRowOrColumn)")]
    public bool isFrozen;
    public int frozenTurnsRemaining;
    private Color frozenOriginalColor;
    private bool hasFrozenOriginalColor;

    [Header("Иконка ловушки (Trap) — назначить в инспекторе на префабе, объект под фишкой, по умолчанию выключен")]
    public GameObject trapIndicator;

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

        if (harmfulType == HarmfulTileType.Ice)
            ClearHarmful(); // лёд как вредная фишка снимается вместе с разморозкой (в т.ч. досрочно, соседним матчем)

        if (!hasFrozenOriginalColor) return;

        var rend = GetComponentInChildren<Renderer>();
        if (rend == null) return;

        rend.material = new Material(rend.material);
        SetTintColor(rend.material, frozenOriginalColor);
    }

    // Спавнится боем по HarmfulTileSpawnRule врага (EnemyData.harmfulTileSpawns) — переиспользует Freeze/isFrozen
    public void MarkAsIceHarmful(int freezeTurns)
    {
        harmfulType = HarmfulTileType.Ice;
        Freeze(freezeTurns);
    }

    public void MarkAsSpikeHarmful(int damagePerTurn)
    {
        harmfulType = HarmfulTileType.Spike;
        harmfulValue = damagePerTurn;
        TintHarmful(new Color(1f, 0.4f, 0.15f));
    }

    // Сама фишка не тонируется (остаётся обычного цвета) — вместо этого под ней показывается иконка ловушки
    public void MarkAsTrapHarmful(int damageOnMatch)
    {
        harmfulType = HarmfulTileType.Trap;
        harmfulValue = damageOnMatch;

        if (trapIndicator != null)
            trapIndicator.SetActive(true);
    }

    public void MarkAsAnchorHarmful(int requiredAdjacentMatches)
    {
        harmfulType = HarmfulTileType.Anchor;
        harmfulValue = Mathf.Max(1, requiredAdjacentMatches);
        anchorMatchStreak = 0;
        TintHarmful(new Color(0.4f, 0.4f, 0.45f));
    }

    public void ClearHarmful()
    {
        harmfulType = HarmfulTileType.None;
        harmfulValue = 0;
        anchorMatchStreak = 0;

        if (trapIndicator != null)
            trapIndicator.SetActive(false);
    }

    private void TintHarmful(Color color)
    {
        var rend = GetComponentInChildren<Renderer>();
        if (rend == null) return;

        rend.material = new Material(rend.material);
        SetTintColor(rend.material, color);
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

        if (trapIndicator != null)
            trapIndicator.SetActive(false);
    }

    // Медленное вращение — единственный визуальный маркер ColorBomb, который не конфликтует
    // с pulse-анимациями (SetSelected/SetHighlighted перезаписывают localScale, но не rotation).
    private void Update()
    {
        if (specialType == SpecialType.ColorBomb)
            transform.Rotate(Vector3.up, 90f * Time.deltaTime);
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
