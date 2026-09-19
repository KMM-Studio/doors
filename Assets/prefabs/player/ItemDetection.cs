using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace prefabs.player
{
    public class ItemDetection : MonoBehaviour
    {   
        [field: Header("UI Reference")]
        public static event Action<bool> OnItemPickupChanged;
    
        [Header("Player References")]
        public PlayerInfo playerInfo;
        public PlayerInput playerInput;
        public Transform dropPoint;

        private GameObject _currentItemInRange;
        private ItemTags _currentItemDetails;

        private float _pickupCooldown;

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
            if (playerInput == null)
            {
                Debug.LogWarning("[ItemDetection] playerInput reference is missing.");
                return;
            }

            var action = playerInput.actions?.FindAction(actionName);
            if (action == null)
            {
                Debug.LogWarning($"[ItemDetection] Action '{actionName}' not found in PlayerInput actions.");
                return;
            }

            if (enable) action.Enable();
            else action.Disable();
        }

        private void OnNext() => playerInfo.ChangeItemTypeNext();
        private void OnPrevious() => playerInfo.ChangeItemTypePrevious();
        private void OnDrop() => DropCurrentItem();

        private void OnInteract()
        {
            Debug.Log($"[ItemDetection] Interact pressed. In range: {(_currentItemInRange != null ? _currentItemInRange.name : "null")}");
            if (_currentItemInRange is not null && _currentItemDetails is not null)
            {
                if (!PickUpItem(_currentItemDetails))
                {
                    Debug.Log("[ItemDetection] PickUpItem returned false; attempting SwapItemTags.");
                    SwapItemTags(_currentItemDetails);
                }
            }
        }

        private void FixedUpdate()
        {
            if (_pickupCooldown > 0f)
            {
                _pickupCooldown -= Time.fixedDeltaTime;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"[ItemDetection] OnTriggerEnter hit by: '{other.name}' (Tag: '{other.tag}')");

            if (!other.CompareTag("Item"))
            {
                Debug.Log($"[ItemDetection] Ignored '{other.name}' because tag is '{other.tag}', not 'Item'.");
                return;
            }

            ItemTags details = other.GetComponent<ItemTags>();
            if (details is null)
            {
                Debug.LogWarning($"[ItemDetection] GameObject '{other.name}' has 'Item' tag but lacks the ItemTags component!");
                return;
            }

            if (playerInfo == null)
            {
                Debug.LogError("[ItemDetection] playerInfo reference is null! Assign it in the Inspector.");
                return;
            }

            bool slotOccupied = playerInfo.inventory.TryGetValue(details.itemType, out ItemTags currentItem) && currentItem is not null;
            Debug.Log($"[ItemDetection] Item detected: {details.itemType}. Slot occupied: {slotOccupied}");

            if (!slotOccupied)
            {   
                Debug.Log($"[ItemDetection] Slot for {details.itemType} is empty. Auto-picking up...");
                PickUpItem(details);
            }
            else
            {
                Debug.Log($"[ItemDetection] Slot for {details.itemType} is full. Prompting player for swap.");
                _currentItemInRange = other.gameObject;
                _currentItemDetails = details;
                OnItemPickupChanged?.Invoke(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Item") && other.gameObject == _currentItemInRange)
            {
                Debug.Log($"[ItemDetection] Exited trigger of item in range: '{other.name}'");
                _currentItemInRange = null;
                _currentItemDetails = null;
                OnItemPickupChanged?.Invoke(false);
            }
        }

        public void DropCurrentItem()
        {
            var itemTagToDrop = playerInfo.currentItemTags;
            if (itemTagToDrop is null)
            {
                Debug.LogWarning("[ItemDetection] Cannot drop item: currentItemTags is null.");
                return;
            }

            Debug.Log($"[ItemDetection] Dropping current item: {itemTagToDrop.itemType} ({itemTagToDrop.name})");
            playerInfo.RemoveItemFromInventory(playerInfo.currentItemType);
            DropItem(itemTagToDrop);
        }

        public void SwapItemTags(ItemTags newItemTags)
        {
            if (_pickupCooldown > 0f)
            {
                Debug.LogWarning($"[ItemDetection] Cannot swap item: cooldown active ({_pickupCooldown:F2}s left).");
                return;
            }

            _pickupCooldown = playerInfo.maxPickupCooldown;
        
            var newItemGameObject = newItemTags.gameObject;
            newItemGameObject.SetActive(false);
            newItemGameObject.transform.SetParent(transform);
            
            var newItemTagsType = newItemTags.itemType;
            var oldItemTags = playerInfo.inventory[newItemTagsType];
        
            playerInfo.inventory[newItemTagsType] = newItemTags;
            playerInfo.ChangeToItem(newItemTagsType);

            if (oldItemTags is not null)
            {
                Debug.Log($"[ItemDetection] Swapped out {oldItemTags.itemType}; dropping old item.");
                DropItem(oldItemTags);
            }

            _currentItemInRange = null;
            _currentItemDetails = null;
            OnItemPickupChanged?.Invoke(false);
        }

        public bool PickUpItem(ItemTags itemTag)
        {
            if (itemTag == null)
            {
                Debug.LogWarning("[ItemDetection] PickUpItem called with null itemTag.");
                return false;
            }

            if (_pickupCooldown > 0f)
            {
                Debug.LogWarning($"[ItemDetection] Cannot pickup {itemTag.itemType}: cooldown active ({_pickupCooldown:F2}s left).");
                return false;
            }

            if (playerInfo.inventory.TryGetValue(itemTag.itemType, out ItemTags existingItem) && existingItem is not null)
            {
                Debug.Log($"[ItemDetection] PickUpItem rejected: Slot {itemTag.itemType} already holds {existingItem.name}.");
                return false;
            }

            playerInfo.inventory[itemTag.itemType] = itemTag;

            GameObject itemObj = itemTag.gameObject;
            itemObj.SetActive(false);
            itemObj.transform.SetParent(transform);

            playerInfo.ChangeToItem(itemTag.itemType);
            Debug.Log($"[ItemDetection] Successfully picked up and equipped: {itemTag.itemType} ({itemObj.name})");

            if (_currentItemInRange == itemObj)
            {
                _currentItemInRange = null;
                _currentItemDetails = null;
                OnItemPickupChanged?.Invoke(false);
            }

            return true;
        }
    
        public void DropItem(ItemTags oldItemTags)
        {
            var oldItemGameobject = oldItemTags.gameObject;
            oldItemGameobject.transform.SetParent(null);
        
            Vector3 dropPosition = dropPoint != null ? dropPoint.position : transform.position + (transform.forward * 1.5f);
            oldItemGameobject.transform.position = dropPosition;
            oldItemGameobject.SetActive(true);
            Debug.Log($"[ItemDetection] Spawned dropped item at position: {dropPosition}");
        }
    }
}