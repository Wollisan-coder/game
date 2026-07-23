using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    public GameObject collectionPanel;
    public GameObject squadPanel;

    [Header("Назва бойової сцени")]
    public string battleSceneName = "SampleScene";

    private void Start()
    {
        ShowCollection();
    }

    public void ShowCollection()
    {
        collectionPanel.SetActive(true);
        squadPanel.SetActive(false);
    }

    public void ShowSquad()
    {
        collectionPanel.SetActive(false);
        squadPanel.SetActive(true);
    }

    public void StartBattle()
    {
        SceneManager.LoadScene(battleSceneName);
    }
}