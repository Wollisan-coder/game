using UnityEngine;

public class InputManager : MonoBehaviour
{
    public GridManager gridManager;
    private Item selectedItem;

    private void Update()
    {
         Debug.Log("Update тіка");
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Item clickedItem = hit.collider.GetComponent<Item>();
                if (clickedItem != null)
                {
                    OnItemClicked(clickedItem);
                }
            }
        }
    }

    private void OnItemClicked(Item item)
    {
        if (selectedItem == null)
        {
            selectedItem = item; // Выбираем первую фишку
        }
        else
        {
            // Проверяем, являются ли фишки соседними
            if (IsAdjacent(selectedItem, item))
            {
                StartCoroutine(gridManager.SwapItems(selectedItem, item));
                selectedItem = null;
            }
            else
            {
                selectedItem = item; // Перевыбор, если нажали на удаленную фишку
            }
        }
    }

    private bool IsAdjacent(Item a, Item b)
    {
        return (Mathf.Abs(a.x - b.x) == 1 && a.y == b.y) || 
               (Mathf.Abs(a.y - b.y) == 1 && a.x == b.x);
    }
}