using UnityEngine;

public class BattleHeroCardsSpawner : MonoBehaviour
{
    public BattleManager battleManager;
    public Transform cardsContainer; // HeroCardsPanel
    public GameObject heroCardPrefab; // HeroBattleCard

    private void Start()
    {
        SpawnCards();
    }

    private void SpawnCards()
    {
        foreach (Transform child in cardsContainer)
            Destroy(child.gameObject);

        foreach (var heroState in battleManager.activeHeroes)
        {
            GameObject cardObj = Instantiate(heroCardPrefab, cardsContainer);
            cardObj.SetActive(true);
            var cardUI = cardObj.GetComponent<HeroCardUI>();
            cardUI.heroData = heroState.data;
            cardUI.battleManager = battleManager;
            // Start() у HeroCardUI сам підхопить heroData через ApplyHeroData()
        }
    }
}