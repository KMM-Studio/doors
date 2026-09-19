using UnityEngine;
using UnityEngine.UI;

public class UI : MonoBehaviour
{
    [Header("UI References")]
    public Image[] inventorySlots;

    // Tablica, która śledzi, czy dany slot jest obecnie zajęty
    private bool[] isSlotOccupied;

    void Start()
    {
        // Inicjalizujemy tablicę z ilością miejsc odpowiadającą ilości slotów UI
        isSlotOccupied = new bool[inventorySlots.Length];
        ResetSlots();
    }

    private void ResetSlots()
    {
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (inventorySlots[i] != null)
                inventorySlots[i].color = new Color(1f, 1f, 1f, 0.2f);

            isSlotOccupied[i] = false;
        }
    }

    // Tłumaczymy typ przedmiotu na konkretny numer slotu (0 to pierwszy slot od lewej)
    public int GetSlotIndex(ItemType type)
    {
        switch (type)
        {
            case ItemType.Primary: return 0;     // Broń zawsze w slocie nr 1
            case ItemType.Secondary: return 1; // Apteczka zawsze w slocie nr 2
            case ItemType.lapis: return 2;    // Klucz zawsze w slocie nr 3
            case ItemType.gowno: return 3; // Amunicja zawsze w slocie nr 4
            default: return -1;               // Błąd / brak przypisania
        }
    }

    // Sprawdzamy, czy slot dedykowany dla tego typu jest już zajęty
    public bool HasItemType(ItemType type)
    {
        int slotIndex = GetSlotIndex(type);

        if (slotIndex >= 0 && slotIndex < isSlotOccupied.Length)
        {
            return isSlotOccupied[slotIndex];
        }
        return false;
    }

    public bool AddItem(ItemType type)
    {
        int slotIndex = GetSlotIndex(type);

        // Jeśli slot istnieje i jest pusty, dodajemy przedmiot
        if (slotIndex >= 0 && slotIndex < inventorySlots.Length && !isSlotOccupied[slotIndex])
        {
            isSlotOccupied[slotIndex] = true;
            inventorySlots[slotIndex].color = Color.white; // Kolorowanie zajętego slotu
            Debug.Log($"Dodano {type} do stałego slotu nr {slotIndex + 1}");
            return true;
        }
        return false;
    }
}