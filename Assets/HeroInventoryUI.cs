using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Video;
using TMPro;

public class HeroInventoryUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Карточка героя")]
    public Image portraitImage;
    public TMP_Text heroNameText;
    public TMP_Text healthText;
    public TMP_Text levelText;
    public TMP_Text descriptionText;

    [Header("Рамка редкости — тёмная плашка + светящийся кант + аббревиатура ранга, см. RarityUtility")]
    public Image rarityFrame;
    private Image rarityKantGlow;
    private TMP_Text rarityTierLabel;

    [Header("Эмблема расы — общий ассет RaceEmblemSet на всю игру")]
    public Image raceEmblem;
    public RaceEmblemSet raceEmblemSet;

    [Header("Вознесение — общий ассет AscensionOverlaySet на всю игру")]
    public Image ascensionOverlay;
    public AscensionOverlaySet ascensionOverlaySet;
    public Button ascendButton;  // клик по самому значку — тратит гемы, поднимает потолок уровня
    public Image ascendOutline;  // тонкое кольцо вокруг значка, видно пока вознесение вообще доступно (relevant)
    public Image ascendGlow;     // мягкое пятно позади значка — маленькое и статичное, пока просто "доступно";
                                  // крупнее и мерцает, когда гемов хватает прямо сейчас (см. RefreshAscendButton)

    [Header("Дивная броня — общий ассет WondrousArmorSkinSet на всю игру")]
    public Image wondrousArmorOverlay;
    public WondrousArmorSkinSet wondrousArmorSkinSet;

    [Header("Пассивка расы — вкл/выкл за RacePassiveUtility.ManaCost маны, см. BattleManager")]
    public TMP_Text racePassiveInfoText;
    public Button racePassiveToggleButton;
    public Image racePassiveToggleHighlight; // необязательно — подсветка "включено", как у passiveSkillHighlights

    [Header("Статы (база героя + бонусы от текущей экипировки) — справа под слотами предметов")]
    public TMP_Text statsText;

    [Header("Навыки — вкладки активного (4 кнопки, по индексу heroData.skills[i])")]
    public Button[] activeSkillTabs;
    public Image[] activeSkillHighlights; // подсветка выбранной вкладки, тот же порядок (необязательно)

    [Header("Навыки — кнопки выбора пассивного (4, тот же индекс; может совпадать с активным)")]
    public Button[] passiveSkillButtons;
    public Image[] passiveSkillHighlights; // необязательно

    [Header("Инфо — оба видны одновременно, каждый под свой текущий выбор (не только по клику)")]
    public TMP_Text activeSkillInfoText;  // мана + описание текущего активного навыка
    public TMP_Text passiveSkillInfoText; // мана + описание текущего пассивного навыка (пусто, если не выбран)

    [Header("Фон под инфо-блоки — панель под текст описания, см. ConfirmationDialog.StyleAsDescriptionPanel")]
    public Image activeSkillInfoBg;
    public Image passiveSkillInfoBg;
    public Image racePassiveInfoBg;

    [Header("Скролл описаний (необязательно) — сбрасывается наверх при смене навыка")]
    public ScrollRect activeSkillInfoScroll;
    public ScrollRect passiveSkillInfoScroll;

    [Header("Инвентарь (предметы)")]
    public Transform itemsContainer;
    public GameObject itemSlotPrefab; // слот с компонентом ItemSlotUI
    public ItemPickerUI itemPicker;

    [Header("Своя картинка пустого слота на каждый тип экипировки (иначе — общий спрайт с itemSlotPrefab)")]
    public Sprite emptySlotWeaponSprite;
    public Sprite emptySlotArmorSprite;
    public Sprite emptySlotAccessorySprite;
    public Sprite emptySlotTrinketSprite;

    private static readonly EquipmentSlotType[] AllSlotTypes =
    {
        EquipmentSlotType.Weapon,
        EquipmentSlotType.Armor,
        EquipmentSlotType.Accessory,
        EquipmentSlotType.Trinket
    };

    [Header("Кнопка закрытия")]
    public Button closeButton;

    // Один "Upgrade", открывающий HeroUpgradeUI (отложенный превью уровня + подтверждение) — заменил собой
    // 3 инлайн-кнопки +1/+10/Max прямого списания (было 2026-08-12..2026-08-17), по прямой просьбе
    // пользователя после живого просмотра: экран прокачки должен показывать было/стало по уровню и
    // отдельное окно, а не тратить валюту сразу по тапу.
    private Button heroUpgradeButton;
    private Image heroUpgradeBg;
    private TMP_Text heroUpgradeText;
    private HeroUpgradeUI heroUpgradeUI;

    // Ручной выбор портрета для маркера карты мира (см. HeroCollectionManager.mapAvatarHeroId /
    // PlayerMapMarkerUI) — кнопка видна только для героя на максимальном вознесении, тот же стек, ещё
    // на шаг выше Upgrade.
    private Button mapAvatarButton;
    private TMP_Text mapAvatarButtonText;

    private const float SwipeThreshold = 80f; // минимальная длина свайпа по X (пиксели), чтобы засчитать переключение героя
    private const float SkillButtonTextPadding = 24f; // запас по ширине сверх самого текста названия навыка

    private HeroData currentHero;
    private HeroOwnershipData currentOwnership;

    // Исходная (заданная в редакторе) ширина каждой кнопки — растём только сверх неё, никогда не сжимаем меньше
    private float[] activeSkillTabBaseWidths;
    private float[] passiveSkillButtonBaseWidths;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);

            // Кнопка "назад" в сцене осталась дефолтной (белый UISprite, текст "Button" из стандартного
            // Unity Button) — приводим к тому же золотому стилю, что Equip Best/Upgrade на этом же экране.
            var closeBg = closeButton.GetComponent<Image>();
            if (closeBg != null) ConfirmationDialog.StyleAsButton(closeBg);
            var closeLabel = closeButton.GetComponentInChildren<TMP_Text>();
            if (closeLabel != null) closeLabel.text = "Back";
        }

        if (racePassiveToggleButton != null)
            racePassiveToggleButton.onClick.AddListener(OnRacePassiveToggleClicked);

        if (ascendButton != null)
            ascendButton.onClick.AddListener(OpenAscendPopup);

        if (ascendGlow != null)
        {
            ascendGlow.sprite = GetRadialGlowSprite();
            ascendGlow.raycastTarget = false;
        }

        if (ascendOutline != null)
        {
            ascendOutline.sprite = GetSquareOutlineSprite();
            ascendOutline.type = Image.Type.Sliced;
            ascendOutline.color = new Color(1f, 0.9f, 0.55f, 1f);
            ascendOutline.raycastTarget = false;
            AlignAscendOutlineToButton();
        }

        if (activeSkillInfoBg != null) ConfirmationDialog.StyleAsDescriptionPanel(activeSkillInfoBg);
        if (passiveSkillInfoBg != null) ConfirmationDialog.StyleAsDescriptionPanel(passiveSkillInfoBg);
        if (racePassiveInfoBg != null) ConfirmationDialog.StyleAsDescriptionPanel(racePassiveInfoBg);

        CacheSkillButtonBaseWidths();
        heroUpgradeUI = gameObject.AddComponent<HeroUpgradeUI>(); // само-создаётся, не Inspector-поле — новый компонент, вайрить в сцене нечему
        CreateUpgradeButtonIfNeeded();
        CreateEquipBestButtonIfNeeded();

        // Панель уже сохранена выключенной в сцене (m_IsActive: 0) — не гасим её здесь ещё раз:
        // если вызвать SetActive(false) из Awake(), а Awake() впервые запускается ИМЕННО во время
        // первого Open()->SetActive(true), этот вызов сразу отменяет только что выполненную активацию.
    }

    // IBeginDragHandler/IDragHandler намеренно пустые — нам нужен только суммарный сдвиг в OnEndDrag,
    // но Unity определяет цель перетаскивания именно через IBeginDragHandler, поэтому без него OnEndDrag не сработает.
    public void OnBeginDrag(PointerEventData eventData) { }
    public void OnDrag(PointerEventData eventData) { }

    // Свайп по X внутри панели — переключает на соседнего героя (влево = предыдущий, вправо = следующий)
    public void OnEndDrag(PointerEventData eventData)
    {
        float deltaX = eventData.position.x - eventData.pressPosition.x;
        if (Mathf.Abs(deltaX) < SwipeThreshold) return;

        NavigateHero(deltaX > 0 ? -1 : 1);
    }

    // Переходит к предыдущему/следующему (по направлению) разблокированному герою в том же порядке,
    // что и в сетке коллекции — берём её последний отображённый порядок (после поиска/фильтров/
    // сортировки, см. HeroCollectionUI.LastDisplayedOrder), а не сырой HeroCollectionManager.allHeroes
    // (баг 2026-08-19: свайп и сетка расходились по порядку). Если коллекция в этой сессии ещё ни разу
    // не открывалась (Inventory открыт откуда-то ещё, например Squad) — откат на старый порядок.
    private void NavigateHero(int direction)
    {
        if (currentHero == null || HeroCollectionManager.Instance == null) return;

        var baseOrder = HeroCollectionUI.LastDisplayedOrder != null && HeroCollectionUI.LastDisplayedOrder.Count > 0
            ? HeroCollectionUI.LastDisplayedOrder
            : HeroCollectionManager.Instance.allHeroes.ToList(); // .ToList() — тернарник иначе не находит общий тип с List<HeroData> выше

        var unlocked = baseOrder.Where(h => h != null && HeroCollectionManager.Instance.IsUnlocked(h)).ToList();
        if (unlocked.Count < 2) return;

        int index = unlocked.FindIndex(h => h.heroId == currentHero.heroId);
        if (index < 0) return;

        int nextIndex = (index + direction + unlocked.Count) % unlocked.Count;
        Open(unlocked[nextIndex]);
    }

    public void Open(HeroData hero)
    {
        if (hero == null || HeroCollectionManager.Instance == null) return;

        heroUpgradeUI?.Close(); // свайп на другого героя (NavigateHero) не должен оставлять попап прокачки открытым на данных ПРЕЖНЕГО героя

        currentHero = hero;
        currentOwnership = HeroCollectionManager.Instance.ownership.Find(o => o.heroId == hero.heroId);
        HeroCollectionManager.Instance.MarkUpgradeBadgeSeen(hero.heroId); // "видел" — бейдж в Collection/Squad погаснет на их следующем Refresh

        transform.SetAsLastSibling(); // поднимаем над другими панелями (Squad, Collection и т.д.), иначе они перехватывают клик
        gameObject.SetActive(true);
        Refresh();
    }

    // Вызывается при закрытии этого попапа — чтобы экран коллекции позади мог перестроить сетку
    // (например, если левелап/вознесение внутри попапа изменили уровень/грейд героя)
    public System.Action OnClosed;

    public void Close()
    {
        gameObject.SetActive(false);
        StopPortraitVideo(); // не тратим декод впустую, пока попап скрыт
        OnClosed?.Invoke();
    }

    // Оверлей поверх portraitImage — RawImage+VideoPlayer, растянутый на весь родительский Image ребёнком
    // (значит, автоматически наследует его позицию/размер, отдельно копировать RectTransform не нужно).
    // Строится один раз лениво, дальше переиспользуется под любого героя с inventoryPortraitVideo.
    private RawImage portraitVideoImage;
    private VideoPlayer portraitVideoPlayer;
    private RenderTexture portraitRenderTexture;

    private void EnsurePortraitVideoBuilt()
    {
        if (portraitVideoImage != null || portraitImage == null) return;

        var videoObj = new GameObject("PortraitVideo", typeof(RectTransform));
        var videoRect = (RectTransform)videoObj.transform;
        videoRect.SetParent(portraitImage.transform, false);
        videoRect.anchorMin = Vector2.zero;
        videoRect.anchorMax = Vector2.one;
        videoRect.offsetMin = Vector2.zero;
        videoRect.offsetMax = Vector2.zero;

        portraitVideoImage = videoObj.AddComponent<RawImage>();

        portraitVideoPlayer = videoObj.AddComponent<VideoPlayer>();
        portraitVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
        portraitVideoPlayer.source = VideoSource.VideoClip;
        portraitVideoPlayer.isLooping = true;
        portraitVideoPlayer.playOnAwake = false;
        portraitVideoPlayer.audioOutputMode = VideoAudioOutputMode.None; // тихий портрет-луп, не ждём звука в клипе
    }

    // Разные герои могут прислать видео разного разрешения — RenderTexture пересоздаём под нативный
    // размер клипа только когда он реально отличается, не на каждый Refresh.
    private void PlayPortraitVideo(VideoClip clip)
    {
        EnsurePortraitVideoBuilt();
        if (portraitVideoPlayer == null) return;

        if (portraitRenderTexture == null || portraitRenderTexture.width != (int)clip.width || portraitRenderTexture.height != (int)clip.height)
        {
            if (portraitRenderTexture != null) portraitRenderTexture.Release();
            portraitRenderTexture = new RenderTexture((int)clip.width, (int)clip.height, 0);
            portraitVideoImage.texture = portraitRenderTexture;
            portraitVideoPlayer.targetTexture = portraitRenderTexture;
        }

        portraitVideoPlayer.clip = clip;
        portraitVideoImage.gameObject.SetActive(true);
        portraitVideoPlayer.Play();
    }

    private void StopPortraitVideo()
    {
        if (portraitVideoPlayer != null) portraitVideoPlayer.Stop();
        if (portraitVideoImage != null) portraitVideoImage.gameObject.SetActive(false);
    }

    private void Refresh()
    {
        if (currentHero == null) return;

        if (portraitImage != null) portraitImage.sprite = HeroAscensionUtility.GetInventoryPortrait(currentHero, currentOwnership);

        if (currentHero.inventoryPortraitVideo != null)
            PlayPortraitVideo(currentHero.inventoryPortraitVideo);
        else
            StopPortraitVideo();
        if (heroNameText != null) heroNameText.text = currentHero.heroName;
        if (healthText != null) healthText.text = $"HP: {currentHero.maxHealth}";
        if (descriptionText != null) descriptionText.text = currentHero.description;

        RarityUtility.ApplyFrame(rarityFrame, currentHero.rarity, ref rarityKantGlow, ref rarityTierLabel);
        HeroAscensionUtility.ApplyOverlay(ascensionOverlay, ascensionOverlaySet, currentHero.rarity, currentOwnership != null ? currentOwnership.ascensionLevel : 0);
        WondrousArmorSkinSet.Apply(wondrousArmorOverlay, wondrousArmorSkinSet, currentHero.heroId, currentOwnership != null && currentOwnership.wondrousArmorWorn);

        if (raceEmblem != null)
        {
            Sprite emblem = raceEmblemSet != null ? raceEmblemSet.GetEmblem(currentHero.race) : null;
            raceEmblem.sprite = emblem;
            raceEmblem.enabled = emblem != null;
        }

        RefreshRacePassiveUI();

        if (levelText != null)
        {
            int level = currentOwnership != null ? currentOwnership.level : 1;

            if (currentOwnership != null && HeroCollectionManager.Instance != null)
            {
                int nextThreshold = HeroCollectionManager.Instance.ExperienceToNextLevel(level);
                levelText.text = $"Lv. {level} ({currentOwnership.experience}/{nextThreshold} Exp)";
            }
            else
            {
                levelText.text = $"Lv. {level}";
            }
        }

        PopulateSkillSelectors();
        PopulateItems();
        RefreshUpgradeButtonTheme();
        RefreshUpgradeButtonVisibility();
        RefreshAscendButton();
        CreateMapAvatarButtonIfNeeded();
        RefreshMapAvatarButton();
    }

    private void PopulateSkillSelectors()
    {
        if (currentHero == null || currentHero.skills == null) return;

        int activeIndex = currentOwnership != null ? currentOwnership.activeSkillIndex : 0;
        int passiveIndex = currentOwnership != null ? currentOwnership.passiveSkillIndex : -1;

        // Перебираем по количеству КНОПОК в сцене, а не по currentHero.skills.Length — герои с меньшим
        // числом скиллов (например, только 1) иначе оставляли кнопки с большими индексами нетронутыми,
        // с исходным дефолтным "New Text" и старой шириной из сцены, вместо того чтобы их скрыть.
        int slotCount = Mathf.Max(activeSkillTabs?.Length ?? 0, passiveSkillButtons?.Length ?? 0);
        for (int i = 0; i < slotCount; i++)
        {
            int index = i; // копия для замыкания в лямбдах ниже
            bool hasSkill = index < currentHero.skills.Length && currentHero.skills[index] != null;
            string skillName = hasSkill ? currentHero.skills[index].skillName : "";

            if (activeSkillTabs != null && index < activeSkillTabs.Length && activeSkillTabs[index] != null)
            {
                // Пустой слот скилла (SkillData не назначен) — прятать кнопку целиком, а не показывать
                // пустую узкую (preferredWidth для пустой строки почти 0, кнопка сжималась до ~24px).
                activeSkillTabs[index].gameObject.SetActive(hasSkill);
                if (hasSkill)
                {
                    activeSkillTabs[index].onClick.RemoveAllListeners();
                    activeSkillTabs[index].onClick.AddListener(() => OnActiveSkillTabClicked(index));

                    float baseWidth = activeSkillTabBaseWidths != null && index < activeSkillTabBaseWidths.Length
                        ? activeSkillTabBaseWidths[index] : 0f;
                    SetButtonLabelAndFitWidth(activeSkillTabs[index], skillName, baseWidth);
                }
            }

            if (activeSkillHighlights != null && index < activeSkillHighlights.Length && activeSkillHighlights[index] != null)
                activeSkillHighlights[index].enabled = index == activeIndex;

            if (passiveSkillButtons != null && index < passiveSkillButtons.Length && passiveSkillButtons[index] != null)
            {
                passiveSkillButtons[index].gameObject.SetActive(hasSkill);
                if (hasSkill)
                {
                    // Один и тот же навык может одновременно быть и активным, и пассивным — кнопки не исключают друг друга.
                    passiveSkillButtons[index].onClick.RemoveAllListeners();
                    passiveSkillButtons[index].onClick.AddListener(() => OnPassiveSkillButtonClicked(index));

                    float baseWidth = passiveSkillButtonBaseWidths != null && index < passiveSkillButtonBaseWidths.Length
                        ? passiveSkillButtonBaseWidths[index] : 0f;
                    SetButtonLabelAndFitWidth(passiveSkillButtons[index], skillName, baseWidth);
                }
            }

            if (passiveSkillHighlights != null && index < passiveSkillHighlights.Length && passiveSkillHighlights[index] != null)
                passiveSkillHighlights[index].enabled = index == passiveIndex;
        }

        // Оба инфо-блока всегда отражают текущий выбор, а не только последний клик — активный и пассивный видны разом
        SkillData activeSkill = activeIndex >= 0 && activeIndex < currentHero.skills.Length ? currentHero.skills[activeIndex] : null;
        SkillData passiveSkill = passiveIndex >= 0 && passiveIndex < currentHero.skills.Length ? currentHero.skills[passiveIndex] : null;

        SetSkillInfoText(activeSkillInfoText, activeSkill);
        SetSkillInfoText(passiveSkillInfoText, passiveSkill);

        ResetScrollToTop(activeSkillInfoScroll);
        ResetScrollToTop(passiveSkillInfoScroll);
    }

    // Длинное описание может не влезть в видимую область скролла — при смене навыка возвращаем его наверх,
    // иначе после переключения останется прокручено туда, где было у предыдущего (другого по длине) текста.
    private static void ResetScrollToTop(ScrollRect scroll)
    {
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
    }

    // Запоминаем изначальную (заданную вручную в редакторе) ширину каждой кнопки один раз — чтобы потом
    // расширять их под длинное название навыка, но никогда не ужимать обратно ниже авторского размера.
    private void CacheSkillButtonBaseWidths()
    {
        if (activeSkillTabs != null)
        {
            activeSkillTabBaseWidths = new float[activeSkillTabs.Length];
            for (int i = 0; i < activeSkillTabs.Length; i++)
            {
                var rect = activeSkillTabs[i] != null ? activeSkillTabs[i].GetComponent<RectTransform>() : null;
                activeSkillTabBaseWidths[i] = rect != null ? rect.sizeDelta.x : 0f;
            }
        }

        if (passiveSkillButtons != null)
        {
            passiveSkillButtonBaseWidths = new float[passiveSkillButtons.Length];
            for (int i = 0; i < passiveSkillButtons.Length; i++)
            {
                var rect = passiveSkillButtons[i] != null ? passiveSkillButtons[i].GetComponent<RectTransform>() : null;
                passiveSkillButtonBaseWidths[i] = rect != null ? rect.sizeDelta.x : 0f;
            }
        }
    }

    // Ставит название навыка на текстовую надпись внутри кнопки и, если оно не влезает в исходную ширину
    // кнопки, увеличивает саму кнопку под текст (но никогда не сужает меньше исходного размера).
    private static void SetButtonLabelAndFitWidth(Button button, string skillName, float baseWidth)
    {
        if (button == null) return;

        var label = button.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = skillName ?? "";
        if (label == null) return;

        float preferredWidth = label.GetPreferredValues(skillName ?? "").x + SkillButtonTextPadding;
        float width = Mathf.Max(baseWidth, preferredWidth);

        // Если кнопка лежит под Horizontal/Vertical Layout Group, та каждый layout-проход перетирает
        // RectTransform.sizeDelta своим расчётом — реальный способ повлиять на итоговую ширину в этом
        // случае это LayoutElement.preferredWidth, который Layout Group учитывает сама.
        var layoutElement = button.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = button.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = width;

        // На случай, если родитель НЕ под Layout Group — правим и sizeDelta напрямую тоже (не мешает,
        // если Layout Group реально управляет размером — она всё равно пересчитает поверх этого).
        var rect = button.GetComponent<RectTransform>();
        if (rect != null)
        {
            Vector2 size = rect.sizeDelta;
            size.x = width;
            rect.sizeDelta = size;
        }
    }

    private const int SkillSummaryMaxChars = 80;

    // Текстовая стена в основном виде — то, чего просит избегать UX-бриф. Показываем короткую версию
    // (авторскую SkillData.shortSummary, либо автообрезку description до SkillSummaryMaxChars), полный
    // текст — по тапу на сам блок, тот же приём "короткий лейбл -> ConfirmationDialog.ShowInfo", что уже
    // используется в BattlePrepPopup для пассивок/вредных фишек.
    private static void SetSkillInfoText(TMP_Text label, SkillData skill)
    {
        if (label == null) return;

        if (skill == null)
        {
            label.text = "";
            SetSkillInfoButton(label, null);
            return;
        }

        string full = $"Mana: {skill.cost}\n{skill.description}";
        bool hasCustomSummary = !string.IsNullOrEmpty(skill.shortSummary);
        bool descriptionTruncated = !hasCustomSummary && skill.description != null && skill.description.Length > SkillSummaryMaxChars;

        label.text = hasCustomSummary
            ? $"Mana: {skill.cost}\n{skill.shortSummary} (tap for more)"
            : descriptionTruncated
                ? $"Mana: {skill.cost}\n{skill.description.Substring(0, SkillSummaryMaxChars)}... (tap for more)"
                : full;

        SetSkillInfoButton(label, hasCustomSummary || descriptionTruncated
            ? () => ConfirmationDialog.ShowInfo(FindAnyObjectByType<Canvas>()?.transform, full, title: skill.skillName)
            : null);
    }

    // Лениво добавляет Button на весь текстовый блок один раз, дальше только переустанавливает onClick —
    // иначе при каждой смене навыка копились бы старые обработчики с устаревшим skill в замыкании.
    // onClick == null — блок не кликабелен (короткий текст и так виден целиком, тапать некуда).
    private static void SetSkillInfoButton(TMP_Text label, System.Action onClick)
    {
        var btn = label.GetComponent<Button>();
        if (btn == null)
        {
            btn = label.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = label;
        }
        btn.onClick.RemoveAllListeners();
        if (onClick != null) btn.onClick.AddListener(() => onClick());
        btn.interactable = onClick != null;
    }

    private void OnActiveSkillTabClicked(int index)
    {
        if (currentHero == null || currentOwnership == null || currentHero.skills == null) return;
        if (index < 0 || index >= currentHero.skills.Length) return;

        SkillData skill = currentHero.skills[index];

        int totalMaxMana = GetTotalMaxMana();
        if (totalMaxMana < skill.cost)
        {
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
                ConfirmationDialog.ShowInfo(canvas.transform,
                    $"Not enough mana for this skill (needs {skill.cost}, hero's max is {totalMaxMana}).");
            return;
        }

        currentOwnership.activeSkillIndex = index;
        HeroCollectionManager.Instance?.SaveOwnership();
        PopulateSkillSelectors();
    }

    private void OnPassiveSkillButtonClicked(int index)
    {
        if (currentHero == null || currentOwnership == null || currentHero.skills == null) return;
        if (index < 0 || index >= currentHero.skills.Length) return;

        // Повторный клик по уже выбранной пассивке снимает её — мана при этом только освобождается,
        // проверка нехватки маны тут не нужна.
        if (currentOwnership.passiveSkillIndex == index)
        {
            currentOwnership.passiveSkillIndex = -1;
            HeroCollectionManager.Instance?.SaveOwnership();
            PopulateSkillSelectors();
            return;
        }

        SkillData passiveSkill = currentHero.skills[index];

        int activeIndex = currentOwnership.activeSkillIndex;
        SkillData activeSkill = activeIndex >= 0 && activeIndex < currentHero.skills.Length
            ? currentHero.skills[activeIndex] : null;
        int requiredForActive = activeSkill != null ? activeSkill.cost : 0;

        // Пассивка "съедает" часть maxResource в бою (см. BattleManager) — если после этого не хватит
        // маны даже на уже выбранный активный навык, выбор пассивки нужно заблокировать попапом.
        int remainingMana = GetTotalMaxMana() - passiveSkill.cost;
        if (remainingMana < requiredForActive)
        {
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
                ConfirmationDialog.ShowInfo(canvas.transform,
                    $"Not enough mana — this passive costs {passiveSkill.cost}, leaving only {remainingMana}, " +
                    $"but the active skill needs {requiredForActive}.");
            return;
        }

        currentOwnership.passiveSkillIndex = index;
        HeroCollectionManager.Instance?.SaveOwnership();
        PopulateSkillSelectors();
    }

    private void OnRacePassiveToggleClicked()
    {
        if (currentHero == null || currentOwnership == null) return;

        // Выключение — мана освобождается сразу, проверка нехватки не нужна (симметрично OnPassiveSkillButtonClicked).
        if (currentOwnership.racePassiveEnabled)
        {
            currentOwnership.racePassiveEnabled = false;
            HeroCollectionManager.Instance?.SaveOwnership();
            RefreshRacePassiveUI();
            RefreshStats();
            return;
        }

        int activeIndex = currentOwnership.activeSkillIndex;
        SkillData activeSkill = currentHero.skills != null && activeIndex >= 0 && activeIndex < currentHero.skills.Length
            ? currentHero.skills[activeIndex] : null;
        int requiredForActive = activeSkill != null ? activeSkill.cost : 0;

        // Пассивка расы тоже "съедает" часть maxResource в бою (см. BattleManager) — та же защита, что и
        // у старого passiveSkillIndex: не дать включить, если после этого не хватит маны на активный скилл.
        int remainingMana = GetTotalMaxMana() - RacePassiveUtility.ManaCost;
        if (remainingMana < requiredForActive)
        {
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
                ConfirmationDialog.ShowInfo(canvas.transform,
                    $"Not enough mana — the race passive costs {RacePassiveUtility.ManaCost}, leaving only {remainingMana}, " +
                    $"but the active skill needs {requiredForActive}.");
            return;
        }

        currentOwnership.racePassiveEnabled = true;
        HeroCollectionManager.Instance?.SaveOwnership();
        RefreshRacePassiveUI();
        RefreshStats();
    }

    // Текст описания + пометка вкл/выкл с ценой, плюс подсветка кнопки (если назначена).
    private void RefreshRacePassiveUI()
    {
        if (currentHero == null) return;

        // Blue-редкость — герой без расы (Race.None, см. UX-правку 2026-08-19: удаление Green-редкости).
        // Расовой пассивки у него нет вообще — прячем весь блок целиком, а не показываем текст/тоггл про
        // фейковую пассивку случайно попавшей расы (по умолчанию было бы Elves, см. HeroData.race).
        bool raceless = currentHero.race == Race.None;
        if (racePassiveInfoText != null) racePassiveInfoText.gameObject.SetActive(!raceless);
        if (racePassiveInfoBg != null) racePassiveInfoBg.gameObject.SetActive(!raceless);
        if (racePassiveToggleButton != null) racePassiveToggleButton.gameObject.SetActive(!raceless);
        if (racePassiveToggleHighlight != null) racePassiveToggleHighlight.gameObject.SetActive(!raceless);
        if (raceless) return;

        bool enabled = currentOwnership != null && currentOwnership.racePassiveEnabled;

        if (racePassiveInfoText != null)
        {
            string state = enabled ? "ON" : "OFF";
            bool maxAscended = currentOwnership != null
                && HeroAscensionUtility.IsMaxAscension(currentHero.rarity, currentOwnership.ascensionLevel);
            racePassiveInfoText.text = $"{RacePassiveUtility.GetDescription(currentHero.race, maxAscended)}\n" +
                $"[{state}] costs {RacePassiveUtility.ManaCost} mana";
        }

        if (racePassiveToggleHighlight != null)
        {
            Color c = racePassiveToggleHighlight.color;
            c.a = enabled ? 0.6f : 0.05f;
            racePassiveToggleHighlight.color = c;
        }
    }

    private void PopulateItems()
    {
        if (itemsContainer == null || itemSlotPrefab == null) return;

        // Обратный проход по индексу + немедленный SetParent(null), не foreach+Destroy() — та же причина,
        // что у HeroCollectionUI.PopulateGrid (баг 2026-08-18, найден повторно на аудите 2026-08-20):
        // Destroy() отложен до конца кадра, поэтому VerticalLayoutGroup на itemsContainer на один кадр
        // видел старые+новые слоты одновременно и раскладку "распирало" при каждом свайпе героя/экипировке.
        for (int i = itemsContainer.childCount - 1; i >= 0; i--)
        {
            var child = itemsContainer.GetChild(i);
            child.SetParent(null);
            Destroy(child.gameObject);
        }

        foreach (var slotType in AllSlotTypes)
        {
            GameObject slotObj = Instantiate(itemSlotPrefab, itemsContainer);
            var slot = slotObj.GetComponent<ItemSlotUI>();
            slot.Setup(slotType, this, GetEmptySlotSprite(slotType));

            string equippedInstanceId = currentOwnership != null ? currentOwnership.GetEquippedItemInstanceId(slotType) : null;
            ItemOwnershipData equippedStack = (!string.IsNullOrEmpty(equippedInstanceId) && ItemCollectionManager.Instance != null)
                ? ItemCollectionManager.Instance.GetStackByInstanceId(equippedInstanceId)
                : null;
            ItemData equippedItem = equippedStack != null ? ItemCollectionManager.Instance.GetItemById(equippedStack.itemId) : null;

            slot.Refresh(equippedItem);
        }

        RefreshStats();
    }

    private Sprite GetEmptySlotSprite(EquipmentSlotType type) => type switch
    {
        EquipmentSlotType.Weapon => emptySlotWeaponSprite,
        EquipmentSlotType.Armor => emptySlotArmorSprite,
        EquipmentSlotType.Accessory => emptySlotAccessorySprite,
        EquipmentSlotType.Trinket => emptySlotTrinketSprite,
        _ => null
    };

    private Button equipBestButton;

    // Заменяет "зайти в слот -> открыть пикер -> выбрать -> закрыть" x4 одним тапом (см. UX-бриф про
    // избыточную вложенность). Никогда не трогает вещи, экипированные на ДРУГИХ героях — это одноразовое
    // "надень лучшее из уже свободного", не грабёж чужого отряда.
    private void CreateEquipBestButtonIfNeeded()
    {
        if (equipBestButton != null || itemsContainer == null) return;

        var referenceRect = (RectTransform)itemsContainer;

        var btnObj = new GameObject("EquipBestButton", typeof(RectTransform));
        var btnRect = (RectTransform)btnObj.transform;
        btnRect.SetParent(referenceRect.parent, false);
        btnRect.anchorMin = referenceRect.anchorMin;
        btnRect.anchorMax = referenceRect.anchorMax;
        btnRect.pivot = new Vector2(0.5f, 1);
        btnRect.sizeDelta = new Vector2(260, 60);
        // Жёстко (0,-230), не вычислено от referenceRect — вычисленная позиция расходилась с фактической
        // (пользователь сверял вживую в Play Mode, см. правку 2026-08-18), проще и надёжнее зафиксировать точно.
        btnRect.anchoredPosition = new Vector2(0f, -230f);

        var btnImg = btnObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(btnImg);
        equipBestButton = btnObj.AddComponent<Button>();
        equipBestButton.targetGraphic = btnImg;
        equipBestButton.onClick.AddListener(OnEquipBestClicked);

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(btnRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "Equip Best";
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
        text.fontSizeMax = 32;
        text.color = ConfirmationDialog.ButtonTextColor;
    }

    private void OnEquipBestClicked()
    {
        if (currentHero == null || currentOwnership == null || ItemCollectionManager.Instance == null || HeroCollectionManager.Instance == null) return;

        var manager = ItemCollectionManager.Instance;
        var heroManager = HeroCollectionManager.Instance;
        int equippedCount = 0;

        foreach (var slotType in AllSlotTypes)
        {
            string currentInstanceId = currentOwnership.GetEquippedItemInstanceId(slotType);
            var currentStack = !string.IsNullOrEmpty(currentInstanceId) ? manager.GetStackByInstanceId(currentInstanceId) : null;
            var currentItem = currentStack != null ? manager.GetItemById(currentStack.itemId) : null;
            float bestValue = currentItem != null ? EquipmentStatCurve.GetValue(slotType, currentItem.rarity, currentStack.level) : 0f;

            ItemOwnershipData bestStack = null;

            // Кандидаты — как ItemSacrificeUI.GetCandidates: владеем, и НИГДЕ не экипировано (в т.ч. и у
            // этого героя в другом слоте) — кнопка никогда не ворует шмот с другого героя молча.
            foreach (var item in manager.GetItemsOfType(slotType))
            {
                foreach (var stack in manager.GetStacks(item.itemId))
                {
                    if (heroManager.IsItemEquippedAnywhere(stack.instanceId)) continue;

                    float value = EquipmentStatCurve.GetValue(slotType, item.rarity, stack.level);
                    if (value > bestValue)
                    {
                        bestValue = value;
                        bestStack = stack;
                    }
                }
            }

            if (bestStack == null) continue; // уже лучшее из доступного (или пусто) — не трогаем

            string toEquip = manager.SplitOneForEquip(bestStack.instanceId) ?? bestStack.instanceId;
            heroManager.UnequipItemFromAllHeroes(toEquip, currentHero.heroId);
            currentOwnership.SetEquippedItem(slotType, toEquip);
            equippedCount++;
        }

        var canvas = FindAnyObjectByType<Canvas>();
        if (equippedCount > 0)
        {
            heroManager.SaveOwnership();
            PopulateItems(); // один общий Refresh на весь батч, а не по разу на каждый слот (см. EquipItem)
            HapticsUtility.PlayLightTap();
            AudioManager.Instance?.PlayUpgradeSound();
        }
        else if (canvas != null)
        {
            ConfirmationDialog.ShowInfo(canvas.transform, "Already wearing the best available gear.");
        }
    }

    // База героя + сумма бонусов от текущей экипировки (через общий HeroStatUtility — тот же расчёт,
    // что использует BattleManager при старте боя, чтобы цифры в меню совпадали с реальными в бою).
    private void RefreshStats()
    {
        if (statsText == null || currentHero == null) return;

        var s = ComputeDisplayedStats();
        statsText.text = $"HP: {s.hp}\nMana: {s.mana}\nAttack: {UpgradeFeedbackUtility.AttackDisplayValue(s.dmg)}\nArmor: {s.armor}\nMight: {s.might}";
    }

    // Вынесено из RefreshStats — переиспользуется AnimateStatGain для снимка "было"/"стало" без дублирования
    // самого расчёта (тот же HeroStatUtility, что и раньше, ничего в цифрах не поменялось). overrideAscensionLevel
    // — для превью "а что если вознестись" (см. OpenAscendPopup) БЕЗ реальной мутации currentOwnership.
    // might — та же "Мощь", что и в коллекции (HeroStatUtility.CalculatePower), level/ascensionLevel те же,
    // что уже используются ниже для остальных статов, поэтому считается от них же, а не от currentOwnership напрямую.
    private (int hp, int mana, float dmg, int armor, int might) ComputeDisplayedStats(int? overrideAscensionLevel = null)
    {
        if (currentHero == null) return (0, 0, 0f, 0, 0);

        int level = currentOwnership != null ? currentOwnership.level : 1;
        int ascensionLevel = overrideAscensionLevel ?? (currentOwnership != null ? currentOwnership.ascensionLevel : 0);

        var baseStats = HeroStatUtility.CalculateBaseStats(currentHero, level, ascensionLevel);
        var bonuses = HeroStatUtility.CalculateEquipmentBonuses(currentOwnership); // экипировка не зависит от вознесения
        int might = HeroStatUtility.CalculatePower(currentHero, currentOwnership, level, ascensionLevel);

        return (baseStats.health + bonuses.health, GetTotalMaxMana(overrideAscensionLevel),
            baseStats.damageMultiplier + bonuses.damageMultiplier, baseStats.armor + bonuses.armor, might);
    }

    // "Было -> стало" — короткая, самоотменяющаяся подсветка изменившихся статов (грей->зелёный + летящие
    // +N + вспышка панели), затем RefreshStats() тихо возвращает обычный плоский вид. Вызывается ПОСЛЕ
    // Refresh() (снимок "было" сделан заранее, до самого действия) — см. OnUpgradeStepClicked/OnAscendClicked.
    private Image statsPanelBg;
    private Coroutine statsFlashRoutine;
    private Coroutine statsRevertRoutine;

    private void AnimateStatGain((int hp, int mana, float dmg, int armor, int might) before)
    {
        if (statsText == null || currentHero == null) return;

        var after = ComputeDisplayedStats();
        var lines = new System.Collections.Generic.List<string>();
        var deltas = new System.Collections.Generic.List<string>();
        bool anyChanged = false;

        if (after.hp != before.hp)
        {
            lines.Add(UpgradeFeedbackUtility.FormatBeforeAfter("HP", before.hp.ToString(), after.hp.ToString()));
            deltas.Add($"+{after.hp - before.hp}");
            anyChanged = true;
        }
        else lines.Add($"HP: {after.hp}");

        if (after.mana != before.mana)
        {
            lines.Add(UpgradeFeedbackUtility.FormatBeforeAfter("Mana", before.mana.ToString(), after.mana.ToString()));
            deltas.Add($"+{after.mana - before.mana}");
            anyChanged = true;
        }
        else lines.Add($"Mana: {after.mana}");

        if (!Mathf.Approximately(after.dmg, before.dmg))
        {
            lines.Add(UpgradeFeedbackUtility.FormatBeforeAfter("Attack", UpgradeFeedbackUtility.AttackDisplayValue(before.dmg).ToString(), UpgradeFeedbackUtility.AttackDisplayValue(after.dmg).ToString()));
            deltas.Add($"+{UpgradeFeedbackUtility.AttackDisplayValue(after.dmg) - UpgradeFeedbackUtility.AttackDisplayValue(before.dmg)}");
            anyChanged = true;
        }
        else lines.Add($"Attack: {UpgradeFeedbackUtility.AttackDisplayValue(after.dmg)}");

        if (after.armor != before.armor)
        {
            lines.Add(UpgradeFeedbackUtility.FormatBeforeAfter("Armor", before.armor.ToString(), after.armor.ToString()));
            deltas.Add($"+{after.armor - before.armor}");
            anyChanged = true;
        }
        else lines.Add($"Armor: {after.armor}");

        if (after.might != before.might)
        {
            lines.Add(UpgradeFeedbackUtility.FormatBeforeAfter("Might", before.might.ToString(), after.might.ToString()));
            deltas.Add($"+{after.might - before.might}");
            anyChanged = true;
        }
        else lines.Add($"Might: {after.might}");

        if (!anyChanged) return; // ничего не выросло (например, вознесение подняло только потолок уровня) — RefreshStats() уже верен

        statsText.text = string.Join("\n", lines);

        EnsureStatsFlashPanel();
        if (statsFlashRoutine != null) StopCoroutine(statsFlashRoutine);
        statsFlashRoutine = UpgradeFeedbackUtility.FlashPanel(this, statsPanelBg, FloatingDamageText.PositiveGainColor);

        float xOffset = -(deltas.Count - 1) * 35f; // центрируем ряд летящих цифр вокруг статов
        foreach (var delta in deltas)
        {
            FloatingDamageText.Spawn(statsText.rectTransform, delta, FloatingDamageText.PositiveGainColor, xOffset);
            xOffset += 70f;
        }

        HapticsUtility.PlayLightTap();
        AudioManager.Instance?.PlayUpgradeSound();

        if (statsRevertRoutine != null) StopCoroutine(statsRevertRoutine);
        statsRevertRoutine = StartCoroutine(RevertStatsTextAfterDelay());
    }

    private void EnsureStatsFlashPanel()
    {
        if (statsPanelBg != null || statsText == null || statsText.transform.parent == null) return;

        var panelObj = new GameObject("StatsFlashPanel", typeof(RectTransform));
        var panelRect = (RectTransform)panelObj.transform;
        panelRect.SetParent(statsText.transform.parent, false);
        var statsRect = statsText.rectTransform;
        panelRect.anchorMin = statsRect.anchorMin;
        panelRect.anchorMax = statsRect.anchorMax;
        panelRect.pivot = statsRect.pivot;
        panelRect.anchoredPosition = statsRect.anchoredPosition;
        panelRect.sizeDelta = statsRect.sizeDelta;
        panelRect.SetSiblingIndex(statsText.transform.GetSiblingIndex()); // рендерится ЗА statsText, не поверх

        statsPanelBg = panelObj.AddComponent<Image>();
        statsPanelBg.color = new Color(0, 0, 0, 0); // стартует прозрачным, красится только на вспышке
        statsPanelBg.raycastTarget = false; // чисто визуальный слой, клики под статами (если есть) не перехватывает
    }

    private IEnumerator RevertStatsTextAfterDelay()
    {
        yield return new WaitForSeconds(2.5f);
        RefreshStats();
        statsRevertRoutine = null;
    }

    // Итоговый максимум маны героя (база*бонус уровня + бонус экипировки, минус пассивка расы) —
    // используется и для отображения статов, и для проверки "хватит ли маны" при выборе активного/
    // пассивного навыка (раньше эти проверки сверялись с "голым" currentHero.maxResource без бонусов,
    // из-за чего расходились с панелью статов). Сам расчёт — в HeroStatUtility.CalculateMaxMana, общий
    // с HeroUpgradeUI (превью на отложенном уровне).
    private int GetTotalMaxMana(int? overrideAscensionLevel = null)
    {
        if (currentHero == null) return 0;
        int level = currentOwnership != null ? currentOwnership.level : 1;
        int ascensionLevel = overrideAscensionLevel ?? (currentOwnership != null ? currentOwnership.ascensionLevel : 0);
        return HeroStatUtility.CalculateMaxMana(currentHero, currentOwnership, level, ascensionLevel);
    }

    public void OpenItemPicker(EquipmentSlotType slotType)
    {
        if (itemPicker != null)
            itemPicker.Open(slotType, this);
    }

    // Instance ID стека, экипированного в указанный слот этого героя (null, если слот пуст)
    public string GetEquippedItemInstanceId(EquipmentSlotType slotType)
    {
        return currentOwnership != null ? currentOwnership.GetEquippedItemInstanceId(slotType) : null;
    }

    public void EquipItem(EquipmentSlotType slotType, string itemInstanceId)
    {
        if (currentOwnership == null) return;

        // Конкретный стек уникален — если он уже экипирован на другом герое, снимаем его оттуда ("переносим" сюда)
        if (!string.IsNullOrEmpty(itemInstanceId) && HeroCollectionManager.Instance != null)
            HeroCollectionManager.Instance.UnequipItemFromAllHeroes(itemInstanceId, currentHero.heroId);

        currentOwnership.SetEquippedItem(slotType, itemInstanceId);
        HeroCollectionManager.Instance?.SaveOwnership();
        PopulateItems();
    }

    // Открывает HeroUpgradeUI (отложенный превью уровня + два источника оплаты — валюта и предметы опыта,
    // см. HeroUpgradeUI.ComputeSpendPlan) вместо прямой траты по тапу. onBeforeApply/onApplied — те же
    // before/after-снимок и AnimateStatGain, что раньше вызывались прямо здесь, просто перенесены за границу
    // попапа (см. OnHeroUpgradeWillApply/OnHeroUpgradeApplied) — само окно ничего не анимирует, чтобы вспышка
    // на статах не могла сработать дважды.
    private void OnUpgradeButtonClicked()
    {
        if (currentHero == null || currentOwnership == null || heroUpgradeUI == null) return;
        heroUpgradeUI.Open(currentHero, currentOwnership, OnHeroUpgradeWillApply, OnHeroUpgradeApplied);
    }

    private (int hp, int mana, float dmg, int armor, int might) heroUpgradeStatsBefore;

    private void OnHeroUpgradeWillApply() => heroUpgradeStatsBefore = ComputeDisplayedStats();

    private void OnHeroUpgradeApplied()
    {
        Refresh();
        AnimateStatGain(heroUpgradeStatsBefore);
    }

    // Одна кнопка "Upgrade" в том же слоте, что раньше занимали 3 степпера, строится программно рядом с
    // Close (копируя его трансформ на всю ширину), чтобы не редактировать вручную разметку панели героя в сцене.
    private void CreateUpgradeButtonIfNeeded()
    {
        if (heroUpgradeButton != null) return;

        RectTransform referenceRect = closeButton != null ? closeButton.GetComponent<RectTransform>() : null;
        if (referenceRect == null) return;

        Vector2 rowPos = referenceRect.anchoredPosition + new Vector2(0, referenceRect.sizeDelta.y + 12f);
        float height = referenceRect.sizeDelta.y;

        var upgradeObj = new GameObject("HeroUpgradeButton", typeof(RectTransform));
        var upgradeRect = (RectTransform)upgradeObj.transform;
        upgradeRect.SetParent(referenceRect.parent, false);
        upgradeRect.anchorMin = referenceRect.anchorMin;
        upgradeRect.anchorMax = referenceRect.anchorMax;
        upgradeRect.pivot = referenceRect.pivot;
        upgradeRect.sizeDelta = new Vector2(referenceRect.sizeDelta.x, height);
        upgradeRect.anchoredPosition = rowPos;

        heroUpgradeBg = upgradeObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(heroUpgradeBg);
        heroUpgradeButton = upgradeObj.AddComponent<Button>();
        heroUpgradeButton.targetGraphic = heroUpgradeBg;
        heroUpgradeButton.onClick.AddListener(OnUpgradeButtonClicked);

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(upgradeRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        heroUpgradeText = textObj.AddComponent<TextMeshProUGUI>();
        heroUpgradeText.text = "Upgrade";
        heroUpgradeText.fontStyle = FontStyles.Bold;
        heroUpgradeText.alignment = TextAlignmentOptions.Center;
        heroUpgradeText.enableAutoSizing = true;
        heroUpgradeText.fontSizeMin = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
        heroUpgradeText.fontSizeMax = 30;

        upgradeObj.SetActive(false);

        CreateHeroExperienceDeepLinkIcon(referenceRect, rowPos + new Vector2(0, height + 24f));
    }

    // Тап по иконке ресурса -> "где его найти" (см. UX-бриф про быстрый доступ к ресурсу). Сидит прямо над
    // рядом степперов — тот единственный реальный CurrencyType, что расходуется на этом экране (прокачка
    // предметов тратит донор-предметы, не валюту).
    private void CreateHeroExperienceDeepLinkIcon(RectTransform referenceRect, Vector2 anchoredPos)
    {
        const float size = 56f;

        var iconObj = new GameObject("HeroExperienceDeepLink", typeof(RectTransform));
        var iconRect = (RectTransform)iconObj.transform;
        iconRect.SetParent(referenceRect.parent, false);
        iconRect.anchorMin = referenceRect.anchorMin;
        iconRect.anchorMax = referenceRect.anchorMax;
        iconRect.pivot = referenceRect.pivot;
        iconRect.sizeDelta = new Vector2(size, size);
        iconRect.anchoredPosition = anchoredPos;

        var img = iconObj.AddComponent<Image>();
        img.sprite = Resources.Load<Sprite>(ConfirmationDialog.GetCurrencyIconPath(CurrencyType.HeroExperience));
        img.preserveAspect = true;

        var btn = iconObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(OnHeroExperienceDeepLinkClicked);
    }

    private void OnHeroExperienceDeepLinkClicked()
    {
        var canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        ConfirmationDialog.ShowActions(canvas.transform,
            "Hero Experience is earned by battling on the World Map, or bought directly with Progress Points in the Shop.",
            "Where to find Hero Experience",
            new (string, bool, System.Action)[]
            {
                ("World Map", true, () => FindAnyObjectByType<MainMenuUI>()?.ShowWorldMap()),
                ("Shop", true, () => FindAnyObjectByType<MainMenuUI>()?.ShowCastleAndOpenShop()),
            });
    }

    // Общий билдер подписанной кнопки в том же вертикальном стеке над Upgrade — использовался и гемом-в-
    // ваучер (кнопка теперь на плитке AscensionGemTile, см. project_hero_experience_audit_2026_08_11 /
    // AscensionGemTileUI.OnClicked), и Map Avatar ниже, остаётся общим ради второго.
    private Button BuildGemConversionButton(RectTransform referenceRect, Vector2 anchoredPos, string label, UnityEngine.Events.UnityAction onClick, out TMP_Text text)
    {
        var obj = new GameObject(label.Replace(" ", "").Replace("+", "") + "Button", typeof(RectTransform));
        var rect = (RectTransform)obj.transform;
        rect.SetParent(referenceRect.parent, false);
        rect.anchorMin = referenceRect.anchorMin;
        rect.anchorMax = referenceRect.anchorMax;
        rect.pivot = referenceRect.pivot;
        rect.sizeDelta = new Vector2(referenceRect.sizeDelta.x * 0.95f, referenceRect.sizeDelta.y);
        rect.anchoredPosition = anchoredPos;

        var bg = obj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(bg);
        var btn = obj.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.color = ConfirmationDialog.ButtonTextColor;
        text.enableAutoSizing = true;
        text.fontSizeMin = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
        text.fontSizeMax = 30;

        return btn;
    }

    // Ручной выбор портрета для маркера карты мира — доступно только на максимальном вознесении (см.
    // HeroCollectionManager.mapAvatarHeroId / PlayerMapMarkerUI.RefreshHeroVisual). Переиспользует
    // BuildGemConversionButton — тот на самом деле общий билдер подписанной кнопки, не только для гемов.
    // Позиция (2.3 шага) — та же, что раньше занимала кнопка "гем -> Voucher" (см. BuildGemConversionButton),
    // освободившаяся после переноса той кнопки на плитку AscensionGemTile.
    private void CreateMapAvatarButtonIfNeeded()
    {
        if (mapAvatarButton != null) return;

        RectTransform referenceRect = closeButton != null ? closeButton.GetComponent<RectTransform>() : null;
        if (referenceRect == null) return;

        float stepY = referenceRect.sizeDelta.y + 12f;
        Vector2 pos = referenceRect.anchoredPosition + new Vector2(0, stepY * 2.3f);

        mapAvatarButton = BuildGemConversionButton(referenceRect, pos, "Set as Map Avatar", OnMapAvatarClicked, out mapAvatarButtonText);
    }

    private void OnMapAvatarClicked()
    {
        if (currentHero == null || HeroCollectionManager.Instance == null) return;

        bool isCurrent = HeroCollectionManager.Instance.mapAvatarHeroId == currentHero.heroId;
        HeroCollectionManager.Instance.SetMapAvatarHero(isCurrent ? "" : currentHero.heroId);
        RefreshMapAvatarButton();
    }

    private void RefreshMapAvatarButton()
    {
        if (mapAvatarButton == null || currentHero == null) return;

        bool maxed = currentOwnership != null
            && HeroAscensionUtility.IsMaxAscension(currentHero.rarity, currentOwnership.ascensionLevel);
        mapAvatarButton.gameObject.SetActive(maxed);
        if (!maxed) return;

        bool isCurrent = HeroCollectionManager.Instance != null
            && HeroCollectionManager.Instance.mapAvatarHeroId == currentHero.heroId;
        if (mapAvatarButtonText != null)
            mapAvatarButtonText.text = isCurrent ? "Unset Map Avatar" : "Set as Map Avatar";
    }

    // Клик по значку вознесения теперь ВСЕГДА открывает попап, а не тратит гемы молча (см. UX-бриф) — какой
    // именно попап зависит от того, хватает ли гемов прямо сейчас. RefreshAscendButton уже гарантирует
    // interactable=false, если вознесение вообще неприменимо (макс. вознесение/раса без него), так что
    // сюда попадаем только когда relevant==true.
    private void OpenAscendPopup()
    {
        if (currentHero == null || currentOwnership == null) return;

        var canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        bool canAscendNow = currentOwnership.ascensionGems >= HeroAscensionUtility.GemsPerAscension;

        if (!canAscendNow)
        {
            int needed = HeroAscensionUtility.GemsPerAscension - currentOwnership.ascensionGems;
            ConfirmationDialog.ShowActions(canvas.transform,
                $"{currentHero.heroName} needs {needed} more Ascension Gem(s) to ascend. Gems come from summoning a duplicate copy of a hero you already own.",
                "Ascension",
                new (string, bool, System.Action)[]
                {
                    ("Go to Altar", true, () => FindAnyObjectByType<MainMenuUI>()?.ShowCastleAndOpenAltar()),
                });
            return;
        }

        // Хватает гемов — показываем было/стало ПЕРЕД тратой, а не молча меняем и объясняем постфактум.
        var before = ComputeDisplayedStats();
        var after = ComputeDisplayedStats(currentOwnership.ascensionLevel + 1);

        string message = $"Ascend {currentHero.heroName} to the next tier?\n\n" +
            UpgradeFeedbackUtility.FormatBeforeAfter("HP", before.hp.ToString(), after.hp.ToString()) + "\n" +
            UpgradeFeedbackUtility.FormatBeforeAfter("Mana", before.mana.ToString(), after.mana.ToString()) + "\n" +
            UpgradeFeedbackUtility.FormatBeforeAfter("Attack", UpgradeFeedbackUtility.AttackDisplayValue(before.dmg).ToString(), UpgradeFeedbackUtility.AttackDisplayValue(after.dmg).ToString()) + "\n" +
            UpgradeFeedbackUtility.FormatBeforeAfter("Armor", before.armor.ToString(), after.armor.ToString()) + "\n" +
            UpgradeFeedbackUtility.FormatBeforeAfter("Might", before.might.ToString(), after.might.ToString());

        ConfirmationDialog.ShowActions(canvas.transform, message, "Ascension",
            new (string, bool, System.Action)[] { ("Ascension", true, PerformAscend) });
    }

    private void PerformAscend()
    {
        if (currentHero == null || HeroCollectionManager.Instance == null) return;

        var before = ComputeDisplayedStats();
        bool success = HeroCollectionManager.Instance.AscendHero(currentHero.heroId);

        if (!success)
        {
            // Не должно случаться (OpenAscendPopup уже проверил), но на всякий случай не молчим —
            // например, если гемы утекли из-под ног между открытием попапа и подтверждением.
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null) ConfirmationDialog.ShowInfo(canvas.transform, "Not enough ascension gems (or already at max ascension).");
            return;
        }

        Refresh();
        AnimateStatGain(before); // сама решит, стоит ли что-то показывать; она же дёргает haptic при реальном изменении статов
    }

    // Клик по самому значку вознесения (не отдельная кнопка — см. project_hero_card_unification_plan).
    // Значок остаётся видимым всегда (см. HeroAscensionUtility.ApplyOverlay). Три состояния подсказки:
    // недоступно вообще (макс. вознесение/раса без него) — обводка и свечение скрыты;
    // доступно, но гемов не хватает — тонкая обводка + маленькое статичное свечение;
    // хватает гемов прямо сейчас — свечение крупнее и мерцает (см. Update/UpdateAscendGlowPulse).
    // Числового счётчика гемов на карточке больше нет — подсказка полностью визуальная.
    private bool ascendReadyNow;

    private void RefreshAscendButton()
    {
        if (currentHero == null || currentOwnership == null) return;

        int maxAscension = HeroAscensionUtility.GetMaxAscension(currentHero.rarity);
        bool relevant = maxAscension > 0 && currentOwnership.ascensionLevel < maxAscension;
        bool canAscendNow = relevant && currentOwnership.ascensionGems >= HeroAscensionUtility.GemsPerAscension;

        // Кликабельна, пока вознесение вообще актуально (relevant), не только когда гемов хватает —
        // клик теперь всегда открывает попап (см. OpenAscendPopup), просто с разным содержимым.
        if (ascendButton != null)
            ascendButton.interactable = relevant;

        if (ascendOutline != null)
            ascendOutline.gameObject.SetActive(relevant);

        if (ascendGlow != null)
        {
            ascendGlow.gameObject.SetActive(relevant);
            if (relevant && !canAscendNow)
            {
                // Просто доступно, гемов ещё не хватает — маленькое, статичное, без мерцания
                ascendGlow.rectTransform.localScale = Vector3.one;
                Color c = ascendGlow.color;
                c.a = AscendGlowIdleAlpha;
                ascendGlow.color = c;
            }
        }

        ascendReadyNow = canAscendNow; // дальше подхватывает Update() для мерцания/крупного размера
    }

    private const float AscendGlowIdleAlpha = 0.35f;    // "свечение 1" — просто доступно
    private const float AscendGlowReadyMinAlpha = 0.45f; // "свечение 3" — гемов хватает, мерцает между этими двумя
    private const float AscendGlowReadyMaxAlpha = 0.95f;
    private const float AscendGlowReadyScale = 1.7f;     // крупнее, чем статичное состояние
    private const float AscendGlowPulseSpeed = 2.5f;

    private void Update()
    {
        UpdateAscendGlowPulse();
    }

    private void UpdateAscendGlowPulse()
    {
        if (ascendGlow == null || !ascendReadyNow) return;

        ascendGlow.rectTransform.localScale = new Vector3(AscendGlowReadyScale, AscendGlowReadyScale, 1f);

        float t = (Mathf.Sin(Time.time * AscendGlowPulseSpeed) + 1f) / 2f;
        Color c = ascendGlow.color;
        c.a = Mathf.Lerp(AscendGlowReadyMinAlpha, AscendGlowReadyMaxAlpha, t);
        ascendGlow.color = c;
    }

    private static Sprite radialGlowSprite;

    // Мягкое радиальное пятно (белое, непрозрачное в центре → прозрачное к краю, квадратичный спад) —
    // тонируется цветом снаружи через Image.color. Тот же приём, что и CastleUI.GetRadialGlowSprite,
    // сгенерировано один раз и закэшировано (см. feedback_unity6_no_builtin_ui_sprites — почему не builtin).
    private static Sprite GetRadialGlowSprite()
    {
        if (radialGlowSprite != null) return radialGlowSprite;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float a = Mathf.Clamp01(1f - dist / radius);
                a *= a; // квадратичный спад — плотный центр, мягкий длинный хвост к краю
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        radialGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return radialGlowSprite;
    }

    private static Sprite squareOutlineSprite;

    // Прямоугольная рамка (белая на прозрачном), 9-слайс — обводка вокруг кнопки вознесения. Раньше тут
    // был круг: ascendButton — это Image БЕЗ спрайта (просто залитый цветом прямоугольник, m_Sprite:
    // fileID 0 в сцене), круг его реальные (прямые) края не обводил. 9-слайс нужен, чтобы толщина рамки
    // не "плыла" при любом размере RectTransform (см. AlignAscendOutlineToButton — размер берётся от
    // самой кнопки + отступ). Не используем штатный компонент Outline — он дублирует спрайт со сдвигом,
    // а не обводит по силуэту (та же оговорка, что и в CastleUI).
    private static Sprite GetSquareOutlineSprite()
    {
        if (squareOutlineSprite != null) return squareOutlineSprite;

        const int size = 32;
        const int thickness = 4;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool onBorder = x < thickness || x >= size - thickness || y < thickness || y >= size - thickness;
                tex.SetPixel(x, y, onBorder ? Color.white : new Color(1f, 1f, 1f, 0f));
            }
        }
        tex.Apply();

        squareOutlineSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(thickness, thickness, thickness, thickness));
        return squareOutlineSprite;
    }

    private const float AscendOutlinePadding = 6f; // отступ рамки НАРУЖУ от реальных краёв кнопки, на сторону

    // Копирует anchor/pivot/anchoredPosition кнопки вознесения на рамку и раздувает sizeDelta на
    // отступ — раньше эти два RectTransform были заданы независимо и по факту не совпадали (другой pivot,
    // другой размер), поэтому даже прямоугольная рамка не легла бы точно на кнопку без этого шага.
    private void AlignAscendOutlineToButton()
    {
        if (ascendOutline == null || ascendButton == null) return;

        var buttonRect = ascendButton.GetComponent<RectTransform>();
        var outlineRect = ascendOutline.rectTransform;
        if (buttonRect == null) return;

        outlineRect.anchorMin = buttonRect.anchorMin;
        outlineRect.anchorMax = buttonRect.anchorMax;
        outlineRect.pivot = buttonRect.pivot;
        outlineRect.anchoredPosition = buttonRect.anchoredPosition;
        outlineRect.sizeDelta = buttonRect.sizeDelta + new Vector2(AscendOutlinePadding * 2f, AscendOutlinePadding * 2f);
    }

    private void RefreshUpgradeButtonTheme()
    {
        if (heroUpgradeBg != null) ConfirmationDialog.StyleAsButton(heroUpgradeBg);
        if (heroUpgradeText != null) heroUpgradeText.color = ConfirmationDialog.ButtonTextColor;
    }

    // Кнопка скрывается только когда следующий уровень вообще недостижим (потолок текущей ступени
    // вознесения) — не по карману "прямо сейчас" ли он, это теперь решает само HeroUpgradeUI при открытии
    // (тот же принцип, что у ascendButton.interactable = relevant, не canAscendNow, см. RefreshAscendButton).
    private void RefreshUpgradeButtonVisibility()
    {
        if (heroUpgradeButton == null || currentHero == null || currentOwnership == null
            || HeroCollectionManager.Instance == null) return;

        int needed = HeroCollectionManager.Instance.GetExperienceNeededForLevel(currentHero.heroId, currentOwnership.level + 1);
        heroUpgradeButton.gameObject.SetActive(needed > 0);
    }
}
