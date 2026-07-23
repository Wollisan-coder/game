using UnityEngine;
using UnityEngine.UI;

public class HeroCollectionCardUI : MonoBehaviour
{
    public Image portrait;
    public Image lockOverlay;
    public Button selectButton;

    private HeroData heroData;
    private HeroCollectionManager collectionManager;

    public void Setup(HeroData data, HeroCollectionManager manager)
    {
        heroData = data;
        collectionManager = manager;

        portrait.sprite = data.portrait;

        bool unlocked = manager.IsUnlocked(data);
        lockOverlay.gameObject.SetActive(!unlocked);
        selectButton.interactable = unlocked;

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(OnSelected);
    }

    private void OnSelected()
    {
        bool added = collectionManager.AddToSquad(heroData);
        if (!added)
            Debug.Log("Не вдалося додати героя — загін повний, герой вже в загоні, або не відкритий.");
    }
}