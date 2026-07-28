using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class Item : MonoBehaviour
{
    public int x;
    public int y;
    public int type;

    [Header("Ефекти")]
    public GameObject destroyEffectPrefab; // призначити в Inspector на кожному префабі геема

    private GridManager gridManager;
    private static Item firstSelected;
    private Coroutine selectAnimCoroutine;
    private Vector3 baseScale;

    private void Awake()
    {
        gridManager = FindAnyObjectByType<GridManager>();
        baseScale = transform.localScale;
    }

    [Header("Свайп")]
    public float swipeThresholdPixels = 50f; // менший рух миші/пальця — вважається тапом, не свайпом

    private Vector3 mouseDownScreenPos;

    private void OnMouseDown()
    {
        gridManager?.ResetIdleTimer(); // будь-який клік скидає таймер підказки і ховає її

        if (gridManager != null && gridManager.isBusy) return; // поле ще анімується — ігноруємо клік

        mouseDownScreenPos = Mouse.current.position.ReadValue();
    }

    private void OnMouseUp()
    {
        if (gridManager != null && gridManager.isBusy) return;

        Vector3 delta = (Vector3)Mouse.current.position.ReadValue() - mouseDownScreenPos;

        if (delta.magnitude >= swipeThresholdPixels)
            HandleSwipe(delta);
        else
            HandleTap();
    }

    // Свайп напряму рухає фішку до сусіда в бік свайпу, минаючи систему тап-тап-вибору
    private void HandleSwipe(Vector3 screenDelta)
    {
        if (firstSelected != null)
        {
            firstSelected.SetSelected(false);
            firstSelected = null;
        }

        int dx = 0, dy = 0;
        if (Mathf.Abs(screenDelta.x) > Mathf.Abs(screenDelta.y))
            dx = screenDelta.x > 0 ? 1 : -1;
        else
            dy = screenDelta.y > 0 ? 1 : -1;

        Item target = gridManager != null ? gridManager.GetItemAt(x + dx, y + dy) : null;
        if (target != null)
            gridManager.StartCoroutine(gridManager.SwapItems(this, target));
    }

    private void HandleTap()
    {
        if (firstSelected == null)
        {
            firstSelected = this;
            SetSelected(true);
        }
        else
        {
            firstSelected.SetSelected(false); // знімаємо підсвітку з попередньої

            if (IsNeighbor(firstSelected, this))
            {
                gridManager.StartCoroutine(
                    gridManager.SwapItems(firstSelected, this)
                );
            }

            firstSelected = null;
        }
    }

    private void SetSelected(bool isSelected)
    {
        if (selectAnimCoroutine != null) StopCoroutine(selectAnimCoroutine);

        if (isSelected)
            selectAnimCoroutine = StartCoroutine(PulseRoutine());
        else
            transform.localScale = baseScale;
    }

    private IEnumerator PulseRoutine()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime * 4f;
            float scaleMod = 1f + Mathf.Sin(t) * 0.1f; // пульсація ±10%
            transform.localScale = baseScale * scaleMod;
            yield return null;
        }
    }

    private Coroutine hintAnimCoroutine;

    // Викликається з GridManager, коли ця фішка — підказка можливого ходу
    public void SetHighlighted(bool isHighlighted)
    {
        if (hintAnimCoroutine != null) StopCoroutine(hintAnimCoroutine);

        if (isHighlighted)
            hintAnimCoroutine = StartCoroutine(HintPulseRoutine());
        else
            transform.localScale = baseScale;
    }

    private IEnumerator HintPulseRoutine()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime * 3f;
            float scaleMod = 1f + Mathf.Sin(t) * 0.15f; // помітніша пульсація, ніж звичайне виділення
            transform.localScale = baseScale * scaleMod;
            yield return null;
        }
    }

    private bool IsNeighbor(Item a, Item b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return (dx == 1 && dy == 0) || (dx == 0 && dy == 1);
    }

    public void MoveTo(Vector3 targetPosition, float duration = 0.2f)
    {
        StopAllCoroutines();
        StartCoroutine(MoveRoutine(targetPosition, duration));
    }

    private IEnumerator MoveRoutine(Vector3 target, float duration)
    {
        Vector3 start = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(start, target, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = target;
    }

    public IEnumerator PlayDestroyAnimation()
    {
        // Запускаємо частинки перед зникненням
        if (destroyEffectPrefab != null)
{
    GameObject effect = Instantiate(destroyEffectPrefab, transform.position, Quaternion.identity);

    var renderer = GetComponentInChildren<Renderer>();
    var ps = effect.GetComponent<ParticleSystem>();
    if (renderer != null && ps != null)
    {
        Color gemColor = Color.white;

        if (renderer.material.HasProperty("_Color"))
            gemColor = renderer.material.color;
        else if (renderer.material.HasProperty("_BaseColor"))
            gemColor = renderer.material.GetColor("_BaseColor");

        var main = ps.main;
        main.startColor = gemColor;

        ps.Play(); // явно запускаємо систему частинок
    }

    Destroy(effect, 1f);
}

        float duration = 0.25f;
        float t = 0f;
        Vector3 startScale = transform.localScale;

        while (t < duration)
        {
            t += Time.deltaTime;
            float p = t / duration;
            float scaleMod = Mathf.Lerp(1f, 0f, p) + Mathf.Sin(p * Mathf.PI) * 0.3f;
            transform.localScale = startScale * Mathf.Max(scaleMod, 0f);
            transform.Rotate(Vector3.up, 360f * Time.deltaTime);
            yield return null;
        }

        transform.localScale = Vector3.zero;
    }
}