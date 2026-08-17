using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Общая карточка героя (450x600) — один источник вида и данных вместо прежней пары
// HeroCollectionCardUI/SquadSlotUI. Используется в Collection (инстанс масштабируется вниз
// до 150x200 отдельным контейнером — см. HeroCollectionUI.PopulateGrid) и в Squad (в натуральную
// величину — см. SquadUI). Inventory её не использует — там отдельная крупная вёрстка.
public class HeroMiniCardUI : MonoBehaviour
{
    [Header("Портрет и клик")]
    public Image portraitImage;
    public Button selectButton;

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

    [Header("Дивная броня — общий ассет WondrousArmorSkinSet на всю игру")]
    public Image wondrousArmorOverlay;
    public WondrousArmorSkinSet wondrousArmorSkinSet;

    [Header("Замок — используется только в Collection (герой ещё не открыт)")]
    public Image lockOverlay;

    [Header("Кнопка удаления — используется только в Squad")]
    public Button removeButton;

    public HeroData HeroData { get; private set; }

    // Фоновая Image карточки (та же, что selectButton.targetGraphic) — не публичное Inspector-поле,
    // читается напрямую с корневого GameObject, на котором сидит и этот компонент. Start(), а НЕ Awake() —
    // HeroCollectionUI.PopulateGrid уменьшает инстанс карточки localScale=0.5 ПОСЛЕ Instantiate(), уже
    // синхронно в том же кадре; Awake() успевает раньше этого масштабирования, и тень/скос копировали бы
    // ещё не применённый scale=1 (баг 2026-08-18 — тень оказалась вдвое крупнее самой карточки в Collection).
    private void Start()
    {
        CardDepthUtility.ApplyCardDepth(GetComponent<Image>());
    }

    public void Setup(HeroData data, HeroOwnershipData ownership)
    {
        HeroData = data;
        if (data == null) return;

        if (portraitImage != null) portraitImage.sprite = HeroAscensionUtility.GetDisplayPortrait(data, ownership);
        if (nameText != null) nameText.text = data.heroName;
        if (levelText != null) levelText.text = ownership != null ? $"Lv. {ownership.level}" : "";

        RarityUtility.ApplyFrame(rarityFrame, rarityFrameSet, data.rarity);
        HeroAscensionUtility.ApplyOverlay(ascensionOverlay, ascensionOverlaySet, data.rarity, ownership != null ? ownership.ascensionLevel : 0);
        WondrousArmorSkinSet.Apply(wondrousArmorOverlay, wondrousArmorSkinSet, data.heroId, ownership != null && ownership.wondrousArmorWorn);

        if (raceEmblem != null)
        {
            Sprite emblem = raceEmblemSet != null ? raceEmblemSet.GetEmblem(data.race) : null;
            raceEmblem.sprite = emblem;
            raceEmblem.enabled = emblem != null;
        }

        RefreshUpgradeBadge(data);
        SetLocked(false);
    }

    // "Тут есть что прокачать прямо сейчас" — тот же красный маркер, что у Castle (см.
    // NotificationBadgeUtility), только повешен на карточку героя. Общая карточка используется и в
    // Collection, и в Squad — бейдж автоматически появляется в обоих местах, отдельно ничего не подключал.
    private GameObject upgradeBadge;

    private void RefreshUpgradeBadge(HeroData data)
    {
        if (portraitImage == null) return;

        if (upgradeBadge == null)
            upgradeBadge = NotificationBadgeUtility.CreateAlertBadge(
                (RectTransform)portraitImage.transform, new Vector2(-6, -6), new Color(0.85f, 0.1f, 0.1f, 0.95f), "!");

        upgradeBadge.SetActive(HeroCollectionManager.Instance != null && HeroCollectionManager.Instance.HasActionableUpgrade(data));
    }

    // Только Collection — герой ещё не открыт: тёмная плашка поверх, клик выключен
    public void SetLocked(bool locked)
    {
        if (lockOverlay != null) lockOverlay.gameObject.SetActive(locked);
        if (selectButton != null) selectButton.interactable = !locked;
    }
}
