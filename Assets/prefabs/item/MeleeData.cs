using UnityEngine;

namespace prefabs.item
{
    /// <summary>
    /// Specialized data configuration for melee weapons.
    /// Overrides the primary action to perform close-range attacks without consuming ammo.
    /// </summary>
    [CreateAssetMenu(fileName = "New Melee Item", menuName = "Items/Melee Data")]
    public class MeleeItemData : ItemData
    {
        [Header("Melee Specific Settings")]
        [Tooltip("The maximum distance the melee attack can reach.")]
        public float hitRange = 2f;

        /// <summary>
        /// Overrides the standard firing logic. Skips ammo reduction.
        /// </summary>
        public override void ExecutePrimary(Transform origin, ref int currentMag)
        {
            // Note: 'currentMag--' is intentionally omitted here

            Debug.Log($"<color=orange><b>[Melee]</b></color> Swung <b>{itemName}</b>! Dealt {power} damage.");

            // TODO: Implement close-quarters hit detection (e.g., Physics.SphereCast)
        }
    }
}