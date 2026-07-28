using UnityEngine;

// ВНИМАНИЕ: клик по фишке уже полностью обрабатывается в Item.OnMouseDown().
// Этот класс дублировал тот же клик отдельным Raycast-ом в Update(), и обе системы
// независимо вызывали GridManager.SwapItems(...) на один и тот же клик —
// это и вызывало races (гонки) в сетке при быстрых кликах. Логика отключена.
public class InputManager : MonoBehaviour
{
    public GridManager gridManager;
}