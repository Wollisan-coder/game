using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Вешается на каждую кнопку-ноду карты мира. Показывает locked/completed состояние и по клику уходит в бой.
public class MapNodeUI : MonoBehaviour
{
    public MapNodeData node;

    [Header("Визуал")]
    public Button button;
    public Image icon;
    public GameObject lockedOverlay;  // например, серая плашка/замок поверх иконки — показывается, пока нода закрыта
    public GameObject completedMark;  // например, галочка/звезда — показывается на уже пройденной ноде

    [Header("Сцена боя")]
    public string battleSceneName = "SampleScene";

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
        if (!WorldMapManager.Instance.SelectNode(node)) return;

        SceneManager.LoadScene(battleSceneName);
    }
}
