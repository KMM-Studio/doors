using UnityEngine;
using UnityEngine.AI;

// Required for NavMesh components

namespace prefabs.enemies
{
    using UnityEngine;
    using UnityEngine.AI;

    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemyStats))]
    public class EnemyMovement : MonoBehaviour
    {
        private NavMeshAgent _agent;
        private EnemyStats _stats;
        private Transform _currentTarget;

        void Start()
        {
            _agent = GetComponent<NavMeshAgent>();
            _stats = GetComponent<EnemyStats>();
        }

        void Update()
        {
            // Continuously check who has the most threat
            _currentTarget = _stats.GetHighestThreatTarget();

            if (_currentTarget)
            {
                _agent.isStopped = false;
                _agent.destination = _currentTarget.position;
            }
            else
            {
                // If no one has attacked yet (or all attackers are dead), stop moving
                // Alternatively, you could put patrol logic here
                _agent.isStopped = true;
            }
        }
    }
}