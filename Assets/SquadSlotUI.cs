using UnityEngine;
using UnityEngine.UI;

public class SquadSlotUI : MonoBehaviour
{
    public Image portrait;
    public Button removeButton;
    public GameObject emptyPlaceholder;

    private HeroData heroData;
    private SquadUI parentUI;

    public void SetHero(HeroData data, SquadUI squadUI)
    {
        heroData = data;
        parentUI = squadUI;

        portrait.gameObject.SetActive(true);
        portrait.sprite = data.portrait;
        emptyPlaceholder.SetActive(false);

        removeButton.onClick.RemoveAllListeners();
        removeButton.onClick.AddListener(OnRemoveClicked);
    }

    public void SetEmpty()
    {
        heroData = null;
        portrait.gameObject.SetActive(false);
        emptyPlaceholder.SetActive(true);
    }

    private void OnRemoveClicked()
    {
        if (heroData == null) return;

        parentUI.collectionManager.RemoveFromSquad(heroData);
        parentUI.RefreshSlots();
    }
}