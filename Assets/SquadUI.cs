using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Отряд — фиксировано 4 слота (Бараки поднимают лимит веса, а не количество слотов, см.
// project_squad_system). Раньше слоты были вручную скопированными объектами в сцене, из-за чего
// они расходились по размеру/данным с карточкой коллекции (см. project_hero_card_unification_plan).
// Теперь оба экрана используют один и тот же префаб карточки (HeroMiniCard) — Squad просто
// инстанцирует его в натуральную величину внутри slotsContainer (GridLayoutGroup, cellSize 450x600).
public class SquadUI : MonoBehaviour
{
    public MainMenuUI mainMenuUI;
    public HeroInventoryUI inventoryUI; // общий попап на всю сцену — тот же, что и в HeroCollectionUI
    public Transform slotsContainer;
    public GameObject heroCardPrefab;

    private const int SlotCount = 4;

    // Рефреш вызывается явно из MainMenuUI.ShowSquad() (не отсюда через OnEnable) — тот единственный
    // реальный вход на этот экран и покрывает оба случая: панель только что активировалась, и повторный
    // клик по вкладке, когда она уже была активна (OnEnable тогда не срабатывает повторно, а обновить
    // всё равно надо). OnEnable+явный вызов вместе означали двойную пересборку карточек на каждый заход.
    public void RefreshSlots()
    {
        if (slotsContainer == null) return;

        foreach (Transform child in slotsContainer)
            Destroy(child.gameObject);

        var collectionManager = HeroCollectionManager.Instance;

        for (int i = 0; i < SlotCount; i++)
        {
            HeroData hero = collectionManager != null && i < collectionManager.squad.Count ? collectionManager.squad[i] : null;

            if (hero != null)
                BuildFilledSlot(i, hero, collectionManager);
            else
                BuildEmptySlot(i);
        }
    }

    private void BuildFilledSlot(int slotIndex, HeroData hero, HeroCollectionManager collectionManager)
    {
        GameObject cardObj = Instantiate(heroCardPrefab, slotsContainer);
        var card = cardObj.GetComponent<HeroMiniCardUI>();

        var ownership = collectionManager.ownership.Find(o => o.heroId == hero.heroId);
        card.Setup(hero, ownership);

        if (card.selectButton != null)
        {
            card.selectButton.onClick.RemoveAllListeners();
            card.selectButton.onClick.AddListener(() =>
            {
                if (inventoryUI != null)
                    inventoryUI.Open(hero);
            });
        }

        if (card.removeButton != null)
        {
            card.removeButton.gameObject.SetActive(true);
            card.removeButton.onClick.RemoveAllListeners();
            card.removeButton.onClick.AddListener(() =>
            {
                HeroCollectionManager.Instance.RemoveFromSquad(slotIndex);
                RefreshSlots();
            });
        }
    }

    // Пустой слот — свой лёгкий плейсхолдер вместо карточки (герою тут нечего показывать), клик
    // запускает выбор героя для этого слота через Collection (тот же приём, что и раньше).
    private void BuildEmptySlot(int slotIndex)
    {
        var obj = new GameObject("EmptySlot", typeof(RectTransform));
        obj.transform.SetParent(slotsContainer, false);

        var bg = obj.AddComponent<Image>();
        bg.color = new Color(1, 1, 1, 0.08f);

        var btn = obj.AddComponent<Button>();
        btn.onClick.AddListener(() => OnEmptySlotClicked(slotIndex));

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(obj.transform, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "+";
        text.fontSize = 120;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1, 1, 1, 0.4f);
    }

    private void OnEmptySlotClicked(int slotIndex)
    {
        if (HeroCollectionManager.Instance == null) return;

        HeroCollectionManager.Instance.StartEditingSlot(slotIndex);

        if (mainMenuUI != null)
            mainMenuUI.ShowCollection();
    }
}
