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

    // Отдельная 3D-модель под спец-фишку (GridManager.lineClearSpecialPrefab/colorBombSpecialPrefab —
    // общие на все цвета, не тонируются под цвет матча, эти модели уже сами по себе "особенные" по
    // дизайну). Исходный гем не удаляется — только прячется (Renderer.enabled=false, не SetActive), так
    // что его Collider остаётся кликабельным в тех же границах, что и раньше.
    private GameObject specialVisualInstance;

    // Если под тип спец-фишки модель ещё не назначена в GridManager — фолбэк на старое поведение:
    // поворот самого гема на 90° вокруг Y (Row/Column, поле лежит в плоскости XZ — см.
    // GridManager.SpawnItem, Y-поворот меняет местами X/Z-протяжённость без искажения геометрии) +
    // лёгкое осветление тона, чтобы цвет остался читаемым (в отличие от MarkAsJoker, который красит целиком).
    public void MarkAsSpecial(SpecialType type)
    {
        specialType = type;

        GameObject prefab = type switch
        {
            SpecialType.LineClearRow or SpecialType.LineClearColumn => gridManager != null ? gridManager.lineClearSpecialPrefab : null,
            SpecialType.ColorBomb => gridManager != null ? gridManager.colorBombSpecialPrefab : null,
            _ => null,
        };

        if (prefab != null)
        {
            // Цвет матча — берём с исходного гема ДО того, как его спрячем (все Renderer'ы одной фишки
            // одного цвета, хватает первого попавшегося). Модель спец-фишки обесцвечена нейтральным
            // серым (см. SpecialTileTextureDesaturator) специально под это — тонирование поверх серой
            // базы даёт чистый цвет, а не грязный оттенок, как было бы поверх исходной красной текстуры.
            Color matchColor = Color.white;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                matchColor = GetTintColor(r.material);
                r.enabled = false;
            }

            // "Сырой" _BaseColor/baseColorFactor гема нередко бледнее, чем кажется на доске (яркость там
            // во многом от PBR-освещения, не от самого базового цвета) — умножение на серое сохраняет
            // соотношение каналов, но не добавляет насыщенности, так что блёклый исходник даёт мутный
            // тинт (замечено на синем: на фиолетовом было терпимо, на синем — явно грязно). Принудительно
            // поднимаем насыщенность и яркость через HSV перед тонированием — цвет спец-фишки всегда чёткий,
            // независимо от того, насколько бледный конкретный гем "по паспорту".
            Color.RGBToHSV(matchColor, out float hue, out float saturation, out float value);
            matchColor = Color.HSVToRGB(hue, Mathf.Max(saturation, 0.75f), Mathf.Max(value, 0.85f));

            specialVisualInstance = Instantiate(prefab, transform);
            specialVisualInstance.transform.localPosition = Vector3.zero;
            // Относительный поворот, а не абсолютный — у LineClearSpecial_Pivot уже запечён свой базовый
            // разворот (подобран вручную в редакторе, см. Row-случай). Если поставить сюда абсолютное
            // значение, это затрёт запечённый поворот префаба — вместо этого только докручиваем ещё
            // 90° для Column, Row не трогаем вообще.
            if (type == SpecialType.LineClearColumn)
                specialVisualInstance.transform.localRotation *= Quaternion.Euler(0f, 0f, 90f);

            var specialRend = specialVisualInstance.GetComponentInChildren<Renderer>();
            if (specialRend != null)
            {
                specialRend.material = new Material(specialRend.material);
                SetTintColor(specialRend.material, matchColor);
            }

            // Плавный рост вместо мгновенной подмены — GridManager.ProcessMatches запускает уничтожение
            // остальных фишек рана СРАЗУ следом (0.25с анимация), а MarkAsSpecial раньше подменяла модель
            // мгновенно за один кадр — визуально казалось, что спец-фишка появляется "до того", как
            // соседние фишки успевают исчезнуть, а не одновременно с ними.
            StartCoroutine(PopInSpecialVisual());
            return;
        }

        switch (type)
        {
            case SpecialType.LineClearRow:
                transform.localRotation = Quaternion.identity;
                break;
            case SpecialType.LineClearColumn:
                transform.localRotation = Quaternion.Euler(0f, 0f, 00f);
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
    public int harmfulValue;      // Spike/Trap: урон герою; Anchor/Cursed: порог в ходах; BloodMark: % урона игроку
    public int anchorMatchStreak; // Anchor: сколько таких матчей уже пережила подряд
    public int cursedTurnsAlive;  // Cursed: сколько ходов прожила без снятия — при достижении harmfulValue заражает соседа

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

    // Спреды за N ходов не снята матчем — см. GridManager.TickHarmfulTiles. Заражение соседа переносит
    // тот же порог value, а счётчик текущей фишки обнуляется, чтобы она могла заразить ещё одного соседа позже.
    public void MarkAsCursedHarmful(int turnsUntilSpread)
    {
        harmfulType = HarmfulTileType.Cursed;
        harmfulValue = Mathf.Max(1, turnsUntilSpread);
        cursedTurnsAlive = 0;
        TintHarmful(new Color(0.35f, 0.05f, 0.45f));
    }

    // Пассивная — пока хоть одна такая фишка на поле, весь урон игроку увеличен на value% (см.
    // GridManager.GetBloodMarkDamageMultiplier / BattleManager.ApplyDamageToHero).
    public void MarkAsBloodMarkHarmful(int damageIncreasePercent)
    {
        harmfulType = HarmfulTileType.BloodMark;
        harmfulValue = Mathf.Max(0, damageIncreasePercent);
        TintHarmful(new Color(0.55f, 0f, 0.05f));
    }

    // Заражает случайного соседа каждый ход, пока её не уберут матчем — см. GridManager.TickHarmfulTiles.
    public void MarkAsRottenHarmful()
    {
        harmfulType = HarmfulTileType.Rotten;
        TintHarmful(new Color(0.3f, 0.28f, 0.05f));
    }

    // Само уничтожение (обычным матчем) триггерит эффект — см. GridManager.ProcessMatches.
    public void MarkAsChaosHarmful()
    {
        harmfulType = HarmfulTileType.Chaos;
        TintHarmful(new Color(0.9f, 0.1f, 0.8f));
    }

    public void ClearHarmful()
    {
        harmfulType = HarmfulTileType.None;
        harmfulValue = 0;
        anchorMatchStreak = 0;
        cursedTurnsAlive = 0;

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

    // У разных шейдеров может не быть ни одного из привычных цветовых свойств — не падаем в этом случае,
    // а просто игнорируем тонирование. baseColorFactor — это конкретно glTF Shader Graph (gltFast):
    // свойства там называются буквально как в спецификации glTF (baseColorTexture/baseColorFactor), а
    // не по юнитёвской конвенции _BaseColor/_Color — см. LineClearSpecial_Gray.mat/BombClearSpecial_Gray.mat.
    private static Color GetTintColor(Material mat)
    {
        if (mat.HasProperty("_BaseColor")) return mat.GetColor("_BaseColor");
        if (mat.HasProperty("_Color")) return mat.color;
        if (mat.HasProperty("baseColorFactor")) return mat.GetColor("baseColorFactor");
        return Color.white;
    }

    private static void SetTintColor(Material mat, Color color)
    {
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else if (mat.HasProperty("_Color")) mat.color = color;
        else if (mat.HasProperty("baseColorFactor")) mat.SetColor("baseColorFactor", color);
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
        if (gridManager != null && gridManager.trainingMode) return; // Boss Training — доской играет ИИ, не игрок

        gridManager?.ResetIdleTimer(); // любой клик сбрасывает таймер подсказки и прячет её

        if (gridManager != null && gridManager.isBusy) return; // поле ещё анимируется — игнорируем клик

        mouseDownScreenPos = Mouse.current.position.ReadValue();
    }

    private void OnMouseUp()
    {
        if (gridManager != null && gridManager.trainingMode) return;
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

    // Растёт от нуля до целевого масштаба за 0.25с — та же длительность, что и PlayDestroyAnimation
    // ниже, так что появление спец-фишки визуально совпадает по времени с исчезновением соседних фишек
    // того же рана, а не выглядит мгновенным щелчком раньше/позже них.
    private IEnumerator PopInSpecialVisual()
    {
        if (specialVisualInstance == null) yield break;

        Vector3 targetScale = specialVisualInstance.transform.localScale;
        specialVisualInstance.transform.localScale = Vector3.zero;

        float duration = 0.25f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            specialVisualInstance.transform.localScale = targetScale * Mathf.Clamp01(t / duration);
            yield return null;
        }

        specialVisualInstance.transform.localScale = targetScale;
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
