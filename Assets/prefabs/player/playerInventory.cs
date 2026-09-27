using System;
using prefabs.item;
using UnityEngine;

namespace prefabs.player
{
    /// <summary>
    /// Manages the player's inventory state.
    /// Stores instances of items (InventoryItem wrapper) rather than raw template data.
    /// Handles swapping, adding, and dropping items via Object Pooling.
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        private const int ItemTypeCount = (int)ItemType.COUNT_DO_NOT_USE_IN_INSPECTOR_OR_YOU_WILL_BE_PART_OF_NEXT_LAY_OFF_WAVE;

        [Header("Inventory Data")]
        [Tooltip("Array holding active instances of items, maintaining runtime state like ammo.")]
        [SerializeField]
        private InventoryItem[] items = new InventoryItem[ItemTypeCount];

        [Header("Dropping Mechanics")]
        [Tooltip("Reference to the player's camera used to calculate drop physics direction.")]
        public Camera playerCamera;

        [Tooltip("The physics force applied when an item is dropped from the inventory.")]
        public float dropForce = 5f;

        public static event Action<PlayerInventory> OnInventoryChanged;

        public ItemType CurrentItemType { get; private set; } = ItemType.Primary;
        public InventoryItem CurrentItem => GetItem(CurrentItemType);

        private void Awake()
        {
            if (playerCamera == null) playerCamera = Camera.main;
        }

        /// <summary>
        /// Attempts to add a data wrapper to the correct inventory slot.
        /// </summary>
        public bool TryToAddItem(InventoryItem itemInstance)
        {
            int slot = (int)itemInstance.data.itemType;

            // NOWE SPRAWDZENIE: Slot jest pe³ny TYLKO wtedy, gdy ma opakowanie ORAZ przypisane dane
            if (items[slot] != null && items[slot].data != null)
            {
                return false;
            }

            items[slot] = itemInstance;

            // Zabezpieczenie przed wyekwipowaniem pustego slota
            if (CurrentItem == null || CurrentItem.data == null) EquipSlot(slot);
            else EquipSlot(CurrentItemType);

            return true;
        }

        public InventoryItem GetItem(int slot) => items[slot];
        public InventoryItem GetItem(ItemType type) => items[(int)type];

        /// <summary>
        /// Removes the item from the inventory, spawns a physical model from the pool, 
        /// and injects the retained state (wrapper) back into the physical model.
        /// </summary>
        public void DropItem(ItemType type)
        {
            InventoryItem itemToDrop = items[(int)type];
            if (itemToDrop == null || itemToDrop.data == null) return;

            items[(int)type] = null;
            if (type == CurrentItemType) SelectOccupiedSlot(1);

            Transform origin = playerCamera != null ? playerCamera.transform : transform;
            Vector3 spawnPos = origin.position + (origin.forward * 1.25f);

            // Fetch a physical model from the Object Pool based on the template's prefab
            Item physicalItem = ItemPool.Instance.SpawnFromPool(itemToDrop.data, spawnPos);

            if (physicalItem != null)
            {
                // Re-inject the soul (wrapper) into the physical suitcase
                physicalItem.savedItemData = itemToDrop;

                // Apply dropping physics
                if (physicalItem.TryGetComponent<Rigidbody>(out Rigidbody rb))
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    Vector3 forceDirection = origin.forward + (Vector3.up * 0.1f);
                    rb.AddForce(forceDirection.normalized * dropForce, ForceMode.Impulse);
                }
            }
        }

        public void DropCurrentItem() => DropItem(CurrentItemType);
        public void CycleNext() => SelectOccupiedSlot(1);
        public void CyclePrevious() => SelectOccupiedSlot(-1);

        private void SelectOccupiedSlot(int direction)
        {
            if (!HasAnyItem())
            {
                OnInventoryChanged?.Invoke(this);
                return;
            }

            for (int i = 1; i < ItemTypeCount; i++)
            {
                int targetIndex = (int)Mathf.Repeat((int)CurrentItemType + (direction * i), ItemTypeCount);
                if (items[targetIndex] != null)
                {
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

        private void EquipSlot(int slotID) => EquipSlot((ItemType)slotID);

        private bool HasAnyItem()
        {
            foreach (var item in items)
            {
                // Musimy sprawdzaæ te¿ data != null, inaczej kod pomyœli, ¿e puste sloty to bronie
                if (item != null && item.data != null) return true;
            }
            return false;
        }
    }
    }
