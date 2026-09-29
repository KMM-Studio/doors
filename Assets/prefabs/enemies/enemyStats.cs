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
    
        void Start()
        {
            currentHealth = maxHealth;
        }
    
        // We updated the signature to require the attacker's Transform
        public void TakeDamage(float damage, [CanBeNull] Transform attacker)
        {
            Debug.Log(damage);
            currentHealth -= damage;
        }
    }
}
