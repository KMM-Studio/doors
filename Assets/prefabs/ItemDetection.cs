using UnityEngine;

public class ItemDetection : MonoBehaviour
{
    [Header("Odnośniki do UI")]
    public UI inventoryUI;
    public GameObject pickupPromptUI;

    [Header("Ustawienia wyrzucania")]
    // Punkt wyrzutu z edytora (opcjonalnie)
    public Transform dropPoint;

    private GameObject currentItemInRange = null;
    private ItemTags currentItemDetails = null; // Używam typu ItemTags z Twojego błędu

    // Zmienna blokująca podnoszenie na krótki czas po wyrzuceniu przedmiotu
    private float pickupDelay = 0f;

    void Start()
    {
        if (pickupPromptUI != null) pickupPromptUI.SetActive(false);
    }

    void Update()
    {
        // Jeśli stoper jest większy od zera, odliczamy czas w dół
        if (pickupDelay > 0f)
        {
            pickupDelay -= Time.deltaTime;
        }

        // 1. Zamiana przedmiotu po wciśnięciu E
        if (currentItemInRange != null && Input.GetKeyDown(KeyCode.E))
        {
            SwapItem(currentItemInRange, currentItemDetails);
        }

        // 2. Obsługa scrolla myszki
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f) // Scroll w górę
        {
            inventoryUI.ChangeSelectedSlot(-1);
        }
        else if (scroll < 0f) // Scroll w dół
        {
            inventoryUI.ChangeSelectedSlot(1);
        }

        // 3. Wyrzucanie przedmiotu z aktywnego slota klawiszem G
        if (Input.GetKeyDown(KeyCode.G))
        {
            DropActiveItem();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Jeśli wyrzuciliśmy przedmiot przed chwilą, ignorujemy kolizję!
        if (pickupDelay > 0f) return;

        if (other.CompareTag("Item"))
        {
            ItemTags details = other.GetComponent<ItemTags>();
            if (details == null) return;

            if (inventoryUI.HasItemType(details.itemType))
            {
                currentItemInRange = other.gameObject;
                currentItemDetails = details;
                if (pickupPromptUI != null) pickupPromptUI.SetActive(true);
            }
            else
            {
                bool added = inventoryUI.AddItem(other.gameObject, details.itemType);

                if (!added)
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
        GameObject oldItem = inventoryUI.GetAndRemoveItem(details.itemType);

        if (oldItem != null)
        {
            // Blokujemy możliwość natychmiastowego podniesienia wyrzuconego przedmiotu
            pickupDelay = 1f;

            oldItem.SetActive(true);

            Vector3 dropPosition = dropPoint != null ? dropPoint.position : transform.position + (transform.forward * 1.5f);
            oldItem.transform.position = dropPosition;
        }

        inventoryUI.AddItem(itemOnGround, details.itemType);

        currentItemInRange = null;
        currentItemDetails = null;
        if (pickupPromptUI != null) pickupPromptUI.SetActive(false);

        Debug.Log("Zamieniono przedmioty. Stary upadł na ziemię!");
    }

    private void DropActiveItem()
    {
        GameObject droppedItem = inventoryUI.DropSelected();

        if (droppedItem != null)
        {
            // Blokujemy możliwość natychmiastowego podniesienia wyrzuconego przedmiotu
            pickupDelay = 1f;

            droppedItem.SetActive(true);

            Vector3 dropPosition = dropPoint != null ? dropPoint.position : transform.position + (transform.forward * 1.5f);
            droppedItem.transform.position = dropPosition;

            // Ukrywamy prompt, jeśli wyrzuciliśmy i zrobiliśmy miejsce
            if (pickupPromptUI != null) pickupPromptUI.SetActive(false);
            currentItemInRange = null;
            currentItemDetails = null;

            Debug.Log("Wyrzucono zaznaczony przedmiot!");
        }
        else
        {
            Debug.Log("Wybrany slot jest pusty, nie ma czego wyrzucić.");
        }
    }
}