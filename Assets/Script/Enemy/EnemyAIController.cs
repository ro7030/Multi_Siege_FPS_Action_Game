using System;
using UnityEngine;
using UnityEngine.AI;
using ProjectM.Player;

namespace ProjectM.Enemy
{
    /// <summary>
    /// NavMeshAgent 기반 적 AI. 가장 가까운 IDamageable을 타깃으로 추격·공격한다.
    /// 우선순위는 Target Tag(예: "DefenseObject") 우선, 없으면 플레이어/임의 대상.
    /// MVP에서는 Host만 실제 AI를 돌리고 Client는 위치 동기화 표시만 한다 (네트워크 단계에서 적용).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(HealthSystem))]
    public class EnemyAIController : MonoBehaviour
    {
        [SerializeField] private EnemyStats stats;
        [SerializeField] private string priorityTargetTag = "DefenseObject";
        [SerializeField] private float targetRefreshInterval = 0.5f;
        [SerializeField] private bool hostAuthoritative = true;

        [Header("시야 체크 (벽/게이트로 막혀있는지 판정)")]
        [Tooltip("이 레이어들이 적-플레이어 사이를 막으면 시야 차단으로 본다. Default + 방어물 + 환경 레이어를 모두 포함시키는 것이 일반적이다.")]
        [SerializeField] private LayerMask sightBlockMask = ~0;   // 기본: 모든 레이어
        [SerializeField] private float eyeHeight = 1.0f;
        [SerializeField] private float targetHeightOffset = 1.0f;

        public EnemyStats Stats => stats;
        public EnemyStateMachine FSM { get; private set; }
        public Transform Target { get; private set; }
        public bool IsAlive => health != null && health.IsAlive;

        public event Action<EnemyAIController> OnDeath;

        private NavMeshAgent agent;
        private HealthSystem health;
        private float nextTargetSearchTime;
        private float nextAttackTime;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<HealthSystem>();
            FSM = new EnemyStateMachine();

            FSM.Bind(EnemyState.Idle,   enter: OnIdleEnter,   tick: OnIdleTick);
            FSM.Bind(EnemyState.Chase,  enter: OnChaseEnter,  tick: OnChaseTick,  exit: OnChaseExit);
            FSM.Bind(EnemyState.Attack, enter: OnAttackEnter, tick: OnAttackTick);
            FSM.Bind(EnemyState.Dead,   enter: OnDeadEnter);
        }

        private void OnEnable()
        {
            health.OnDied += HandleDied;
            if (stats != null)
            {
                health.SetMaxHp(stats.maxHp, refill: true);
                agent.speed = stats.moveSpeed;
            }
        }

        private void OnDisable() => health.OnDied -= HandleDied;

        private void HandleDied(GameObject _) => FSM.ChangeState(EnemyState.Dead);

        private void Update()
        {
            if (!hostAuthoritative) return; // 추후 네트워크 단계에서 Host만 동작
            FSM.Tick();
        }

        // ── State callbacks ────────────────────────────────────────
        private void OnIdleEnter()
        {
            if (agent.isOnNavMesh) agent.ResetPath();
        }

        private void OnIdleTick()
        {
            RefreshTargetIfNeeded();
            if (Target != null) FSM.ChangeState(EnemyState.Chase);
        }

        private void OnChaseEnter()
        {
            if (agent.isOnNavMesh) agent.isStopped = false;
        }

        private void OnChaseTick()
        {
            RefreshTargetIfNeeded();
            if (Target == null) { FSM.ChangeState(EnemyState.Idle); return; }

            if (agent.isOnNavMesh) agent.SetDestination(Target.position);

            float dist = Vector3.Distance(transform.position, Target.position);
            if (dist <= stats.attackRange) FSM.ChangeState(EnemyState.Attack);
        }

        private void OnChaseExit()
        {
            if (agent.isOnNavMesh) agent.ResetPath();
        }

        private void OnAttackEnter()
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            nextAttackTime = Time.time;
        }

        private void OnAttackTick()
        {
            if (Target == null) { FSM.ChangeState(EnemyState.Idle); return; }

            // 타깃을 바라본다
            Vector3 dir = Target.position - transform.position; dir.y = 0;
            if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);

            float dist = Vector3.Distance(transform.position, Target.position);
            if (dist > stats.attackRange * 1.15f) { FSM.ChangeState(EnemyState.Chase); return; }

            if (Time.time >= nextAttackTime)
            {
                DoAttack();
                nextAttackTime = Time.time + stats.attackInterval;
            }
        }

        private void DoAttack()
        {
            if (Target == null) return;
            var dmg = Target.GetComponent<IDamageable>() ?? Target.GetComponentInParent<IDamageable>();
            if (dmg != null && dmg.IsAlive) dmg.TakeDamage(stats.attackDamage, gameObject);
        }

        private void OnDeadEnter()
        {
            if (agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            OnDeath?.Invoke(this);
            // 시체 정리는 Spawner 측에서 풀로 회수하거나 일정 시간 후 Destroy
            Destroy(gameObject, 2f);
        }

        // ── 타깃 선정 ──────────────────────────────────────────────
        private void RefreshTargetIfNeeded()
        {
            if (Time.time < nextTargetSearchTime) return;
            nextTargetSearchTime = Time.time + targetRefreshInterval;
            Target = FindBestTarget();
        }

        private Transform FindBestTarget()
        {
            // 후보 1) 가장 가까운 방어물 (거리 무제한)
            Transform closestDefense = FindClosestDefense();

            // 후보 2) detectRange 안 가장 가까운 플레이어
            Transform closestPlayer = FindClosestPlayerInRange();

            // 플레이어 후보 없으면 → 방어물
            if (closestPlayer == null) return closestDefense;

            // 플레이어와의 시야 체크
            // 시야 트임 → 플레이어 공격 (PlayerFirst/DefenseFirst 둘 다 어그로)
            // 시야 막힘 → 가장 가까운 방어물(게이트) 공격
            //   예) 적 ─ 벽 ─ 게이트 ─ 플레이어  ⇒  게이트부터 부수러 감
            //       적 ──────────  플레이어     ⇒  플레이어 직접 공격
            if (HasLineOfSightTo(closestPlayer))
                return closestPlayer;

            return closestDefense;
        }

        /// <summary>적과 대상 사이가 벽/게이트로 가로막혀 있는지 검사한다. 트여있으면 true.</summary>
        private bool HasLineOfSightTo(Transform target)
        {
            if (target == null) return false;

            Vector3 origin    = transform.position + Vector3.up * eyeHeight;
            Vector3 targetPos = target.position    + Vector3.up * targetHeightOffset;
            Vector3 diff      = targetPos - origin;
            float distance    = diff.magnitude;
            if (distance < 0.001f) return true;

            Vector3 dir = diff / distance;

            // QueryTriggerInteraction.Ignore → 트리거 콜라이더(상점 영역 등)는 시야 차단으로 보지 않음
            if (Physics.Raycast(origin, dir, out RaycastHit hit, distance, sightBlockMask, QueryTriggerInteraction.Ignore))
            {
                // 자기 자신/자식 무시
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                    return true;

                // 맞은 게 대상 본인이면 시야 트임
                if (hit.transform == target || hit.transform.IsChildOf(target) || target.IsChildOf(hit.transform))
                    return true;

                // 그 외 (벽/게이트 등)에 가로막힘
                return false;
            }
            return true; // 충돌 없음 = 트임
        }

        /// <summary>가장 가까운 살아있는 방어 오브젝트(priorityTargetTag 태그). 거리 무제한.</summary>
        private Transform FindClosestDefense()
        {
            if (string.IsNullOrEmpty(priorityTargetTag)) return null;

            Transform best = null;
            float bestDist = float.MaxValue;
            try
            {
                var tagged = GameObject.FindGameObjectsWithTag(priorityTargetTag);
                foreach (var go in tagged)
                {
                    var dmg = go.GetComponentInParent<IDamageable>();
                    if (dmg == null || !dmg.IsAlive) continue;
                    float d = Vector3.Distance(transform.position, go.transform.position);
                    if (d < bestDist) { bestDist = d; best = go.transform; }
                }
            }
            catch (UnityException) { /* 태그 미정의 시 무시 */ }
            return best;
        }

        /// <summary>detectRange 안에서 가장 가까운 플레이어(IDamageable, 적/방어물 제외).</summary>
        private Transform FindClosestPlayerInRange()
        {
            if (stats == null) return null;

            Transform best = null;
            float bestDist = float.MaxValue;

            var hits = Physics.OverlapSphere(transform.position, stats.detectRange);
            foreach (var h in hits)
            {
                if (h.gameObject == gameObject) continue;
                if (h.GetComponentInParent<EnemyAIController>() != null) continue; // 다른 적 제외
                if (!string.IsNullOrEmpty(priorityTargetTag) && h.CompareTag(priorityTargetTag)) continue; // 방어물 제외

                var dmg = h.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive) continue;

                float d = Vector3.Distance(transform.position, h.transform.position);
                if (d < bestDist) { bestDist = d; best = h.transform; }
            }
            return best;
        }
    }
}
