using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class ItemDetection : MonoBehaviour
{
    [Header("Odnośniki do UI")]
    public UI inventoryUI;
    public GameObject pickupPromptUI;
    public PlayerInput playerInput;

    private GameObject currentItemInRange = null;
    private ItemTags currentItemDetails = null; // Zapisujemy detale przedmiotu w zasięgu

    void Start()
    {
        if (pickupPromptUI != null) pickupPromptUI.SetActive(false);
    }

    private void OnEnable()
    {
        ToggleAction("Interact", true);
    }

    private void OnDisable()
    {
        ToggleAction("Interact", false);
    }

    private void ToggleAction(string actionName, bool enable)
    {
        if (playerInput == null) return;
        var action = playerInput.actions.FindAction(actionName);
        if (action == null) return;

        if (enable) action.Enable();
        else action.Disable();
    }

    void Update()
    {
        // Zamiana następuje po wciśnięciu E, gdy mamy przedmiot w zasięgu
        if (currentItemInRange is not null && playerInput.actions.FindAction("Interact").ReadValue<bool>() )
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