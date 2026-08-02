using UnityEngine;

/// <summary>
/// Single line of dialogue: who's speaking, their portrait, the text, and optional
/// overrides (background change, voice clip). Part of a DialogueSequence.
/// </summary>
[System.Serializable]
public class DialogueLine
{
    public string speakerName;
    public Sprite speakerPortrait;

    [TextArea(2, 5)]
    public string text;

    [Tooltip("Leave null to keep whatever background is currently showing.")]
    public Sprite backgroundOverride;

    [Tooltip("Optional voice-over clip for this line.")]
    public AudioClip voiceClip;

    [Tooltip("If true, portrait appears on the left side instead of the right.")]
    public bool speakerOnLeft = true;
}

/// <summary>
/// A full scripted conversation (e.g. one story-level cutscene, one race-unlock scene).
/// Create via Assets → Create → Story → Dialogue Sequence.
/// </summary>
[CreateAssetMenu(menuName = "Story/Dialogue Sequence", fileName = "NewDialogueSequence")]
public class DialogueSequence : ScriptableObject
{
    [Tooltip("Stable id for save/skip-tracking purposes, independent of the asset name.")]
    public string sequenceId;

    public DialogueLine[] lines;

    [Tooltip("Optional: scene/event to trigger automatically when this sequence finishes " +
             "(e.g. unlock a race, open a dungeon). Left as a plain string hook for now — " +
             "wire it up to whatever event system you use.")]
    public string onCompleteEventId;
}
