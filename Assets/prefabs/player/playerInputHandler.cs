using System;
using prefabs.item;
using UnityEngine;
using UnityEngine.InputSystem;

namespace prefabs.player
{
    /// <summary>
    /// Handles player input actions, manages inventory slot cycling, and processes physical item interactions 
    /// via trigger colliders within the game world.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PlayerInputHandler : MonoBehaviour
    {   
        [field: Header("UI Reference")]
        [field: Tooltip("Fired when an item enters or exits the pickup range. True if in range, false otherwise.")]
        public static event Action<bool> OnItemPickupChanged;
    
        [Space(10)]
        [Header("Player References")]
        [Tooltip("Reference to the player's inventory system. Required for picking up and dropping items.")]
        public PlayerInventory inventory;

        [Space(10)]
        [Header("Debug Settings")]
        [Tooltip("Toggle visual gizmos and rich console logging for input and item interactions.")]
        [SerializeField] private bool enableDebug = true;

        // Caches the physical component instead of the GameObject or ScriptableObject
        private Item _itemInRange; 

        private void Awake()
        {
            // Fail-Safe Asserts: Validating critical references before gameplay begins
            Debug.Assert(inventory != null, "<color=red><b>[PlayerInputHandler]</b></color> PlayerInventory reference is missing! Please assign it in the Inspector.", this);
            
            Collider[] cols = GetComponents<Collider>();
            bool hasTrigger = false;

            foreach (var col in cols)
            {
                if (col != null && col.isTrigger)
                {
                    hasTrigger = true;
                    break; // We found one, no need to check the rest
                }
            }

            Debug.Assert(hasTrigger, "<color=red><b>[PlayerInputHandler]</b></color> Missing a trigger Collider on this GameObject! Interaction triggers will not fire.", this);
            
        }

        // --- Input Event Callbacks ---

        /// <summary>
        /// Cycles to the next item slot in the inventory.
        /// </summary>
        /// <param name="context">The input action callback context.</param>
        public void OnNext(InputAction.CallbackContext context) 
        { 
            if (context.performed) 
            {
                if (enableDebug) Debug.Log("<color=cyan><b>[PlayerInputHandler]</b></color> Input: <color=white>Cycle Next</color>");
                inventory?.CycleNext(); 
            }
        }

        /// <summary>
        /// Cycles to the previous item slot in the inventory.
        /// </summary>
        /// <param name="context">The input action callback context.</param>
        public void OnPrevious(InputAction.CallbackContext context) 
        { 
            if (context.performed)
            {
                if (enableDebug) Debug.Log("<color=cyan><b>[PlayerInputHandler]</b></color> Input: <color=white>Cycle Previous</color>");
                inventory?.CyclePrevious();
            }
        }
        
        /// <summary>
        /// Drops the currently equipped item into the world.
        /// </summary>
        /// <param name="context">The input action callback context.</param>
        public void OnDrop(InputAction.CallbackContext context) 
        { 
            if (context.performed && inventory != null) 
            {
                if (enableDebug) Debug.Log($"<color=cyan><b>[PlayerInputHandler]</b></color> Input: <color=white>Drop Item</color>. Dropping: <color=yellow>{inventory.CurrentItemType}</color>");
                // Tells the inventory to drop whatever is in the currently equipped slot
                inventory.DropCurrentItem(); 
            }
        }

        /// <summary>
        /// Attempts to manually interact with or swap an item currently in range.
        /// </summary>
        /// <param name="context">The input action callback context.</param>
        public void OnInteract(InputAction.CallbackContext context)
        {
            if (context.performed && _itemInRange != null)
            {
                if (enableDebug) Debug.Log($"<color=cyan><b>[PlayerInputHandler]</b></color> Input: <color=white>Interact</color>. Swapping with: <color=yellow>{_itemInRange.name}</color>");
                SwapItems(_itemInRange);
            }
            else if (context.performed && enableDebug)
            {
                Debug.Log("<color=cyan><b>[PlayerInputHandler]</b></color> Input: <color=white>Interact</color> ignored. No valid item in range.");
            }
        }

        // --- Trigger Events ---

        /// <summary>
        /// Detects when an interactable item enters the player's trigger radius.
        /// </summary>
        /// <param name="other">The collider of the object that entered the trigger.</param>
        private void OnTriggerEnter(Collider other)
        {   
            Item pickup = other.GetComponentInParent<Item>();
            
            // Keeps your strict null and tag checks
            if (pickup == null || (!other.CompareTag("Item") && !pickup.CompareTag("Item")) || inventory == null) return;
            
            if (enableDebug) Debug.Log($"<color=cyan><b>[PlayerInputHandler]</b></color> Item entered trigger: <color=green>{pickup.name}</color>");

            // Try to pick it up automatically first. 
            // If TryToPickup returns false (because the slot is full), cache it for a manual swap.
            if (!pickup.TryToPickup(inventory))
            {
                if (enableDebug) Debug.Log($"<color=cyan><b>[PlayerInputHandler]</b></color> Auto-pickup failed (Slot full). Caching <color=yellow>{pickup.name}</color> for manual interaction.");
                _itemInRange = pickup;
                OnItemPickupChanged?.Invoke(true);
            }
            else if (enableDebug)
            {
                Debug.Log($"<color=cyan><b>[PlayerInputHandler]</b></color> Auto-pickup successful: <color=green>{pickup.name}</color>");
            }
        }

        /// <summary>
        /// Clears the cached item when it leaves the player's trigger radius.
        /// </summary>
        /// <param name="other">The collider of the object that exited the trigger.</param>
        private void OnTriggerExit(Collider other)
        {
            Item pickup = other.GetComponentInParent<Item>();
            
            if (pickup != null && pickup == _itemInRange)
            {
                if (enableDebug) Debug.Log($"<color=cyan><b>[PlayerInputHandler]</b></color> Cached item left trigger: <color=orange>{pickup.name}</color>. Clearing cache.");
                
                _itemInRange = null;
                OnItemPickupChanged?.Invoke(false);
            }
        }

        // --- Item Management ---

        /// <summary>
        /// Drops the currently held item of the target type and picks up the new item.
        /// </summary>
        /// <param name="newItem">The new item to swap into the inventory.</param>
        private void SwapItems(Item newItem)
        {
            if (!newItem.CanBePickedUp)
            {
                Debug.Log($"<color=cyan><b>[PlayerInputHandler]</b></color> Cannot swap item with item: <color=orange>{newItem.name}</color> due to its Pickup cooldown");
                return;
            }
            
            ItemType targetType = newItem.itemData.itemType;
            
            // 1. Tell the inventory to drop whatever item is occupying this target slot
            inventory.DropItem(targetType);
            
            // 2. Pick up the new item (which will now succeed since the slot is empty)
            if (newItem.TryToPickup(inventory))
            {
                if (enableDebug) Debug.Log($"<color=cyan><b>[PlayerInputHandler]</b></color> Successfully swapped in: <color=green>{newItem.name}</color>");
                
                _itemInRange = null;
                OnItemPickupChanged?.Invoke(false);
            }
            else if (enableDebug)
            {
                Debug.LogError($"<color=red><b>[PlayerInputHandler]</b></color> Swap failed! Could not pick up: {newItem.name} after dropping {targetType}");
            }
        }

#if UNITY_EDITOR
        // --- Visual Debugging ---
        private void OnDrawGizmos()
        {
            if (!enableDebug) return;

            // Draw interaction radius if a trigger collider exists
            Collider col = GetComponent<Collider>();
            if (col != null && col is SphereCollider sphere)
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.2f); // Transparent Cyan
                Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
            }

            // Edge Case Visualization: Warn if inventory is missing visually
            if (inventory == null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.5f);
            }

            // Visualize currently cached item
            if (_itemInRange != null)
            {
                Gizmos.color = Color.green;
                // Draw a line connecting the player to the cached item
                Gizmos.DrawLine(transform.position, _itemInRange.transform.position);
                // Highlight the target item
                Gizmos.DrawWireSphere(_itemInRange.transform.position, 0.5f);
            }
        }
#endif
    }
}