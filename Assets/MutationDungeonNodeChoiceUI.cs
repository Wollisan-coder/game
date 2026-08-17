using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Экран выбора следующего узла (2-3 варианта, см. MutationDungeonManager.PendingChoices) — открывается
// после StartNewRun() (см. MutationDungeonEntryUI) и переоткрывается CastleUI при возврате из боя, пока
// ран активен (см. ReopenMutationDungeonIfActive/returningFromMutationDungeon). Battle/Elite/Boss тратят
// энергию и грузят боевую сцену (тот же приём, что DeathDungeonMapUI.OnNodeClicked); Rest/Treasure
// разрешаются тут же без боя и сразу перерисовывают тот же экран со следующим набором вариантов.
public class MutationDungeonNodeChoiceUI : MonoBehaviour
{
    private Transform canvasRoot;
    private MainMenuUI mainMenuUI;

    private GameObject overlayRoot;
    private TMP_Text headerText;
    private Transform breadcrumbContainer;
    private Transform cardsContainer;

    public void Open(Transform canvasRoot, MainMenuUI mainMenuUI)
    {
        this.canvasRoot = canvasRoot;
        this.mainMenuUI = mainMenuUI;

        if (MutationDungeonManager.Instance == null || !MutationDungeonManager.Instance.IsRunActive) return;

        BuildOverlayIfNeeded();
        overlayRoot.transform.SetAsLastSibling();
        overlayRoot.SetActive(true);
        Populate();
    }

    public void Close()
    {
        if (overlayRoot != null) overlayRoot.SetActive(false);
    }

    private void BuildOverlayIfNeeded()
    {
        if (overlayRoot != null) return;

        overlayRoot = new GameObject("MutationDungeonChoiceOverlay", typeof(RectTransform));
        var overlayRect = (RectTransform)overlayRoot.transform;
        overlayRect.SetParent(canvasRoot, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        var dim = overlayRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.8f);

        var windowObj = new GameObject("Window", typeof(RectTransform));
        var windowRect = (RectTransform)windowObj.transform;
        windowRect.SetParent(overlayRect, false);
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(1000, 1000);
        var windowBg = windowObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsPanel(windowBg);

        var titleObj = new GameObject("Title", typeof(RectTransform));
        var titleRect = (RectTransform)titleObj.transform;
        titleRect.SetParent(windowRect, false);
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 45);
        titleRect.anchoredPosition = new Vector2(0, -30);
        var title = titleObj.AddComponent<TextMeshProUGUI>();
        title.text = "Choose Your Path";
        title.fontSize = 32;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;

        var headerObj = new GameObject("Header", typeof(RectTransform));
        var headerRect = (RectTransform)headerObj.transform;
        headerRect.SetParent(windowRect, false);
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = new Vector2(1, 1);
        headerRect.pivot = new Vector2(0.5f, 1);
        headerRect.sizeDelta = new Vector2(0, 30);
        headerRect.anchoredPosition = new Vector2(0, -70);
        headerText = headerObj.AddComponent<TextMeshProUGUI>();
        headerText.fontSize = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize, см. project_ui_text_size_standard
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.color = new Color(1, 1, 1, 0.8f);

        // Хлебные крошки — по одной "пипе" на узел рана (см. RefreshBreadcrumb): пройденные показывают
        // тип узла, текущая (та, что сейчас выбирают) подсвечена золотой обводкой, будущие — "?" (путь
        // генерируется лениво, реального ответа для них ещё нет). Добавлено 2026-08-12 по запросу — раньше
        // экран выбора не показывал никакой истории/прогресса рана вообще.
        var breadcrumbObj = new GameObject("Breadcrumb", typeof(RectTransform));
        var breadcrumbRect = (RectTransform)breadcrumbObj.transform;
        breadcrumbRect.SetParent(windowRect, false);
        breadcrumbRect.anchorMin = new Vector2(0.5f, 1);
        breadcrumbRect.anchorMax = new Vector2(0.5f, 1);
        breadcrumbRect.pivot = new Vector2(0.5f, 1);
        breadcrumbRect.sizeDelta = new Vector2(MutationDungeonManager.TotalNodes * 50 + (MutationDungeonManager.TotalNodes - 1) * 8, 50);
        breadcrumbRect.anchoredPosition = new Vector2(0, -110);
        var breadcrumbLayout = breadcrumbObj.AddComponent<HorizontalLayoutGroup>();
        breadcrumbLayout.spacing = 8;
        breadcrumbLayout.childAlignment = TextAnchor.MiddleCenter;
        breadcrumbLayout.childForceExpandWidth = false;
        breadcrumbLayout.childForceExpandHeight = false;
        breadcrumbLayout.childControlWidth = true;
        breadcrumbLayout.childControlHeight = true;
        breadcrumbContainer = breadcrumbRect;

        var cardsObj = new GameObject("Cards", typeof(RectTransform));
        var cardsRect = (RectTransform)cardsObj.transform;
        cardsRect.SetParent(windowRect, false);
        cardsRect.anchorMin = new Vector2(0.5f, 1);
        cardsRect.anchorMax = new Vector2(0.5f, 1);
        cardsRect.pivot = new Vector2(0.5f, 1);
        cardsRect.sizeDelta = new Vector2(3 * 280 + 2 * 20, 550);
        cardsRect.anchoredPosition = new Vector2(0, -190);
        var cardsLayout = cardsObj.AddComponent<HorizontalLayoutGroup>();
        cardsLayout.spacing = 20;
        cardsLayout.childAlignment = TextAnchor.MiddleCenter;
        cardsLayout.childForceExpandWidth = false;
        cardsLayout.childForceExpandHeight = false;
        cardsLayout.childControlWidth = true;
        cardsLayout.childControlHeight = true;
        cardsContainer = cardsRect;

        // Тот же приём, что и DeathDungeonMapUI.Close/"Hide Map" — прячет оверлей, НЕ трогая ран, чтобы
        // можно было глянуть отряд/инвентарь в Замке и вернуться (клик по кнопке Mutation Dungeon в Замке
        // снова открывает этот же экран, пока ран активен, см. MutationDungeonEntryUI.Open).
        var hideObj = new GameObject("HideButton", typeof(RectTransform));
        var hideRect = (RectTransform)hideObj.transform;
        hideRect.SetParent(windowRect, false);
        hideRect.anchorMin = new Vector2(0.28f, 0);
        hideRect.anchorMax = new Vector2(0.28f, 0);
        hideRect.pivot = new Vector2(0.5f, 0);
        hideRect.sizeDelta = new Vector2(260, 50);
        hideRect.anchoredPosition = new Vector2(0, 25);
        var hideImg = hideObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(hideImg);
        var hideBtn = hideObj.AddComponent<Button>();
        hideBtn.onClick.AddListener(Close);

        var hideTextObj = new GameObject("Text", typeof(RectTransform));
        var hideTextRect = (RectTransform)hideTextObj.transform;
        hideTextRect.SetParent(hideRect, false);
        hideTextRect.anchorMin = Vector2.zero;
        hideTextRect.anchorMax = Vector2.one;
        hideTextRect.offsetMin = Vector2.zero;
        hideTextRect.offsetMax = Vector2.zero;
        var hideText = hideTextObj.AddComponent<TextMeshProUGUI>();
        hideText.text = "Hide";
        hideText.alignment = TextAlignmentOptions.Center;
        hideText.color = ConfirmationDialog.ButtonTextColor;

        var abandonObj = new GameObject("AbandonButton", typeof(RectTransform));
        var abandonRect = (RectTransform)abandonObj.transform;
        abandonRect.SetParent(windowRect, false);
        abandonRect.anchorMin = new Vector2(0.72f, 0);
        abandonRect.anchorMax = new Vector2(0.72f, 0);
        abandonRect.pivot = new Vector2(0.5f, 0);
        abandonRect.sizeDelta = new Vector2(260, 50);
        abandonRect.anchoredPosition = new Vector2(0, 25);
        var abandonImg = abandonObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(abandonImg);
        var abandonBtn = abandonObj.AddComponent<Button>();
        abandonBtn.onClick.AddListener(OnAbandonClicked);

        var abandonTextObj = new GameObject("Text", typeof(RectTransform));
        var abandonTextRect = (RectTransform)abandonTextObj.transform;
        abandonTextRect.SetParent(abandonRect, false);
        abandonTextRect.anchorMin = Vector2.zero;
        abandonTextRect.anchorMax = Vector2.one;
        abandonTextRect.offsetMin = Vector2.zero;
        abandonTextRect.offsetMax = Vector2.zero;
        var abandonText = abandonTextObj.AddComponent<TextMeshProUGUI>();
        abandonText.text = "Abandon Run";
        abandonText.alignment = TextAlignmentOptions.Center;
        abandonText.color = ConfirmationDialog.ButtonTextColor;

        overlayRoot.SetActive(false);
    }

    private void Populate()
    {
        var dungeon = MutationDungeonManager.Instance;
        if (dungeon == null || !dungeon.IsRunActive || cardsContainer == null) return;

        if (headerText != null)
            headerText.text = $"Node {dungeon.currentNodeIndex + 1}/{MutationDungeonManager.TotalNodes}";

        RefreshBreadcrumb(dungeon);

        foreach (Transform child in cardsContainer)
            Destroy(child.gameObject);

        foreach (var choice in dungeon.PendingChoices)
            BuildCard(choice);
    }

    // Одна "пипа" на узел рана — пройденные показывают тип узла (см. GetNodeTypeShortLabel/Color),
    // текущая (i == History.Count — тот самый, для которого сейчас показаны PendingChoices, см.
    // MutationDungeonManager.History) подсвечена золотой обводкой, остальные будущие — просто "?".
    private void RefreshBreadcrumb(MutationDungeonManager dungeon)
    {
        if (breadcrumbContainer == null) return;

        foreach (Transform child in breadcrumbContainer)
            Destroy(child.gameObject);

        for (int i = 0; i < MutationDungeonManager.TotalNodes; i++)
        {
            bool completed = i < dungeon.History.Count;
            bool current = i == dungeon.History.Count;

            var pipObj = new GameObject($"Pip_{i}", typeof(RectTransform));
            pipObj.transform.SetParent(breadcrumbContainer, false);
            var pipLE = pipObj.AddComponent<LayoutElement>();
            pipLE.preferredWidth = 50;
            pipLE.preferredHeight = 50;

            var pipImg = pipObj.AddComponent<Image>();
            pipImg.color = completed ? GetNodeTypeColor(dungeon.History[i].nodeType) : new Color(0.2f, 0.2f, 0.22f);

            if (current)
            {
                var outline = pipObj.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 0.85f, 0.3f);
                outline.effectDistance = new Vector2(3, -3);
            }

            var textObj = new GameObject("Text", typeof(RectTransform));
            var textRect = (RectTransform)textObj.transform;
            textRect.SetParent(pipObj.transform, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = completed ? GetNodeTypeShortLabel(dungeon.History[i].nodeType) : "?";
            text.fontSize = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
        }
    }

    private static string GetNodeTypeShortLabel(MutationDungeonNodeType type) => type switch
    {
        MutationDungeonNodeType.Battle => "B",
        MutationDungeonNodeType.Elite => "E",
        MutationDungeonNodeType.Rest => "R",
        MutationDungeonNodeType.Treasure => "T",
        MutationDungeonNodeType.Boss => "S", // "Skull" — избегаем не-ASCII глифов, которых может не быть в шрифте
        _ => "?",
    };

    private static Color GetNodeTypeColor(MutationDungeonNodeType type) => type switch
    {
        MutationDungeonNodeType.Battle => new Color(0.5f, 0.52f, 0.58f),
        MutationDungeonNodeType.Elite => new Color(0.75f, 0.35f, 0.2f),
        MutationDungeonNodeType.Rest => new Color(0.3f, 0.6f, 0.35f),
        MutationDungeonNodeType.Treasure => new Color(0.75f, 0.65f, 0.2f),
        MutationDungeonNodeType.Boss => new Color(0.5f, 0.1f, 0.1f),
        _ => new Color(0.25f, 0.25f, 0.28f),
    };

    private void BuildCard(MutationDungeonChoice choice)
    {
        var cardObj = new GameObject($"Card_{choice.nodeType}", typeof(RectTransform));
        var cardRect = (RectTransform)cardObj.transform;
        cardRect.SetParent(cardsContainer, false);
        var cardLE = cardObj.AddComponent<LayoutElement>();
        cardLE.preferredWidth = 280;
        cardLE.preferredHeight = 550; // подобрано вживую в Play Mode пользователем 2026-08-12

        var cardBg = cardObj.AddComponent<Image>();
        bool positive = choice.HasMutation && MutationUtility.IsPositive(choice.mutationType);
        // StyleAsRectFrame (Windo.png), не StyleAsButton — карточка портретная (280x380), DialogHeaderFrame
        // растягивается некрасиво на такой пропорции (см. ConfirmationDialog.StyleAsRectFrame).
        ConfirmationDialog.StyleAsRectFrame(cardBg, positive ? new Color(0.2f, 0.4f, 0.2f) : (Color?)null);
        var cardBtn = cardObj.AddComponent<Button>();
        cardBtn.onClick.AddListener(() => OnCardClicked(choice));

        var vlayout = cardObj.AddComponent<VerticalLayoutGroup>();
        vlayout.padding = new RectOffset(16, 16, 20, 20);
        vlayout.spacing = 10;
        vlayout.childAlignment = TextAnchor.UpperCenter;
        vlayout.childControlWidth = true;
        vlayout.childControlHeight = true;
        vlayout.childForceExpandWidth = true;
        vlayout.childForceExpandHeight = false;

        var titleObj = new GameObject("NodeType", typeof(RectTransform));
        titleObj.transform.SetParent(cardRect, false);
        var titleLE = titleObj.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 48;
        var titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = GetNodeTypeLabel(choice.nodeType);
        titleText.fontSize = 30;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = ConfirmationDialog.ButtonTextColor;

        var descObj = new GameObject("Description", typeof(RectTransform));
        descObj.transform.SetParent(cardRect, false);
        var descLE = descObj.AddComponent<LayoutElement>();
        descLE.preferredHeight = 452; // 550 - padding(40) - spacing(10) - title(48)
        var descText = descObj.AddComponent<TextMeshProUGUI>();
        descText.text = GetNodeDescription(choice);
        descText.fontSize = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize, см. project_ui_text_size_standard
        descText.alignment = TextAlignmentOptions.Top;
        descText.color = ConfirmationDialog.ButtonTextColor;
        descText.enableAutoSizing = true;
        descText.fontSizeMin = 28;
        descText.fontSizeMax = 30;
    }

    private static string GetNodeTypeLabel(MutationDungeonNodeType type) => type switch
    {
        MutationDungeonNodeType.Battle => "Battle",
        MutationDungeonNodeType.Elite => "Elite Battle",
        MutationDungeonNodeType.Rest => "Rest",
        MutationDungeonNodeType.Treasure => "Treasure",
        MutationDungeonNodeType.Boss => "BOSS",
        _ => "",
    };

    private static string GetNodeDescription(MutationDungeonChoice choice)
    {
        string body = choice.nodeType switch
        {
            MutationDungeonNodeType.Battle => "A standard fight.",
            MutationDungeonNodeType.Elite => "A tougher fight — better reward (a bonus item on top of the usual loot).",
            MutationDungeonNodeType.Rest => "No fight. Fully heals your squad's HP and mana before the next node.",
            MutationDungeonNodeType.Treasure => "No fight. Guaranteed bonus item.",
            MutationDungeonNodeType.Boss => "The final fight of this run.",
            _ => "",
        };

        if (choice.HasMutation)
            body += $"\n\n<b>{MutationUtility.GetShortLabel(choice.mutationType)}</b>\n" +
                MutationUtility.GetDescription(choice.mutationType, choice.mutationValue);

        return body;
    }

    private void OnCardClicked(MutationDungeonChoice choice)
    {
        var dungeon = MutationDungeonManager.Instance;
        if (dungeon == null) return;

        switch (choice.nodeType)
        {
            case MutationDungeonNodeType.Battle:
            case MutationDungeonNodeType.Elite:
            case MutationDungeonNodeType.Boss:
                LaunchBattle(choice);
                break;

            case MutationDungeonNodeType.Rest:
                dungeon.SelectNode(choice);
                dungeon.HealSquadFully();
                dungeon.AdvanceAfterWin();
                ConfirmationDialog.ShowInfo(canvasRoot, "Your squad is fully healed.", 170, Populate);
                break;

            case MutationDungeonNodeType.Treasure:
                dungeon.SelectNode(choice);
                dungeon.AdvanceAfterWin();
                GrantTreasure();
                break;
        }
    }

    private void GrantTreasure()
    {
        var manager = ItemCollectionManager.Instance;
        var catalog = manager != null ? manager.allItems : null;
        if (catalog == null || catalog.Length == 0)
        {
            Populate();
            return;
        }

        var item = catalog[Random.Range(0, catalog.Length)];
        manager.AddItemCopy(item);
        ConfirmationDialog.ShowInfo(canvasRoot, $"You found: {item.itemName} +1", 170, Populate);
    }

    private void LaunchBattle(MutationDungeonChoice choice)
    {
        if (mainMenuUI == null || AccountManager.Instance == null) return;

        if (!AccountManager.Instance.SpendEnergy(mainMenuUI.battleEnergyCost))
        {
            ConfirmationDialog.ShowInfo(canvasRoot, "Not enough energy to start a battle.", iconPath: "UI/Currency/Energy");
            return;
        }

        MutationDungeonManager.Instance.SelectNode(choice);
        AccountManager.Instance.pendingMutationDungeon = true;
        SceneManager.LoadScene(mainMenuUI.battleSceneName);
    }

    private void OnAbandonClicked()
    {
        ConfirmationDialog.Show(canvasRoot, "Abandon this run? Your heroes are safe either way, but you'll lose your current path progress.", () =>
        {
            MutationDungeonManager.Instance?.EndRun();
            Close();
        });
    }
}
