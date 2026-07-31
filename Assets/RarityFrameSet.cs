using UnityEngine;

// Общий набор рамок редкости — один ассет на всю игру (Collection card, Hero inventory, Squad slot),
// чтобы не дублировать одни и те же 5 спрайтов в каждом UI-компоненте отдельно.
[CreateAssetMenu(fileName = "RarityFrameSet", menuName = "UI/Rarity Frame Set")]
public class RarityFrameSet : ScriptableObject
{
    // Индекс = (int)Rarity (White/Green/Blue/Purple/Orange). White больше не выдаётся игрокам —
    // слот под него можно оставить пустым, GetFrame() тогда просто вернёт null.
    public Sprite[] frames;

    public Sprite GetFrame(Rarity rarity)
    {
        int index = (int)rarity;
        return frames != null && index >= 0 && index < frames.Length ? frames[index] : null;
    }
}
