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

        [Header("막힌 경로 폴백 (방어물 공격용)")]
        [Tooltip("NavMeshObstacle 등으로 경로가 막혀 더 못 갈 때, attackRange 위에 더해줄 허용 거리. 방어물(priorityTargetTag) 타깃에만 적용된다.")]
        [SerializeField] private float blockedAttackTolerance = 0.3f;
        [Tooltip("'경로가 사실상 끝났다'고 볼 속도 임계값. 이 값 이하 & 남은거리 ≈ stoppingDistance 면 정지로 본다.")]
        [SerializeField] private float blockedSpeedThreshold = 0.05f;

        [Header("디버그")]
        [Tooltip("타깃 선정/공격 진입 시 로그를 콘솔에 출력. 문제 진단용. 평소엔 꺼두기.")]
        [SerializeField] private bool debugLogTargeting = false;

        public EnemyStats Stats => stats;
        public EnemyStateMachine FSM { get; private set; }
        public Transform Target { get; private set; }
        public bool IsAlive => health != null && health.IsAlive;

        public event Action<EnemyAIController> OnDeath;

        private NavMeshAgent agent;
        private HealthSystem health;
        private float nextTargetSearchTime;
        private float nextAttackTime;
        private Collider targetCollider;

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

            float dist = DistanceToTargetSurface();
            if (dist <= stats.attackRange)
            {
                FSM.ChangeState(EnemyState.Attack);
                return;
            }

            // NavMesh가 막혀 더 이상 진행 못 하는데 타깃이 방어물이면 표면 거리 + 약간의 허용치로 공격 진입
            if (TargetIsDefense() && IsAgentBlocked() && dist <= stats.attackRange + blockedAttackTolerance)
            {
                FSM.ChangeState(EnemyState.Attack);
            }
        }

        private void OnChaseExit()
        {
            if (agent.isOnNavMesh) agent.ResetPath();
        }

        private void OnAttackEnter()
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            nextAttackTime = Time.time;

            if (debugLogTargeting && Target != null)
            {
                float pivot = Vector3.Distance(transform.position, Target.position);
                float surf = DistanceToTargetSurface();
                Debug.Log($"[EnemyAI] {name} ENTER Attack → target='{Target.name}' surfaceDist={surf:F2} pivotDist={pivot:F2} attackRange={stats.attackRange:F2}");
            }
        }

        private void OnAttackTick()
        {
            RefreshTargetIfNeeded();
            if (Target == null) { FSM.ChangeState(EnemyState.Idle); return; }

            // 타깃을 바라본다
            Vector3 dir = Target.position - transform.position; dir.y = 0;
            if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);

            float dist = DistanceToTargetSurface();
            float keepRange = stats.attackRange * 1.15f;
            // 방어물이 NavMeshObstacle로 막혀있는 상황에서는 허용치만큼 유지 사거리 확장
            if (TargetIsDefense()) keepRange += blockedAttackTolerance;

            if (dist > keepRange) { FSM.ChangeState(EnemyState.Chase); return; }

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
            // 사망/다운 등으로 타깃이 더 이상 유효하지 않으면 즉시 해제 (다음 줄에서 바로 재탐색)
            if (Target != null && !IsTargetDamageableAlive(Target))
            {
                Target = null;
                targetCollider = null;
                nextTargetSearchTime = 0f;
            }

            if (Time.time < nextTargetSearchTime) return;
            nextTargetSearchTime = Time.time + targetRefreshInterval;

            var newTarget = FindBestTarget();
            if (newTarget != Target)
            {
                Target = newTarget;
                targetCollider = newTarget != null ? PickTargetCollider(newTarget) : null;

                if (debugLogTargeting)
                {
                    if (newTarget != null)
                    {
                        string colInfo = targetCollider != null
                            ? $"{targetCollider.name}(trigger={targetCollider.isTrigger}, extents={targetCollider.bounds.extents})"
                            : "<no collider>";
                        Debug.Log($"[EnemyAI] {name} → target='{newTarget.name}' pivotDist={Vector3.Distance(transform.position, newTarget.position):F2} col={colInfo}");
                    }
                    else
                    {
                        Debug.Log($"[EnemyAI] {name} → target cleared");
                    }
                }
            }
        }

        /// <summary>거리 판정에 쓸 콜라이더를 고른다. 트리거(상호작용 영역 등)는 제외하고 본체에 가까운 콜라이더를 우선한다.</summary>
        private static Collider PickTargetCollider(Transform t)
        {
            // 1순위: 같은 GameObject의 비-트리거 콜라이더 (DefenseObject 본체)
            if (t.TryGetComponent<Collider>(out var self) && self.enabled && !self.isTrigger)
                return self;

            // 2순위: 자식 트리 안의 비-트리거 콜라이더
            var all = t.GetComponentsInChildren<Collider>(includeInactive: false);
            foreach (var c in all)
            {
                if (c == null || !c.enabled) continue;
                if (c.isTrigger) continue;
                return c;
            }

            // 3순위(폴백): 트리거라도 있으면 쓴다 (콜라이더가 트리거뿐일 때)
            foreach (var c in all)
            {
                if (c != null && c.enabled) return c;
            }
            return null;
        }

        /// <summary>
        /// 적 → 타깃 표면까지의 거리.
        /// Collider.ClosestPoint는 입력 위치가 콜라이더 내부에 있을 때 그 위치를 그대로 반환하므로
        /// pivot 거리에서 AABB 반지름을 뺀 값을 하한선으로 둬서 0/근접 오판정을 막는다.
        /// </summary>
        private float DistanceToTargetSurface()
        {
            if (Target == null) return float.MaxValue;
            Vector3 myPos = transform.position;
            float pivotDist = Vector3.Distance(myPos, Target.position);

            if (targetCollider == null) return pivotDist;

            float surfaceDist = Vector3.Distance(myPos, targetCollider.ClosestPoint(myPos));
            // pivot 기준 도달 가능한 최소 표면 거리. 콜라이더가 비정상적으로 크거나 enemy가 내부에 있을 때 발생하는 0-거리 오판정을 막는다.
            float lowerBound = Mathf.Max(0f, pivotDist - targetCollider.bounds.extents.magnitude);
            return Mathf.Max(surfaceDist, lowerBound);
        }

        /// <summary>현재 타깃이 방어물(priorityTargetTag)인지.</summary>
        private bool TargetIsDefense()
        {
            if (Target == null || string.IsNullOrEmpty(priorityTargetTag)) return false;
            try { return Target.CompareTag(priorityTargetTag); }
            catch (UnityException) { return false; }
        }

        /// <summary>NavMeshAgent가 더 이상 진행하지 못하는 상태(부분/무효 경로 또는 목적지 도달 후 정지).</summary>
        private bool IsAgentBlocked()
        {
            if (agent == null || !agent.isOnNavMesh) return false;
            if (agent.pathPending) return false;

            if (agent.pathStatus == NavMeshPathStatus.PathPartial ||
                agent.pathStatus == NavMeshPathStatus.PathInvalid)
                return true;

            // 경로가 완전(complete)이라도 끝까지 가서 멈췄으면 막힌 것으로 간주
            if (agent.remainingDistance <= agent.stoppingDistance + 0.05f &&
                agent.velocity.sqrMagnitude <= blockedSpeedThreshold * blockedSpeedThreshold)
                return true;

            return false;
        }

        private static bool IsTargetDamageableAlive(Transform t)
        {
            if (t == null) return false;
            var dmg = t.GetComponent<IDamageable>() ?? t.GetComponentInParent<IDamageable>();
            return dmg != null && dmg.IsAlive;
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

            // 트리거 콜라이더(수리 영역 등 방어물의 자식 트리거)는 어그로 후보에서 제외
            var hits = Physics.OverlapSphere(transform.position, stats.detectRange, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.gameObject == gameObject) continue;
                if (h.GetComponentInParent<EnemyAIController>() != null) continue; // 다른 적 제외
                if (IsDefenseOrChild(h.transform)) continue;                        // 방어물 본체 + 그 자식 콜라이더 모두 제외

                var dmg = h.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive) continue;

                float d = Vector3.Distance(transform.position, h.transform.position);
                if (d < bestDist) { bestDist = d; best = h.transform; }
            }
            return best;
        }

        /// <summary>해당 Transform 또는 그 부모 중 하나라도 priorityTargetTag(방어물 태그)면 true.</summary>
        private bool IsDefenseOrChild(Transform t)
        {
            if (t == null || string.IsNullOrEmpty(priorityTargetTag)) return false;
            try
            {
                for (var cur = t; cur != null; cur = cur.parent)
                {
                    if (cur.CompareTag(priorityTargetTag)) return true;
                }
            }
            catch (UnityException) { /* 태그 미정의 시 무시 */ }
            return false;
        }
    }
}
