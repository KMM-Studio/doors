using System;
using System.Collections.Generic;
using UnityEngine;

namespace prefabs.enemies
{
    public class EnemyBehavior : MonoBehaviour
    {
        private static readonly int Walk = Animator.StringToHash("OnWalk");
        private static readonly int Run = Animator.StringToHash("OnRun");
        private static readonly int Idle = Animator.StringToHash("OnIdle");

        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private EnemyMovement movementScript; 
        
        [Header("Settings")]
        [SerializeField] private float chaseRange = 15f; 
        [SerializeField] private float runRange = 7f; // The distance at which the enemy starts running
        [SerializeField] private LayerMask playerLayer; 
        [SerializeField] private EnemyAttack[] availableAttacks;
        
        private Transform _currentPlayerTransform;
        private EnemyState _currentState = EnemyState.Idle;
        private MoveMode _currentMoveMode = MoveMode.Idle; // Tracks animation state to prevent trigger spam
        private float _nextAttackTime = 0f;

        private void Update()
        {   
            switch (_currentState)
            {
                case EnemyState.Idle:
                    UpdateIdle();
                    break;
                case EnemyState.Chasing:
                    UpdateChasing();
                    break;
                case EnemyState.Attacking:
                    UpdateAttacking();
                    break;
            }
        }

        private void UpdateIdle()
        {
            FindClosestTarget();

            if (_currentPlayerTransform != null)
            {
                ChangeState(EnemyState.Chasing);
            }
        }

        private void FindClosestTarget()
        {   
            Collider[] hitPlayers = Physics.OverlapSphere(transform.position, chaseRange, playerLayer);

            if (hitPlayers.Length == 0)
            {
                _currentPlayerTransform = null;
                return;
            }

            Transform closestPlayer = null;
            float minDistanceSqr = Mathf.Infinity;
            Vector3 currentPos = transform.position;

            foreach (Collider playerCol in hitPlayers)
            {
                float distSqr = (playerCol.transform.position - currentPos).sqrMagnitude;
                if (distSqr < minDistanceSqr)
                {
                    minDistanceSqr = distSqr;
                    closestPlayer = playerCol.transform;
                }
            }

            _currentPlayerTransform = closestPlayer;
        }

        private void UpdateChasing()
        {
            if (_currentPlayerTransform == null) 
            {
                ChangeState(EnemyState.Idle);
                return;
            }

            float distanceToPlayer = Vector3.Distance(transform.position, _currentPlayerTransform.position);

            if (distanceToPlayer > chaseRange)
            {
                _currentPlayerTransform = null;
                ChangeState(EnemyState.Idle);
                return;
            }

            // Decide to walk or run based on distance
            if (distanceToPlayer <= runRange)
            {
                ChangeMoveMode(MoveMode.Running);
            }
            else
            {
                ChangeMoveMode(MoveMode.Walking);
            }

            if (movementScript != null)
            {
                movementScript.SetDestination(_currentPlayerTransform.position);
            }
                
            if (Time.time >= _nextAttackTime)
            {
                Vector3 direction = (_currentPlayerTransform.position - transform.position).normalized;
                float angleToPlayer = Vector3.Dot(transform.forward, direction);
                
                DecideAttack(distanceToPlayer, angleToPlayer);
            }
        }

        private void UpdateAttacking()
        {
            if (_currentPlayerTransform == null) return;

            Vector3 direction = (_currentPlayerTransform.position - transform.position).normalized;
            direction.y = 0; 
            if (direction != Vector3.zero)
            {
                Quaternion lookRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
            }
        }

        private void DecideAttack(float distance, float angle)
        {
            List<EnemyAttack> validAttacks = new List<EnemyAttack>();

            foreach (var attack in availableAttacks)
            {
                if (distance <= attack.maxRange)
                {
                    if (Mathf.Approximately(Mathf.Sign(angle), Mathf.Sign(attack.requiredAlignment)) || attack.requiredAlignment == 0)
                    {
                        validAttacks.Add(attack);
                    }
                }
            }

            if (validAttacks.Count > 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, validAttacks.Count);
                PerformAttack(validAttacks[randomIndex]);
            }
        }

        private void PerformAttack(EnemyAttack attack)
        {
            if (animator != null) animator.SetTrigger(attack.animationTriggerName);
            _nextAttackTime = Time.time + attack.cooldown;
            
            ChangeState(EnemyState.Attacking);
        }

        private void ChangeState(EnemyState newState)
        {
            _currentState = newState;
    
            if (_currentState == EnemyState.Chasing)
            {
                if (movementScript != null) movementScript.ResumeMoving();
            }
            else if (_currentState == EnemyState.Idle)
            {
                // Only force the Idle animation if the state is actually Idle
                ChangeMoveMode(MoveMode.Idle);
                if (movementScript != null) movementScript.StopMoving();
            }
            else if (_currentState == EnemyState.Attacking)
            {
                // Stop the NavMeshAgent from sliding forward, but DO NOT force the Idle animation!
                if (movementScript != null) movementScript.StopMoving();
            }
        }

        // Evaluates if we are already in the requested animation state before firing triggers
        private void ChangeMoveMode(MoveMode newMode)
        {
            if (_currentMoveMode == newMode) return; 

            _currentMoveMode = newMode;

            switch (newMode)
            {
                case MoveMode.Idle:
                    if (animator != null) animator.SetTrigger(Idle);
                    break;
                case MoveMode.Walking:
                    if (animator != null) animator.SetTrigger(Walk);
                    if (movementScript != null) movementScript.SetSpeed(false);
                    break;
                case MoveMode.Running:
                    if (animator != null) animator.SetTrigger(Run);
                    if (movementScript != null) movementScript.SetSpeed(true);
                    break;
            }
        }

        public void OnAttackAnimationFinished()
        {
            // Reset the move mode to force it to re-evaluate after an attack
            _currentMoveMode = MoveMode.Idle; 
            ChangeState(EnemyState.Chasing);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, chaseRange);
            
            // Draw a yellow sphere to easily see where the walk ends and the run begins
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, runRange);
        }
    }

    public enum EnemyState
    {
        Idle,
        Chasing,
        Attacking
    }

    // New Enum solely for tracking the physical movement animation
    public enum MoveMode
    {
        Idle,
        Walking,
        Running
    }

    [Serializable]
    public struct EnemyAttack
    {
        public string attackName;
        public string animationTriggerName;
        public float cooldown;
        public float damage;
        public float maxRange;
        public float requiredAlignment;
    }
}