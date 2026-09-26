using prefabs.item;
using prefabs.player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles the primary user interface updates for player inventory and interaction prompts.
/// Listens to global player events to reflect current item states, active selection, 
/// and dynamically adjusts layout visibility for right-aligned inventory slots.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The main container holding all inventory slots. Will be hidden if the inventory is completely empty.")]
    public GameObject inventoryContainer;

    [Tooltip("Assign in order: Element 0 = Primary, 1 = Secondary, 2 = Melee, 3 = Utils")]
    public Image[] inventorySlots;

    [Space]
    [Tooltip("Text element shown when looking at an interactable or pickup item.")]
    public TextMeshProUGUI changeText;

    [Header("Debug Settings")]
    [Tooltip("Toggle visual and console debugging for inventory UI state changes.")]
    [SerializeField] private bool enableDebug = true;

    
    public Slider healthBar;
    public TextMeshProUGUI healthText;
    [SerializeField] private float targetHealth;
    [SerializeField] private float animationSpeed;

    private void Awake()
    {
        // Fail-Safe Asserts
        Debug.Assert(inventoryContainer != null,
            "<color=red><b>[UI]</b></color> Inventory Container reference is not assigned in the Inspector!", this);
        Debug.Assert(inventorySlots != null && inventorySlots.Length >= 4,
            "<color=red><b>[UI]</b></color> Inventory slots array is missing or under-populated! Requires at least 4 slots.", this);
        Debug.Assert(changeText != null,
            "<color=red><b>[UI]</b></color> Change Text reference is not assigned in the Inspector!", this);
        
        targetHealth = healthBar.maxValue;
    }

    private void OnEnable()
    {
        PlayerInputHandler.OnItemPickupChanged += UpdateItemPickup;
        PlayerInventory.OnInventoryChanged += UpdateItemUI;
        PlayerStats.OnHealthChanged += UpdateHealthBar;
    }

    private void OnDisable()
    {
        PlayerInputHandler.OnItemPickupChanged -= UpdateItemPickup;
        PlayerInventory.OnInventoryChanged -= UpdateItemUI;
        PlayerStats.OnHealthChanged -= UpdateHealthBar;
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
    /// Disables empty slots to allow HorizontalLayoutGroup to dynamically align active items to the right.
    /// Hides the entire inventory container if no items are held.
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
        bool hasAnyItem = false;

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

            // Toggle visibility so the Horizontal Layout Group can rebuild and snap items to the right
            inventorySlots[i].gameObject.SetActive(isOccupied);

            if (isOccupied)
            {
                hasAnyItem = true;

                // Highlight current active item and scale it up slightly
                inventorySlots[i].color = isCurrent ? Color.yellow : Color.white;
                inventorySlots[i].transform.localScale = isCurrent ? new Vector3(1.15f, 1.15f, 1f) : Vector3.one;
            }
        }

        // Hide the entire UI bar if there are no items to display
        if (inventoryContainer != null)
        {
            inventoryContainer.SetActive(hasAnyItem);

            if (enableDebug)
                Debug.Log($"<color=cyan><b>[UI]</b></color> Inventory container visibility set to: <color={(hasAnyItem ? "green" : "grey")}>{hasAnyItem}</color>");
        }
    }

    private void UpdateHealthBar(float currentHealth)
    {   
        targetHealth = currentHealth;
        healthText.text = currentHealth * 100  + "%";
    }
    
    void Update()
    {
        // If the slider isn't at the target health, smoothly slide it over
        if (Mathf.Abs(healthBar.value - targetHealth) > 0.01f)
        {
            healthBar.value = Mathf.Lerp(healthBar.value, targetHealth, Time.deltaTime * animationSpeed);
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
                // Only draw bounds for slots that are currently active in the hierarchy
                if (inventorySlots[i] != null && inventorySlots[i].gameObject.activeInHierarchy)
                {
                    RectTransform rt = inventorySlots[i].rectTransform;
                    Vector3[] corners = new Vector3[4];
                    rt.GetWorldCorners(corners);

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