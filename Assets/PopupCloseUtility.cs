using UnityEngine;
using UnityEngine.UI;

// Общий хелпер "тап мимо окна = закрыть" — проектный аудит 2026-08-19 нашёл этот же паттерн скопированным
// вручную в ~15 файлах (см. HeroCollectionUI.BuildRacePopup, SquadEditPopupUI.Build и др.), здесь —
// единая точка, чтобы новые/дочиненные попапы не плодили ещё одну копию.
public static class PopupCloseUtility
{
    // overlay — GameObject с полноэкранным затемнением (Image), window — центрированное окно поверх него.
    // Button на overlay ловит клик по затемнению; пустой блокер-Button на window нужен, потому что window —
    // ребёнок overlay, и необработанный клик внутри окна иначе всплывёт до Button на overlay и закроет попап.
    public static void AddTapOutsideToClose(GameObject overlay, RectTransform window, UnityEngine.Events.UnityAction onClose)
    {
        var dimBtn = overlay.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(onClose);

        if (window != null)
        {
            var blocker = window.gameObject.AddComponent<Button>();
            blocker.transition = Selectable.Transition.None;
        }
    }
}
