using System.Collections.Generic;
using ProjectM.Auth;
using UnityEngine;
using ProjectM.Core;
using ProjectM.Network;
using ProjectM.Defense;
using ProjectM.Enemy;
using ProjectM.Player;

namespace ProjectM.Data
{
    /// <summary>
    /// 로컬 플레이어의 인게임 기여도를 추적한다.
    /// NGO 세션에서는 NetworkMatchStats 스냅샷을 읽는 파사드, 오프라인에서는 로컬 이벤트 구독.
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

        public int FinalScore => Kills * 100 + HarvestCount * 10 + ReviveCount * 200;

        private float scanTimer;
        private readonly HashSet<HealthSystem> trackedEnemyHealth = new();
        private readonly HashSet<FarmPlot> trackedFarms = new();
        private readonly HashSet<ReviveSystem> trackedRevives = new();

        private bool UseNetworkFacade =>
            NetworkSessionHelper.IsMultiplayerSession && NetworkMatchStats.Instance != null;

        public void SetLocalNickname(string nickname) => localNickname = nickname;

        private void Awake()
        {
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
            if (localPlayer == null)
                localPlayer = ResolveLocalPlayerObject();
        }

        private void Start()
        {
            string nickname = AuthSessionManager.ResolveNickname(localNickname);
            if (!string.IsNullOrEmpty(nickname))
                localNickname = nickname;
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
            Debug.Log("[Stats] 카운터 리셋");
        }

        private void Update()
        {
            if (UseNetworkFacade)
            {
                RefreshFromNetworkStats();
                return;
            }

            scanTimer += Time.deltaTime;
            if (scanTimer >= 1.5f) { scanTimer = 0; RescanSubscriptions(); }
        }

        private void RefreshFromNetworkStats()
        {
            var netStats = NetworkMatchStats.Instance;
            if (netStats == null) return;

            var snap = netStats.GetLocalSnapshot();
            Kills = snap.Kills;
            HarvestCount = snap.HarvestCount;
            ReviveCount = snap.ReviveCount;
            DamageDealt = snap.DamageDealt;

            if (!snap.Nickname.IsEmpty)
                localNickname = snap.Nickname.ToString();
        }

        private static GameObject ResolveLocalPlayerObject()
        {
            var netLocal = NetworkPlayerRegistry.LocalPlayer;
            if (netLocal != null) return netLocal.gameObject;

            foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (pc.IsLocalPlayer) return pc.gameObject;
            }

            var tagged = GameObject.FindGameObjectWithTag("Player");
            return tagged;
        }

        private void RescanSubscriptions()
        {
            if (localPlayer == null)
                localPlayer = ResolveLocalPlayerObject();
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
            // 부활
            foreach (var r in FindObjectsByType<ReviveSystem>(FindObjectsSortMode.None))
            {
                if (r.gameObject == localPlayer) continue; // 본인 부활은 카운트 안 함
                if (trackedRevives.Add(r)) r.OnRevived += HandleAllyRevived;
            }

            // 파괴된 객체 정리
            trackedEnemyHealth.RemoveWhere(h => h == null);
            trackedFarms.RemoveWhere(f => f == null);
            trackedRevives.RemoveWhere(r => r == null);
        }

        private void UnsubscribeAll()
        {
            foreach (var h in trackedEnemyHealth) if (h != null) { h.OnDamaged -= HandleEnemyDamaged; h.OnDied -= HandleEnemyDied; }
            foreach (var f in trackedFarms) if (f != null) f.OnHarvested -= HandleFarmHarvested;
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

        private void HandleAllyRevived() => ReviveCount++;

        private bool IsLocalAttacker(GameObject attacker)
        {
            if (attacker == null || localPlayer == null) return false;
            return attacker == localPlayer || attacker.transform.IsChildOf(localPlayer.transform);
        }
    }
}
