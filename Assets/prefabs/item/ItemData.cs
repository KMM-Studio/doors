using UnityEngine;

namespace prefabs.item
{
    public enum ItemType { Primary, Secondary, Melee, Utils, COUNT_DO_NOT_USE_IN_INSPECTOR_OR_YOU_WILL_BE_PART_OF_NEXT_LAY_OFF_WAVE }

    /// <summary>
    /// The core template data defining an item's base properties and combat rules.
    /// Designed as a read-only ScriptableObject to prevent cross-player data overrides.
    /// </summary>
    [CreateAssetMenu(fileName = "New Item", menuName = "Items/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Header("Identity")]
        public ItemType itemType;
        public string itemName;

        [Header("Visuals & Networking")]
        [Tooltip("The 3D model seen by other players, or dropped on the ground.")]
        public Item physicalPrefab;

        [Tooltip("The high-res first-person model rendered on the URP Overlay Camera.")]
        public GameObject viewModelPrefab;

        [Header("Combat Stats")]
        public float power = 20f;
        public float actionRate = 0.5f;
        public int maxMagSize = 30;

        /// <summary>
        /// Executes the primary action (e.g., shooting). 
        /// Takes the local ammo state by reference to modify it without altering the global template.
        /// </summary>
        public virtual void ExecutePrimary(Transform origin, ref int currentMag)
        {
            if (currentMag <= 0)
            {
                Debug.Log($"<color=red><b>[ItemData]</b></color> Click! Out of ammo in <b>{itemName}</b>.");
                return;
            }

            currentMag--;
            Debug.Log($"<color=orange><b>[ItemData]</b></color> Fired <b>{itemName}</b> (Power: {power})! Ammo left: {currentMag}");

            // TODO: Implement Raycast or Instantiate logic here
        }

        /// <summary>
        /// Executes the secondary action (e.g., melee bash, ADS).
        /// </summary>
        public virtual void ExecuteAlt(GameObject user)
        {
            Debug.Log($"<color=yellow><b>[ItemData]</b></color> Performed alternate action for <b>{itemName}</b>.");
        }
    }
}