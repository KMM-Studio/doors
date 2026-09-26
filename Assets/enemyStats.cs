using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemyStats : MonoBehaviour
{
    [SerializeField]private float currentHealth;
    [SerializeField]private float maxHealth;
    
    private List<GameObject> _targets;
    private GameObject _currentTarget;
    
    void Start()
    {
        currentHealth = maxHealth;
    }

    public void UpdateTargets(GameObject[] targets)
    {
        _targets = targets;
        _targets.Sort((x, y) => Vector3.Distance(transform.position, x.transform.position).CompareTo(Vector3.Distance(transform.position, y.transform.position)));
    }
    
    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Destroy(this); // to change when inpolementing enemy pooling
        }
    }
}
