using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace prefabs.enemies
{
    public class EnemyStats : MonoBehaviour
    {
        [SerializeField] private float currentHealth;
        [SerializeField] private float maxHealth;
    
        // The Threat Table: Maps an attacker's Transform to their total damage dealt
        private readonly Dictionary<Transform, float> _threatTable = new Dictionary<Transform, float>();
    
        void Start()
        {
            currentHealth = maxHealth;
        }
    
        // We updated the signature to require the attacker's Transform
        public void TakeDamage(float damage, [CanBeNull] Transform attacker)
        {
            currentHealth -= damage;

            // Add or update the attacker in the threat table
            if (attacker != null)
            {
                if (!_threatTable.TryAdd(attacker, damage))
                {
                    _threatTable[attacker] += damage;
                }
            }

            if (currentHealth <= 0)
            {
                Destroy(gameObject); // later change to pooling
            }
        }

        // Returns the Transform of the player with the most threat
        public Transform GetHighestThreatTarget()
        {
            if (_threatTable.Count == 0) return null;

            // Clean up the table in case an attacker disconnected or died
            var keysToRemove = _threatTable.Keys.Where(k => k == null).ToList();
            foreach (var key in keysToRemove)
            {
                _threatTable.Remove(key);
            }

            if (_threatTable.Count == 0) return null;

            // Find and return the attacker with the highest damage value
            return _threatTable.Aggregate((x, y) => x.Value > y.Value ? x : y).Key;
        }
    }
}
