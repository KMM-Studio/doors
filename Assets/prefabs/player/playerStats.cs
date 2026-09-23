using UnityEngine;

namespace prefabs.player
{
    public class PlayerStats : MonoBehaviour
    {
        [Header("Player Stats")]
        public int maxHealth = 100;
        public int currentHealth;
        public float maxSpeed = 5f;
    
        private void Awake()
        {
            if (currentHealth <= 0) currentHealth = maxHealth;
        }
    }
}