using prefabs.player;
using UnityEngine;

namespace prefabs.item
{
    /// <summary>
    /// Represents a physical item in the game world that can be picked up and added to a player's inventory.
    /// Handles pickup cooldowns, inventory interactions, and object pooling.
    /// </summary>
    public class Item : MonoBehaviour
    {
        [Header("Item Configuration")]
        [Tooltip("The core data defining this item's properties. Must be assigned in the Inspector.")]
        public ItemData itemData;

        [Space(10)]
        [Header("Debug & Visualization")]
        [Tooltip("Toggle visual gizmos and console debugging for this item.")]
        [SerializeField] private bool enableDebug = true;

        private float _pickupBlockTimer = 0f;

        private void Awake()
        {
            // Fail-Safe Assertion: Ensures required data is assigned before gameplay begins
            Debug.Assert(itemData != null, $"<color=red><b>[Item]</b></color> ItemData is missing on <b>{gameObject.name}</b>! Please assign it in the Inspector.", this);
        }

        private void Update()
        {
            if (_pickupBlockTimer > 0f) _pickupBlockTimer -= Time.deltaTime;
        }

        /// <summary>
        /// Applies a temporary block preventing the item from being picked up immediately.
        /// Useful for when items are dropped by enemies or the player.
        /// </summary>
        /// <param name="cooldown">The duration in seconds before the item can be picked up.</param>
        public void ApplyPickupCooldown(float cooldown)
        {
            _pickupBlockTimer = cooldown;

            if (enableDebug)
            {
                Debug.Log($"<color=cyan><b>[Item]</b></color> Applied <color=orange>{cooldown}s</color> cooldown to <b>{gameObject.name}</b>.");
            }
        }

        /// <summary>
        /// Indicates whether the item is currently eligible to be picked up.
        /// </summary>
        public bool CanBePickedUp => _pickupBlockTimer <= 0f;

        /// <summary>
        /// Attempts to add this item to the provided inventory. If successful, stores the physical representation in the item pool.
        /// </summary>
        /// <param name="inventory">The target inventory attempting to pick up the item.</param>
        /// <returns>True if successfully added to the inventory, false otherwise (e.g., inventory full or item on cooldown).</returns>
        public bool TryToPickup(PlayerInventory inventory)
        {
            if (!CanBePickedUp)
            {
                if (enableDebug) Debug.Log($"<color=cyan><b>[Item]</b></color> Pickup blocked for <b>{gameObject.name}</b>. Cooldown remaining: <color=orange>{_pickupBlockTimer:F2}s</color>");
                return false;
            }

            if (inventory == null)
            {
                Debug.LogError($"<color=red><b>[Item]</b></color> Attempted to pick up <b>{gameObject.name}</b> with a null inventory reference!");
                return false;
            }

            // 1. Give the pure data to the inventory
            if (inventory.TryToAddItem(itemData))
            {
                if (enableDebug) Debug.Log($"<color=cyan><b>[Item]</b></color> <b>{(itemData != null ? itemData.name : "Unknown")}</b> successfully added to inventory. Returning to pool.");
                
                // 2. If successful, put this physical 3D model into the pool
                if (ItemPool.Instance != null)
                {
                    ItemPool.Instance.StoreInPool(this);
                }
                else
                {
                    Debug.LogError($"<color=red><b>[Item]</b></color> ItemPool.Instance is null! Destroying <b>{gameObject.name}</b> instead to avoid memory leaks.");
                    Destroy(gameObject);
                }
                
                return true;
            }

            if (enableDebug) Debug.Log($"<color=cyan><b>[Item]</b></color> Failed to add <b>{gameObject.name}</b>. Inventory is full or rejected the item.");
            return false; // Inventory slot was full
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!enableDebug) return;

            // Edge Case Visualization: Missing ItemData mapping
            if (itemData == null)
            {
                Gizmos.color = Color.magenta; // Magenta highlights critical setup errors
                Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
                return; 
            }

            // Spatial Logic: Draw interaction state spheres
            bool isPickable = CanBePickedUp;
            Gizmos.color = isPickable ? new Color(0f, 1f, 0f, 0.3f) : new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawSphere(transform.position, 0.25f);
            
            Gizmos.color = isPickable ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.25f);

            // Edge Case Visualization: Cooldown state vertical line indicator
            if (!isPickable)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, transform.position + (Vector3.up * _pickupBlockTimer));
            }
        }
#endif
    }
}