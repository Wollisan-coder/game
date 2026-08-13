using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Экран входа в Mutation Dungeon (roguelike C+A — мутация поля + ветвящийся путь, см.
// project_future_backlog #7 / MutationDungeonManager). В отличие от Death Dungeon — никакого
// permadeath-предупреждения не нужно (см. class-комментарий MutationDungeonManager), просто "начать ран".
// Если ран уже активен (вышли из Castle и вернулись) — сразу открывает экран выбора узла вместо этого
// экрана, см. Open().
public class MutationDungeonEntryUI : MonoBehaviour
{
    private Transform canvasRoot;
    private MainMenuUI mainMenuUI;

    private GameObject overlayRoot;
    private TMP_Text bodyText;

    public void Open(Transform canvasRoot, MainMenuUI mainMenuUI)
    {
        this.canvasRoot = canvasRoot;
        this.mainMenuUI = mainMenuUI;

        if (MutationDungeonManager.Instance != null && MutationDungeonManager.Instance.IsRunActive)
        {
            var choiceUI = GetComponent<MutationDungeonNodeChoiceUI>();
            choiceUI?.Open(canvasRoot, mainMenuUI);
            return;
        }

        BuildOverlayIfNeeded();
        overlayRoot.transform.SetAsLastSibling();
        overlayRoot.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        if (overlayRoot != null) overlayRoot.SetActive(false);
    }

    private void Refresh()
    {
        if (bodyText != null)
            bodyText.text = $"A gauntlet of {MutationDungeonManager.TotalNodes} nodes to a boss — pick your path as you go.\n\n" +
                "Each fight mutates the board itself (frozen tiles, missing colors, smaller boards, damage swings...). " +
                "Your gear and hero levels work normally here — no stat equalizing.\n\n" +
                "Losing a fight simply ends the run. Your heroes are never at risk.";
    }

    private void BuildOverlayIfNeeded()
    {
        if (overlayRoot != null) return;

        overlayRoot = new GameObject("MutationDungeonEntryOverlay", typeof(RectTransform));
        var overlayRect = (RectTransform)overlayRoot.transform;
        overlayRect.SetParent(canvasRoot, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        var dim = overlayRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.75f);

        var closeAreaObj = new GameObject("CloseArea", typeof(RectTransform));
        var closeAreaRect = (RectTransform)closeAreaObj.transform;
        closeAreaRect.SetParent(overlayRect, false);
        closeAreaRect.anchorMin = Vector2.zero;
        closeAreaRect.anchorMax = Vector2.one;
        closeAreaRect.offsetMin = Vector2.zero;
        closeAreaRect.offsetMax = Vector2.zero;
        var closeAreaImg = closeAreaObj.AddComponent<Image>();
        closeAreaImg.color = new Color(0, 0, 0, 0);
        var closeAreaBtn = closeAreaObj.AddComponent<Button>();
        closeAreaBtn.transition = Selectable.Transition.None;
        closeAreaBtn.onClick.AddListener(Close);

        var windowObj = new GameObject("Window", typeof(RectTransform));
        var windowRect = (RectTransform)windowObj.transform;
        windowRect.SetParent(overlayRect, false);
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(900, 560);
        var windowBg = windowObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsPanel(windowBg);

        var titleObj = new GameObject("Title", typeof(RectTransform));
        var titleRect = (RectTransform)titleObj.transform;
        titleRect.SetParent(windowRect, false);
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 70);
        titleRect.anchoredPosition = new Vector2(0, -30);
        var title = titleObj.AddComponent<TextMeshProUGUI>();
        title.text = "Mutation Trial";
        title.fontSize = 42;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;

        var bodyObj = new GameObject("Body", typeof(RectTransform));
        var bodyRect = (RectTransform)bodyObj.transform;
        bodyRect.SetParent(windowRect, false);
        bodyRect.anchorMin = new Vector2(0, 0);
        bodyRect.anchorMax = new Vector2(1, 1);
        bodyRect.offsetMin = new Vector2(60, 140);
        bodyRect.offsetMax = new Vector2(-60, -110);
        bodyText = bodyObj.AddComponent<TextMeshProUGUI>();
        bodyText.fontSize = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.color = new Color(1, 1, 1, 0.9f);

        var startObj = new GameObject("StartButton", typeof(RectTransform));
        var startRect = (RectTransform)startObj.transform;
        startRect.SetParent(windowRect, false);
        startRect.anchorMin = new Vector2(0.3f, 0);
        startRect.anchorMax = new Vector2(0.3f, 0);
        startRect.pivot = new Vector2(0.5f, 0);
        startRect.sizeDelta = new Vector2(280, 80);
        startRect.anchoredPosition = new Vector2(0, 40);
        var startImg = startObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(startImg);
        var startBtn = startObj.AddComponent<Button>();
        startBtn.onClick.AddListener(OnStartClicked);

        var startTextObj = new GameObject("Text", typeof(RectTransform));
        var startTextRect = (RectTransform)startTextObj.transform;
        startTextRect.SetParent(startRect, false);
        startTextRect.anchorMin = Vector2.zero;
        startTextRect.anchorMax = Vector2.one;
        startTextRect.offsetMin = Vector2.zero;
        startTextRect.offsetMax = Vector2.zero;
        var startText = startTextObj.AddComponent<TextMeshProUGUI>();
        startText.text = "Start Run";
        startText.fontSize = 30;
        startText.fontStyle = FontStyles.Bold;
        startText.alignment = TextAlignmentOptions.Center;
        startText.color = ConfirmationDialog.ButtonTextColor;

        var exitObj = new GameObject("ExitButton", typeof(RectTransform));
        var exitRect = (RectTransform)exitObj.transform;
        exitRect.SetParent(windowRect, false);
        exitRect.anchorMin = new Vector2(0.7f, 0);
        exitRect.anchorMax = new Vector2(0.7f, 0);
        exitRect.pivot = new Vector2(0.5f, 0);
        exitRect.sizeDelta = new Vector2(280, 80);
        exitRect.anchoredPosition = new Vector2(0, 40);
        var exitImg = exitObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(exitImg);
        var exitBtn = exitObj.AddComponent<Button>();
        exitBtn.onClick.AddListener(Close);

        var exitTextObj = new GameObject("Text", typeof(RectTransform));
        var exitTextRect = (RectTransform)exitTextObj.transform;
        exitTextRect.SetParent(exitRect, false);
        exitTextRect.anchorMin = Vector2.zero;
        exitTextRect.anchorMax = Vector2.one;
        exitTextRect.offsetMin = Vector2.zero;
        exitTextRect.offsetMax = Vector2.zero;
        var exitText = exitTextObj.AddComponent<TextMeshProUGUI>();
        exitText.text = "Exit";
        exitText.fontSize = 30;
        exitText.fontStyle = FontStyles.Bold;
        exitText.alignment = TextAlignmentOptions.Center;
        exitText.color = ConfirmationDialog.ButtonTextColor;

        overlayRoot.SetActive(false);
    }

    private void OnStartClicked()
    {
        if (MutationDungeonManager.Instance == null) return;

        MutationDungeonManager.Instance.StartNewRun();
        Close();

        var choiceUI = GetComponent<MutationDungeonNodeChoiceUI>();
        choiceUI?.Open(canvasRoot, mainMenuUI);
    }
}
