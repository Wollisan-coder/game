using UnityEngine;

// УВАГА: клік по фішці вже повністю обробляється в Item.OnMouseDown().
// Цей клас дублював той самий клік окремим Raycast-ом у Update(), і обидві системи
// незалежно викликали GridManager.SwapItems(...) на один і той самий клік —
// це й спричиняло races (гонки) у сітці при швидких кліках. Логіку відключено.
public class InputManager : MonoBehaviour
{
    public GridManager gridManager;
}