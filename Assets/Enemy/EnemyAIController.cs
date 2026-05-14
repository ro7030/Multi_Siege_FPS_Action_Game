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
            Transform best = null;
            float bestDist = float.MaxValue;

            // 1순위: priorityTargetTag (방어 오브젝트)
            if (!string.IsNullOrEmpty(priorityTargetTag))
            {
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
            }
            if (best != null) return best;

            // 2순위: 감지 범위 내 IDamageable (플레이어 등). 다른 적은 제외.
            var hits = Physics.OverlapSphere(transform.position, stats.detectRange);
            foreach (var h in hits)
            {
                if (h.gameObject == gameObject) continue;
                if (h.GetComponentInParent<EnemyAIController>() != null) continue; // 자기 + 다른 적 모두 제외
                var dmg = h.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive) continue;
                float d = Vector3.Distance(transform.position, h.transform.position);
                if (d < bestDist) { bestDist = d; best = h.transform; }
            }
            return best;
        }
    }
}
