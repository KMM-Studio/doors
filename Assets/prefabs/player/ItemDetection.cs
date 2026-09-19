using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class ItemDetection : MonoBehaviour
{   

    [Header("odnośniki do playera")]
    public PlayerInfo playerInfo;
    public PlayerInput playerInput;
    public Transform dropPoint;

    private GameObject currentItemInRange = null;
    private ItemTags currentItemDetails = null; // Zapisujemy detale przedmiotu w zasięgu
    
    private float pickupCooldown = 0f;

    private void OnEnable()
    {
        ToggleAction("Interact", true);
        ToggleAction("Drop", true);
        ToggleAction("Next", true);
        ToggleAction("Previous", true);
    }

    private void OnDisable()
    {
        ToggleAction("Interact", false);
        ToggleAction("Drop", false);
        ToggleAction("Next", false);
        ToggleAction("Previous", false);
    }

    private void ToggleAction(string actionName, bool enable)
    {
        if (playerInput == null) return;
        var action = playerInput.actions.FindAction(actionName);
        if (action == null) return;

        if (enable) action.Enable();
        else action.Disable();
    }

    private void OnNext()
    {
        playerInfo.ChangeItemTypeNext();
    }

    private void OnPrevious()
    {
        playerInfo.ChangeItemTypePrevious();
    }

    private void OnDrop()
    {
        DropCurrentItem();
    }

    private void OnInteract()
    {
        if (currentItemInRange is not null)
        {
            if (!PickUpItem(currentItemDetails))
            {
                SwapItemTags(currentItemDetails);
            }
        }
    }

    private void FixedUpdate()
    {
        // Jeśli stoper jest większy od zera, odliczamy czas w dół
        if (pickupCooldown > 0f)
        {
            pickupCooldown -= Time.fixedDeltaTime;
        }
    }
    
    

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Item"))
        {
            ItemTags details = other.GetComponent<ItemTags>();

            // Zabezpieczenie: jeśli obiekt nie ma skryptu ItemDetails, ignorujemy go
            if (details is null) return;
            
            // Jeśli MAMY już ten typ w ekwipunku, blokujemy auto-podnoszenie i włączamy napis
            if (playerInfo.inventory.TryGetValue(details.itemType, out ItemTags itemTags) && itemTags is not null ) 
            {   
                Debug.Log("Picked up " + details.itemType);
                PickUpItem(details);
            }
            else
            {
                currentItemInRange = other.gameObject;
                currentItemDetails = details;
                SendMessage("UpdateItemPickup", true, SendMessageOptions.DontRequireReceiver); // message to UI
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Item") && other.gameObject == currentItemInRange)
        {
            currentItemInRange = null;
            currentItemDetails = null;
            SendMessage("UpdateItemPickup", false, SendMessageOptions.DontRequireReceiver); // message to UI
        }
    }

    public void DropCurrentItem()
    {
        var itemTagToDrop = playerInfo.currentItemTags;
        playerInfo.RemoveItemFromInventory(currentItemDetails.itemType);
        if (itemTagToDrop is not null)
        {
            DropItem(itemTagToDrop);
        }
        
    }
    public void SwapItemTags(ItemTags newItemTags)
    {
        if (pickupCooldown > 0f) return;
        pickupCooldown = playerInfo.maxPickupCooldown;
        
        var newItemGameObject = newItemTags.gameObject;
        newItemGameObject.SetActive(false);
        newItemGameObject.transform.SetParent(transform);
            
        var newItemTagsType = newItemTags.itemType;
        var oldItemTags = playerInfo.inventory[newItemTagsType];
        playerInfo.inventory[newItemTagsType] = newItemTags;

        if (oldItemTags is not null)
        {
            DropItem(oldItemTags);
        }
    }

    public bool PickUpItem(ItemTags itemTag)
    {
        if (pickupCooldown > 0f) return false; // pickup delay still didnt pass
        if (playerInfo.inventory.TryAdd(itemTag.itemType, itemTag)) return false;
        playerInfo.ChangeToItem(itemTag.itemType);
        return true;
    }
    
    public void DropItem(ItemTags oldItemTags)
    {
        var oldItemGameobject = oldItemTags.gameObject;
        oldItemGameobject.SetActive(true);
        oldItemGameobject.transform.SetParent(null);
        
        Vector3 dropPosition = dropPoint?.position ?? transform.position + (transform.forward * 1.5f);
        oldItemGameobject.transform.position = dropPosition;
    }
}