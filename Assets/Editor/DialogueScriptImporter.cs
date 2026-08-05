using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Импортирует простой текстовый сценарий (.txt) в DialogueSequence-ассет — пишешь лор в обычном
// текстовом файле, вместо того чтобы тыкать Array-элементы DialogueLine[] по одному в Inspector.
//
// Формат строки:
//   id: some_id                 — задаёт DialogueSequence.sequenceId (обычно первая строка файла)
//   BG: spriteName               — фон для СЛЕДУЮЩЕЙ реплики (ищется в Resources/Backgrounds/spriteName)
//   Speaker (left|right): text   — реплика; left/right необязателен, по умолчанию left
//   : text                       — реплика без спикера (нарратив/закадровый текст, портрета нет)
//   # comment                    — комментарий, игнорируется
//   (пустая строка)              — игнорируется, чисто для читаемости файла
// Портрет ищется автоматически по Resources/Portraits/{SpeakerName}.png — если такого файла нет,
// реплика просто останется без портрета (Resources.Load возвращает null, DialogueManager это уже
// обрабатывает мягко — см. DialogueManager.ApplyPortrait).
public static class DialogueScriptImporter
{
    private static readonly Regex LinePattern = new Regex(@"^(?<speaker>[^:(]*)(\((?<side>left|right)\))?\s*:\s*(?<text>.*)$");

    [MenuItem("Assets/Story/Import Dialogue Script...")]
    public static void ImportFromMenu()
    {
        string absolutePath = EditorUtility.OpenFilePanel("Select dialogue script (.txt)", "Assets", "txt");
        if (string.IsNullOrEmpty(absolutePath)) return;

        if (!absolutePath.StartsWith(Application.dataPath))
        {
            Debug.LogError("DialogueScriptImporter: file must be inside this project's Assets folder.");
            return;
        }

        string relativePath = "Assets" + absolutePath.Substring(Application.dataPath.Length);
        Import(relativePath);
    }

    public static void Import(string txtAssetPath)
    {
        if (!File.Exists(txtAssetPath))
        {
            Debug.LogError($"DialogueScriptImporter: file not found at {txtAssetPath}");
            return;
        }

        string[] rawLines = File.ReadAllLines(txtAssetPath);

        string sequenceId = null;
        string pendingBackground = null;
        var lines = new List<DialogueLine>();

        foreach (string raw in rawLines)
        {
            string trimmed = raw.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("#")) continue;

            if (trimmed.StartsWith("id:"))
            {
                sequenceId = trimmed.Substring(3).Trim();
                continue;
            }

            if (trimmed.StartsWith("BG:"))
            {
                pendingBackground = trimmed.Substring(3).Trim();
                continue;
            }

            var match = LinePattern.Match(trimmed);
            if (!match.Success)
            {
                Debug.LogWarning($"DialogueScriptImporter: couldn't parse line, skipped: \"{raw}\"");
                continue;
            }

            string speaker = match.Groups["speaker"].Value.Trim();
            string side = match.Groups["side"].Success ? match.Groups["side"].Value : "left";
            string text = match.Groups["text"].Value.Trim();

            var line = new DialogueLine
            {
                speakerName = speaker,
                text = text,
                speakerOnLeft = side == "left",
            };

            if (!string.IsNullOrEmpty(speaker))
                line.speakerPortrait = Resources.Load<Sprite>($"Portraits/{speaker}");

            if (pendingBackground != null)
            {
                line.backgroundOverride = Resources.Load<Sprite>($"Backgrounds/{pendingBackground}");
                pendingBackground = null;
            }

            lines.Add(line);
        }

        if (string.IsNullOrEmpty(sequenceId))
        {
            sequenceId = Path.GetFileNameWithoutExtension(txtAssetPath);
            Debug.LogWarning($"DialogueScriptImporter: no 'id:' line found in the script, using file name as sequenceId: {sequenceId}");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Story"))
            AssetDatabase.CreateFolder("Assets", "Story");

        string outputPath = $"Assets/Story/{sequenceId}.asset";

        var sequence = AssetDatabase.LoadAssetAtPath<DialogueSequence>(outputPath);
        bool isNew = sequence == null;
        if (isNew)
            sequence = ScriptableObject.CreateInstance<DialogueSequence>();

        sequence.sequenceId = sequenceId;
        sequence.lines = lines.ToArray();

        if (isNew)
            AssetDatabase.CreateAsset(sequence, outputPath);

        EditorUtility.SetDirty(sequence);
        AssetDatabase.SaveAssets();

        Debug.Log($"DialogueScriptImporter: imported {lines.Count} lines -> {outputPath}");
    }
}
