using System.Collections.Generic;
using UnityEngine;
using ProjectM.Core;
using ProjectM.Defense;
using ProjectM.Enemy;
using ProjectM.Player;

namespace ProjectM.Data
{
    /// <summary>
    /// 로컬 플레이어의 인게임 기여도를 누적 추적한다.
    /// 매치 시작 시 0으로 리셋, 종료 시 ResultUploader가 DTO로 변환.
    /// </summary>
    public class PlayerStatsTracker : MonoBehaviour
    {
        [SerializeField] private GameObject localPlayer;
        [SerializeField] private GameSessionManager session;
        [SerializeField] private string localNickname = "Player1";

        public int Kills { get; private set; }
        public int HarvestCount { get; private set; }
        public int RepairCount { get; private set; }
        public int ReviveCount { get; private set; }
        public float DamageDealt { get; private set; }
        public string LocalNickname => localNickname;

        public int FinalScore => Kills * 100 + HarvestCount * 10 + RepairCount * 20 + ReviveCount * 200;

        private float scanTimer;
        private readonly HashSet<HealthSystem> trackedEnemyHealth = new();
        private readonly HashSet<FarmPlot> trackedFarms = new();
        private readonly HashSet<DefenseObject> trackedDefenses = new();
        private readonly HashSet<ReviveSystem> trackedRevives = new();

        // 수리는 OnRepaired가 매 프레임 호출되므로 시작/끝을 구분해 1회로 카운트.
        private readonly Dictionary<DefenseObject, float> lastRepairTime = new();
        [SerializeField] private float repairGroupTimeout = 1.0f;

        public void SetLocalNickname(string nickname) => localNickname = nickname;

        private void Awake()
        {
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
            if (localPlayer == null)
            {
                var pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) localPlayer = pc.gameObject;
            }
        }

        private void OnEnable()
        {
            if (session != null) session.OnMatchStarted += ResetAll;
        }

        private void OnDisable()
        {
            if (session != null) session.OnMatchStarted -= ResetAll;
            UnsubscribeAll();
        }

        public void ResetAll()
        {
            Kills = 0; HarvestCount = 0; RepairCount = 0; ReviveCount = 0; DamageDealt = 0;
            lastRepairTime.Clear();
            Debug.Log("[Stats] 카운터 리셋");
        }

        private void Update()
        {
            scanTimer += Time.deltaTime;
            if (scanTimer >= 1.5f) { scanTimer = 0; RescanSubscriptions(); }
        }

        private void RescanSubscriptions()
        {
            // 적
            foreach (var ai in FindObjectsByType<EnemyAIController>(FindObjectsSortMode.None))
            {
                var hp = ai.GetComponent<HealthSystem>();
                if (hp != null && trackedEnemyHealth.Add(hp))
                {
                    hp.OnDamaged += HandleEnemyDamaged;
                    hp.OnDied += HandleEnemyDied;
                }
            }
            // 농장
            foreach (var f in FindObjectsByType<FarmPlot>(FindObjectsSortMode.None))
            {
                if (trackedFarms.Add(f)) f.OnHarvested += HandleFarmHarvested;
            }
            // 방어물
            foreach (var d in FindObjectsByType<DefenseObject>(FindObjectsSortMode.None))
            {
                if (trackedDefenses.Add(d)) d.OnRepaired += HandleDefenseRepaired;
            }
            // 부활
            foreach (var r in FindObjectsByType<ReviveSystem>(FindObjectsSortMode.None))
            {
                if (r.gameObject == localPlayer) continue; // 본인 부활은 카운트 안 함
                if (trackedRevives.Add(r)) r.OnRevived += HandleAllyRevived;
            }

            // 파괴된 객체 정리
            trackedEnemyHealth.RemoveWhere(h => h == null);
            trackedFarms.RemoveWhere(f => f == null);
            trackedDefenses.RemoveWhere(d => d == null);
            trackedRevives.RemoveWhere(r => r == null);
        }

        private void UnsubscribeAll()
        {
            foreach (var h in trackedEnemyHealth) if (h != null) { h.OnDamaged -= HandleEnemyDamaged; h.OnDied -= HandleEnemyDied; }
            foreach (var f in trackedFarms) if (f != null) f.OnHarvested -= HandleFarmHarvested;
            foreach (var d in trackedDefenses) if (d != null) d.OnRepaired -= HandleDefenseRepaired;
            foreach (var r in trackedRevives) if (r != null) r.OnRevived -= HandleAllyRevived;
        }

        // ── 이벤트 핸들러 ──────────────────────────────────────────
        private void HandleEnemyDamaged(float amount, GameObject attacker)
        {
            if (!IsLocalAttacker(attacker)) return;
            DamageDealt += amount;
        }

        private void HandleEnemyDied(GameObject attacker)
        {
            if (!IsLocalAttacker(attacker)) return;
            Kills++;
        }

        private void HandleFarmHarvested(FarmPlot _, int __) => HarvestCount++;

        private void HandleDefenseRepaired(DefenseObject d, float _)
        {
            float now = Time.time;
            if (!lastRepairTime.TryGetValue(d, out float last) || now - last > repairGroupTimeout)
            {
                RepairCount++;
            }
            lastRepairTime[d] = now;
        }

        private void HandleAllyRevived() => ReviveCount++;

        private bool IsLocalAttacker(GameObject attacker)
        {
            if (attacker == null || localPlayer == null) return false;
            return attacker == localPlayer || attacker.transform.IsChildOf(localPlayer.transform);
        }
    }
}
