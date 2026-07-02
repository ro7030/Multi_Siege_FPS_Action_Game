using UnityEngine;
using UnityEngine.AI;
using ProjectM.Network;

namespace ProjectM.Enemy
{
    /// <summary>
    /// NavMeshAgent·FSM 상태를 Animator 파라미터(Speed/Grounded/Attack)에 반영한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyAnimator : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int SprintHash = Animator.StringToHash("Sprint");
        private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");

        [SerializeField] private EnemyAIController ai;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Animator animator;

        private void Awake()
        {
            if (ai == null) ai = GetComponent<EnemyAIController>();
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
        }

        private void OnEnable()
        {
            if (ai != null && ai.FSM != null)
                ai.FSM.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (ai != null && ai.FSM != null)
                ai.FSM.OnStateChanged -= HandleStateChanged;
        }

        private void Update()
        {
            if (animator == null || agent == null) return;
            if (NetworkSessionHelper.IsMultiplayerSession && !NetworkSessionHelper.IsServer)
                return;

            Vector3 velocity = agent.velocity;
            velocity.y = 0f;
            float normalized = agent.speed > 0.01f ? velocity.magnitude / agent.speed : velocity.magnitude;

            animator.SetFloat(SpeedHash, normalized);
            animator.SetBool(GroundedHash, agent.isOnNavMesh);
            animator.SetBool(SprintHash, normalized > 0.75f);
            animator.SetFloat(VerticalSpeedHash, agent.velocity.y);

            if (ai != null && ai.FSM != null && ai.FSM.Current == EnemyState.Dead)
                animator.SetFloat(SpeedHash, 0f);
        }

        private void HandleStateChanged(EnemyState previous, EnemyState current)
        {
            if (animator == null) return;
            if (current == EnemyState.Attack && previous != EnemyState.Attack)
                animator.SetTrigger(AttackHash);
        }
    }
}
