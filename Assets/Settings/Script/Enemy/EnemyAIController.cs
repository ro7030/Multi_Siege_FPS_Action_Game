using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using ProjectM.Combat;
using ProjectM.Defense;
using ProjectM.Player;
using ProjectM.Network;

namespace ProjectM.Enemy
{
    /// <summary>
    /// NavMeshAgent 기반 적 AI.
    /// 감지 범위 안: 플레이어 > 게이트 > 밭 > 베이스 (동티어 최단 표면거리).
    /// 범위 안 후보 없음: 전역 fallback — 플레이어 제외, 게이트·밭·베이스만(공성 march).
    /// Host(서버)만 시뮬레이션한다.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(HealthSystem))]
    public class EnemyAIController : MonoBehaviour, IStunnable
    {
        private enum TargetKind
        {
            Player = 0,
            Gate = 1,
            Farm = 2,
            BaseCamp = 3
        }

        private readonly struct TargetCandidate
        {
            public readonly Transform Transform;
            public readonly TargetKind Kind;
            public readonly float SurfaceDistance;
            public readonly Collider Collider;

            public TargetCandidate(Transform transform, TargetKind kind, float surfaceDistance, Collider collider)
            {
                Transform = transform;
                Kind = kind;
                SurfaceDistance = surfaceDistance;
                Collider = collider;
            }

            public int Tier => (int)Kind;
        }

        [SerializeField] private EnemyStats stats;
        [SerializeField] private string priorityTargetTag = "DefenseObject";
        [SerializeField] private float targetRefreshInterval = 0.5f;
        [SerializeField] private bool hostAuthoritative = true;

        [Header("NavMesh 추격")]
        [SerializeField] private float navSampleRadius = 2f;

        [Header("막힌 경로 폴백 (방어물 공격용)")]
        [Tooltip("NavMeshObstacle 등으로 경로가 막혀 더 못 갈 때, attackRange 위에 더해줄 허용 거리.")]
        [SerializeField] private float blockedAttackTolerance = 0.3f;
        [Tooltip("'경로가 사실상 끝났다'고 볼 속도 임계값.")]
        [SerializeField] private float blockedSpeedThreshold = 0.05f;

        [Header("디버그")]
        [SerializeField] private bool debugLogTargeting = false;

        public EnemyStats Stats => stats;
        public EnemyStateMachine FSM { get; private set; }
        public Transform Target { get; private set; }
        public bool IsAlive => health != null && health.IsAlive;
        public bool IsStunned => Time.time < stunEndTime;

        public event Action<EnemyAIController> OnDeath;

        public void SetSimulationEnabled(bool enabled)
        {
            hostAuthoritative = enabled;
            if (agent == null) return;

            if (!enabled)
            {
                if (agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.ResetPath();
                }

                agent.enabled = false;
                return;
            }

            agent.enabled = true;
        }

        private NavMeshAgent agent;
        private HealthSystem health;
        private float stunEndTime;
        private bool wasStunned;
        private float nextTargetSearchTime;
        private float nextAttackTime;
        private Collider targetCollider;
        private TargetKind currentTargetKind = TargetKind.BaseCamp;
        private readonly List<TargetCandidate> candidateBuffer = new();

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

        public void ApplyStun(float duration, GameObject source)
        {
            if (!IsAlive || duration <= 0f)
                return;

            stunEndTime = Mathf.Max(stunEndTime, Time.time + duration);
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
        }

        private void Update()
        {
            if (NetworkSessionHelper.IsMultiplayerSession && !NetworkSessionHelper.IsServer)
                return;
            if (!hostAuthoritative) return;

            bool stunned = IsStunned;
            if (wasStunned && !stunned && agent != null && agent.isOnNavMesh
                && FSM.Current == EnemyState.Chase)
            {
                agent.isStopped = false;
            }

            wasStunned = stunned;
            FSM.Tick();
        }

        // ── State callbacks ────────────────────────────────────────
        private void OnIdleEnter()
        {
            if (agent.isOnNavMesh) agent.ResetPath();
        }

        private void OnIdleTick()
        {
            if (IsStunned)
            {
                if (agent.isOnNavMesh) agent.isStopped = true;
                return;
            }

            RefreshTargetIfNeeded();
            if (Target != null) FSM.ChangeState(EnemyState.Chase);
        }

        private void OnChaseEnter()
        {
            if (agent.isOnNavMesh) agent.isStopped = false;
        }

        private void OnChaseTick()
        {
            if (IsStunned)
            {
                if (agent.isOnNavMesh) agent.isStopped = true;
                return;
            }

            RefreshTargetIfNeeded();
            if (Target == null) { FSM.ChangeState(EnemyState.Idle); return; }

            UpdateChaseDestination();

            if (stats == null) return;

            float dist = DistanceToTargetSurface();
            if (dist <= stats.attackRange)
            {
                FSM.ChangeState(EnemyState.Attack);
                return;
            }

            if (TargetIsDefense() && IsAgentBlocked() && dist <= stats.attackRange + blockedAttackTolerance)
                FSM.ChangeState(EnemyState.Attack);
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
                Debug.Log($"[EnemyAI] {name} ENTER Attack → target='{Target.name}' kind={currentTargetKind} surfaceDist={surf:F2} attackRange={stats.attackRange:F2}");
            }
        }

        private void OnAttackTick()
        {
            if (IsStunned)
            {
                if (agent.isOnNavMesh) agent.isStopped = true;
                return;
            }

            RefreshTargetIfNeeded();
            if (Target == null) { FSM.ChangeState(EnemyState.Idle); return; }

            Vector3 dir = Target.position - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);

            if (stats == null) return;

            float dist = DistanceToTargetSurface();
            float keepRange = stats.attackRange * 1.15f;
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
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            OnDeath?.Invoke(this);

            if (TryGetComponent<NetworkObject>(out var netObj) && netObj.IsSpawned)
            {
                if (NetworkSessionHelper.IsServer)
                    StartCoroutine(DespawnAfterDelay(2f));
                return;
            }

            Destroy(gameObject, 2f);
        }

        private IEnumerator DespawnAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (TryGetComponent<NetworkObject>(out var netObj) && netObj.IsSpawned)
                netObj.Despawn(true);
            else if (gameObject != null)
                Destroy(gameObject);
        }

        private void UpdateChaseDestination()
        {
            if (Target == null || agent == null || !agent.isOnNavMesh) return;

            Vector3 dest = Target.position;
            if (targetCollider != null)
                dest = targetCollider.ClosestPoint(transform.position);

            if (NavMesh.SamplePosition(dest, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
                dest = hit.position;

            agent.SetDestination(dest);
        }

        // ── 타깃 선정 ──────────────────────────────────────────────
        private void RefreshTargetIfNeeded()
        {
            if (Target != null && !IsTargetDamageableAlive(Target))
            {
                Target = null;
                targetCollider = null;
                nextTargetSearchTime = 0f;
            }

            if (ShouldForceRetarget())
                nextTargetSearchTime = 0f;

            if (Time.time < nextTargetSearchTime) return;
            nextTargetSearchTime = Time.time + targetRefreshInterval;

            ApplyTarget(FindBestTarget());
        }

        private void ApplyTarget(Transform newTarget)
        {
            if (newTarget == Target) return;

            Target = newTarget;
            targetCollider = newTarget != null ? PickTargetCollider(newTarget) : null;
            if (newTarget != null)
                currentTargetKind = ResolveTargetKind(newTarget);

            if (!debugLogTargeting) return;

            if (newTarget != null)
            {
                float surf = ComputeSurfaceDistance(transform.position, newTarget, targetCollider);
                Debug.Log($"[EnemyAI] {name} → target='{newTarget.name}' kind={currentTargetKind} surfaceDist={surf:F2}");
            }
            else
            {
                Debug.Log($"[EnemyAI] {name} → target cleared");
            }
        }

        /// <summary>감지 범위 안 더 높은 티어 후보가 있으면 즉시 재탐색.</summary>
        private bool ShouldForceRetarget()
        {
            if (stats == null) return false;

            GatherCandidates(candidateBuffer, useRangeFilter: true, includePlayers: true);
            var inRangeBest = SelectBest(candidateBuffer);
            if (inRangeBest.Transform == null) return false;
            if (Target == null) return true;

            int currentTier = (int)currentTargetKind;
            if (inRangeBest.Tier < currentTier) return true;

            return inRangeBest.Transform != Target && inRangeBest.Tier == currentTier
                   && inRangeBest.SurfaceDistance + 0.05f < DistanceToTargetSurface();
        }

        private Transform FindBestTarget()
        {
            if (stats == null) return null;

            GatherCandidates(candidateBuffer, useRangeFilter: true, includePlayers: true);
            var inRangeBest = SelectBest(candidateBuffer);
            if (inRangeBest.Transform != null)
                return inRangeBest.Transform;

            // 전역 fallback: 거리 제한 없음, 플레이어 제외(감지 범위 안에서만 플레이어 타겟)
            GatherCandidates(candidateBuffer, useRangeFilter: false, includePlayers: false);
            return SelectBest(candidateBuffer).Transform;
        }

        private void GatherCandidates(List<TargetCandidate> buffer, bool useRangeFilter, bool includePlayers)
        {
            buffer.Clear();
            Vector3 myPos = transform.position;
            float detectRange = stats.detectRange;

            if (includePlayers)
            {
                foreach (var pc in UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                {
                    if (pc == null) continue;
                    var hs = pc.GetComponent<HealthSystem>();
                    if (hs == null || !hs.IsAlive) continue;

                    var col = PickTargetCollider(pc.transform);
                    float dist = ComputeSurfaceDistance(myPos, pc.transform, col);
                    if (useRangeFilter && dist > detectRange) continue;

                    buffer.Add(new TargetCandidate(pc.transform, TargetKind.Player, dist, col));
                }
            }

            foreach (var defense in UnityEngine.Object.FindObjectsByType<DefenseObject>(FindObjectsSortMode.None))
            {
                if (defense == null || !defense.gameObject.activeInHierarchy) continue;

                var dmg = defense.GetComponent<IDamageable>();
                if (dmg == null || !dmg.IsAlive) continue;

                var kind = GetDefenseKind(defense);
                var col = PickTargetCollider(defense.transform);
                float dist = ComputeSurfaceDistance(myPos, defense.transform, col);
                if (useRangeFilter && dist > detectRange) continue;

                buffer.Add(new TargetCandidate(defense.transform, kind, dist, col));
            }
        }

        private static TargetCandidate SelectBest(List<TargetCandidate> candidates)
        {
            TargetCandidate best = default;
            bool hasBest = false;
            float bestScore = float.MaxValue;

            foreach (var c in candidates)
            {
                float score = c.Tier * 100000f + c.SurfaceDistance;
                if (score >= bestScore) continue;
                bestScore = score;
                best = c;
                hasBest = true;
            }

            return hasBest ? best : default;
        }

        private static TargetKind GetDefenseKind(DefenseObject defense)
        {
            if (defense.GetComponent<FarmPlot>() != null)
                return TargetKind.Farm;

            if (defense.GetComponent<BaseCampController>() != null)
                return TargetKind.BaseCamp;

            return TargetKind.Gate;
        }

        private static TargetKind ResolveTargetKind(Transform t)
        {
            if (t.GetComponentInParent<PlayerController>() != null)
                return TargetKind.Player;

            var defense = t.GetComponent<DefenseObject>() ?? t.GetComponentInParent<DefenseObject>();
            return defense != null ? GetDefenseKind(defense) : TargetKind.Gate;
        }

        private static Collider PickTargetCollider(Transform t)
        {
            if (t.TryGetComponent<Collider>(out var self) && self.enabled && !self.isTrigger)
                return self;

            var all = t.GetComponentsInChildren<Collider>(includeInactive: false);
            foreach (var c in all)
            {
                if (c == null || !c.enabled) continue;
                if (c.isTrigger) continue;
                return c;
            }

            foreach (var c in all)
            {
                if (c != null && c.enabled) return c;
            }

            return null;
        }

        private static float ComputeSurfaceDistance(Vector3 from, Transform target, Collider col)
        {
            if (target == null) return float.MaxValue;

            float pivotDist = Vector3.Distance(from, target.position);
            if (col == null) return pivotDist;

            float surfaceDist = Vector3.Distance(from, col.ClosestPoint(from));
            float lowerBound = Mathf.Max(0f, pivotDist - col.bounds.extents.magnitude);
            return Mathf.Max(surfaceDist, lowerBound);
        }

        private float DistanceToTargetSurface()
        {
            if (Target == null) return float.MaxValue;
            return ComputeSurfaceDistance(transform.position, Target, targetCollider);
        }

        private bool TargetIsDefense() => currentTargetKind != TargetKind.Player;

        private bool IsAgentBlocked()
        {
            if (agent == null || !agent.isOnNavMesh) return false;
            if (agent.pathPending) return false;

            if (agent.pathStatus == NavMeshPathStatus.PathPartial ||
                agent.pathStatus == NavMeshPathStatus.PathInvalid)
                return true;

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
    }
}
