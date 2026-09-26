using System;
using prefabs.item;
using UnityEngine;

namespace prefabs.player
{
    /// <summary>
    /// Manages the player's inventory state, allowing adding, dropping, and cycling through items.
    /// Handles communicating inventory changes to the UI and WeaponManager via events.
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        private const int ItemTypeCount =
            (int)ItemType.COUNT_DO_NOT_USE_IN_INSPECTOR_OR_YOU_WILL_BE_PART_OF_NEXT_LAY_OFF_WAVE;

        [Header("Inventory Data")]
        [Tooltip("The core array representing physical item slots in the player's inventory.")]
        [SerializeField]
        private ItemData[] items = new ItemData[ItemTypeCount];

        [Header("Dropping Mechanics")]
        [Tooltip("Reference to the player's main camera to calculate drop direction.")]
        public Camera playerCamera;

        [Tooltip("The physical force applied to push the item forward when dropped.")]
        public float dropForce = 5f;

        [Header("Debugging & Constraints")]
        [Tooltip("Toggle visual gizmos and rich console logging for inventory actions.")]
        [SerializeField]
        private bool enableDebug = true;

        /// <summary>
        /// Fired whenever an item is added, removed, or the equipped slot changes.
        /// </summary>
        public static event Action<PlayerInventory> OnInventoryChanged;

        /// <summary>
        /// The enum identifier of the currently equipped item slot.
        /// </summary>
        public ItemType CurrentItemType { get; private set; } = ItemType.Primary;

        /// <summary>
        /// Returns the ItemData residing in the currently equipped slot.
        /// </summary>
        public ItemData CurrentItem => GetItem(CurrentItemType);

        private void Awake()
        {
            // Fail-Safe Asserts
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
                if (playerCamera == null)
                {
                    Debug.LogWarning("<color=orange><b>[PlayerInventory]</b></color> PlayerCamera is not assigned and MainCamera wasn't found. Drop direction will fallback to player body forward.", this);
                }
            }

            Debug.Assert(items.Length == ItemTypeCount,
                "<color=red><b>[PlayerInventory]</b></color> Items array length does not match ItemTypeCount!", this);
        }

        public bool TryToAddItem(ItemData data)
        {
            int slot = (int)data.itemType;
            if (items[slot] != null)
            {
                if (enableDebug) Debug.Log($"<color=orange><b>[PlayerInventory]</b></color> Failed to add <b>{data.name}</b>. Slot {data.itemType} is occupied.");
                return false;
            }

            items[slot] = data;
            if (enableDebug) Debug.Log($"<color=green><b>[PlayerInventory]</b></color> Successfully added <b>{data.name}</b> to slot {data.itemType}.");

            if (CurrentItem == null) EquipSlot(slot);
            else EquipSlot(CurrentItemType);

            return true;
        }

        public ItemData GetItem(int slot)
        {
            return items[slot];
        }

        public ItemData GetItem(ItemType type)
        {
            return items[(int)type];
        }

        /// <summary>
        /// Drops the item from the specified slot. Spawns it in front of the camera and applies forward momentum.
        /// </summary>
        /// <param name="type">The specific item slot to drop.</param>
        public void DropItem(ItemType type)
        {
            ItemData itemToDrop = items[(int)type];
            if (itemToDrop == null)
            {
                if (enableDebug) Debug.LogWarning($"<color=yellow><b>[PlayerInventory]</b></color> Attempted to drop from empty slot: {type}.");
                return;
            }

            // 1. Remove the data from inventory
            items[(int)type] = null;

            if (type == CurrentItemType) SelectOccupiedSlot(1);

            // 2. Calculate spawn point slightly in front of the camera to avoid clipping into player body
            Transform origin = playerCamera != null ? playerCamera.transform : transform;
            Vector3 spawnPos = origin.position + (origin.forward * 1.25f);

            // 3. Ask pool to drop the physical 3D model into the world and get reference to it
            Item physicalItem = ItemPool.Instance.SpawnFromPool(itemToDrop, spawnPos);

            // 4. Apply physical momentum (force) to the Rigidbody
            if (physicalItem != null)
            {
                if (physicalItem.TryGetComponent<Rigidbody>(out Rigidbody rb))
                {
                    // Reset velocity in case it was stored with old momentum
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;

                    // Add impulse force forward and slightly up
                    Vector3 forceDirection = origin.forward + (Vector3.up * 0.1f);
                    rb.AddForce(forceDirection.normalized * dropForce, ForceMode.Impulse);

                    if (enableDebug) Debug.Log($"<color=cyan><b>[PlayerInventory]</b></color> Dropped and pushed <b>{itemToDrop.name}</b> with force: {dropForce}");
                }
                else
                {
                    if (enableDebug) Debug.LogWarning($"<color=orange><b>[PlayerInventory]</b></color> Item <b>{itemToDrop.name}</b> does not have a Rigidbody component. Cannot apply drop force.");
                }
            }
        }

        public void DropCurrentItem()
        {
            DropItem(CurrentItemType);
        }

        public void CycleNext() => SelectOccupiedSlot(1);

        public void CyclePrevious() => SelectOccupiedSlot(-1);

        private void SelectOccupiedSlot(int direction)
        {
            if (!HasAnyItem())
            {
                OnInventoryChanged?.Invoke(this);
                if (enableDebug) Debug.Log("<color=grey><b>[PlayerInventory]</b></color> Cycling aborted: Inventory is completely empty.");
                return;
            }

            for (int i = 1; i < ItemTypeCount; i++)
            {
                int targetIndex = (int)Mathf.Repeat((int)CurrentItemType + (direction * i), ItemTypeCount);

                if (items[targetIndex] != null)
                {
                    if (enableDebug) Debug.Log($"<color=cyan><b>[PlayerInventory]</b></color> Cycled from {CurrentItemType} to {(ItemType)targetIndex}.");
                    EquipSlot((ItemType)targetIndex);
                    return;
                }
            }
        }

        private void EquipSlot(ItemType type)
        {
            CurrentItemType = type;
            OnInventoryChanged?.Invoke(this);
        }

        private void EquipSlot(int slotID)
        {
            EquipSlot((ItemType)slotID);
        }

        private bool HasAnyItem()
        {
            foreach (var item in items)
                if (item != null)
                    return true;
            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!enableDebug) return;

            Transform origin = playerCamera != null ? playerCamera.transform : transform;
            Vector3 previewDrop = origin.position + (origin.forward * 1.25f);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(previewDrop, 0.25f);
            Gizmos.DrawRay(previewDrop, origin.forward * 1f);
        }
#endif
    }
}