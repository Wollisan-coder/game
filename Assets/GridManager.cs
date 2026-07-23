using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Настройки сетки")]
    public int width = 7;
    public int height = 7;
    public float cellSize = 1.0f; // Расстояние между центрами фишек

    [Header("Префабы")]
    public GameObject[] itemPrefabs; // Массив 3D-префабов разных типов

    [Header("Бій")]
    public BattleManager battleManager;

    [Header("Тип 'Red' в масиві itemPrefabs")]
public int redTypeIndex = 0; // перевір, що Element 0 = RedGem у твоєму масиві

public IEnumerator ExecuteConvertAndDestroySkill(int convertCount)
{
    // Збираємо всі не-червоні фішки на полі
    List<Item> nonRed = new List<Item>();
    for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            if (grid[x, y] != null && grid[x, y].type != redTypeIndex)
                nonRed.Add(grid[x, y]);

    // Перемішуємо список (Fisher-Yates), щоб вибір був випадковим
    for (int i = nonRed.Count - 1; i > 0; i--)
    {
        int r = Random.Range(0, i + 1);
        (nonRed[i], nonRed[r]) = (nonRed[r], nonRed[i]);
    }

    int actualCount = Mathf.Min(convertCount, nonRed.Count);

    // Конвертуємо обрані фішки в червоні (пересоздаём на тому ж місці)
    for (int i = 0; i < actualCount; i++)
    {
        Item old = nonRed[i];
        int x = old.x, y = old.y;

        Destroy(old.gameObject);
        SpawnItem(x, y, redTypeIndex);

        StartCoroutine(PopInAnimation(grid[x, y].transform));
    }

    yield return new WaitForSeconds(0.35f);

    // Збираємо всі червоні фішки (і старі, і щойно конвертовані)
    List<Item> allRed = new List<Item>();
    for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            if (grid[x, y] != null && grid[x, y].type == redTypeIndex)
                allRed.Add(grid[x, y]);

    if (allRed.Count > 0)
    {
        turnMatchedTypes.Clear();
        yield return StartCoroutine(ProcessMatches(allRed));
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
        // невеликий "перескок" через 1 для приємного pop-ефекту
        float scaleMod = Mathf.Sin(p * Mathf.PI * 0.5f) * 1.1f;
        t.localScale = targetScale * Mathf.Min(scaleMod, 1f);
        yield return null;
    }

    t.localScale = targetScale;
}

    private Dictionary<int, int> turnMatchedTypes = new Dictionary<int, int>();

    private Item[,] grid;

    private void Start()
    {
        Debug.Log("Start викликано на " + gameObject.name);
        grid = new Item[width, height];
        GenerateBoard();
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
        turnMatchedTypes.Clear(); // старт обліку нового ходу
        yield return StartCoroutine(ProcessMatches(matches));
    }
    else
    {
        grid[aX, aY] = a;
        grid[bX, bY] = b;

        a.x = aX; a.y = aY;
        b.x = bX; b.y = bY;

        a.MoveTo(GetWorldPosition(a.x, a.y));
        b.MoveTo(GetWorldPosition(b.x, b.y));
    }
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
                    if (a.type == b.type && b.type == c.type)
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
                    if (a.type == b.type && b.type == c.type)
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
        // трохи "розпухає", потім стискається до нуля — приємний pop-ефект
        float scaleMod = Mathf.Lerp(1f, 0f, p) + Mathf.Sin(p * Mathf.PI) * 0.3f;
        transform.localScale = startScale * Mathf.Max(scaleMod, 0f);
        transform.Rotate(Vector3.up, 360f * Time.deltaTime); // легке обертання для ефекту
        yield return null;
    }

    transform.localScale = Vector3.zero;
}

    // Обработка уничтожения и падения
    private IEnumerator ProcessMatches(List<Item> matches)
{
    List<Coroutine> animations = new List<Coroutine>();

    foreach (var item in matches)
    {
        if (!turnMatchedTypes.ContainsKey(item.type))
            turnMatchedTypes[item.type] = 0;
        turnMatchedTypes[item.type]++;

        grid[item.x, item.y] = null;
        animations.Add(StartCoroutine(item.PlayDestroyAnimation()));
    }

    foreach (var anim in animations)
        yield return anim;

    foreach (var item in matches)
        Destroy(item.gameObject);

    yield return StartCoroutine(CollapseGrid());
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
        // Каскади завершились — це реальний кінець ходу гравця
        if (battleManager != null && turnMatchedTypes.Count > 0)
        {
            battleManager.ResolvePlayerTurn(turnMatchedTypes);
        }
    }
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
}    