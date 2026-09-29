using UnityEngine;
using UnityEngine.AI;

namespace prefabs.enemies
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemyStats))]
    public class EnemyMovement : MonoBehaviour
    {
        [Header("Movement Speeds")]
        [SerializeField] private float walkSpeed = 2f;
        [SerializeField] private float runSpeed = 5f;

        private NavMeshAgent _agent;
        private EnemyStats _stats;

        void Awake()
        {
            // Changed from Start to Awake to ensure it's ready before SetTarget calls it
            _agent = GetComponent<NavMeshAgent>();
            _stats = GetComponent<EnemyStats>();
            
            _agent.speed = walkSpeed;
        }

        public void SetDestination(Vector3 targetPosition)
        {
            _agent.SetDestination(targetPosition);
        }

        // Behavior script calls this to toggle between the configured walk and run speeds
        public void SetSpeed(bool isRunning)
        {
            if (_agent != null)
            {
                _agent.speed = isRunning ? runSpeed : walkSpeed;
            }
        }

        public void StopMoving()
        { 
            _agent.isStopped = true;
            _agent.ResetPath();
        }

        public void ResumeMoving()
        {
            _agent.isStopped = false;
        }
    }
}