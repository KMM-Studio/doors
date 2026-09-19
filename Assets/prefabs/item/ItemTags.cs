using UnityEngine;

public enum ItemType { Primary, Secondary, Melee, Utils, COUNT_DO_NOT_USE_IN_INSPECTOR_OR_YOU_WILL_BE_PART_OF_NEXT_LAY_OFF_WAVE}

public class ItemTags : MonoBehaviour
{
    public ItemType itemType;
}