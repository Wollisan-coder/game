using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Singleton that plays a DialogueSequence through a UI panel: background image,
/// speaker portrait (left or right), name label, typewriter-animated text, and
/// a "tap to continue" flow. Same DontDestroyOnLoad singleton pattern as your
/// other managers (HeroCollectionManager, ProgressCurrencyManager).
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("Panel root (toggled on/off)")]
    public GameObject dialoguePanel;

    [Header("Background")]
    public Image backgroundImage;

    [Header("Left / Right portrait slots")]
    public Image leftPortrait;
    public Image rightPortrait;
    [Range(0f, 1f)] public float inactivePortraitDim = 0.45f; // dims the non-speaking side if both are shown

    [Header("Text box")]
    public TMP_Text nameLabel;
    public TMP_Text bodyLabel;
    public GameObject continueIndicator; // e.g. a small bouncing arrow, shown when line is fully revealed

    [Header("Typewriter settings")]
    public float charactersPerSecond = 45f;

    [Header("Input")]
    public Button advanceButton; // full-panel invisible button, or a dedicated "next" button

    private DialogueSequence currentSequence;
    private int currentLineIndex;
    private Coroutine typingRoutine;
    private bool lineFullyRevealed;

    public event Action<DialogueSequence> OnSequenceCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (advanceButton != null) advanceButton.onClick.AddListener(Advance);
    }

    /// <summary>Starts playing a sequence from the beginning.</summary>
    public void Play(DialogueSequence sequence)
    {
        if (sequence == null || sequence.lines == null || sequence.lines.Length == 0)
        {
            Debug.LogWarning("DialogueManager.Play called with an empty sequence.");
            return;
        }

        currentSequence = sequence;
        currentLineIndex = -1;
        if (dialoguePanel != null) dialoguePanel.SetActive(true);

        ShowNextLine();
    }

    /// <summary>Call from the advance button, or from anywhere the player taps to continue.</summary>
    public void Advance()
    {
        if (currentSequence == null) return;

        if (!lineFullyRevealed)
        {
            // Tap while typing → snap straight to full text instead of animating.
            SkipTypewriter();
            return;
        }

        ShowNextLine();
    }

    private void ShowNextLine()
    {
        currentLineIndex++;

        if (currentLineIndex >= currentSequence.lines.Length)
        {
            EndSequence();
            return;
        }

        DialogueLine line = currentSequence.lines[currentLineIndex];

        if (line.backgroundOverride != null && backgroundImage != null)
            backgroundImage.sprite = line.backgroundOverride;

        ApplyPortrait(line);

        if (nameLabel != null) nameLabel.text = line.speakerName;

        if (line.voiceClip != null)
        {
            // Hook up to your audio manager here if you have one, e.g.:
            // AudioManager.Instance.PlayVoice(line.voiceClip);
        }

        if (typingRoutine != null) StopCoroutine(typingRoutine);
        typingRoutine = StartCoroutine(TypeLine(line.text));
    }

    private void ApplyPortrait(DialogueLine line)
    {
        bool hasPortrait = line.speakerPortrait != null;

        Image activeSlot = line.speakerOnLeft ? leftPortrait : rightPortrait;
        Image otherSlot = line.speakerOnLeft ? rightPortrait : leftPortrait;

        if (activeSlot != null)
        {
            activeSlot.enabled = hasPortrait;
            if (hasPortrait) activeSlot.sprite = line.speakerPortrait;
            activeSlot.color = new Color(1f, 1f, 1f, 1f);
        }

        // Dim (don't hide) the other side if it already has a sprite showing, so a
        // two-character conversation can visually show "who's talking now".
        if (otherSlot != null && otherSlot.sprite != null)
        {
            otherSlot.color = new Color(1f, 1f, 1f, inactivePortraitDim);
        }
    }

    private IEnumerator TypeLine(string fullText)
    {
        lineFullyRevealed = false;
        if (continueIndicator != null) continueIndicator.SetActive(false);
        if (bodyLabel != null) bodyLabel.text = "";

        float delay = 1f / Mathf.Max(1f, charactersPerSecond);
        var builder = new System.Text.StringBuilder();

        for (int i = 0; i < fullText.Length; i++)
        {
            builder.Append(fullText[i]);
            if (bodyLabel != null) bodyLabel.text = builder.ToString();
            yield return new WaitForSeconds(delay);
        }

        lineFullyRevealed = true;
        if (continueIndicator != null) continueIndicator.SetActive(true);
    }

    private void SkipTypewriter()
    {
        if (typingRoutine != null) StopCoroutine(typingRoutine);

        DialogueLine line = currentSequence.lines[currentLineIndex];
        if (bodyLabel != null) bodyLabel.text = line.text;

        lineFullyRevealed = true;
        if (continueIndicator != null) continueIndicator.SetActive(true);
    }

    private void EndSequence()
    {
        DialogueSequence finished = currentSequence;

        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        currentSequence = null;
        currentLineIndex = -1;

        OnSequenceCompleted?.Invoke(finished);

        // Hook for your own event system, e.g. unlocking a race after its intro cutscene:
        // if (!string.IsNullOrEmpty(finished.onCompleteEventId))
        //     GameEvents.Trigger(finished.onCompleteEventId);
    }
}
