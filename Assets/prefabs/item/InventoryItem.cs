using UnityEngine;

namespace prefabs.item
{
    /// <summary>
    /// Represents a specific instance of an item in the player's inventory.
    /// Acts as a data wrapper that holds the mutable runtime state (like current ammo)
    /// while strictly referencing the read-only template (ItemData) for base stats.
    /// </summary>
    [System.Serializable]
    public class InventoryItem
    {
        [Tooltip("The core read-only data defining this item's base properties.")]
        public ItemData data;

        [Header("Runtime State")]
        [Tooltip("The current amount of ammo in the magazine for this specific instance.")]
        public int currentMag;

        [Tooltip("The total reserve ammo carried for this specific instance.")]
        public int reserveAmmo;

        /// <summary>
        /// Constructor called when picking up a brand new item from the world.
        /// </summary>
        public InventoryItem(ItemData templateData, int startingReserve = 90)
        {
            data = templateData;
            currentMag = data.maxMagSize;
            reserveAmmo = startingReserve;
        }

        /// <summary>
        /// Helper method to execute the primary action directly from the inventory wrapper.
        /// </summary>
        public void TryShoot(Transform origin)
        {
            if (data != null)
            {
                // Passes the local state by reference so the template can modify it safely
                data.ExecutePrimary(origin, ref currentMag);
            }
        }
    }
}