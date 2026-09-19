using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using prefabs.player;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class UI : MonoBehaviour
{
    [Header("UI References")] 
    [SerializeField]public SerializableDictionary<ItemType, Image> inventorySlots;
    public TextMeshProUGUI changeText;
    
    private void OnEnable()
    {
        ItemDetection.OnItemPickupChanged += UpdateItemPickup;
        PlayerInfo.OnItemUIUpdated += UpdateItemUI;
    }

    private void OnDisable()
    {
        ItemDetection.OnItemPickupChanged -= UpdateItemPickup;
        PlayerInfo.OnItemUIUpdated -= UpdateItemUI;
    }
    
    private void UpdateItemPickup(bool enable)
    {
        changeText.gameObject.SetActive(enable);        
    }

    private void Awake()
    {
        UpdateItemUI(null);
    }

    // Funkcja aktualizująca wygląd (kolory i rozmiar)
    private void UpdateItemUI([CanBeNull] PlayerInfo playerInfo)
    {   
        var inventory = playerInfo?.inventory ?? new SerializableDictionary<ItemType, ItemTags>
        {
            [ItemType.Primary] = null,
            [ItemType.Secondary] = null,
            [ItemType.Melee] = null,
            [ItemType.Utils] = null
        };;
        var currentItem = playerInfo?.currentItemType ?? ItemType.Primary;
        
        
        foreach (var inventorySlot in inventorySlots)
        {
            if (inventorySlot.Key.Equals(currentItem))
            {
                inventorySlot.Value.color = inventory[currentItem] is not null? Color.yellow : new Color(1f, 0.92f, 0.016f, 0.5f);
                inventorySlot.Value.transform.localScale = new Vector3(1.15f, 1.15f, 1f); // Lekko powiększony
            }
            else
            {
                inventorySlot.Value.color = inventory[inventorySlot.Key] is not null ? Color.white : new Color(1f, 1f, 1f, 0.2f);
                inventorySlot.Value.transform.localScale = Vector3.one; // Normalny rozmiar
            }
            
        }
    }
}