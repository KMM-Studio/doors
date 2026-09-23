using prefabs.item;
using prefabs.player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles the primary user interface updates for player inventory and interaction prompts.
/// Listens to global player events to reflect current item states and active selection.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UI : MonoBehaviour
{
    [Header("UI References")] 
    [Tooltip("Assign in order: Element 0 = Primary, 1 = Secondary, 2 = Melee, 3 = Utils")]
    public Image[] inventorySlots; 
        
    [Space]
    [Tooltip("Text element shown when looking at an interactable or pickup item.")]
    public TextMeshProUGUI changeText;
        
    [Header("Debug Settings")]
    [Tooltip("Toggle visual and console debugging for inventory UI state changes.")]
    [SerializeField] private bool enableDebug = true;

    private void Awake()
    {
        // Fail-Safe Asserts
        Debug.Assert(inventorySlots != null && inventorySlots.Length >= 4, 
            "<color=red><b>[UI]</b></color> Inventory slots array is missing or under-populated! Requires at least 4 slots.");
        Debug.Assert(changeText != null, 
            "<color=red><b>[UI]</b></color> Change Text reference is not assigned in the Inspector!");
    }

    private void OnEnable()
    {
        PlayerInputHandler.OnItemPickupChanged += UpdateItemPickup;
        PlayerInventory.OnInventoryChanged += UpdateItemUI; 
    }

    private void OnDisable()
    {
        PlayerInputHandler.OnItemPickupChanged -= UpdateItemPickup;
        PlayerInventory.OnInventoryChanged -= UpdateItemUI;
    }
        
    /// <summary>
    /// Toggles the visibility of the interaction prompt text.
    /// </summary>
    /// <param name="enable">True to show the prompt, false to hide it.</param>
    private void UpdateItemPickup(bool enable)
    {
        if (changeText != null)
        {
            changeText.gameObject.SetActive(enable);
                
            if (enableDebug)
            {
                string stateColor = enable ? "green" : "grey";
                Debug.Log($"<color=cyan><b>[UI]</b></color> Interaction prompt set to: <color={stateColor}>{enable}</color>");
            }
        }
    }

    /// <summary>
    /// Rebuilds the visual state of the inventory slots based on the provided inventory data payload.
    /// Highlights the currently selected item and adjusts opacity for occupied/empty slots.
    /// </summary>
    /// <param name="inventory">The updated player inventory state payload.</param>
    private void UpdateItemUI(PlayerInventory inventory)
    {   
        if (inventory == null)
        {
            if (enableDebug) Debug.LogWarning("<color=orange><b>[UI]</b></color> Received null PlayerInventory payload.");
            return;
        }

        ItemType currentItem = inventory.CurrentItemType;
            
        if (enableDebug)
            Debug.Log($"<color=cyan><b>[UI]</b></color> Refreshing Inventory. Current Active Item: <b>{currentItem}</b>");
            
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (inventorySlots[i] == null)
            {
                // Edge Case Visualization via Console
                if (enableDebug) Debug.LogWarning($"<color=orange><b>[UI]</b></color> Inventory slot image at index {i} is null!");
                continue;
            }

            ItemType slotType = (ItemType)i;
            bool isOccupied = inventory.GetItem(slotType) != null;
            bool isCurrent = slotType == currentItem;

            if (isCurrent)
            {
                inventorySlots[i].color = isOccupied ? Color.yellow : new Color(1f, 0.92f, 0.016f, 0.5f);
                inventorySlots[i].transform.localScale = new Vector3(1.15f, 1.15f, 1f);
            }
            else
            {
                inventorySlots[i].color = isOccupied ? Color.white : new Color(1f, 1f, 1f, 0.2f);
                inventorySlots[i].transform.localScale = Vector3.one; 
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!enableDebug) return;

        // Visual Scene Debugging: Draw bounds around assigned UI slots
        if (inventorySlots != null)
        {
            for (int i = 0; i < inventorySlots.Length; i++)
            {
                if (inventorySlots[i] != null)
                {
                    RectTransform rt = inventorySlots[i].rectTransform;
                    Vector3[] corners = new Vector3[4];
                    rt.GetWorldCorners(corners);

                    // Edge Case Visualization: Draw red cross over unassigned/missing slots, green box over valid ones
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(corners[0], corners[1]);
                    Gizmos.DrawLine(corners[1], corners[2]);
                    Gizmos.DrawLine(corners[2], corners[3]);
                    Gizmos.DrawLine(corners[3], corners[0]);
                }
            }
        }
        else
        {
            // Draw a prominent red sphere at the UI root if slots are entirely missing
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 50f);
        }
    }
#endif
}