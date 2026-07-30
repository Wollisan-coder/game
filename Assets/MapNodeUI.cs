using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Вешается на каждую кнопку-ноду карты. Показывает locked/completed состояние.
// По клику либо уходит в бой (обычная нода), либо открывает внутреннюю карту города (если задан cityMapPanel) —
// т.е. один и тот же компонент обслуживает и боевые ноды, и ноды-города, без дублирования логики замка/галочки.
public class MapNodeUI : MonoBehaviour
{
    public MapNodeData node;

    [Header("Визуал")]
    public Button button;
    public Image icon;
    public GameObject lockedOverlay;  // например, серая плашка/замок поверх иконки — показывается, пока нода закрыта
    public GameObject completedMark;  // например, галочка/звезда — показывается на уже пройденной ноде

    [Header("Сцена боя (для обычной ноды — оставить cityMapPanel пустым)")]
    public string battleSceneName = "SampleScene";

    [Header("Нода-город (если задано — клик открывает внутреннюю карту вместо боя)")]
    public GameObject worldMapContent; // что скрыть при входе в город (сама мировая карта/панель)
    public GameObject cityMapPanel;    // что показать — панель с дочерними нодами этого города

    private void Awake()
    {
        if (button != null)
            button.onClick.AddListener(OnClicked);
    }

    private void OnEnable() => Refresh();

    // Вызывать также при обновлении всей карты (например, при возврате из боя)
    public void Refresh()
    {
        if (node == null) return;

        bool unlocked = WorldMapManager.Instance != null && WorldMapManager.Instance.IsUnlocked(node);
        bool completed = WorldMapManager.Instance != null && WorldMapManager.Instance.IsCompleted(node);

        if (icon != null) icon.sprite = node.icon;
        if (button != null) button.interactable = unlocked;
        if (lockedOverlay != null) lockedOverlay.SetActive(!unlocked);
        if (completedMark != null) completedMark.SetActive(completed);
    }

    private void OnClicked()
    {
        if (WorldMapManager.Instance == null || node == null) return;

        if (cityMapPanel != null)
        {
            // Нода-город: не запускаем бой, просто переключаем видимую панель на внутреннюю карту города
            if (!WorldMapManager.Instance.IsUnlocked(node)) return;

            if (worldMapContent != null) worldMapContent.SetActive(false);
            cityMapPanel.SetActive(true);
            return;
        }

        if (!WorldMapManager.Instance.SelectNode(node)) return;

        // Запоминаем имя своей панели (WorldMapPanel либо конкретный CityMap_...), чтобы после боя
        // MainMenuUI вернул игрока именно туда, а не на общую мировую карту по умолчанию.
        WorldMapManager.Instance.lastActiveMapPanelName = transform.parent != null ? transform.parent.name : null;

        SceneManager.LoadScene(battleSceneName);
    }
}
