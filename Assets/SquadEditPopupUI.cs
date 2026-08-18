using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Попап пересборки отряда — открывается кликом по любому портрету в Squad (см. SquadUI.OpenSquadEditPopup).
// Весь состав отряда собирается за один заход, без ухода с экрана Squad (в отличие от прежнего варианта
// через вкладку Collection — см. правку 2026-08-19: пользователь попросил вернуть как было и сделать
// отдельным попапом вместо навигации между экранами). Строится целиком в рантайме (как ConfirmationDialog/
// SummonRevealUI) — в сцене для него ничего не размечено, heroCardPrefab передаётся при Open() (тот же,
// что уже wired на SquadUI/HeroCollectionUI — так новому попапу не нужна собственная Inspector-ссылка).
public class SquadEditPopupUI : MonoBehaviour
{
    public event Action OnApplied;

    private const float PopupSize = 1000f;
    private const float CardWidth = 225f;  // тот же множитель 0.5 от базовых 450x600, что и HeroCollectionUI.PopulateGrid — "как в коллекции"
    private const float CardHeight = 300f;
    private const float CardScale = 0.5f;

    private static readonly Color SelectedGreen = new Color(0.25f, 0.85f, 0.35f);

    private GameObject heroCardPrefab;
    private GameObject root;
    private Transform canvasRoot;
    private RectTransform windowRect;
    private RectTransform slotsRow;
    private TMP_Text powerText;
    private RectTransform gridContent;

    private List<HeroData> pending;
    private readonly Dictionary<string, Transform> gridCardsByHeroId = new Dictionary<string, Transform>();

    public void Open(GameObject prefab)
    {
        var collectionManager = HeroCollectionManager.Instance;
        if (collectionManager == null || prefab == null) return;

        heroCardPrefab = prefab;
        pending = new List<HeroData>(collectionManager.squad);
        while (pending.Count < collectionManager.MaxSquadSize) pending.Add(null);

        if (root == null) Build();
        root.SetActive(true);
        root.transform.SetAsLastSibling();

        BuildGrid(collectionManager);
        RefreshTopSection();
    }

    private void Close()
    {
        if (root != null) root.SetActive(false);
    }

    private void OnApplyClicked()
    {
        HeroCollectionManager.Instance?.SetSquad(pending);
        OnApplied?.Invoke();
        Close();
    }

    // --- Постройка статичной оболочки (окно/заголовок/слоты-контейнер/скролл/кнопки) — один раз ---

    private void Build()
    {
        var canvas = GetComponentInParent<Canvas>();
        canvasRoot = canvas != null ? canvas.transform : transform;

        root = new GameObject("SquadEditPopupRoot", typeof(RectTransform));
        var overlayRect = (RectTransform)root.transform;
        overlayRect.SetParent(canvasRoot, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        var dim = root.AddComponent<Image>();
        dim.color = new Color(0, 0, 0, 0.6f);
        var dimBtn = root.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(Close); // клик мимо окна = отмена (тот же приём, что и race-попап в HeroCollectionUI)

        var windowObj = new GameObject("Window", typeof(RectTransform));
        windowRect = (RectTransform)windowObj.transform;
        windowRect.SetParent(overlayRect, false);
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(PopupSize, PopupSize);
        windowRect.anchoredPosition = Vector2.zero;

        var windowBg = windowObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsPanel(windowBg);
        var windowBlocker = windowObj.AddComponent<Button>();
        windowBlocker.transition = Selectable.Transition.None; // клик по самому окну не должен проваливаться на dimBtn

        CreateText(windowRect, new Vector2(0, -30), new Vector2(400, 50), "Edit Squad", 36, FontStyles.Bold);
        powerText = CreateText(windowRect, new Vector2(0, -90), new Vector2(400, 40), "Power: 0", 28, FontStyles.Normal);

        var slotsRowObj = new GameObject("SlotsRow", typeof(RectTransform));
        slotsRow = (RectTransform)slotsRowObj.transform;
        slotsRow.SetParent(windowRect, false);
        slotsRow.anchorMin = new Vector2(0.5f, 1);
        slotsRow.anchorMax = new Vector2(0.5f, 1);
        slotsRow.pivot = new Vector2(0.5f, 1);
        slotsRow.sizeDelta = new Vector2(PopupSize - 40, CardHeight);
        slotsRow.anchoredPosition = new Vector2(0, -140);
        var slotsLayout = slotsRowObj.AddComponent<HorizontalLayoutGroup>();
        slotsLayout.spacing = 15;
        slotsLayout.childAlignment = TextAnchor.MiddleCenter;
        slotsLayout.childControlWidth = false;
        slotsLayout.childControlHeight = false;
        slotsLayout.childForceExpandWidth = false;
        slotsLayout.childForceExpandHeight = false;

        CreateText(windowRect, new Vector2(0, -455), new Vector2(400, 40), "Available Heroes", 26, FontStyles.Bold);

        var scrollObj = new GameObject("ScrollView", typeof(RectTransform));
        var scrollRectTransform = (RectTransform)scrollObj.transform;
        scrollRectTransform.SetParent(windowRect, false);
        scrollRectTransform.anchorMin = Vector2.zero;
        scrollRectTransform.anchorMax = Vector2.one;
        scrollRectTransform.offsetMin = new Vector2(20, 110);   // низ — место под кнопки
        scrollRectTransform.offsetMax = new Vector2(-20, -495); // верх — место под заголовок/статы/слоты/подпись

        var scrollRect = scrollObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        var viewportObj = new GameObject("Viewport", typeof(RectTransform));
        var viewportRect = (RectTransform)viewportObj.transform;
        viewportRect.SetParent(scrollRectTransform, false);
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewportObj.AddComponent<RectMask2D>();

        var contentObj = new GameObject("Content", typeof(RectTransform));
        gridContent = (RectTransform)contentObj.transform;
        gridContent.SetParent(viewportRect, false);
        gridContent.anchorMin = new Vector2(0, 1);
        gridContent.anchorMax = new Vector2(1, 1);
        gridContent.pivot = new Vector2(0.5f, 1);
        gridContent.anchoredPosition = Vector2.zero;

        var gridLayout = contentObj.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(CardWidth, CardHeight);
        gridLayout.spacing = new Vector2(15, 15);
        gridLayout.padding = new RectOffset(10, 10, 10, 10);
        gridLayout.childAlignment = TextAnchor.UpperCenter;

        var contentFitter = contentObj.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRect;
        scrollRect.content = gridContent;

        CreateButton(windowRect, new Vector2(-140, 50), "Cancel", Close);
        CreateButton(windowRect, new Vector2(140, 50), "Apply", OnApplyClicked);

        root.SetActive(false);
    }

    // --- Верхний отсек: 4 слота отряда (живой предпросмотр pending) + суммарная мощь ---

    private void RefreshTopSection()
    {
        for (int i = slotsRow.childCount - 1; i >= 0; i--)
        {
            var child = slotsRow.GetChild(i);
            child.SetParent(null);
            Destroy(child.gameObject);
        }

        var collectionManager = HeroCollectionManager.Instance;
        for (int i = 0; i < pending.Count; i++)
        {
            if (pending[i] != null)
            {
                var (wrapper, card) = CreateCardWrapper(slotsRow);
                var ownership = collectionManager.ownership.Find(o => o.heroId == pending[i].heroId);
                card.Setup(pending[i], ownership);
            }
            else
            {
                CreateEmptySlotPreview(slotsRow);
            }
        }

        int power = pending.Where(h => h != null)
            .Sum(h => HeroStatUtility.CalculatePower(h, collectionManager.ownership.FirstOrDefault(o => o.heroId == h.heroId)));
        powerText.text = $"Power: {power}";
    }

    private void CreateEmptySlotPreview(Transform parent)
    {
        var obj = new GameObject("EmptySlot", typeof(RectTransform));
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(CardWidth, CardHeight);

        var bg = obj.AddComponent<Image>();
        bg.color = new Color(1, 1, 1, 0.08f);

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "+";
        text.fontSize = 60;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1, 1, 1, 0.4f);
    }

    // --- Нижний отсек: все доступные герои, тот же вид, что и в коллекции, с зелёной рамкой у выбранных ---

    private void BuildGrid(HeroCollectionManager collectionManager)
    {
        for (int i = gridContent.childCount - 1; i >= 0; i--)
        {
            var child = gridContent.GetChild(i);
            child.SetParent(null);
            Destroy(child.gameObject);
        }
        gridCardsByHeroId.Clear();

        foreach (var hero in collectionManager.allHeroes)
        {
            if (hero == null) continue;

            var (wrapper, card) = CreateCardWrapper(gridContent);
            var ownership = collectionManager.ownership.Find(o => o.heroId == hero.heroId);
            card.Setup(hero, ownership);
            card.SetLocked(!collectionManager.IsUnlocked(hero));

            if (card.selectButton != null)
            {
                card.selectButton.onClick.RemoveAllListeners();
                card.selectButton.onClick.AddListener(() => OnGridCardClicked(hero, wrapper));
            }

            gridCardsByHeroId[hero.heroId] = wrapper;
            SetCardSelected(wrapper, pending.Any(h => h != null && h.heroId == hero.heroId));
        }
    }

    private void OnGridCardClicked(HeroData hero, Transform wrapper)
    {
        var collectionManager = HeroCollectionManager.Instance;
        if (collectionManager == null) return;

        int existingIndex = pending.FindIndex(h => h != null && h.heroId == hero.heroId);

        if (existingIndex >= 0)
        {
            pending[existingIndex] = null;
            SetCardSelected(wrapper, false);
        }
        else
        {
            int emptyIndex = pending.FindIndex(h => h == null);
            if (emptyIndex < 0)
            {
                ShowInfo("Squad is full — remove someone first.");
                return;
            }

            int projectedWeight = pending.Where(h => h != null).Sum(h => h.weight) + hero.weight;
            if (projectedWeight > collectionManager.MaxSquadWeight)
            {
                ShowInfo($"Not enough squad weight capacity ({projectedWeight}/{collectionManager.MaxSquadWeight}).\nUpgrade Barracks to fit this hero.");
                return;
            }

            pending[emptyIndex] = hero;
            SetCardSelected(wrapper, true);
        }

        RefreshTopSection();
    }

    private void ShowInfo(string message)
    {
        if (canvasRoot != null) ConfirmationDialog.ShowInfo(canvasRoot, message);
    }

    // Зелёная рамка поверх карточки — отдельный дочерний объект, создаётся один раз лениво и дальше
    // просто включается/выключается, а не пересоздаётся на каждый тогл.
    private void SetCardSelected(Transform cardWrapper, bool selected)
    {
        var existing = cardWrapper.Find("SelectedFrame");
        if (existing == null)
        {
            if (!selected) return;

            var frameObj = new GameObject("SelectedFrame", typeof(RectTransform));
            var frameRect = (RectTransform)frameObj.transform;
            frameRect.SetParent(cardWrapper, false);
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = new Vector2(-8, -8);
            frameRect.offsetMax = new Vector2(8, 8);

            var img = frameObj.AddComponent<Image>();
            img.sprite = ItemBadgeUtility.GetBorderSprite();
            img.type = Image.Type.Sliced;
            img.color = SelectedGreen;
            img.raycastTarget = false;
            return;
        }

        existing.gameObject.SetActive(selected);
    }

    // --- Общие билдеры ---

    // Обёртка фиксированного размера (CardWidth x CardHeight) + внутри — инстанс heroCardPrefab, уменьшенный
    // localScale. Обёртка нужна потому, что и GridLayoutGroup, и HorizontalLayoutGroup сами выставляют
    // sizeDelta прямому потомку — если масштабировать сам инстанс карточки напрямую, лэйаут тут же вернёт
    // его к исходному размеру (тот же приём, что и HeroCollectionUI.PopulateGrid).
    private (RectTransform wrapper, HeroMiniCardUI card) CreateCardWrapper(Transform parent)
    {
        var wrapperObj = new GameObject("CardSlot", typeof(RectTransform));
        var wrapperRect = (RectTransform)wrapperObj.transform;
        wrapperRect.SetParent(parent, false);
        wrapperRect.sizeDelta = new Vector2(CardWidth, CardHeight);

        GameObject cardObj = Instantiate(heroCardPrefab, wrapperRect);
        var cardRect = (RectTransform)cardObj.transform;
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.localScale = new Vector3(CardScale, CardScale, 1f);

        return (wrapperRect, cardObj.GetComponent<HeroMiniCardUI>());
    }

    private TMP_Text CreateText(RectTransform parent, Vector2 anchoredPosition, Vector2 size, string content, int fontSize, FontStyles style)
    {
        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(parent, false);
        textRect.anchorMin = new Vector2(0.5f, 1);
        textRect.anchorMax = new Vector2(0.5f, 1);
        textRect.pivot = new Vector2(0.5f, 1);
        textRect.sizeDelta = size;
        textRect.anchoredPosition = anchoredPosition;

        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        return text;
    }

    private void CreateButton(RectTransform parent, Vector2 anchoredPosition, string label, UnityEngine.Events.UnityAction onClick)
    {
        var btnObj = new GameObject(label + "Button", typeof(RectTransform));
        var btnRect = (RectTransform)btnObj.transform;
        btnRect.SetParent(parent, false);
        btnRect.anchorMin = new Vector2(0.5f, 0);
        btnRect.anchorMax = new Vector2(0.5f, 0);
        btnRect.pivot = new Vector2(0.5f, 0);
        btnRect.sizeDelta = new Vector2(260, 80);
        btnRect.anchoredPosition = anchoredPosition;

        var img = btnObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(img);
        var btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(btnRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 32;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = ConfirmationDialog.ButtonTextColor;
    }
}
