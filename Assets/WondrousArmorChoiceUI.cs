using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Попап выбора героя для Дивной брони (куск 6 Death Dungeon, пересобран — см.
// project_gem_economy_v2_redesign_pending) — 3 случайных разблокированных героя, ЛЮБЫХ (пул больше не
// иссякает — см. HeroCollectionManager.DrawWondrousArmorCandidates), выбор одного добавляет ему неношеный
// инстанс скина (см. GrantWondrousArmorInstance) — надеть/распылить его можно во вкладке Consumables
// инвентаря предметов (см. ItemCollectionUI/WondrousArmorTileUI). Никакого расового бонуса — только
// squad-wide +5% при полном заскиненном отряде
// (см. HeroCollectionManager.GetWondrousArmorSquadBonus). Открывается из BattleManager после сезонной
// чистой победы гаунтлета — строится полностью в рантайме, та же схема, что и у DeathDungeonBuffChoiceUI.
public class WondrousArmorChoiceUI : MonoBehaviour
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

        overlayRoot = new GameObject("WondrousArmorChoiceOverlay", typeof(RectTransform));
        var overlayRect = (RectTransform)overlayRoot.transform;
        overlayRect.SetParent(canvasRoot, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        var dim = overlayRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.82f);
        // Намеренно без клика-мимо-закрытия — выбор обязателен, это разовая награда за сезон.

        var windowObj = new GameObject("Window", typeof(RectTransform));
        var windowRect = (RectTransform)windowObj.transform;
        windowRect.SetParent(overlayRect, false);
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(1000, 720);
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
        title.text = "The Dungeon Yields Wondrous Armor";
        title.fontSize = 36;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;

        var subtitleObj = new GameObject("Subtitle", typeof(RectTransform));
        var subtitleRect = (RectTransform)subtitleObj.transform;
        subtitleRect.SetParent(windowRect, false);
        subtitleRect.anchorMin = new Vector2(0, 1);
        subtitleRect.anchorMax = new Vector2(1, 1);
        subtitleRect.pivot = new Vector2(0.5f, 1);
        subtitleRect.sizeDelta = new Vector2(0, 40);
        subtitleRect.anchoredPosition = new Vector2(0, -90);
        var subtitle = subtitleObj.AddComponent<TextMeshProUGUI>();
        subtitle.text = "Choose who gets a new skin — dress your whole squad in Wondrous Armor for a squad-wide damage bonus.";
        subtitle.fontSize = 20;
        subtitle.alignment = TextAlignmentOptions.Center;
        subtitle.color = new Color(1, 1, 1, 0.75f);

        var optionsObj = new GameObject("Options", typeof(RectTransform));
        var optionsRect = (RectTransform)optionsObj.transform;
        optionsRect.SetParent(windowRect, false);
        optionsRect.anchorMin = new Vector2(0.5f, 0.5f);
        optionsRect.anchorMax = new Vector2(0.5f, 0.5f);
        optionsRect.pivot = new Vector2(0.5f, 0.5f);
        optionsRect.sizeDelta = new Vector2(3 * 280 + 2 * 24, 480);
        optionsRect.anchoredPosition = new Vector2(0, -50);
        var optionsLayout = optionsObj.AddComponent<HorizontalLayoutGroup>();
        optionsLayout.spacing = 24;
        optionsLayout.childForceExpandWidth = false;
        optionsLayout.childForceExpandHeight = false;
        optionsLayout.childControlWidth = true; // без этого карточки не получают размер из LayoutElement.preferredWidth/Height
        optionsLayout.childControlHeight = true;
        optionsLayout.childAlignment = TextAnchor.MiddleCenter;
        optionsContainer = optionsRect;

        overlayRoot.SetActive(false);
    }

    private void Populate()
    {
        foreach (Transform child in optionsContainer)
            Destroy(child.gameObject);

        var choices = HeroCollectionManager.Instance != null
            ? HeroCollectionManager.Instance.DrawWondrousArmorCandidates(ChoiceCount)
            : new List<HeroData>();

        foreach (var hero in choices)
            BuildOption(hero);
    }

    private void BuildOption(HeroData hero)
    {
        var cardObj = new GameObject(hero.heroName, typeof(RectTransform));
        var cardRect = (RectTransform)cardObj.transform;
        cardRect.SetParent(optionsContainer, false);
        var layoutElement = cardObj.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 280;
        layoutElement.preferredHeight = 480;

        var bg = cardObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsPanel(bg);
        CardDepthUtility.ApplyCardDepth(bg); // тень + тёмный скос, см. UX-правку 2026-08-18

        var btn = cardObj.AddComponent<Button>();
        btn.onClick.AddListener(() => OnOptionClicked(hero));

        var portraitObj = new GameObject("Portrait", typeof(RectTransform));
        var portraitRect = (RectTransform)portraitObj.transform;
        portraitRect.SetParent(cardRect, false);
        portraitRect.anchorMin = new Vector2(0, 1);
        portraitRect.anchorMax = new Vector2(1, 1);
        portraitRect.pivot = new Vector2(0.5f, 1);
        portraitRect.sizeDelta = new Vector2(-20, 280);
        portraitRect.anchoredPosition = new Vector2(0, -16);
        var portraitImg = portraitObj.AddComponent<Image>();
        portraitImg.sprite = hero.portrait;
        portraitImg.preserveAspect = true;

        var nameObj = new GameObject("Name", typeof(RectTransform));
        var nameRect = (RectTransform)nameObj.transform;
        nameRect.SetParent(cardRect, false);
        nameRect.anchorMin = new Vector2(0, 1);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.pivot = new Vector2(0.5f, 1);
        nameRect.sizeDelta = new Vector2(-20, 50);
        nameRect.anchoredPosition = new Vector2(0, -304);
        var nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.text = hero.heroName;
        nameText.fontStyle = FontStyles.Bold;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = hero.GetRarityColor();
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 18;
        nameText.fontSizeMax = 26;

        var raceObj = new GameObject("Race", typeof(RectTransform));
        var raceRect = (RectTransform)raceObj.transform;
        raceRect.SetParent(cardRect, false);
        raceRect.anchorMin = new Vector2(0, 0);
        raceRect.anchorMax = new Vector2(1, 1);
        raceRect.offsetMin = new Vector2(20, 20);
        raceRect.offsetMax = new Vector2(-20, -360);
        var raceText = raceObj.AddComponent<TextMeshProUGUI>();
        // Race.None — Blue-редкость без расы (см. UX-правку 2026-08-19) — пустая строка вместо буквального "None"
        raceText.text = hero.race == Race.None ? "" : hero.race.ToString();
        raceText.alignment = TextAlignmentOptions.Center;
        raceText.color = new Color(1, 1, 1, 0.9f);
        raceText.enableAutoSizing = true;
        raceText.fontSizeMin = 14;
        raceText.fontSizeMax = 20;
    }

    private void OnOptionClicked(HeroData hero)
    {
        HeroCollectionManager.Instance?.GrantWondrousArmorInstance(hero.heroId);
        overlayRoot.SetActive(false);
        onChosen?.Invoke();
    }
}
