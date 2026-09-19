using UnityEngine;

public class ItemDetection : MonoBehaviour
{
    [Header("Odnośniki do UI")]
    public UI inventoryUI;
    public GameObject pickupPromptUI;

    private GameObject currentItemInRange = null;
    private ItemTags currentItemDetails = null; // Zapisujemy detale przedmiotu w zasięgu

    void Start()
    {
        if (pickupPromptUI != null) pickupPromptUI.SetActive(false);
    }

    void Update()
    {
        // Zamiana następuje po wciśnięciu E, gdy mamy przedmiot w zasięgu
        if (currentItemInRange != null && Input.GetKeyDown(KeyCode.E))
        {
            SwapItem(currentItemInRange, currentItemDetails);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Item"))
        {
            ItemTags details = other.GetComponent<ItemTags>();

            // Zabezpieczenie: jeśli obiekt nie ma skryptu ItemDetails, ignorujemy go
            if (details == null) return;

            // Jeśli MAMY już ten typ w ekwipunku, blokujemy auto-podnoszenie i włączamy napis
            if (inventoryUI.HasItemType(details.itemType))
            {
                currentItemInRange = other.gameObject;
                currentItemDetails = details;
                if (pickupPromptUI != null) pickupPromptUI.SetActive(true);
            }
            else
            {
                // Jeśli nie mamy tego typu, próbujemy go dodać (jeśli jest miejsce)
                bool added = inventoryUI.AddItem(details.itemType);

                if (added)
                {
                    Destroy(other.gameObject);
                }
                else // Ekwipunek jest pełny innych przedmiotów
                {
                    currentItemInRange = other.gameObject;
                    currentItemDetails = details;
                    if (pickupPromptUI != null) pickupPromptUI.SetActive(true);
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Item") && other.gameObject == currentItemInRange)
        {
            currentItemInRange = null;
            currentItemDetails = null;
            if (pickupPromptUI != null) pickupPromptUI.SetActive(false);
        }
    }

    private void SwapItem(GameObject itemOnGround, ItemTags details)
    {
        Debug.Log("Zamieniono przedmiot typu: " + details.itemType);

        // Tutaj docelowo zaimplementujemy fizyczne upuszczenie starego przedmiotu
        Destroy(itemOnGround);

        currentItemInRange = null;
        currentItemDetails = null;
        if (pickupPromptUI != null) pickupPromptUI.SetActive(false);
    }
}