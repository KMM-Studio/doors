using System.Collections.Generic;
using prefabs.item;
using UnityEngine;

/// <summary>
/// Manages object pooling for physical in-game items to optimize performance.
/// Aligns with CLAUDE.md architecture rules by decoupling state and preventing repetitive allocations.
/// </summary>
public class ItemPool : MonoBehaviour
{
    /// <summary>
    /// Global singleton instance for the ItemPool.
    /// </summary>
    public static ItemPool Instance { get; private set; }

    [Space]
    [Header("Runtime State")]
    [Tooltip("Tracks available 3D models queued for each ItemData type.")]
    private Dictionary<ItemData, Queue<Item>> pool = new Dictionary<ItemData, Queue<Item>>();
    
    [Header("Debug Settings")]
    [Tooltip("Toggle visual gizmos and console debugging logs.")]
    [SerializeField] private bool enableDebug = true;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            if (enableDebug) Debug.LogWarning($"<color=orange><b>[ItemPool]</b></color> Duplicate instance found on {gameObject.name}. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        // Fail-Safe Assertion
        Debug.Assert(pool != null, "<color=red><b>[ItemPool]</b></color> Pool dictionary failed to initialize!", this);
    }

    /// <summary>
    /// Deactivates a physical item in the world and parks it under the pool's hierarchy for future reuse.
    /// </summary>
    /// <param name="physicalItem">The specific Item instance being picked up or despawned.</param>
    public void StoreInPool(Item physicalItem)
    {
        // Edge Case: Prevent null reference exceptions
        if (physicalItem == null)
        {
            if (enableDebug) Debug.LogError("<color=red><b>[ItemPool]</b></color> Attempted to store a null item in the pool!");
            return;
        }

        ItemData data = physicalItem.itemData;
            
        // Edge Case: Validate ItemData existence
        if (data == null)
        {
            if (enableDebug) Debug.LogError($"<color=red><b>[ItemPool]</b></color> Item <color=yellow>{physicalItem.gameObject.name}</color> has no assigned ItemData!");
            return;
        }

        if (!pool.ContainsKey(data))
            pool[data] = new Queue<Item>();

        // Park the item "under the map" by deactivating it and hiding it in the pool
        physicalItem.gameObject.SetActive(false);
        physicalItem.transform.SetParent(transform); 
            
        pool[data].Enqueue(physicalItem);

        if (enableDebug)
            Debug.Log($"<color=cyan><b>[ItemPool]</b></color> Stored <color=yellow>{data.name}</color>. Total in pool: {pool[data].Count}");
    }

    /// <summary>
    /// Attempts to pull an existing item model from the pool at the target location. 
    /// Instantiates a new item only if the pool for that data type is empty.
    /// </summary>
    /// <param name="data">The scriptable object data defining the item.</param>
    /// <param name="spawnPosition">The Vector3 world coordinates for the item drop.</param>
    public void SpawnFromPool(ItemData data, Vector3 spawnPosition)
    {
        if (data == null)
        {
            if (enableDebug) Debug.LogError("<color=red><b>[ItemPool]</b></color> Spawn failed: Provided ItemData is null.");
            return;
        }

        // 1. If we have a parked 3D model of this exact item, pull it out
        if (pool.ContainsKey(data) && pool[data].Count > 0)
        {
            Item parkedItem = pool[data].Dequeue();
            parkedItem.transform.position = spawnPosition;
            parkedItem.transform.SetParent(null);
            parkedItem.gameObject.SetActive(true);
            parkedItem.ApplyPickupCooldown(2f); // Prevent instant re-pickup
                
            if (enableDebug)
                Debug.Log($"<color=green><b>[ItemPool]</b></color> Reusing <color=yellow>{data.name}</color> from pool. Remaining available: {pool[data].Count}");
                
            return;
        }

        // Edge Case: Missing prefab reference on the ScriptableObject
        if (data.physicalPrefab == null)
        {
            if (enableDebug) Debug.LogError($"<color=red><b>[ItemPool]</b></color> Spawn failed: <color=yellow>{data.name}</color> lacks a physicalPrefab reference.");
            return;
        }

        // 2. ONLY if the pool is completely empty do we ever use Instantiate
        Item newItem = Instantiate(data.physicalPrefab, spawnPosition, Quaternion.identity);
        newItem.itemData = data;
        newItem.ApplyPickupCooldown(2f);

        if (enableDebug)
            Debug.Log($"<color=magenta><b>[ItemPool]</b></color> Pool empty. Instantiating new <color=yellow>{data.name}</color>.");
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!enableDebug) return;

        // Visualize the central pool storage hub
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, new Vector3(2f, 2f, 2f));

        // Visualize currently parked items resting inside the pool hierarchy
        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.5f); // Transparent yellow
        foreach (Transform child in transform)
        {
            if (!child.gameObject.activeInHierarchy)
            {
                Gizmos.DrawWireSphere(child.position, 0.3f);
            }
        }
    }
#endif
}