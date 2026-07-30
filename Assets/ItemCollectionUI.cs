using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemCollectionUI : MonoBehaviour
{
    public Transform gridContainer;
    public GameObject itemCardPrefab;
    public ItemDetailUI detailUI;

    private const float TitleReservedHeight = 100f; // высота существующего заголовка панели (чтобы вкладки не наезжали на него)
    private const float TabBarHeight = 40f;
    private const float TabBarTopGap = 4f;

    private static readonly Color TabInactiveColor = new Color(1f, 1f, 1f, 0.08f);

    // Категории — по аналогии с попапом сфер опыта (HeroExperienceItemPickerUI), но вкладками сверху этой же панели
    private readonly (string label, ItemCategory? category, EquipmentSlotType? slot)[] tabs =
    {
        ("All", null, null),
        ("Weapon", ItemCategory.Equipment, EquipmentSlotType.Weapon),
        ("Armor", ItemCategory.Equipment, EquipmentSlotType.Armor),
        ("Accessory", ItemCategory.Equipment, EquipmentSlotType.Accessory),
        ("Trinket", ItemCategory.Equipment, EquipmentSlotType.Trinket),
        ("Consumable", ItemCategory.HeroExperience, null),
    };

    private readonly List<Image> tabBackgrounds = new List<Image>();
    private int selectedTabIndex = 0;

    private void Awake()
    {
        BuildTabBar();

        // Перестраиваем сетку, когда закрывается попап деталей предмета — иначе апгрейд/использование,
        // сделанные внутри попапа, не будут видны в каталоге, пока вкладку не переключить вручную.
        if (detailUI != null)
            detailUI.OnClosed += PopulateGrid;
    }

    private void Start()
    {
        PopulateGrid();
    }

    // Строит ряд кнопок-вкладок над сеткой и освобождает под него место, подрезая Scroll View сверху.
    private void BuildTabBar()
    {
        var panelRect = (RectTransform)transform;

        var scrollRect = gridContainer.GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
        {
            var scrollRectTransform = scrollRect.GetComponent<RectTransform>();
            float shrink = TitleReservedHeight + TabBarTopGap + TabBarHeight;
            scrollRectTransform.offsetMax = new Vector2(scrollRectTransform.offsetMax.x, scrollRectTransform.offsetMax.y - shrink);
        }

        var barObj = new GameObject("CategoryTabs", typeof(RectTransform));
        var barRect = (RectTransform)barObj.transform;
        barRect.SetParent(panelRect, false);
        barRect.anchorMin = new Vector2(0, 1);
        barRect.anchorMax = new Vector2(1, 1);
        barRect.pivot = new Vector2(0.5f, 1);
        barRect.sizeDelta = new Vector2(-40, TabBarHeight);
        barRect.anchoredPosition = new Vector2(0, -(TitleReservedHeight + TabBarTopGap));

        var layout = barObj.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i; // захватываем копию для замыкания
            var tabObj = new GameObject(tabs[i].label, typeof(RectTransform));
            var tabRect = (RectTransform)tabObj.transform;
            tabRect.SetParent(barRect, false);

            var bg = tabObj.AddComponent<Image>();
            var btn = tabObj.AddComponent<Button>();
            btn.onClick.AddListener(() => SelectTab(index));

            var textObj = new GameObject("Text", typeof(RectTransform));
            var textRect = (RectTransform)textObj.transform;
            textRect.SetParent(tabRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = tabs[i].label;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 14;
            text.color = ConfirmationDialog.ButtonTextColor;

            tabBackgrounds.Add(bg);
        }

        RefreshTabVisuals();
    }

    private void SelectTab(int index)
    {
        selectedTabIndex = index;
        RefreshTabVisuals();
        PopulateGrid();
    }

    private void RefreshTabVisuals()
    {
        for (int i = 0; i < tabBackgrounds.Count; i++)
            tabBackgrounds[i].color = i == selectedTabIndex ? ConfirmationDialog.ButtonColor : TabInactiveColor;
    }

    private bool MatchesCurrentTab(ItemData item)
    {
        var (_, category, slot) = tabs[selectedTabIndex];
        if (category == null) return true; // вкладка "All"
        if (item.category != category.Value) return false;
        if (slot.HasValue && item.slotType != slot.Value) return false;
        return true;
    }

    private void PopulateGrid()
    {
        var collectionManager = ItemCollectionManager.Instance;
        if (collectionManager == null) return;

        foreach (Transform child in gridContainer)
            Destroy(child.gameObject);

        // Сортируем по редкости (White -> Orange), в пределах одной редкости — по названию
        var sortedItems = collectionManager.allItems
            .Where(MatchesCurrentTab)
            .OrderBy(i => (int)i.rarity).ThenBy(i => i.itemName);

        var heroManager = HeroCollectionManager.Instance;

        foreach (var item in sortedItems)
        {
            var allStacks = collectionManager.GetStacks(item.itemId);

            if (allStacks.Count == 0)
            {
                // Ни одной копии не получено — одна заблокированная карточка-плейсхолдер
                CreateCard(item, null);
                continue;
            }

            // Экипированные на герое стеки в инвентаре не показываем — предмет "занят"
            var visibleStacks = heroManager != null
                ? allStacks.Where(s => !heroManager.IsItemEquippedAnywhere(s.instanceId)).ToList()
                : allStacks;

            // Все имеющиеся копии сейчас экипированы — карточку вообще не показываем (она не "заблокирована", просто занята)
            if (visibleStacks.Count == 0) continue;

            // Каждый стек (отдельный уровень) — отдельная карточка, чтобы предметы разного уровня не сливались в одну ячейку
            foreach (var stack in visibleStacks)
                CreateCard(item, stack);
        }
    }

    private void CreateCard(ItemData item, ItemOwnershipData stack)
    {
        GameObject cardObj = Instantiate(itemCardPrefab, gridContainer);
        var card = cardObj.GetComponent<ItemCollectionCardUI>();
        card.Setup(item, stack, detailUI);
    }
}
