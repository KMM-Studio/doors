using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
{
    [Serializable]
    public struct Entry
    {
        public TKey key;
        public TValue value;
    }

    // A single unified list replaces the two separate lists
    [SerializeField] private List<Entry> entries = new List<Entry>();

    public void OnBeforeSerialize()
    {
        // Only overwrite the list from code while the game is running.
        // In the Editor, the Inspector list is your source of truth.
        if (Application.isPlaying)
        {
            entries.Clear();
            foreach (var pair in this)
            {
                entries.Add(new Entry { key = pair.Key, value = pair.Value });
            }
        }
    }

    public void OnAfterDeserialize()
    {
        Clear();
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.key != null && !ContainsKey(entry.key))
            {
                Add(entry.key, entry.value);
            }
        }
    }
}