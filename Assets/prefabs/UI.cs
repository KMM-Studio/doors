using UnityEngine;
using UnityEngine.UI;

public class UI : MonoBehaviour
{
    [Header("UI References")]
    public Image[] inventorySlots;
  

    private GameObject[] storedItems;
    private int currentSelectedSlot = 0; // Który slot jest teraz wybrany (0-3)

    void Start()
    {
        storedItems = new GameObject[inventorySlots.Length];
        ResetSlots();
        UpdateSelectionVisuals(); // Aktualizujemy wygląd na start
    }

    private void ResetSlots()
    {
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            storedItems[i] = null;
        }
    }

    public int GetSlotIndex(ItemType type)
    {
        switch (type)
        {
            case ItemType.Primary: return 0;
            case ItemType.Secondary: return 1;
            case ItemType.lapis: return 2;
            case ItemType.gowno: return 3;
            default: return -1;
        }
    }

    public bool HasItemType(ItemType type)
    {
        int slotIndex = GetSlotIndex(type);
        if (slotIndex >= 0 && slotIndex < storedItems.Length)
        {
            return storedItems[slotIndex] != null;
        }
        return false;
    }

    public bool AddItem(GameObject itemObj, ItemType type)
    {
        int slotIndex = GetSlotIndex(type);
        if (slotIndex >= 0 && slotIndex < inventorySlots.Length && storedItems[slotIndex] == null)
        {
            storedItems[slotIndex] = itemObj;
            itemObj.SetActive(false);
            UpdateSelectionVisuals(); // Odświeżamy kolory
            return true;
        }
        return false;
    }

    // Metoda używana do ZAMIANY
    public GameObject GetAndRemoveItem(ItemType type)
    {
        int slotIndex = GetSlotIndex(type);
        return RemoveItemByIndex(slotIndex);
    }

    // Nowa metoda do WYRZUCANIA aktywnego przedmiotu scrollem
    public GameObject DropSelected()
    {
        return RemoveItemByIndex(currentSelectedSlot);
    }

    // Wspólna logika usuwania
    private GameObject RemoveItemByIndex(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < storedItems.Length && storedItems[slotIndex] != null)
        {
            GameObject oldItem = storedItems[slotIndex];
            storedItems[slotIndex] = null;
            UpdateSelectionVisuals(); // Odświeżamy kolory
            return oldItem;
        }
        return null;
    }

    // Zmiana aktywnego slota (scroll)
    public void ChangeSelectedSlot(int direction)
    {
        currentSelectedSlot += direction;

        // Zapętlanie scrolla (jak zjedziemy poniżej zera, wraca na koniec i odwrotnie)
        if (currentSelectedSlot < 0) currentSelectedSlot = inventorySlots.Length - 1;
        if (currentSelectedSlot >= inventorySlots.Length) currentSelectedSlot = 0;

        UpdateSelectionVisuals();
    }

    // Funkcja aktualizująca wygląd (kolory i rozmiar)
    private void UpdateSelectionVisuals()
    {
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (inventorySlots[i] == null) continue;

            bool isSelected = (i == currentSelectedSlot);
            bool hasItem = (storedItems[i] != null);

            if (isSelected)
            {
                // Zaznaczony: mocny żółty jeśli pełny, przezroczysty żółty jeśli pusty
                inventorySlots[i].color = hasItem ? Color.yellow : new Color(1f, 0.92f, 0.016f, 0.5f);
                inventorySlots[i].transform.localScale = new Vector3(1.15f, 1.15f, 1f); // Lekko powiększony
            }
            else
            {
                // Niezaznaczony: biały jeśli pełny, przezroczysty szary jeśli pusty
                inventorySlots[i].color = hasItem ? Color.white : new Color(1f, 1f, 1f, 0.2f);
                inventorySlots[i].transform.localScale = Vector3.one; // Normalny rozmiar
            }
        }
    }
}