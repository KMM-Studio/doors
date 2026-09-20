/*using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace prefabs.player
{
    public class ItemDetection : MonoBehaviour
    {   
        [field: Header("UI Reference")]
        public static event Action<bool> OnItemPickupChanged;
    
        [Header("Player References")]
        public  playerInfo;
        public Transform dropPoint;

        private GameObject _currentItemInRange;
        private Item _currentItemDetails;

        // --- Input Event Callbacks ---
        public void OnNext(InputAction.CallbackContext context) { if (context.performed) playerInfo?.ChangeItemTypeNext(); }
        public void OnPrevious(InputAction.CallbackContext context) { if (context.performed) playerInfo?.ChangeItemTypePrevious(); }
        public void OnDrop(InputAction.CallbackContext context) { if (context.performed) DropCurrentItem(); }

        public void OnInteract(InputAction.CallbackContext context)
        {
            if (context.performed && _currentItemInRange != null && _currentItemDetails != null)
            {
                if (_currentItemDetails.TryToPickup(playerInfo))
                    SwapItemTags(_currentItemDetails);
            }
        }

        // --- Trigger Events ---
        private void OnTriggerEnter(Collider other)
        {   
            
            Item details = other.GetComponentInParent<Item>();
            
            if (details is null || (!other.CompareTag("Item") && !details.CompareTag("Item")) || playerInfo == null) return;
            
            
            if (!details.TryToPickup(playerInfo))
            {
                _currentItemInRange = details.gameObject;
                _currentItemDetails = details;
                OnItemPickupChanged?.Invoke(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            Item details = other.GetComponentInParent<Item>();
            if (details != null && details.gameObject == _currentItemInRange)
            {
                _currentItemInRange = null;
                _currentItemDetails = null;
                OnItemPickupChanged?.Invoke(false);
            }
        }

        // --- Item Management ---
        public void DropCurrentItem()
        {   
            if (playerInfo == null || playerInfo.currentItem == null) return;

            playerInfo.currentItem.Drop(playerInfo);
        }

        public void SwapItemTags(Item newItem)
        {
            var slotIndex = (int)newItem.itemType;
            
            var oldItem = playerInfo.inventory[slotIndex];
            
            if (oldItem != null) oldItem.Drop(playerInfo);
            newItem.TryToPickup(playerInfo);
            
            _currentItemInRange = null;
            _currentItemDetails = null;
            OnItemPickupChanged?.Invoke(false);
        }
    }
}*/