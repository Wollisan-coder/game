using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Общая геометрия плитки "иконка + подпись" для рантайм-попапов выбора (жертва предметов, выбор
// предмета опыта, выбор героя). Раньше эти три места строили одинаковую иерархию вручную и успели
// разойтись (не все ставили рамку редкости, формат уровня отличался) — теперь каркас строится в одном месте,
// а каждый вызывающий код донастраивает только то, что у него действительно своё (текст, рамка, бедж, клик).
public static class PickerTileUtility
{
    public static GameObject BuildTile(Transform parent, string name, Color bgColor,
        out Image bg, out Image icon, out TMP_Text label, out Button button)
    {
        var entryObj = new GameObject(name, typeof(RectTransform));
        var entryRect = (RectTransform)entryObj.transform;
        entryRect.SetParent(parent, false);

        bg = entryObj.AddComponent<Image>();
        bg.color = bgColor;
        button = entryObj.AddComponent<Button>();
        button.targetGraphic = bg; // без этого interactable=false ничего не красит — плитка выглядит активной

        var iconObj = new GameObject("Icon", typeof(RectTransform));
        var iconRect = (RectTransform)iconObj.transform;
        iconRect.SetParent(entryRect, false);
        iconRect.anchorMin = new Vector2(0, 0.35f);
        iconRect.anchorMax = new Vector2(1, 1);
        iconRect.offsetMin = new Vector2(6, 0);
        iconRect.offsetMax = new Vector2(-6, -6);
        icon = iconObj.AddComponent<Image>();
        icon.preserveAspect = true;

        var labelObj = new GameObject("Label", typeof(RectTransform));
        var labelRect = (RectTransform)labelObj.transform;
        labelRect.SetParent(entryRect, false);
        labelRect.anchorMin = new Vector2(0, 0);
        labelRect.anchorMax = new Vector2(1, 0.35f);
        labelRect.offsetMin = new Vector2(4, 2);
        labelRect.offsetMax = new Vector2(-4, 0);
        label = labelObj.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;

        return entryObj;
    }
}
