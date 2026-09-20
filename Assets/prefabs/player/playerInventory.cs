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

        [Space] [Tooltip("The transform representing the origin point where items should spawn when dropped.")]
        public Transform dropPoint;

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
            if (dropPoint == null)
            {
                Debug.LogError(
                    "<color=red><b>[PlayerInventory]</b></color> DropPoint is not assigned! Items will fallback to spawning inside the player.",
                    this);
            }

            Debug.Assert(items.Length == ItemTypeCount,
                "<color=red><b>[PlayerInventory]</b></color> Items array length does not match ItemTypeCount!", this);
        }

        /// <summary>
        /// Attempts to add a new item to its designated type slot.
        /// </summary>
        /// <param name="data">The scriptable object data of the item to add.</param>
        /// <returns>True if the item was added successfully, false if the slot was already occupied.</returns>
        public bool TryToAddItem(ItemData data)
        {
            int slot = (int)data.itemType;
            if (items[slot] != null)
            {
                if (enableDebug)
                    Debug.Log(
                        $"<color=orange><b>[PlayerInventory]</b></color> Failed to add <b>{data.name}</b>. Slot {data.itemType} is occupied.");
                return false;
            }

            items[slot] = data;
            if (enableDebug)
                Debug.Log(
                    $"<color=green><b>[PlayerInventory]</b></color> Successfully added <b>{data.name}</b> to slot {data.itemType}.");
            
            if(CurrentItem == null) EquipSlot(slot);
            else EquipSlot(CurrentItemType);
            return true;
        }

        /// <summary>
        /// Retrieves the item data at a specific integer index.
        /// </summary>
        /// <param name="slot">The integer index of the inventory slot.</param>
        /// <returns>The ItemData at the specified slot, or null if empty.</returns>
        public ItemData GetItem(int slot)
        {
            return items[slot];
        }

        /// <summary>
        /// Retrieves the item data for a specific ItemType.
        /// </summary>
        /// <param name="type">The ItemType representing the slot.</param>
        /// <returns>The ItemData at the specified slot, or null if empty.</returns>
        public ItemData GetItem(ItemType type)
        {
            return items[(int)type];
        }

        /// <summary>
        /// Drops the item from the specified slot into the physical world space.
        /// </summary>
        /// <param name="type">The specific item slot to drop.</param>
        public void DropItem(ItemType type)
        {
            ItemData itemToDrop = items[(int)type];
            if (itemToDrop == null)
            {
                if (enableDebug)
                    Debug.LogWarning(
                        $"<color=yellow><b>[PlayerInventory]</b></color> Attempted to drop from empty slot: {type}.");
                return;
            }

            // 1. Remove the data from inventory
            items[(int)type] = null;
            
            if(type == CurrentItemType) SelectOccupiedSlot(1);

            // 2. Tell the pool to drop the physical 3D model into the world
            Vector3 spawnPos = dropPoint != null ? dropPoint.position : transform.position + (transform.forward * 1.5f);

            ItemPool.Instance.SpawnFromPool(itemToDrop, spawnPos);
        }
        /// <summary>
        /// Drops current item into the physical world space.
        /// </summary>
        public void DropCurrentItem()
        {
            DropItem(CurrentItemType);
        }

        /// <summary>
        /// Cycles to the next occupied inventory slot moving forward.
        /// </summary>
        public void CycleNext() => SelectOccupiedSlot(1);

        /// <summary>
        /// Cycles to the previous occupied inventory slot moving backward.
        /// </summary>
        public void CyclePrevious() => SelectOccupiedSlot(-1);

        /// <summary>
        /// Iterates through the inventory array in the specified direction to find and equip the next non-null item.
        /// </summary>
        /// <param name="direction">1 to search forward, -1 to search backward.</param>
        private void SelectOccupiedSlot(int direction)
        {
            if (!HasAnyItem())
            {   
                OnInventoryChanged?.Invoke(this);
                if (enableDebug)
                    Debug.Log(
                        "<color=grey><b>[PlayerInventory]</b></color> Cycling aborted: Inventory is completely empty.");
                return;
            }

            // Loop through the slots in the given direction
            for (int i = 1; i < ItemTypeCount; i++)
            {
                // Mathf.Repeat perfectly wraps around the array (e.g., going past Utils loops back to Primary)
                int targetIndex = (int)Mathf.Repeat((int)CurrentItemType + (direction * i), ItemTypeCount);

                // If we find a slot with data in it, equip it and stop searching
                if (items[targetIndex] != null)
                {
                    if (enableDebug)
                        Debug.Log(
                            $"<color=cyan><b>[PlayerInventory]</b></color> Cycled from {CurrentItemType} to {(ItemType)targetIndex}.");
                    EquipSlot((ItemType)targetIndex);
                    return;
                }
            }
        }

        /// <summary>
        /// Equips the targeted item type and fires the update event.
        /// </summary>
        /// <param name="type">The ItemType to equip.</param>
        private void EquipSlot(ItemType type)
        {
            CurrentItemType = type;

            // Tells the UI and the WeaponManager to update
            OnInventoryChanged?.Invoke(this);
        }

        /// <summary>
        /// Equips the targeted slotID and fires the update event.
        /// </summary>
        /// <param name="slotID">The slotID to equip.</param>
        private void EquipSlot(int slotID)
        {
            EquipSlot((ItemType)slotID);
        }

        /// <summary>
        /// Evaluates whether the player holds at least one item.
        /// </summary>
        /// <returns>True if any slot contains ItemData, false otherwise.</returns>
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

            // Visualize the drop point
            if (dropPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(dropPoint.position, 0.25f);
                Gizmos.DrawRay(dropPoint.position, dropPoint.forward * 0.5f);
            }
        }
#endif
    }
}