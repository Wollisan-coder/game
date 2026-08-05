using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SquadSlotUI : MonoBehaviour
{
    public int slotIndex;

    public Image portrait;
    public Button removeButton;
    public Button selectButton;
    public GameObject emptyPlaceholder;

    [Header("Имя и уровень")]
    public TMP_Text nameText;
    public TMP_Text levelText;

    [Header("Рамка редкости — общий ассет RarityFrameSet на всю игру")]
    public Image rarityFrame;
    public RarityFrameSet rarityFrameSet;

    [Header("Эмблема расы — общий ассет RaceEmblemSet на всю игру")]
    public Image raceEmblem;
    public RaceEmblemSet raceEmblemSet;

    [Header("Вознесение — общий ассет AscensionOverlaySet на всю игру")]
    public Image ascensionOverlay;
    public AscensionOverlaySet ascensionOverlaySet;

    private HeroData heroData;
    private SquadUI parentUI;
    private HeroInventoryUI inventoryUI;

    // Вызывается один раз при инициализации слотов в SquadUI
    public void Initialize(SquadUI squadUI, HeroInventoryUI inventory)
    {
        parentUI = squadUI;
        inventoryUI = inventory;
    }

    public void SetHero(HeroData data, SquadUI squadUI)
    {
        heroData = data;
        parentUI = squadUI;

        portrait.gameObject.SetActive(true);
        var ownership = HeroCollectionManager.Instance != null
            ? HeroCollectionManager.Instance.ownership.Find(o => o.heroId == data.heroId)
            : null;
        portrait.sprite = HeroAscensionUtility.GetDisplayPortrait(data, ownership);
        emptyPlaceholder.SetActive(false);

        removeButton.gameObject.SetActive(true);
        removeButton.onClick.RemoveAllListeners();
        removeButton.onClick.AddListener(OnRemoveClicked);

        if (nameText != null) nameText.text = data.heroName;
        if (levelText != null) levelText.text = ownership != null ? $"Lv. {ownership.level}" : "";

        RarityUtility.ApplyFrame(rarityFrame, rarityFrameSet, data.rarity);
        HeroAscensionUtility.ApplyOverlay(ascensionOverlay, ascensionOverlaySet, data.rarity, ownership != null ? ownership.ascensionLevel : 0);

        if (raceEmblem != null)
        {
            Sprite emblem = raceEmblemSet != null ? raceEmblemSet.GetEmblem(data.race) : null;
            raceEmblem.sprite = emblem;
            raceEmblem.enabled = emblem != null;
        }
    }

    public void SetEmpty()
    {
        heroData = null;
        portrait.gameObject.SetActive(false);
        emptyPlaceholder.SetActive(true);
        removeButton.gameObject.SetActive(false);

        if (nameText != null) nameText.text = "";
        if (levelText != null) levelText.text = "";
        if (rarityFrame != null) rarityFrame.enabled = false;
        if (raceEmblem != null) raceEmblem.enabled = false;
        if (ascensionOverlay != null) ascensionOverlay.enabled = false;
    }

    private void Awake()
    {
        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnSelectClicked);
        }
    }

    private void OnSelectClicked()
    {
        // Если в слоте уже стоит герой — клик по картинке открывает его инвентарь (экипировку), а не выбор нового
        if (heroData != null)
        {
            if (inventoryUI != null)
                inventoryUI.Open(heroData);
            else
                Debug.LogWarning("SquadSlotUI: inventoryUI не призначено!");
            return;
        }

        HeroCollectionManager.Instance.StartEditingSlot(slotIndex);

        if (parentUI != null && parentUI.mainMenuUI != null)
            parentUI.mainMenuUI.ShowCollection();
        else
            Debug.LogWarning("SquadSlotUI: parentUI або mainMenuUI не призначено!");
    }

    private void OnRemoveClicked()
    {
        HeroCollectionManager.Instance.RemoveFromSquad(slotIndex);
        if (parentUI != null)
            parentUI.RefreshSlots();
    }
}