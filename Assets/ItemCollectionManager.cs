using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ItemCollectionManager : MonoBehaviour
{
    public static ItemCollectionManager Instance { get; private set; }

    [Header("Всі предмети гри (каталог)")]
    public ItemData[] allItems;

    [Header("Предмети, якими володіє гравець")]
    public List<string> ownedItemIds = new List<string>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadOwnedItems();

        foreach (var item in allItems)
            UnlockItem(item); // ВРЕМЕННО для тесту, як і герої/вороги
    }

    public bool IsOwned(ItemData item) => item != null && ownedItemIds.Contains(item.itemId);

    public void UnlockItem(ItemData item)
    {
        if (item == null || ownedItemIds.Contains(item.itemId)) return;

        ownedItemIds.Add(item.itemId);
        SaveOwnedItems();
    }

    public ItemData GetItemById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return allItems.FirstOrDefault(i => i.itemId == id);
    }

    // Всі предмети певного типу слота — і отримані, і ще ні (для показу "наявності")
    public List<ItemData> GetItemsOfType(EquipmentSlotType slotType)
    {
        return allItems.Where(i => i.slotType == slotType).ToList();
    }

    private void SaveOwnedItems()
    {
        PlayerPrefs.SetString("owned_item_ids", string.Join(",", ownedItemIds));
        PlayerPrefs.Save();
    }

    private void LoadOwnedItems()
    {
        ownedItemIds.Clear();

        string saved = PlayerPrefs.GetString("owned_item_ids", "");
        if (string.IsNullOrEmpty(saved)) return;

        ownedItemIds.AddRange(saved.Split(','));
    }
}
