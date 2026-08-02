using System.Collections;
using UnityEngine;

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

    private void OnEnable()
    {
        if (WorldMapManager.Instance != null)
            WorldMapManager.Instance.OnProgressChanged += HandleProgressChanged;

        SnapOrMoveToCurrentNode(instant: true);
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
