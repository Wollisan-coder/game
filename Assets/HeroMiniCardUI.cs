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

    [Header("Замок — используется только в Collection (герой ещё не открыт)")]
    public Image lockOverlay;

    [Header("Кнопка удаления — используется только в Squad")]
    public Button removeButton;

    public HeroData HeroData { get; private set; }

    public void Setup(HeroData data, HeroOwnershipData ownership)
    {
        HeroData = data;
        if (data == null) return;

        if (portraitImage != null) portraitImage.sprite = HeroAscensionUtility.GetDisplayPortrait(data, ownership);
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

        SetLocked(false);
    }

    // Только Collection — герой ещё не открыт: тёмная плашка поверх, клик выключен
    public void SetLocked(bool locked)
    {
        if (lockOverlay != null) lockOverlay.gameObject.SetActive(locked);
        if (selectButton != null) selectButton.interactable = !locked;
    }
}
