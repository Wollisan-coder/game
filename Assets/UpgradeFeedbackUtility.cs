using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Общий "было -> стало" визуальный язык для экранов прокачки (герой в HeroInventoryUI и HeroUpgradeUI,
// предмет в ItemSacrificeUI) — единая палитра, вспышка панели и Attack-конвертация, чтобы экраны не
// разъезжались по ощущениям.
public static class UpgradeFeedbackUtility
{
    public static readonly Color OldValueColor = new Color(0.65f, 0.65f, 0.65f);
    public static readonly Color NewValueColor = FloatingDamageText.PositiveGainColor;

    // damageMultiplier — множитель 1.0 = "стандартный урон" (см. BattleManager.GetHeroDamageMultiplier),
    // а не сам стат. "Attack" — просто более привычное игроку отображение того же числа ×100 (1.25 -> 125),
    // сам расчёт урона в бою эту цифру не использует. Общая для HeroInventoryUI/HeroUpgradeUI (герой) и
    // ItemSacrificeUI (Weapon-слот) — раньше было 2 независимые копии этой формулы, разъезжались бы молча.
    public static int AttackDisplayValue(float damageMultiplier) => Mathf.RoundToInt(damageMultiplier * 100f);

    // TMP-теги <color> — старое значение серым, новое зелёным со стрелкой. label остаётся обычным белым.
    public static string FormatBeforeAfter(string label, string oldValue, string newValue)
    {
        string oldHex = ColorUtility.ToHtmlStringRGB(OldValueColor);
        string newHex = ColorUtility.ToHtmlStringRGB(NewValueColor);
        return $"{label}: <color=#{oldHex}>{oldValue}</color> <color=#{newHex}>→ {newValue}</color>";
    }

    // Возвращает Coroutine — вызывающий должен сам хранить ссылку и останавливать предыдущую вспышку перед
    // запуском новой (статический хелпер не может безопасно держать per-host состояние сам).
    public static Coroutine FlashPanel(MonoBehaviour host, Image panel, Color color)
    {
        if (host == null || panel == null || !host.isActiveAndEnabled) return null;
        return host.StartCoroutine(FlashPanelRoutine(panel, color));
    }

    private static IEnumerator FlashPanelRoutine(Image panel, Color color)
    {
        const float duration = 0.6f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0.55f, 0f, elapsed / duration);
            panel.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }
        panel.color = new Color(color.r, color.g, color.b, 0f);
    }
}
