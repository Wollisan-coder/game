using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

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

    // Показатель мощи отряда + переключатель 5 сохранённых вариантов состава (см.
    // HeroCollectionManager.LoadoutCount/SwitchLoadout) — построены один раз программно поверх
    // заголовка "Squad" (тот же приём, что и FilterBar в HeroCollectionUI), т.к. панель размечена в
    // сцене без готовых мест под них.
    private TMP_Text powerText;
    private readonly List<Image> loadoutButtonImages = new List<Image>();
    private readonly List<TMP_Text> loadoutButtonTexts = new List<TMP_Text>();
    private bool headerBuilt;

    // Перестраиваем слоты, когда закрывается попап инвентаря героя — иначе левелап/вознесение/экипировка,
    // сделанные внутри попапа (в т.ч. бейдж "видел" — см. HeroCollectionManager.MarkUpgradeBadgeSeen), не
    // будут видны на карточках отряда, пока не сменить вкладку туда-обратно вручную. Тот же приём, что и
    // HeroCollectionUI.Start() (тот же общий inventoryUI, оба подписчика независимы).
    private void Awake()
    {
        if (inventoryUI != null)
            inventoryUI.OnClosed += RefreshSlots;
    }

    // Рефреш вызывается явно из MainMenuUI.ShowSquad() (не отсюда через OnEnable) — тот единственный
    // реальный вход на этот экран и покрывает оба случая: панель только что активировалась, и повторный
    // клик по вкладке, когда она уже была активна (OnEnable тогда не срабатывает повторно, а обновить
    // всё равно надо). OnEnable+явный вызов вместе означали двойную пересборку карточек на каждый заход.
    public void RefreshSlots()
    {
        if (slotsContainer == null) return;

        BuildHeaderIfNeeded();

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

        RefreshHeader(collectionManager);
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
        CardDepthUtility.ApplyCardDepth(bg); // тень + тёмный скос — иначе пустой слот визуально плоский рядом с занятыми (см. UX-правку 2026-08-18)

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

    // --- Мощь отряда + переключатель вариантов — построены один раз поверх заголовка "Squad"
    // (fileID заголовка в сцене: anchoredPosition (0,-50), sizeDelta (400,100), т.е. занимает Y от 0 до
    // -100 от верхнего края панели) — оба новых блока встают ниже него, слотов не касаются (slotsContainer
    // стоит намного ниже, см. project_squad_system).

    private void BuildHeaderIfNeeded()
    {
        if (headerBuilt) return;
        headerBuilt = true;

        var panelRect = slotsContainer.parent as RectTransform;
        if (panelRect == null) return;

        var powerObj = new GameObject("PowerText", typeof(RectTransform));
        var powerRect = (RectTransform)powerObj.transform;
        powerRect.SetParent(panelRect, false);
        powerRect.anchorMin = new Vector2(0.5f, 1);
        powerRect.anchorMax = new Vector2(0.5f, 1);
        powerRect.pivot = new Vector2(0.5f, 1);
        powerRect.sizeDelta = new Vector2(400, 50);
        powerRect.anchoredPosition = new Vector2(0, -110);
        powerText = powerObj.AddComponent<TextMeshProUGUI>();
        powerText.alignment = TextAlignmentOptions.Center;
        powerText.fontSize = 32;
        powerText.color = Color.white;

        var rowObj = new GameObject("LoadoutRow", typeof(RectTransform));
        var rowRect = (RectTransform)rowObj.transform;
        rowRect.SetParent(panelRect, false);
        rowRect.anchorMin = new Vector2(0.5f, 1);
        rowRect.anchorMax = new Vector2(0.5f, 1);
        rowRect.pivot = new Vector2(0.5f, 1);
        rowRect.sizeDelta = new Vector2(450, 70);
        rowRect.anchoredPosition = new Vector2(0, -170);

        var layout = rowObj.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        for (int i = 0; i < HeroCollectionManager.LoadoutCount; i++)
        {
            int loadoutIndex = i;
            var (img, text) = CreateLoadoutButton(rowRect, (i + 1).ToString(), () => OnLoadoutClicked(loadoutIndex));
            loadoutButtonImages.Add(img);
            loadoutButtonTexts.Add(text);
        }
    }

    private (Image, TMP_Text) CreateLoadoutButton(RectTransform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var btnObj = new GameObject("LoadoutButton_" + label, typeof(RectTransform));
        var btnRect = (RectTransform)btnObj.transform;
        btnRect.SetParent(parent, false);
        btnRect.sizeDelta = new Vector2(70, 60);

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
        text.fontSize = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
        text.alignment = TextAlignmentOptions.Center;
        text.color = ConfirmationDialog.ButtonTextColor;

        return (img, text);
    }

    private void OnLoadoutClicked(int loadoutIndex)
    {
        if (HeroCollectionManager.Instance == null) return;

        HeroCollectionManager.Instance.SwitchLoadout(loadoutIndex);
        RefreshSlots();
    }

    private void RefreshHeader(HeroCollectionManager collectionManager)
    {
        if (collectionManager == null) return;

        if (powerText != null)
            powerText.text = $"Power: {collectionManager.GetSquadPower()}";

        for (int i = 0; i < loadoutButtonImages.Count; i++)
        {
            bool active = i == collectionManager.activeLoadoutIndex;
            if (loadoutButtonImages[i] != null)
                loadoutButtonImages[i].color = active ? ConfirmationDialog.ButtonColor : new Color(1, 1, 1, 0.35f);
        }
    }
}
