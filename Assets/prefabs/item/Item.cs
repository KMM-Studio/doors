using UnityEngine;
using prefabs.player;

namespace prefabs.item
{
    /// <summary>
    /// Represents a physical item in the game world that can be picked up.
    /// Acts as a "suitcase" holding the InventoryItem data wrapper to retain state (like ammo) when dropped.
    /// </summary>
    public class Item : MonoBehaviour
    {
        [Header("Runtime Item Configuration")]
        [Tooltip("The runtime instance of the item holding mutable state (e.g., ammo left).")]
        public InventoryItem savedItemData;

        [Tooltip("Assign this if the item is manually placed in the Scene before play. The script will generate a new wrapper from it.")]
        public ItemData defaultStartingData;

        [Space(10)]
        [Header("Debug & Visualization")]
        [Tooltip("Toggle visual gizmos and console debugging for this item.")]
        [SerializeField] private bool enableDebug = true;

        private float _pickupBlockTimer = 0f;

        private void Start()
        {
            // Fail-Safe Assertion: Ensures required data is available for manually placed scene objects
            if (savedItemData == null || savedItemData.data == null)
            {
                if (defaultStartingData != null)
                {
                    savedItemData = new InventoryItem(defaultStartingData);
                }
                else
                {
                    Debug.LogError($"<color=red><b>[Item]</b></color> Default Starting Data is missing on <b>{gameObject.name}</b>! Please assign it in the Inspector.", this);
                }
            }
        }

        private void Update()
        {
            if (_pickupBlockTimer > 0f) _pickupBlockTimer -= Time.deltaTime;
        }

        /// <summary>
        /// Applies a temporary block preventing the item from being picked up immediately.
        /// Useful for when items are dropped by enemies or the player.
        /// </summary>
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
        /// Attempts to add this item's wrapper (state) to the provided inventory. 
        /// If successful, stores the physical representation in the object pool.
        /// </summary>
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

            if (savedItemData == null || savedItemData.data == null)
            {
                Debug.LogError($"<color=red><b>[Item]</b></color> Attempted to pick up <b>{gameObject.name}</b> but it contains no valid saved item data!");
                return false;
            }

            // 1. Give the data wrapper (soul) to the inventory
            if (inventory.TryToAddItem(savedItemData))
            {
                if (enableDebug) Debug.Log($"<color=cyan><b>[Item]</b></color> <b>{savedItemData.data.itemName}</b> successfully added to inventory. Returning to pool.");

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

            if (enableDebug) Debug.Log($"<color=cyan><b>[Item]</b></color> Failed to add <b>{gameObject.name}</b>. Inventory slot might be full.");
            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!enableDebug) return;

            // Edge Case Visualization: Missing Data mapping
            if (savedItemData == null && defaultStartingData == null)
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

        // Ta funkcja odpala siê automatycznie, gdy cokolwiek wejdzie w Sphere Collider (z zaznaczonym Is Trigger)
        private void OnTriggerEnter(Collider other)
        {
            // Sprawdzamy, czy obiekt, który na nas nadepn¹³, ma tag "Player"
            if (other.CompareTag("Player"))
            {
                // Szukamy ekwipunku na graczu, przeszukuj¹c ca³¹ jego hierarchiê (dzieci i rodziców)
                PlayerInventory inventory = other.GetComponentInChildren<PlayerInventory>();
                if (inventory == null)
                {
                    inventory = other.GetComponentInParent<PlayerInventory>();
                }

                if (inventory != null)
                {
                    // Próbujemy podnieœæ!
                    TryToPickup(inventory);
                }
                else
                {
                    // Ostrze¿enie w konsoli, jeœli gracz ma tag, ale nie mo¿na na nim znaleŸæ skryptu PlayerInventory
                    Debug.LogWarning($"<color=yellow><b>[Item]</b></color> Obiekt <b>{other.name}</b> ma tag 'Player', ale nie znaleziono na nim ani w jego hierarchii skryptu PlayerInventory!");
                }
            }
        }
    }
}