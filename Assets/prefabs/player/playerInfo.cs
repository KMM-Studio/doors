using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

public class PlayerInfo : MonoBehaviour
{
        public int maxHealth = 100;
        public int currentHealth = 0;
        public float maxSpeed = 5;
        [SerializeField] [ItemCanBeNull] public SerializableDictionary<ItemType, ItemTags> inventory = new();
        [SerializeField] public ItemType currentItemType;
        [NonSerialized] public ItemTags currentItemTags;
        
        public float maxPickupCooldown = 0f;
        
        private void Start()
        {
                if(currentHealth == 0) 
                {
                    currentHealth = maxHealth;
                }
                
                inventory = new SerializableDictionary<ItemType, ItemTags>
                {
                    [ItemType.Primary] = null,
                    [ItemType.Secondary] = null,
                    [ItemType.Melee] = null,
                    [ItemType.Utils] = null
                };
                
                currentItemType = ItemType.Primary;
                currentItemTags = inventory[ItemType.Primary];
        }

        public void RemoveItemFromInventory(ItemType itemType)
        {   
            inventory[itemType] = null;
            if (itemType.Equals(currentItemType))
            {
                UpdateItemTags(); // in case of item being currently equipped
            }
        }

        public void ChangeToItem(ItemType itemType)
        {
            currentItemType = itemType;
            UpdateItemTags();
        }
        
        public void ChangeItemTypeNext()
        {   
            // Go Next 
            currentItemType = (ItemType)(int)Mathf.Repeat((int)currentItemType + 1, (int)ItemType.COUNT_DO_NOT_USE_IN_INSPECTOR_OR_YOU_WILL_BE_PART_OF_NEXT_LAY_OFF_WAVE);
            UpdateItemTags();
        }

        public void ChangeItemTypePrevious()
        {
            // Go Previous 
            currentItemType = (ItemType)(int)Mathf.Repeat((int)currentItemType - 1, (int)ItemType.COUNT_DO_NOT_USE_IN_INSPECTOR_OR_YOU_WILL_BE_PART_OF_NEXT_LAY_OFF_WAVE);
            UpdateItemTags();
        }

        private void UpdateItemTags()
        {   
            currentItemTags = inventory[currentItemType];
            SendMessage("UpdateItemUI", this, SendMessageOptions.DontRequireReceiver); // message to UI
        }
}
