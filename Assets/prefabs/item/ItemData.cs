using UnityEngine;

namespace prefabs.item
{
    public enum ItemType { Primary, Secondary, Melee, Utils, COUNT_DO_NOT_USE_IN_INSPECTOR_OR_YOU_WILL_BE_PART_OF_NEXT_LAY_OFF_WAVE}

    [CreateAssetMenu(fileName = "New Item", menuName = "Items/Item Data")]
    public class ItemData : ScriptableObject
    {
        public ItemType itemType;
        public string itemName;
    
        // The pool uses this to know what 3D model to grab/create
        public Item physicalPrefab; 
    }
}