using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillEntryUI : MonoBehaviour
{
    [Header("UI елементи")]
    public TMP_Text skillNameText;
    public Button setActiveButton;
    public Button setPassiveButton;
    public GameObject activeMarker;  // показывается, если этот скилл сейчас выбран активным
    public GameObject passiveMarker; // показывается, если этот скилл сейчас выбран пассивным

    private int skillIndex;
    private HeroInventoryUI owner;

    public void Setup(SkillData skill, int index, HeroInventoryUI ownerUI)
    {
        skillIndex = index;
        owner = ownerUI;

        if (skillNameText != null)
            skillNameText.text = skill != null ? skill.skillName : "";

        if (setActiveButton != null)
        {
            setActiveButton.onClick.RemoveAllListeners();
            setActiveButton.onClick.AddListener(() => owner.SetActiveSkill(skillIndex));
        }

        if (setPassiveButton != null)
        {
            setPassiveButton.onClick.RemoveAllListeners();
            setPassiveButton.onClick.AddListener(() => owner.SetPassiveSkill(skillIndex));
        }
    }

    public void RefreshMarkers(int activeIndex, int passiveIndex)
    {
        if (activeMarker != null) activeMarker.SetActive(skillIndex == activeIndex);
        if (passiveMarker != null) passiveMarker.SetActive(skillIndex == passiveIndex);
    }
}
