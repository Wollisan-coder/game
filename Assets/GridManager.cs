using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Настройки сетки")]
    public int width = 8;
    public int height = 8;
    public float cellSize = 1.0f; // Расстояние между центрами фишек

    [Header("Префабы")]
    public GameObject[] itemPrefabs; // Массив 3D-префабов разных типов

    // Отдельные модели под спец-фишки (матч 4/5+) — общие для всех цветов, не завязаны на itemPrefabs.
    // Если не назначены — Item.MarkAsSpecial откатывается на старый фолбэк (поворот/тонирование самого
    // гема), см. комментарий там же.
    [Header("Спец-фишки (матч 4/5+) — общие модели, см. Item.MarkAsSpecial")]
    public GameObject lineClearSpecialPrefab; // 2Sides.glb — звезда с орнаментом, поворачивается 90° под Row/Column
    public GameObject colorBombSpecialPrefab; // Bomb.glb — гранат-кристалл

    [Header("Бой")]
    public BattleManager battleManager;

    [Header("Тип 'Red' в массиве itemPrefabs")]
public int redTypeIndex = 0; // проверь, что Element 0 = RedGem в твоём массиве

    // Пока true — ввод (клик по фишке) игнорируется: идёт свап/падение/каскад
    public bool isBusy;

    [Header("Подсказка хода (если игрок долго не играет)")]
    public float hintDelay = 6f;
    private float idleTimer = 0f;
    private Item hintItem;
    private bool hintActive = false;

    // Строгий порядок ходов, а не таймер: ИИ делает РОВНО один ход по запросу (RequestNextAiMove), затем
    // молчит, пока BattleManager снова не попросит — тот открывает "окно игрока" между ходами ИИ
    // (один скилл босса ИЛИ таймаут — см. BattleManager.OpenBossTrainingPlayerWindow) вместо простого
    // тикающего таймера, который позволял спамить один и тот же скилл бесконечно между автоходами.
    [Header("Boss Training — доской играет ИИ вместо игрока (см. BattleManager.isBossTraining)")]
    public bool trainingMode;

    private Coroutine aiMoveCoroutine;

    public void SetTrainingMode(bool enabled)
    {
        trainingMode = enabled;

        if (enabled)
            RequestNextAiMove();
        else if (aiMoveCoroutine != null)
        {
            StopCoroutine(aiMoveCoroutine);
            aiMoveCoroutine = null;
        }
    }

    // Вызывать, когда пора сходить ИИ: сразу при старте тренировки и затем каждый раз, когда
    // BattleManager закрывает "окно игрока" (скилл использован или истёк таймаут).
    public void RequestNextAiMove()
    {
        if (!trainingMode) return;
        if (aiMoveCoroutine != null) return; // ход уже идёт — не дублируем запрос

        aiMoveCoroutine = StartCoroutine(PlayOneAiMove());
    }

    private IEnumerator PlayOneAiMove()
    {
        // grid ещё может быть null: BattleManager.Awake() (который запускает тренировку и, значит, первый
        // ход ИИ) выполняется РАНЬШЕ GridManager.Start() (которая и создаёт/заполняет grid) — Unity сначала
        // прогоняет Awake() у всех объектов сцены, и только потом Start() у всех. Ждём, пока доска реально появится.
        while (grid == null || isBusy)
            yield return null;

        // Прежде чем ходить по доске, ИИ пробует применить навык тренируемого героя (если хватает
        // маны) — так же, как реальный игрок мог бы кликнуть по HeroCardUI вместо свапа фишек.
        // Сам вызов уже закрывает окно/переходит ход дальше (см. BattleManager.TryUseSkill), поэтому
        // ход по доске в этом случае не делаем.
        if (battleManager != null && battleManager.TryUseTrainedHeroSkillIfPossible())
        {
            aiMoveCoroutine = null;
            yield break;
        }

        if (!TryFindAnyValidMove(out int x1, out int y1, out int x2, out int y2))
            yield return StartCoroutine(ReshuffleIfNoMoves());
        else
            yield return StartCoroutine(SwapItems(grid[x1, y1], grid[x2, y2]));

        aiMoveCoroutine = null;
    }

    // Как WouldCreateMatch/FindHintItem, но возвращает координаты ОБЕИХ клеток свапа — игроку
    // FindHintItem показывает только "куда ткнуть", а автоигроку нужна полная пара для SwapItems.
    private bool TryFindAnyValidMove(out int x1, out int y1, out int x2, out int y2)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (x < width - 1 && WouldCreateMatch(x, y, x + 1, y))
                {
                    x1 = x; y1 = y; x2 = x + 1; y2 = y;
                    return true;
                }

                if (y < height - 1 && WouldCreateMatch(x, y, x, y + 1))
                {
                    x1 = x; y1 = y; x2 = x; y2 = y + 1;
                    return true;
                }
            }
        }

        x1 = y1 = x2 = y2 = -1;
        return false;
    }

    private void Update()
    {
        if (isBusy)
        {
            idleTimer = 0f;
            return;
        }

        idleTimer += Time.deltaTime;

        if (!hintActive && idleTimer >= hintDelay)
            ShowHint();
    }

    // Вызывается из Item.OnMouseDown() при любом клике игрока — сбрасывает таймер и прячет подсказку
    public void ResetIdleTimer()
    {
        idleTimer = 0f;
        ClearHint();
    }

    private void ShowHint()
    {
        Item hint = FindHintItem();
        if (hint == null) return;

        hintItem = hint;
        hintItem.SetHighlighted(true);
        hintActive = true;
    }

    private void ClearHint()
    {
        if (hintActive && hintItem != null)
            hintItem.SetHighlighted(false);

        hintActive = false;
        hintItem = null;
    }

    // Безопасный доступ к сетке снаружи (например, из Item во время свайпа)
    public Item GetItemAt(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return null;
        return grid[x, y];
    }

    // Ищет первую фишку, которую можно передвинуть так, чтобы собрать 3+ в ряд одного цвета
    private Item FindHintItem()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (x < width - 1 && WouldCreateMatch(x, y, x + 1, y))
                    return grid[x, y];

                if (y < height - 1 && WouldCreateMatch(x, y, x, y + 1))
                    return grid[x, y];
            }
        }

        return null;
    }

public IEnumerator ExecuteConvertAndDestroySkill(int convertCount)
{
    isBusy = true;

    // Собираем все не-красные фишки на поле
    List<Item> nonRed = new List<Item>();
    for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            if (grid[x, y] != null && grid[x, y].type != redTypeIndex)
                nonRed.Add(grid[x, y]);

    // Перемешиваем список (Fisher-Yates), чтобы выбор был случайным
    for (int i = nonRed.Count - 1; i > 0; i--)
    {
        int r = Random.Range(0, i + 1);
        (nonRed[i], nonRed[r]) = (nonRed[r], nonRed[i]);
    }

    int actualCount = Mathf.Min(convertCount, nonRed.Count);

    // Конвертируем выбранные фишки в красные (пересоздаём на том же месте)
    for (int i = 0; i < actualCount; i++)
    {
        Item old = nonRed[i];
        int x = old.x, y = old.y;

        Destroy(old.gameObject);
        SpawnItem(x, y, redTypeIndex);

        StartCoroutine(PopInAnimation(grid[x, y].transform));
    }

    yield return new WaitForSeconds(0.35f);

    // Собираем все красные фишки (и старые, и только что конвертированные)
    List<Item> allRed = new List<Item>();
    for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            if (grid[x, y] != null && grid[x, y].type == redTypeIndex)
                allRed.Add(grid[x, y]);

    if (allRed.Count > 0)
    {
        turnMatchedTypes.Clear();
        yield return StartCoroutine(ProcessMatches(allRed, isSkillDestroy: true));
    }
    else
    {
        isBusy = false;
    }
}

private IEnumerator PopInAnimation(Transform t)
{
    Vector3 targetScale = t.localScale;
    t.localScale = Vector3.zero;

    float duration = 0.25f;
    float elapsed = 0f;

    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        float p = elapsed / duration;
        // небольшой "перескок" через 1 для приятного pop-эффекта
        float scaleMod = Mathf.Sin(p * Mathf.PI * 0.5f) * 1.1f;
        t.localScale = targetScale * Mathf.Min(scaleMod, 1f);
        yield return null;
    }

    t.localScale = targetScale;
}

    private Dictionary<int, int> turnMatchedTypes = new Dictionary<int, int>();

    private Item[,] grid;

    [Header("Перегородка под фишками (прячет их при перевороте доски)")]
    public float occluderYOffset = -0.05f; // чуть ниже плоскости фишек (Y=0)
    public float occluderMargin = 0f;      // запас по краям сверх размера сетки
    public Color occluderColor = new Color(0.15f, 0.15f, 0.15f);

    private void Start()
    {
        Debug.Log("Start вызван на " + gameObject.name);
        grid = new Item[width, height];
        CreateChipOccluder();
        GenerateBoard();
        StartCoroutine(InitialDeadlockCheck());
    }

    // Сплошная непрозрачная панель под фишками — часть той же риг-иерархии (дочерний объект GridManager),
    // поэтому вращается вместе с доской и фишками при перевороте и всегда прячет их снизу,
    // независимо от точности пивота самого переворота.
    private void CreateChipOccluder()
    {
        GameObject occluder = GameObject.CreatePrimitive(PrimitiveType.Cube);
        occluder.name = "ChipOccluder";
        Destroy(occluder.GetComponent<Collider>());

        occluder.transform.SetParent(transform, false);

        float centerX = (width - 1) * cellSize / 2f;
        float centerZ = (height - 1) * cellSize / 2f;
        occluder.transform.position = new Vector3(centerX, occluderYOffset, centerZ);
        occluder.transform.rotation = Quaternion.identity;
        occluder.transform.localScale = new Vector3(
            width * cellSize + occluderMargin,
            0.02f,
            height * cellSize + occluderMargin);

        Renderer rend = occluder.GetComponent<Renderer>();
        rend.material = new Material(rend.material);
        rend.material.color = occluderColor;
    }

    private IEnumerator InitialDeadlockCheck()
    {
        isBusy = true;
        yield return StartCoroutine(ReshuffleIfNoMoves());

        // Спавним вредные фишки только после того, как доска гарантированно устаканилась —
        // ReshuffleBoard() уничтожает и пересоздаёт ВСЕ фишки, включая уже размеченные вредными
        SpawnHarmfulTilesFromEnemy();

        isBusy = false;
    }

    // Разово в начале боя размечает случайные существующие фишки как вредные согласно currentEnemy.harmfulTileSpawns
    private void SpawnHarmfulTilesFromEnemy()
    {
        if (battleManager == null || battleManager.currentEnemy == null) return;

        HarmfulTileSpawnRule[] rules = battleManager.currentEnemy.harmfulTileSpawns;
        if (rules == null) return;

        List<Item> available = new List<Item>();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (grid[x, y] != null) available.Add(grid[x, y]);

        foreach (var rule in rules)
        {
            for (int i = 0; i < rule.count && available.Count > 0; i++)
            {
                int index = Random.Range(0, available.Count);
                Item item = available[index];
                available.RemoveAt(index);

                switch (rule.type)
                {
                    case HarmfulTileType.Ice: item.MarkAsIceHarmful(rule.value); break;
                    case HarmfulTileType.Spike: item.MarkAsSpikeHarmful(rule.value); break;
                    case HarmfulTileType.Trap: item.MarkAsTrapHarmful(rule.value); break;
                    case HarmfulTileType.Anchor: item.MarkAsAnchorHarmful(rule.value); break;
                    case HarmfulTileType.Cursed: item.MarkAsCursedHarmful(rule.value); break;
                    case HarmfulTileType.BloodMark: item.MarkAsBloodMarkHarmful(rule.value); break;
                    case HarmfulTileType.Rotten: item.MarkAsRottenHarmful(); break;
                    case HarmfulTileType.Chaos: item.MarkAsChaosHarmful(); break;
                }
            }
        }
    }

    // Генерация поля без начальных совпадений "3 в ряд"
    private void GenerateBoard()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int randomType = GetValidRandomType(x, y);
                SpawnItem(x, y, randomType);
            }
        }
    }

    private void SpawnItem(int x, int y, int type)
{
    Vector3 worldPos = GetWorldPosition(x, y);
    GameObject prefab = itemPrefabs[type];
    GameObject obj = Instantiate(prefab, worldPos, prefab.transform.rotation, transform);

    Item item = obj.GetComponent<Item>();
    item.x = x;
    item.y = y;
    item.type = type;

    grid[x, y] = item;
}

    // Расчет 3D-координаты на основе (X, Y) сетки
    public Vector3 GetWorldPosition(int x, int y)
    {
        // Поле строится в плоскости XZ (Y = 0)
        return new Vector3(x * cellSize, 0f, y * cellSize);
    }

    // Исключаем образование "3 в ряд" при старте
    private int GetValidRandomType(int x, int y)
    {
        List<int> validTypes = new List<int>();
        for (int i = 0; i < itemPrefabs.Length; i++) validTypes.Add(i);

        // Проверка по горизонтали
        if (x >= 2)
        {
            if (grid[x - 1, y].type == grid[x - 2, y].type)
            {
                validTypes.Remove(grid[x - 1, y].type);
            }
        }

        // Проверка по вертикали
        if (y >= 2)
        {
            if (grid[x, y - 1].type == grid[x, y - 2].type)
            {
                validTypes.Remove(grid[x, y - 1].type);
            }
        }

        return validTypes[Random.Range(0, validTypes.Count)];
    }

// Обмен двух соседних фишек местами
    public IEnumerator SwapItems(Item a, Item b)
{
    if (a.isFrozen || b.isFrozen)
        yield break; // замороженную фишку нельзя двигать (FreezeRandomRowOrColumn)

    if (a.harmfulType == HarmfulTileType.Anchor || b.harmfulType == HarmfulTileType.Anchor)
        yield break; // якорь нельзя сдвинуть обычным свайпом

    isBusy = true;

    int aX = a.x, aY = a.y;
    int bX = b.x, bY = b.y;

    grid[aX, aY] = b;
    grid[bX, bY] = a;

    a.x = bX; a.y = bY;
    b.x = aX; b.y = aY;

    a.MoveTo(GetWorldPosition(a.x, a.y));
    b.MoveTo(GetWorldPosition(b.x, b.y));

    yield return new WaitForSeconds(0.25f);

    List<Item> matches = FindMatches();

    if (matches.Count > 0)
    {
        turnMatchedTypes.Clear(); // старт учёта нового хода
        yield return StartCoroutine(ProcessMatches(matches, preferredSurvivors: new HashSet<Item> { a, b }));
    }
    else
    {
        grid[aX, aY] = a;
        grid[bX, bY] = b;

        a.x = aX; a.y = aY;
        b.x = bX; b.y = bY;

        a.MoveTo(GetWorldPosition(a.x, a.y));
        b.MoveTo(GetWorldPosition(b.x, b.y));

        yield return new WaitForSeconds(0.25f);
        isBusy = false;
    }
}

    // Джокер (isJoker) матчится с фишкой любого цвета — ConvertCellToJoker (Эльфы).
    // Якорь никогда не участвует в обычных матчах — "нельзя удалить обычным матчем" (см. HarmfulTileSpawnRule).
    private bool TypesMatch(Item a, Item b)
    {
        if (a.harmfulType == HarmfulTileType.Anchor || b.harmfulType == HarmfulTileType.Anchor)
            return false;

        return a.isJoker || b.isJoker || a.type == b.type;
    }

    // Поиск всех фишек, собранных по 3 и более в ряд
    public List<Item> FindMatches()
    {
        HashSet<Item> matchedItems = new HashSet<Item>();

        // Проверка горизонталей
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width - 2; x++)
            {
                Item a = grid[x, y];
                Item b = grid[x + 1, y];
                Item c = grid[x + 2, y];

                if (a != null && b != null && c != null)
                {
                    if (TypesMatch(a, b) && TypesMatch(b, c) && TypesMatch(a, c))
                    {
                        matchedItems.Add(a);
                        matchedItems.Add(b);
                        matchedItems.Add(c);
                    }
                }
            }
        }

        // Проверка вертикалей
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height - 2; y++)
            {
                Item a = grid[x, y];
                Item b = grid[x, y + 1];
                Item c = grid[x, y + 2];

                if (a != null && b != null && c != null)
                {
                    if (TypesMatch(a, b) && TypesMatch(b, c) && TypesMatch(a, c))
                    {
                        matchedItems.Add(a);
                        matchedItems.Add(b);
                        matchedItems.Add(c);
                    }
                }
            }
        }

        return new List<Item>(matchedItems);
    }

public IEnumerator PlayDestroyAnimation()
{
    float duration = 0.25f;
    float t = 0f;
    Vector3 startScale = transform.localScale;

    while (t < duration)
    {
        t += Time.deltaTime;
        float p = t / duration;
        // немного "распухает", потом стягивается до нуля — приятный pop-эффект
        float scaleMod = Mathf.Lerp(1f, 0f, p) + Mathf.Sin(p * Mathf.PI) * 0.3f;
        transform.localScale = startScale * Mathf.Max(scaleMod, 0f);
        transform.Rotate(Vector3.up, 360f * Time.deltaTime); // лёгкое вращение для эффекта
        yield return null;
    }

    transform.localScale = Vector3.zero;
}

    // Обработка уничтожения и падения
    // isSkillDestroy: true, когда фишки убираются скиллом (не обычным матчем игрока) — в этом случае
    // Trap не наносит урон герою, ведь наказание за ловушку привязано именно к попаданию в матч, а не к её снятию.
    private IEnumerator ProcessMatches(List<Item> matches, bool isSkillDestroy = false, HashSet<Item> preferredSurvivors = null)
{
    List<Coroutine> animations = new List<Coroutine>();

    // --- Спец-фишки (4/5 в ряд) ---
    // Раны 4+ по прямой линии внутри matches: одна фишка рана выживает и становится спец-фишкой (не
    // уничтожается в этом проходе), остальные из этого рана уничтожаются как обычно. Если матч пришёл
    // от свайпа игрока (preferredSurvivors = {a, b} из SwapItems) — выживает именно та из двух свайпнутых
    // фишек, что попала в этот ран: игрок ожидает, что фишка, которую он передвинул, и станет особенной,
    // а не "геометрический центр" рана (у чётного рана — 4 фишки — центра как такового и не существует,
    // items.Count/2 всегда смещён к одному концу). Для матчей без свайпа (каскады, скиллы) — старый
    // индексный фолбэк, там нет "той самой" фишки, на которую можно было бы ориентироваться.
    List<MatchRun> runs = FindStraightRuns(matches);
    HashSet<Item> survivors = new HashSet<Item>();
    foreach (var run in runs)
    {
        // Никогда не выбираем УЖЕ спец-фишку выживающей — иначе MarkAsSpecial перезапишет её тип поверх
        // старого, а ExpandSpecialEffects ниже пропустит её (раз она теперь в survivors), так и не дав
        // сработать эффекту, который она несла ДО этого матча (баг: "фишка Line была на поле — эффект
        // не сработал, просто стала новой Line"). Предпочитаем свайпнутую фишку, если она НЕ спец, иначе
        // любую не-спец фишку рана; если совсем все фишки рана уже спец (редкий крайний случай) —
        // старый индексный фолбэк, переигрывать всё равно что-то придётся.
        Item survivor = null;
        if (preferredSurvivors != null)
            survivor = run.items.FirstOrDefault(i => preferredSurvivors.Contains(i) && i.specialType == Item.SpecialType.None);
        if (survivor == null)
            survivor = run.items.FirstOrDefault(i => i.specialType == Item.SpecialType.None);
        if (survivor == null)
            survivor = run.items[run.items.Count / 2];

        if (survivors.Contains(survivor)) continue; // гориз- и верт-ран пересеклись в одной клетке — не переразмечаем дважды

        survivors.Add(survivor);
        Item.SpecialType newType = run.items.Count >= 5
            ? Item.SpecialType.ColorBomb
            : (run.isHorizontal ? Item.SpecialType.LineClearRow : Item.SpecialType.LineClearColumn);

        survivor.MarkAsSpecial(newType);
    }

    // Уже существующие спец-фишки, попавшие в этот же матч, — раскрываем в цепочку целей их эффекта
    // (может зацепить ещё одну спец-фишку — цепная реакция обрабатывается тут же, за один проход).
    // survivors передаём отдельно — только что созданная этим же матчем спец-фишка не должна СРАБАТЫВАТЬ
    // как источник эффекта (сносить весь ряд/столбец/цвет) в момент своего рождения, только при
    // реальной активации отдельным матчем позже. ExceptWith ниже защищает её от уничтожения, но не от
    // того, чтобы она сама стала триггером — это и была причина "эффект срабатывает при превращении".
    HashSet<Item> toDestroySet = ExpandSpecialEffects(matches, survivors);
    toDestroySet.ExceptWith(survivors); // новые спец-фишки этого хода не должны уничтожаться сами собой

    // List, не HashSet — TickAnchorsAdjacentToMatches ДОБАВЛЯЕТ в переданный список снятые якоря (see ниже),
    // и этот же список дальше используется для цикла уничтожения.
    List<Item> toDestroy = new List<Item>(toDestroySet);

    ThawAdjacentIce(toDestroy);       // сосед матчится рядом со льдом — лёд тает досрочно, сам не уничтожаясь
    TickAnchorsAdjacentToMatches(toDestroy); // якорь не входит в матч сам, но считает матчи по соседству

    // Хаос-клетка: срабатывает, когда сама попадает в этот снос — меняет цвет всех живых соседей,
    // которые сами НЕ входят в этот же снос (иначе бессмысленно красить то, что и так сейчас исчезнет).
    foreach (var chaosItem in toDestroy.Where(i => i.harmfulType == HarmfulTileType.Chaos).ToList())
    {
        foreach (var neighbor in GetNeighborItems(chaosItem.x, chaosItem.y))
        {
            if (neighbor == null || toDestroy.Contains(neighbor) || neighbor.harmfulType == HarmfulTileType.Anchor)
                continue;

            int nx = neighbor.x, ny = neighbor.y;
            int newType = Random.Range(0, itemPrefabs.Length);
            Destroy(neighbor.gameObject);
            SpawnItem(nx, ny, newType);
        }
    }

    foreach (var item in toDestroy)
    {
        if (item.harmfulType == HarmfulTileType.Trap && !isSkillDestroy)
        {
            battleManager?.DamageRandomHero(item.harmfulValue); // ловушка: урон герою вместо пользы от матча
        }
        else
        {
            if (!turnMatchedTypes.ContainsKey(item.type))
                turnMatchedTypes[item.type] = 0;
            turnMatchedTypes[item.type]++;
        }

        grid[item.x, item.y] = null;
        animations.Add(StartCoroutine(item.PlayDestroyAnimation()));
    }

    foreach (var anim in animations)
        yield return anim;

    foreach (var item in toDestroy)
        Destroy(item.gameObject);

    yield return StartCoroutine(CollapseGrid());
}

    private struct MatchRun
    {
        public List<Item> items;
        public bool isHorizontal; // true = вдоль ряда (общий Y, разный X) — снесёт ROW; false — снесёт COLUMN
    }

    // Находит внутри matches максимальные прямые последовательности (4+) по текущему состоянию grid[,] —
    // вызывать ДО того, как matches начнут обнуляться в grid (см. начало ProcessMatches).
    private List<MatchRun> FindStraightRuns(List<Item> matches)
    {
        var matchedSet = new HashSet<Item>(matches);
        var runs = new List<MatchRun>();

        for (int y = 0; y < height; y++)
        {
            int x = 0;
            while (x < width)
            {
                Item start = grid[x, y];
                if (start == null || !matchedSet.Contains(start)) { x++; continue; }

                var run = new List<Item> { start };
                int nx = x + 1;
                while (nx < width && grid[nx, y] != null && matchedSet.Contains(grid[nx, y]) && TypesMatch(grid[nx - 1, y], grid[nx, y]))
                {
                    run.Add(grid[nx, y]);
                    nx++;
                }

                if (run.Count >= 4)
                    runs.Add(new MatchRun { items = run, isHorizontal = true });

                x = nx;
            }
        }

        for (int x = 0; x < width; x++)
        {
            int y = 0;
            while (y < height)
            {
                Item start = grid[x, y];
                if (start == null || !matchedSet.Contains(start)) { y++; continue; }

                var run = new List<Item> { start };
                int ny = y + 1;
                while (ny < height && grid[x, ny] != null && matchedSet.Contains(grid[x, ny]) && TypesMatch(grid[x, ny - 1], grid[x, ny]))
                {
                    run.Add(grid[x, ny]);
                    ny++;
                }

                if (run.Count >= 4)
                    runs.Add(new MatchRun { items = run, isHorizontal = false });

                y = ny;
            }
        }

        return runs;
    }

    // Раскрывает набор фишек в реальный список на уничтожение: любая спец-фишка внутри matches добавляет
    // всю свою линию/цвет в очередь, и если туда попадает ещё одна спец-фишка — цепная реакция продолжается.
    private HashSet<Item> ExpandSpecialEffects(List<Item> matches, HashSet<Item> newlyCreatedSurvivors)
    {
        var toDestroy = new HashSet<Item>(matches);
        var queue = new Queue<Item>(matches);

        while (queue.Count > 0)
        {
            Item item = queue.Dequeue();
            if (item.specialType == Item.SpecialType.None) continue;
            if (newlyCreatedSurvivors.Contains(item)) continue; // родилась этим же матчем — не триггерит эффект на себя

            foreach (var extra in GetSpecialEffectTargets(item))
            {
                if (extra != null && toDestroy.Add(extra))
                    queue.Enqueue(extra);
            }
        }

        return toDestroy;
    }

    private List<Item> GetSpecialEffectTargets(Item special)
    {
        var result = new List<Item>();

        switch (special.specialType)
        {
            case Item.SpecialType.LineClearRow:
                for (int gx = 0; gx < width; gx++)
                {
                    Item it = grid[gx, special.y];
                    if (it != null && it != special) result.Add(it);
                }
                break;

            case Item.SpecialType.LineClearColumn:
                for (int gy = 0; gy < height; gy++)
                {
                    Item it = grid[special.x, gy];
                    if (it != null && it != special) result.Add(it);
                }
                break;

            case Item.SpecialType.ColorBomb:
                for (int gx = 0; gx < width; gx++)
                    for (int gy = 0; gy < height; gy++)
                    {
                        Item it = grid[gx, gy];
                        if (it != null && it != special && it.type == special.type) result.Add(it);
                    }
                break;
        }

        return result;
    }

    // Лёд, оказавшийся рядом с местом матча (но не входивший в него сам), тает раньше срока
    private void ThawAdjacentIce(List<Item> matches)
    {
        foreach (var matched in matches)
        {
            foreach (var neighbor in GetNeighborItems(matched.x, matched.y))
            {
                if (neighbor != null && neighbor.harmfulType == HarmfulTileType.Ice && !matches.Contains(neighbor))
                    neighbor.Unfreeze();
            }
        }
    }

    // Якорь исключён из TypesMatch, поэтому сам никогда не попадает в matches — считаем матчи по соседству
    // с ним отдельно; при достижении harmfulValue снимается вместе с текущим матчем.
    private void TickAnchorsAdjacentToMatches(List<Item> matches)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Item item = grid[x, y];
                if (item == null || item.harmfulType != HarmfulTileType.Anchor) continue;

                bool adjacentMatch = false;
                foreach (var neighbor in GetNeighborItems(x, y))
                {
                    if (neighbor != null && matches.Contains(neighbor)) { adjacentMatch = true; break; }
                }

                if (!adjacentMatch) continue;

                item.anchorMatchStreak++;
                if (item.anchorMatchStreak >= item.harmfulValue)
                {
                    item.ClearHarmful();
                    matches.Add(item);
                }
            }
        }
    }

    private IEnumerable<Item> GetNeighborItems(int x, int y)
    {
        yield return GetItemAt(x + 1, y);
        yield return GetItemAt(x - 1, y);
        yield return GetItemAt(x, y + 1);
        yield return GetItemAt(x, y - 1);
    }

    // Логика падения фишек вниз и генерация новых сверху
    private IEnumerator CollapseGrid()
{
    for (int x = 0; x < width; x++)
    {
        for (int y = 0; y < height; y++)
        {
            if (grid[x, y] == null)
            {
                for (int aboveY = y + 1; aboveY < height; aboveY++)
                {
                    if (grid[x, aboveY] != null)
                    {
                        grid[x, y] = grid[x, aboveY];
                        grid[x, y].x = x;
                        grid[x, y].y = y;
                        grid[x, aboveY] = null;

                        grid[x, y].MoveTo(GetWorldPosition(x, y));
                        break;
                    }
                }

                if (grid[x, y] == null)
                {
                    int randomType = Random.Range(0, itemPrefabs.Length);
                    SpawnItem(x, y, randomType);

                    grid[x, y].transform.position = GetWorldPosition(x, height);
                    grid[x, y].MoveTo(GetWorldPosition(x, y));
                }
            }
        }
    }

    yield return new WaitForSeconds(0.25f);

    List<Item> newMatches = FindMatches();
    if (newMatches.Count > 0)
    {
        yield return StartCoroutine(ProcessMatches(newMatches));
    }
    else
    {
        // Каскады завершились — это реальный конец хода игрока
        // Не гейтим на turnMatchedTypes.Count>0: матч из одних Trap-фишек не пишет в этот словарь
        // (уходит в прямой урон герою), но ход всё равно должен резолвиться как реальный.
        if (battleManager != null)
        {
            TickFrozenTiles();
            TickHarmfulTiles();
            battleManager.ResolvePlayerTurn(turnMatchedTypes);
        }

        // Проверяем, остался ли хоть один возможный ход — если нет, перегенерируем поле
        yield return StartCoroutine(ReshuffleIfNoMoves());

        isBusy = false;
    }
}

// Есть ли хотя бы один соседний обмен, который даст матч 3+
private bool HasPossibleMoves()
{
    for (int x = 0; x < width; x++)
    {
        for (int y = 0; y < height; y++)
        {
            if (x < width - 1 && WouldCreateMatch(x, y, x + 1, y))
                return true;

            if (y < height - 1 && WouldCreateMatch(x, y, x, y + 1))
                return true;
        }
    }

    return false;
}

// Временно меняет местами в логической сетке (без анимации), проверяет матч, возвращает как было
private bool WouldCreateMatch(int x1, int y1, int x2, int y2)
{
    Item a = grid[x1, y1];
    Item b = grid[x2, y2];

    // Та же блокировка, что и в SwapItems — иначе ИИ/подсказка могут выбрать своп с замороженной
    // (Ice) или заякоренной (Anchor) фишкой: WouldCreateMatch скажет "да, матч будет", а реальный
    // SwapItems тихо оборвётся (yield break) до самого свопа — ход молча пропадёт, ИИ "зависнет".
    if (a.isFrozen || b.isFrozen) return false;
    if (a.harmfulType == HarmfulTileType.Anchor || b.harmfulType == HarmfulTileType.Anchor) return false;

    grid[x1, y1] = b;
    grid[x2, y2] = a;

    bool hasMatch = FindMatches().Count > 0;

    grid[x1, y1] = a;
    grid[x2, y2] = b;

    return hasMatch;
}

// Если на поле нет ни одного возможного хода — перегенерируем его (без начисления хода игроку)
private IEnumerator ReshuffleIfNoMoves()
{
    int safetyCounter = 0;

    while (!HasPossibleMoves() && safetyCounter < 20)
    {
        Debug.Log("Нет возможных ходов — перегенерирую поле.");
        yield return StartCoroutine(ReshuffleBoard());
        safetyCounter++;
    }
}

private IEnumerator ReshuffleBoard()
{
    for (int x = 0; x < width; x++)
    {
        for (int y = 0; y < height; y++)
        {
            if (grid[x, y] != null)
            {
                Destroy(grid[x, y].gameObject);
                grid[x, y] = null;
            }
        }
    }

    yield return null; // дать Destroy() отработать перед новым спавном

    for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            int randomType = GetValidRandomType(x, y);
            SpawnItem(x, y, randomType);
        }

    yield return new WaitForSeconds(0.25f);
}
private void OnDrawGizmos()
{
    Gizmos.color = Color.yellow;

    // Рисуем рамку по границе всей сетки
    Vector3 bottomLeft = GetWorldPosition(0, 0) - new Vector3(cellSize / 2f, 0, cellSize / 2f);
    Vector3 size = new Vector3(width * cellSize, 0.1f, height * cellSize);
    Vector3 center = bottomLeft + new Vector3(size.x / 2f, 0, size.z / 2f);

    Gizmos.DrawWireCube(center, size);

    // Дополнительно — сетка по каждой ячейке
    Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
    for (int x = 0; x < width; x++)
    {
        for (int y = 0; y < height; y++)
        {
            Vector3 pos = GetWorldPosition(x, y);
            Gizmos.DrawWireCube(pos, new Vector3(cellSize * 0.9f, 0.05f, cellSize * 0.9f));
        }
    }
}

            // Уничтожает все фишки в диапазоне рядов [rowStart, rowEnd] включительно (по оси Y сетки)
        public IEnumerator ExecuteDestroyRowsSkill(int rowStart, int rowEnd)
        {
            isBusy = true;

            List<Item> toDestroy = new List<Item>();

            int clampedStart = Mathf.Clamp(rowStart, 0, height - 1);
            int clampedEnd = Mathf.Clamp(rowEnd, 0, height - 1);

            for (int y = clampedStart; y <= clampedEnd; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (grid[x, y] != null)
                        toDestroy.Add(grid[x, y]);
                }
            }

            if (toDestroy.Count > 0)
            {
                turnMatchedTypes.Clear();
                yield return StartCoroutine(ProcessMatches(toDestroy, isSkillDestroy: true));
            }
            else
            {
                isBusy = false;
            }
}

// Уничтожает N случайных фишек на поле (Эльфы, T1)
public IEnumerator ExecuteDestroyRandomGemsSkill(int count)
{
    isBusy = true;

    List<Item> allItems = new List<Item>();
    for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            if (grid[x, y] != null)
                allItems.Add(grid[x, y]);

    for (int i = allItems.Count - 1; i > 0; i--)
    {
        int r = Random.Range(0, i + 1);
        (allItems[i], allItems[r]) = (allItems[r], allItems[i]);
    }

    int actualCount = Mathf.Min(count, allItems.Count);
    List<Item> toDestroy = allItems.GetRange(0, actualCount);

    if (toDestroy.Count > 0)
    {
        turnMatchedTypes.Clear();
        yield return StartCoroutine(ProcessMatches(toDestroy, isSkillDestroy: true));
    }
    else
    {
        isBusy = false;
    }
}

// Уничтожает первую найденную "вредную" фишку (Эльфы, T2) — это и есть тот самый "спец-скилл",
// который по дизайну умеет снять Anchor без N матчей подряд.
public IEnumerator ExecuteDestroyHarmfulTileSkill()
{
    isBusy = true;

    Item harmful = null;
    for (int x = 0; x < width && harmful == null; x++)
        for (int y = 0; y < height && harmful == null; y++)
            if (grid[x, y] != null && grid[x, y].harmfulType != HarmfulTileType.None)
                harmful = grid[x, y];

    if (harmful != null)
    {
        turnMatchedTypes.Clear();
        yield return StartCoroutine(ProcessMatches(new List<Item> { harmful }, isSkillDestroy: true));
    }
    else
    {
        isBusy = false;
    }
}

// Превращает случайную фишку в джокер (Эльфы, T3) — не уничтожает ничего и не вызывает каскад
public void ConvertRandomCellToJoker()
{
    List<Item> candidates = new List<Item>();
    for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            if (grid[x, y] != null && !grid[x, y].isJoker)
                candidates.Add(grid[x, y]);

    if (candidates.Count == 0) return;

    candidates[Random.Range(0, candidates.Count)].MarkAsJoker();
}

// Полный шафл доски с гарантированным крупным (4 в ряд) матчем в пользу игрока (Эльфы, T4)
public IEnumerator ExecuteFavorableReshuffleSkill()
{
    isBusy = true;

    for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            if (grid[x, y] != null)
            {
                Destroy(grid[x, y].gameObject);
                grid[x, y] = null;
            }

    yield return null;

    for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            int randomType = GetValidRandomType(x, y);
            SpawnItem(x, y, randomType);
        }

    yield return new WaitForSeconds(0.25f);

    // Форсируем гарантированный матч 4-в-ряд в случайной строке
    int guaranteedType = Random.Range(0, itemPrefabs.Length);
    int rowY = Random.Range(0, height);
    int startX = Random.Range(0, width - 3);

    List<Item> guaranteedMatch = new List<Item>();
    for (int i = 0; i < 4; i++)
    {
        int gx = startX + i;
        Destroy(grid[gx, rowY].gameObject);
        SpawnItem(gx, rowY, guaranteedType);
        guaranteedMatch.Add(grid[gx, rowY]);
    }

    yield return new WaitForSeconds(0.1f);

    turnMatchedTypes.Clear();
    yield return StartCoroutine(ProcessMatches(guaranteedMatch, isSkillDestroy: true));
}

// Замораживает случайную строку либо столбец на N ходов (негативный эффект гемблинг-колеса)
public void FreezeRandomRowOrColumn(int turns)
{
    List<Item> toFreeze = new List<Item>();

    if (Random.value < 0.5f)
    {
        int y = Random.Range(0, height);
        for (int x = 0; x < width; x++)
            if (grid[x, y] != null) toFreeze.Add(grid[x, y]);
    }
    else
    {
        int x = Random.Range(0, width);
        for (int y = 0; y < height; y++)
            if (grid[x, y] != null) toFreeze.Add(grid[x, y]);
    }

    foreach (var item in toFreeze)
        item.Freeze(turns);
}

// Уменьшает счётчик заморозки на замороженных фишках в конце каждого реального хода игрока
private void TickFrozenTiles()
{
    for (int x = 0; x < width; x++)
    {
        for (int y = 0; y < height; y++)
        {
            Item item = grid[x, y];
            if (item == null || !item.isFrozen) continue;

            item.frozenTurnsRemaining--;
            if (item.frozenTurnsRemaining <= 0)
                item.Unfreeze();
        }
    }
}

// Шип наносит фиксированный урон герою каждый реальный ход, пока фишка остаётся на поле.
// Cursed/Rotten заражают соседей — снимок берётся ДО заражений этого тика, чтобы свежезаражённая
// соседняя фишка не заражала дальше в тот же ход (иначе поле выгорело бы за пару ходов).
private void TickHarmfulTiles()
{
    List<Item> snapshot = new List<Item>();
    for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            if (grid[x, y] != null) snapshot.Add(grid[x, y]);

    foreach (var item in snapshot)
    {
        switch (item.harmfulType)
        {
            case HarmfulTileType.Spike:
                battleManager?.DamageRandomHero(item.harmfulValue);
                break;

            case HarmfulTileType.Cursed:
                item.cursedTurnsAlive++;
                if (item.cursedTurnsAlive >= item.harmfulValue)
                {
                    item.cursedTurnsAlive = 0;
                    int spreadValue = item.harmfulValue;
                    SpreadHarmfulToRandomNeighbor(item, t => t.MarkAsCursedHarmful(spreadValue));
                }
                break;

            case HarmfulTileType.Rotten:
                SpreadHarmfulToRandomNeighbor(item, t => t.MarkAsRottenHarmful());
                break;
        }
    }
}

// Заражает ОДНОГО случайного живого соседа (не Anchor, не уже заражённого тем же типом) — общий
// хелпер для Cursed/Rotten. Ничего не делает, если подходящих соседей нет (например, все уже заражены).
private void SpreadHarmfulToRandomNeighbor(Item source, System.Action<Item> markAction)
{
    var candidates = GetNeighborItems(source.x, source.y)
        .Where(n => n != null && n.harmfulType != source.harmfulType && n.harmfulType != HarmfulTileType.Anchor)
        .ToList();

    if (candidates.Count == 0) return;

    markAction(candidates[Random.Range(0, candidates.Count)]);
}

// Ставит вредную клетку заданного типа на случайную подходящую клетку поля (не Anchor, не уже такого же
// типа) — используется скиллом CastTrap в Boss Training, но не привязан к нему специально.
public void CastHarmfulTileRandom(HarmfulTileType type, int value)
{
    List<Item> candidates = new List<Item>();
    for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            if (grid[x, y] != null && grid[x, y].harmfulType != type && grid[x, y].harmfulType != HarmfulTileType.Anchor)
                candidates.Add(grid[x, y]);

    if (candidates.Count == 0) return;

    Item target = candidates[Random.Range(0, candidates.Count)];
    switch (type)
    {
        case HarmfulTileType.Ice: target.MarkAsIceHarmful(value); break;
        case HarmfulTileType.Spike: target.MarkAsSpikeHarmful(value); break;
        case HarmfulTileType.Trap: target.MarkAsTrapHarmful(value); break;
        case HarmfulTileType.Anchor: target.MarkAsAnchorHarmful(value); break;
        case HarmfulTileType.Cursed: target.MarkAsCursedHarmful(value); break;
        case HarmfulTileType.BloodMark: target.MarkAsBloodMarkHarmful(value); break;
        case HarmfulTileType.Rotten: target.MarkAsRottenHarmful(); break;
        case HarmfulTileType.Chaos: target.MarkAsChaosHarmful(); break;
    }
}

// Пока на поле есть хоть одна BloodMark-фишка — суммарный множитель урона игроку (см.
// BattleManager.ApplyDamageToHero). 1f, если ни одной такой фишки нет.
public float GetBloodMarkDamageMultiplier()
{
    float mult = 1f;
    if (grid == null) return mult;

    for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            Item item = grid[x, y];
            if (item != null && item.harmfulType == HarmfulTileType.BloodMark)
                mult += item.harmfulValue / 100f;
        }

    return mult;
}

}
