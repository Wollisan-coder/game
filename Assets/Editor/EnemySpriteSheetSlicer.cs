#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Нарезает боевые спрайт-листы врагов (Assets/EnemyCards/*.png) на отдельные PNG-файлы и подключает их
// к соответствующим EnemyData (idleFrames[0] + attackFrames[1..]) — общий инструмент вместо одного
// хардкодного скрипта под конкретного скелета (см. историю — SkeletonEnemySpriteSlicer, снесён, эта
// версия полностью его заменяет). НЕ использует TextureImporter.spritesheet/Multiple sprite mode — в
// проекте не установлен пакет com.unity.2d.sprite, а легаси spritesheet-сеттер в этой версии Unity 6
// молча ничего не сохраняет (проверено на практике). Вместо этого режем пиксели вручную
// (GetPixels/EncodeToPNG) на независимые PNG — по одному на кадр, каждый импортируется как обычный
// одиночный спрайт (тот же приём, что уже работает в UIAssetImportSetup.SetupSprite).
//
// У каждого спрайт-листа СВОЯ раскладка (не всегда 3x3, пустая ячейка не всегда последняя — проверено
// глазами на каждом листе), поэтому явно перечисляем список занятых ячеек (row, col) на лист, а не
// угадываем автоматически по альфе/цвету фона (у разных листов разный фон — где-то белый непрозрачный,
// где-то настоящая прозрачность).
public static class EnemySpriteSheetSlicer
{
    private class SheetConfig
    {
        public readonly string TexturePath;
        public readonly string EnemyDataPath;
        public readonly string OutputFolder;
        public readonly string FramePrefix;
        public readonly int Columns;
        public readonly int Rows;
        public readonly (int row, int col)[] Cells; // порядок = порядок кадров, [0] = Idle, остальное = Attack

        public SheetConfig(string enemyId, int columns, int rows, (int, int)[] cells)
        {
            TexturePath = $"Assets/EnemyCards/{enemyId}.png";
            EnemyDataPath = $"Assets/EnemyCards/{enemyId} Enemie.asset";
            OutputFolder = $"Assets/EnemyCards/{enemyId}_Frames";
            FramePrefix = enemyId;
            Columns = columns;
            Rows = rows;
            Cells = cells;
        }
    }

    // row/col — 0 = ВЕРХНИЙ ряд / ЛЕВЫЙ столбец, как видно глазами на картинке (не Unity-координаты низа
    // текстуры — пересчёт делает SliceOne). Добавляя нового врага — просто дописать сюда новую строку.
    //
    // ДВЕ РАЗНЫЕ РАСКЛАДКИ у 8 листов "по одному врагу на территорию" (724x1080, 2x4, все ячейки заняты):
    // - RowMajorOrder — 1_1/2_1/3_1: обычное чтение слева направо, сверху вниз.
    // - ReversedColumnOrder — 4_1..8_1: СНАЧАЛА весь правый столбец сверху вниз (кадры idle/подготовка),
    //   ПОТОМ весь левый столбец сверху вниз (нарастающая вспышка атаки) — см. скриншот пользователя с
    //   ручной нумерацией кадров 1..8. Перепутать местами с RowMajorOrder — герой будет дёргаться между
    //   позами вместо связной анимации.
    private static readonly (int row, int col)[] RowMajorOrder =
        { (0,0), (0,1), (1,0), (1,1), (2,0), (2,1), (3,0), (3,1) };

    private static readonly (int row, int col)[] ReversedColumnOrder =
        { (0,1), (1,1), (2,1), (3,1), (0,0), (1,0), (2,0), (3,0) };

    private static readonly SheetConfig[] Sheets =
    {
        new SheetConfig("1_1", 2, 4, RowMajorOrder),
        new SheetConfig("2_1", 2, 4, RowMajorOrder),
        new SheetConfig("3_1", 2, 4, RowMajorOrder),
        new SheetConfig("4_1", 2, 4, ReversedColumnOrder),
        new SheetConfig("5_1", 2, 4, ReversedColumnOrder),
        new SheetConfig("6_1", 2, 4, ReversedColumnOrder),
        new SheetConfig("7_1", 2, 4, ReversedColumnOrder),
        new SheetConfig("8_1", 2, 4, ReversedColumnOrder),

        // Старые листы под "_2" (второй враг территории) — раскладка отдельная, не трогать при апдейте выше.
        // Лучник — 3x3, но пустая ячейка (2,0), НЕ последняя.
        new SheetConfig("1_2", 3, 3, new[] { (0,0), (0,1), (0,2), (1,0), (1,1), (1,2), (2,1), (2,2) }),
        // Латный скелет с молотом — 2x4, все 8 ячеек заняты.
        new SheetConfig("2_2", 2, 4, new[] { (0,0), (0,1), (1,0), (1,1), (2,0), (2,1), (3,0), (3,1) }),
        // Орк с топором — 2x4, все 8 ячеек заняты.
        new SheetConfig("3_2", 2, 4, new[] { (0,0), (0,1), (1,0), (1,1), (2,0), (2,1), (3,0), (3,1) }),
    };

    [MenuItem("Tools/Slice All Enemy Sprite Sheets")]
    public static void SliceAll()
    {
        foreach (var sheet in Sheets)
            SliceOne(sheet);

        AssetDatabase.SaveAssets();
        Debug.Log($"Sliced {Sheets.Length} enemy sprite sheets.");
    }

    private static void SliceOne(SheetConfig sheet)
    {
        var sheetImporter = AssetImporter.GetAtPath(sheet.TexturePath) as TextureImporter;
        if (sheetImporter == null) { Debug.LogWarning($"{sheet.TexturePath} not found — skipping."); return; }

        // Нужен доступ к пикселям — временно включаем Read/Write, режем, потом возвращаем как было.
        bool wasReadable = sheetImporter.isReadable;
        if (!wasReadable)
        {
            sheetImporter.isReadable = true;
            sheetImporter.SaveAndReimport();
        }

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(sheet.TexturePath);
        if (tex == null) { Debug.LogWarning($"Could not load texture at {sheet.TexturePath} — skipping."); return; }

        if (!Directory.Exists(sheet.OutputFolder)) Directory.CreateDirectory(sheet.OutputFolder);

        int cellW = tex.width / sheet.Columns;
        int cellH = tex.height / sheet.Rows;

        // Сначала читаем пиксели КАЖДОЙ ячейки, затем берём ОБЪЕДИНЕНИЕ bounding box'ов персонажа и
        // обрезаем ВСЕ кадры этого врага ОДИНАКОВО по нему — кадры анимации не "прыгают" друг
        // относительно друга, а разные враги не отличаются размером просто из-за разного "воздуха" вокруг
        // персонажа в исходном листе (см. project — "разные размеры врагов").
        //
        // В объединение бокса берём только ПЕРВЫЕ 6 из 8 кадров — на всех листах последние 2 кадра
        // (замах -> удар) добавляют большую вспышку эффекта, которая раздувает бокс далеко в сторону и
        // делает персонажа визуально мелким после Preserve Aspect (см. project — "враг маленький, увеличить
        // в 3 раза"). Вспышка на последних кадрах при таком обрезе может слегка обрезаться по краю — это
        // приемлемо, персонаж от этого не страдает (поза меняется несильно между 6-м и 8-м кадром).
        const int framesForBoundsUnion = 6;

        var cellPixels = new Color[sheet.Cells.Length][];
        int unionMinX = cellW, unionMinY = cellH, unionMaxX = -1, unionMaxY = -1;

        for (int i = 0; i < sheet.Cells.Length; i++)
        {
            var (row, col) = sheet.Cells[i];
            int cx = col * cellW;
            // row 0 — верхний ряд картинки, но начало координат текстуры у Unity снизу-слева,
            // поэтому верхний ряд имеет самый БОЛЬШОЙ y.
            int cy = tex.height - (row + 1) * cellH;

            cellPixels[i] = tex.GetPixels(cx, cy, cellW, cellH);
            if (i >= framesForBoundsUnion) continue; // кадр всё равно вырежется ниже, просто не участвует в размере рамки

            var (bx, by, bw, bh) = FindContentBounds(cellPixels[i], cellW, cellH);

            unionMinX = Mathf.Min(unionMinX, bx);
            unionMinY = Mathf.Min(unionMinY, by);
            unionMaxX = Mathf.Max(unionMaxX, bx + bw - 1);
            unionMaxY = Mathf.Max(unionMaxY, by + bh - 1);
        }

        int unionW = unionMaxX - unionMinX + 1;
        int unionH = unionMaxY - unionMinY + 1;

        var framePaths = new string[sheet.Cells.Length];
        for (int i = 0; i < sheet.Cells.Length; i++)
        {
            var frameTex = new Texture2D(unionW, unionH, TextureFormat.RGBA32, false);
            frameTex.SetPixels(GetSubRegion(cellPixels[i], cellW, unionMinX, unionMinY, unionW, unionH));
            frameTex.Apply();

            string framePath = $"{sheet.OutputFolder}/{sheet.FramePrefix}_{i}.png";
            File.WriteAllBytes(framePath, frameTex.EncodeToPNG());
            Object.DestroyImmediate(frameTex);
            framePaths[i] = framePath;
        }

        if (!wasReadable)
        {
            sheetImporter.isReadable = false;
            sheetImporter.SaveAndReimport();
        }

        AssetDatabase.Refresh();

        foreach (var path in framePaths)
        {
            var frameImporter = AssetImporter.GetAtPath(path) as TextureImporter;
            if (frameImporter == null) continue;

            frameImporter.textureType = TextureImporterType.Sprite;
            frameImporter.spriteImportMode = SpriteImportMode.Single;
            frameImporter.alphaIsTransparency = true;
            frameImporter.spritePixelsPerUnit = 100;
            frameImporter.SaveAndReimport();
        }

        WireEnemyData(sheet, framePaths);

        Debug.Log($"Sliced {sheet.TexturePath} into {framePaths.Length} frames under {sheet.OutputFolder}.");
    }

    // Ищет самый маленький прямоугольник, вне которого все пиксели — "фон": либо почти прозрачные, либо
    // почти белые непрозрачные (на разных листах фон разный, см. класс-комментарий). Добавляет небольшой
    // отступ (padding), чтобы не обрезать край персонажа впритык. Если ячейка целиком фон (пустая ячейка
    // случайно попала в список Cells) — возвращает всю ячейку как есть, не пытаясь ужать в ничто.
    private static (int x, int y, int w, int h) FindContentBounds(Color[] pixels, int width, int height)
    {
        const float alphaThreshold = 0.05f;
        const float whiteThreshold = 0.94f;
        const int padding = 4;

        bool IsBackground(Color c) =>
            c.a < alphaThreshold || (c.r > whiteThreshold && c.g > whiteThreshold && c.b > whiteThreshold);

        int minX = width, minY = height, maxX = -1, maxY = -1;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (IsBackground(pixels[y * width + x])) continue;

                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < minX || maxY < minY)
            return (0, 0, width, height);

        minX = Mathf.Max(0, minX - padding);
        minY = Mathf.Max(0, minY - padding);
        maxX = Mathf.Min(width - 1, maxX + padding);
        maxY = Mathf.Min(height - 1, maxY + padding);

        return (minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    // source — плоский массив, локальный для своей ячейки (0..sourceWidth*sourceHeight-1), НЕ вся текстура.
    private static Color[] GetSubRegion(Color[] source, int sourceWidth, int x, int y, int w, int h)
    {
        var result = new Color[w * h];
        for (int row = 0; row < h; row++)
            for (int col = 0; col < w; col++)
                result[row * w + col] = source[(y + row) * sourceWidth + (x + col)];
        return result;
    }

    private static void WireEnemyData(SheetConfig sheet, string[] framePaths)
    {
        var sprites = framePaths.Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p)).ToArray();
        if (sprites.Any(s => s == null))
        {
            Debug.LogWarning($"Some sliced frames failed to load as Sprite for {sheet.TexturePath} — skipping EnemyData wiring.");
            return;
        }

        var enemyData = AssetDatabase.LoadAssetAtPath<EnemyData>(sheet.EnemyDataPath);
        if (enemyData == null) { Debug.LogWarning($"{sheet.EnemyDataPath} not found — skipping wiring."); return; }

        enemyData.idleFrames = new[] { sprites[0] };
        enemyData.attackFrames = sprites.Skip(1).ToArray();
        enemyData.portrait = sprites[0]; // фолбэк-портрет тоже обновляем — для экранов без анимации (превью коллекции и т.п.)

        EditorUtility.SetDirty(enemyData);
    }
}
#endif
