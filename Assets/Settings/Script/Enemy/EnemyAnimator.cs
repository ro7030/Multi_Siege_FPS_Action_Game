using UnityEngine;
using UnityEngine.AI;
using ProjectM.Network;

namespace ProjectM.Enemy
{
    /// <summary>
    /// NavMeshAgent·FSM 상태를 Animator 파라미터(Speed/Grounded/Attack)에 반영한다.
    /// 서버(Host)가 계산한 값을 NetworkEnemyAnimationBridge로 복제하고,
    /// 클라이언트는 해당 브리지 값을 그대로 재생한다.
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
        [SerializeField] private NetworkEnemyAnimationBridge animBridge;

        private void Awake()
        {
            if (ai == null) ai = GetComponent<EnemyAIController>();
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animBridge == null) animBridge = GetComponent<NetworkEnemyAnimationBridge>();
        }

        private void OnEnable()
        {
            if (ai != null && ai.FSM != null)
                ai.FSM.OnStateChanged += HandleStateChanged;
            if (animBridge != null)
                animBridge.OnAttackRequested += HandleRemoteAttack;
        }

        private void OnDisable()
        {
            if (ai != null && ai.FSM != null)
                ai.FSM.OnStateChanged -= HandleStateChanged;
            if (animBridge != null)
                animBridge.OnAttackRequested -= HandleRemoteAttack;
        }

        private void Update()
        {
            if (animator == null || agent == null) return;

            bool isNetworkedClient = NetworkSessionHelper.IsMultiplayerSession && !NetworkSessionHelper.IsServer;
            if (isNetworkedClient)
            {
                ApplyRemoteState();
                return;
            }

            Vector3 velocity = agent.velocity;
            velocity.y = 0f;
            float normalized = agent.speed > 0.01f ? velocity.magnitude / agent.speed : velocity.magnitude;
            bool grounded = agent.isOnNavMesh;
            bool sprint = normalized > 0.75f;
            float verticalSpeed = agent.velocity.y;

            if (ai != null && ai.FSM != null && ai.FSM.Current == EnemyState.Dead)
                normalized = 0f;

            animator.SetFloat(SpeedHash, normalized);
            animator.SetBool(GroundedHash, grounded);
            animator.SetBool(SprintHash, sprint);
            animator.SetFloat(VerticalSpeedHash, verticalSpeed);

            animBridge?.Publish(normalized, grounded, sprint, verticalSpeed);
        }

        private void ApplyRemoteState()
        {
            if (animBridge == null) return;

            animator.SetFloat(SpeedHash, animBridge.SyncedSpeed);
            animator.SetBool(GroundedHash, animBridge.SyncedGrounded);
            animator.SetBool(SprintHash, animBridge.SyncedSprint);
            animator.SetFloat(VerticalSpeedHash, animBridge.SyncedVerticalSpeed);
        }

        private void HandleStateChanged(EnemyState previous, EnemyState current)
        {
            if (animator == null) return;
            if (current != EnemyState.Attack || previous == EnemyState.Attack) return;

            animator.SetTrigger(AttackHash);
            animBridge?.PublishAttack();
        }

        /// <summary>서버가 전파한 Attack 트리거를 클라이언트에서 재생한다 (서버 자신은 위 HandleStateChanged에서 이미 처리).</summary>
        private void HandleRemoteAttack()
        {
            if (animator == null) return;
            if (!NetworkSessionHelper.IsMultiplayerSession || NetworkSessionHelper.IsServer) return;

            animator.SetTrigger(AttackHash);
        }
    }
}
