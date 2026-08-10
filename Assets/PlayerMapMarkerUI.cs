using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Значок "игрок здесь" — сам едет к ноде, где сейчас находится игрок (следующая открытая, но ещё не
// пройденная нода; либо последняя пройденная/стартовая, если открывать больше нечего — см. WorldMapManager.GetCurrentPlayerNodeId).
// Класть внутрь ТОГО ЖЕ родителя, где лежат ноды этой конкретной карты (мировой или городской) — маркер ищет
// совпадение по nodeId среди MapNodeUI-сиблингов и плавно едет к позиции найденной ноды. Работает через обычный
// Transform.position (мировые координаты), а не RectTransform/anchoredPosition — годится и для Canvas UI, и для
// 3D-сцены карты с панорамной камерой.
// Если текущая нода игрока принадлежит другой карте (например, он внутри города, а это маркер мировой карты) —
// маркер прячется целиком (SetActive(false)), а не зависает на старой/дефолтной позиции.
public class PlayerMapMarkerUI : MonoBehaviour
{
    [Header("Скорость движения между нодами (юнитов в секунду)")]
    public float moveSpeed = 8f;

    [Header("Покачивание флажка на месте (когда не едет к ноде)")]
    public float bobAmplitude = 8f; // высота покачивания в тех же юнитах, что и позиция (пиксели для Canvas UI)
    public float bobSpeed = 2f;     // колебаний в секунду

    // "Логическая" позиция ноды без покачивания — то, куда едет MoveTo и вокруг чего колеблется transform.position.
    // Раздельно от transform.position, чтобы Update() мог накладывать bob поверх нужной точки, а не поверх
    // уже сдвинутой bob'ом позиции с прошлого кадра (иначе колебание накапливалось бы/уезжало).
    private Vector3 basePosition;
    private bool isMoving;

    // Портрет вознёсшегося на максимум героя вместо флажка — визуальная награда за полное вознесение
    // (см. project_hero_ascension_system: "how they're shown on the world map"). Ничего нового в Inspector
    // не заводим — icon это Image, уже висящий на этом же GameObject (см. сцену), defaultIconSprite
    // захватывается один раз в Awake до первой подмены, чтобы было куда откатиться, если в отряде нет
    // максимально вознёсшегося героя. Так апгрейд работает сразу на всех 10 маркеров (мировая карта +
    // каждый CityMap_*) без правки сцены.
    private Image icon;
    private Sprite defaultIconSprite;
    private Outline ascensionOutline;

    private void Awake()
    {
        icon = GetComponent<Image>();
        if (icon != null) defaultIconSprite = icon.sprite;
    }

    private void OnEnable()
    {
        if (WorldMapManager.Instance != null)
            WorldMapManager.Instance.OnProgressChanged += HandleProgressChanged;

        RefreshHeroVisual();
        SnapOrMoveToCurrentNode(instant: true);
    }

    // Сначала ручной выбор игрока (HeroCollectionManager.mapAvatarHeroId, см. HeroInventoryUI "Set as Map
    // Avatar" — доступно только если тот герой ещё на максимальном вознесении, иначе игнорируем, как
    // будто выбор не задан). Если не задан/невалиден — авто-выбор среди отряда: Orange предпочтительнее
    // Purple, если максимальны оба (реже/престижнее). Никого не нашли — остаётся обычный флаг.
    private void RefreshHeroVisual()
    {
        if (icon == null) return;

        HeroData bestHero = null;
        HeroOwnershipData bestOwnership = null;

        if (HeroCollectionManager.Instance != null && HeroCollectionManager.Instance.squad != null)
        {
            string preferredId = HeroCollectionManager.Instance.mapAvatarHeroId;

            foreach (var hero in HeroCollectionManager.Instance.squad)
            {
                if (hero == null) continue;
                var ownership = HeroCollectionManager.Instance.ownership.Find(o => o.heroId == hero.heroId);
                if (ownership == null || !HeroAscensionUtility.IsMaxAscension(hero.rarity, ownership.ascensionLevel))
                    continue;

                if (!string.IsNullOrEmpty(preferredId) && hero.heroId == preferredId)
                {
                    bestHero = hero;
                    bestOwnership = ownership;
                    break; // ручной выбор всегда побеждает — дальше искать/сравнивать не нужно
                }

                if (bestHero == null || (hero.rarity == Rarity.Orange && bestHero.rarity != Rarity.Orange))
                {
                    bestHero = hero;
                    bestOwnership = ownership;
                }
            }
        }

        if (bestHero == null)
        {
            icon.sprite = defaultIconSprite;
            icon.preserveAspect = false;
            if (ascensionOutline != null) ascensionOutline.enabled = false;
            return;
        }

        icon.sprite = HeroAscensionUtility.GetDisplayPortrait(bestHero, bestOwnership);
        icon.preserveAspect = true;

        if (ascensionOutline == null)
        {
            ascensionOutline = gameObject.AddComponent<Outline>();
            ascensionOutline.effectDistance = new Vector2(4, -4);
        }
        ascensionOutline.effectColor = new Color(1f, 0.85f, 0.2f, 0.9f); // тёплое золото — тот же тон, что и glow иконок Castle
        ascensionOutline.enabled = true;
    }

    private void OnDisable()
    {
        if (WorldMapManager.Instance != null)
            WorldMapManager.Instance.OnProgressChanged -= HandleProgressChanged;
    }

    private void Update()
    {
        if (isMoving || !gameObject.activeSelf) return;

        transform.position = basePosition + new Vector3(0, Mathf.Sin(Time.time * bobSpeed) * bobAmplitude, 0);
    }

    private void HandleProgressChanged() => SnapOrMoveToCurrentNode(instant: false);

    private void SnapOrMoveToCurrentNode(bool instant)
    {
        if (WorldMapManager.Instance == null) return;

        string currentId = WorldMapManager.Instance.GetCurrentPlayerNodeId();
        Transform target = string.IsNullOrEmpty(currentId) ? null : FindNodeTransform(currentId);

        if (target == null)
        {
            // Игрок сейчас не на этой карте — прячем маркер, а не оставляем висеть на старой позиции.
            // OnDisable() сам отпишется от OnProgressChanged; заново подпишется и перепроверит при
            // следующем OnEnable() (панель этой карты открывается заново — см. MapNodeUI.OnClicked()).
            gameObject.SetActive(false);
            return;
        }

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        StopAllCoroutines();
        basePosition = target.position;

        if (instant)
        {
            isMoving = false;
            transform.position = basePosition;
        }
        else
        {
            StartCoroutine(MoveTo(basePosition));
        }
    }

    private Transform FindNodeTransform(string nodeId)
    {
        if (transform.parent == null) return null;

        var siblings = transform.parent.GetComponentsInChildren<MapNodeUI>(true);
        foreach (var nodeUi in siblings)
            if (nodeUi.node != null && nodeUi.node.nodeId == nodeId)
                return nodeUi.transform;

        return null;
    }

    private IEnumerator MoveTo(Vector3 targetPos)
    {
        isMoving = true;

        while (Vector3.Distance(transform.position, targetPos) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = targetPos;
        isMoving = false; // с этого момента Update() подхватывает bob вокруг basePosition
    }
}
