using UnityEngine;

// Handheld.Vibrate() — задокументированно безопасный no-op на платформах без вибро (PC/Editor), отдельный
// #if UNITY_ANDROID не нужен. На PC/Editor (единственный способ, которым весь проект тестировался до сих
// пор) визуально/аудибельно ничего не произойдёт — это ожидаемо, не баг, проверяется только на реальном
// Android-устройстве.
public static class HapticsUtility
{
    public static void PlayLightTap() => Handheld.Vibrate();
}
