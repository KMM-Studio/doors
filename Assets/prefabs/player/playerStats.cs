using System;
using Unity.Mathematics;
using UnityEngine;

namespace prefabs.player
{
    public class PlayerStats : MonoBehaviour
    {
        [Header("Player Stats")]
        public int maxHealth = 100;
        public int currentHealth;
        public float maxSpeed = 5f;
    
        public static event Action<float> OnHealthChanged;
    
        private void Awake()
        {
            if (currentHealth <= 0) currentHealth = maxHealth;
        }
    
        private void Update()
        {
            OnHealthChanged?.Invoke((float)currentHealth / maxHealth);
        }
    }
}