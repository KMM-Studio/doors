using UnityEngine;

// Make sure we can see your EnemyBehavior script

namespace prefabs.enemies
{
    public class AttackFinishedBehaviour : StateMachineBehaviour
    {
        // This runs automatically the exact moment the animation state finishes or transitions out
        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            // Find the enemy script attached to this character
            EnemyBehavior enemy = animator.GetComponentInParent<EnemyBehavior>();
        
            if (enemy != null)
            {
                enemy.OnAttackAnimationFinished();
            }
        }
    }
}