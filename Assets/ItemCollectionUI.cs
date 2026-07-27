using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemCollectionUI : MonoBehaviour
{
    public ItemCollectionManager collectionManager;
    public Transform gridContainer;
    public GameObject itemCardPrefab;
    public ItemDetailUI detailUI;

    private const float TitleReservedHeight = 100f; // висота існуючого заголовка панелі (щоб вкладки не наїжджали на нього)
    private const float TabBarHeight = 40f;
    private const float TabBarTopGap = 4f;

    private static readonly Color TabInactiveColor = new Color(1f, 1f, 1f, 0.08f);

    // Категорії — по аналогії з попапом сфер досвіду (HeroExperienceItemPickerUI), але вкладками зверху цієї ж панелі
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

        // Перебудовуємо сітку, коли закривається попап деталей предмета — інакше апгрейд/використання,
        // зроблені всередині попапу, не будуть видні в каталозі, поки вкладку не перемкнути вручну.
        if (detailUI != null)
            detailUI.OnClosed += PopulateGrid;
    }

    private void Start()
    {
        PopulateGrid();
    }

    // Будує ряд кнопок-вкладок над сіткою і звільняє під нього місце, підрізаючи Scroll View зверху.
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
            int index = i; // захоплюємо копію для замикання
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
        foreach (Transform child in gridContainer)
            Destroy(child.gameObject);

        // Сортуємо за рідкістю (White -> Orange), в межах однієї рідкості — за назвою
        var sortedItems = collectionManager.allItems
            .Where(MatchesCurrentTab)
            .OrderBy(i => (int)i.rarity).ThenBy(i => i.itemName);

        var heroManager = HeroCollectionManager.Instance;

        foreach (var item in sortedItems)
        {
            var allStacks = collectionManager.GetStacks(item.itemId);

            if (allStacks.Count == 0)
            {
                // Жодної копії не отримано — одна заблокована картка-плейсхолдер
                CreateCard(item, null);
                continue;
            }

            // Екіпіровані на герої стеки в інвентарі не показуємо — предмет "зайнятий"
            var visibleStacks = heroManager != null
                ? allStacks.Where(s => !heroManager.IsItemEquippedAnywhere(s.instanceId)).ToList()
                : allStacks;

            // Усі наявні копії зараз екіпіровані — картку взагалі не показуємо (вона не "заблокована", просто зайнята)
            if (visibleStacks.Count == 0) continue;

            // Кожен стек (окремий рівень) — окрема картка, щоб предмети різного рівня не зливались в одну ячейку
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
