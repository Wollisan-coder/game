using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Попап выбора баффа на узлах типа Buff (см. project_death_dungeon_concept) — 3 случайных варианта из
// DeathDungeonManager.buffPool, выбор одного добавляет его в activeBuffs на остаток забега. Открывается
// из BattleManager.EndBattleVictory (бой в SampleScene), поэтому строится полностью в рантайме, без
// зависимости от сцены/префабов — та же схема, что и у остальных попапов проекта.
public class DeathDungeonBuffChoiceUI : MonoBehaviour
{
    private const int ChoiceCount = 3;

    private GameObject overlayRoot;
    private Transform optionsContainer;
    private System.Action onChosen;

    public void Open(Transform canvasRoot, System.Action onChosen)
    {
        this.onChosen = onChosen;
        BuildOverlayIfNeeded(canvasRoot);
        overlayRoot.transform.SetAsLastSibling();
        overlayRoot.SetActive(true);
        Populate();
    }

    private void BuildOverlayIfNeeded(Transform canvasRoot)
    {
        if (overlayRoot != null) return;

        overlayRoot = new GameObject("DeathDungeonBuffChoiceOverlay", typeof(RectTransform));
        var overlayRect = (RectTransform)overlayRoot.transform;
        overlayRect.SetParent(canvasRoot, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        var dim = overlayRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.82f);
        // Намеренно без клика-мимо-закрытия — выбор баффа обязателен, попап нельзя закрыть, не выбрав.

        var windowObj = new GameObject("Window", typeof(RectTransform));
        var windowRect = (RectTransform)windowObj.transform;
        windowRect.SetParent(overlayRect, false);
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(1000, 620);
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
        title.text = "Choose a Blessing";
        title.fontSize = 42;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;

        var optionsObj = new GameObject("Options", typeof(RectTransform));
        var optionsRect = (RectTransform)optionsObj.transform;
        optionsRect.SetParent(windowRect, false);
        optionsRect.anchorMin = new Vector2(0.5f, 0.5f);
        optionsRect.anchorMax = new Vector2(0.5f, 0.5f);
        optionsRect.pivot = new Vector2(0.5f, 0.5f);
        optionsRect.sizeDelta = new Vector2(3 * 280 + 2 * 24, 440);
        optionsRect.anchoredPosition = new Vector2(0, -30);
        var optionsLayout = optionsObj.AddComponent<HorizontalLayoutGroup>();
        optionsLayout.spacing = 24;
        optionsLayout.childForceExpandWidth = false;
        optionsLayout.childForceExpandHeight = false;
        optionsLayout.childControlWidth = true; // false игнорирует LayoutElement.preferredWidth/Height у карточек — падают на дефолтный 100x100
        optionsLayout.childControlHeight = true;
        optionsLayout.childAlignment = TextAnchor.MiddleCenter;
        optionsContainer = optionsRect;

        overlayRoot.SetActive(false);
    }

    private void Populate()
    {
        foreach (Transform child in optionsContainer)
            Destroy(child.gameObject);

        var choices = DeathDungeonManager.Instance != null
            ? DeathDungeonManager.Instance.DrawBuffChoices(ChoiceCount)
            : new List<DeathDungeonBuffData>();

        foreach (var buff in choices)
            BuildOption(buff);
    }

    private void BuildOption(DeathDungeonBuffData buff)
    {
        var cardObj = new GameObject(buff.buffName, typeof(RectTransform));
        var cardRect = (RectTransform)cardObj.transform;
        cardRect.SetParent(optionsContainer, false);
        var layoutElement = cardObj.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 280;
        layoutElement.preferredHeight = 440;

        var bg = cardObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsPanel(bg); // DialogWindowFrame, а не DialogHeaderFrame

        var btn = cardObj.AddComponent<Button>();
        btn.onClick.AddListener(() => OnOptionClicked(buff));

        var nameObj = new GameObject("Name", typeof(RectTransform));
        var nameRect = (RectTransform)nameObj.transform;
        nameRect.SetParent(cardRect, false);
        nameRect.anchorMin = new Vector2(0, 1);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.pivot = new Vector2(0.5f, 1);
        nameRect.sizeDelta = new Vector2(-20, 90);
        nameRect.anchoredPosition = new Vector2(0, -20);
        var nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.text = buff.buffName;
        nameText.fontStyle = FontStyles.Bold;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = ConfirmationDialog.ButtonTextColor;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
        nameText.fontSizeMax = 32;

        var descObj = new GameObject("Description", typeof(RectTransform));
        var descRect = (RectTransform)descObj.transform;
        descRect.SetParent(cardRect, false);
        descRect.anchorMin = new Vector2(0, 0);
        descRect.anchorMax = new Vector2(1, 1);
        descRect.offsetMin = new Vector2(20, 20);
        descRect.offsetMax = new Vector2(-20, -120);
        var descText = descObj.AddComponent<TextMeshProUGUI>();
        descText.text = buff.description;
        descText.alignment = TextAlignmentOptions.Center;
        descText.color = new Color(1, 1, 1, 0.9f);
        descText.enableAutoSizing = true;
        descText.fontSizeMin = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
        descText.fontSizeMax = 32;
    }

    private void OnOptionClicked(DeathDungeonBuffData buff)
    {
        DeathDungeonManager.Instance?.activeBuffs.Add(buff);
        overlayRoot.SetActive(false);
        onChosen?.Invoke();
    }
}
