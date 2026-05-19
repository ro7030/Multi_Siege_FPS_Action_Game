using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectM.Core;
using ProjectM.Defense;

namespace ProjectM.Economy
{
    /// <summary>
    /// 밭 시스템 중앙 매니저.
    ///
    /// 책임
    /// - 활성 밭 추적 (최대 N개 제한)
    /// - 정비 시간(Preparation) 에만 설치 허용
    /// - 웨이브 종료 시: 모든 활성 밭에 yieldPerWave 누적 (FarmPlot.OnWavePassed)
    /// - 플레이어 F 키 수확 시: 해당 밭의 누적분을 팀 전체 지갑에 균등 분배
    /// - 파괴된 밭은 매니저에서 제거 (누적분도 0)
    /// </summary>
    public class FarmManager : MonoBehaviour
    {
        public static FarmManager Instance { get; private set; }

        [SerializeField] private FarmSettings settings;
        [SerializeField] private GameSessionManager session;

        [Header("프리팹/배치")]
        [SerializeField] private GameObject farmPrefab;

        [Header("상태 (읽기 전용)")]
        [SerializeField] private int activeFarmCount;

        private readonly List<FarmPlot> activeFarms = new();

        public int ActiveCount => activeFarms.Count;
        public int MaxFarms => settings != null ? settings.maxActiveFarms : 4;
        public bool CanPlaceMore => ActiveCount < MaxFarms;
        public FarmSettings Settings => settings;

        public event Action<FarmPlot> OnFarmPlaced;
        public event Action<FarmPlot, int> OnFarmHarvested; // (plot, 1인당 지급액)
        public event Action<FarmPlot> OnFarmDestroyed;

        // ─────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
        }

        private void OnEnable()
        {
            if (session != null) session.OnWaveEnded += HandleWaveEnded;
        }

        private void OnDisable()
        {
            if (session != null) session.OnWaveEnded -= HandleWaveEnded;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            activeFarmCount = activeFarms.Count;
        }

        // ─────────────────────────────────────────────────────────────
        // 설치
        // ─────────────────────────────────────────────────────────────

        public bool TryPlaceFarm(Vector3 position, Quaternion rotation, out FarmPlot placed)
        {
            placed = null;

            if (!IsPlacementAllowed())
            {
                Debug.LogWarning("[FarmManager] 설치 불가: 정비 시간이 아니거나 최대 개수 도달");
                return false;
            }

            if (farmPrefab == null)
            {
                Debug.LogError("[FarmManager] farmPrefab 이 설정되지 않음");
                return false;
            }

            var go = Instantiate(farmPrefab, position, rotation);
            var plot = go.GetComponent<FarmPlot>();
            if (plot == null)
            {
                Debug.LogError("[FarmManager] 프리팹에 FarmPlot 컴포넌트가 없음");
                Destroy(go);
                return false;
            }

            RegisterExistingFarm(plot);
            placed = plot;
            return true;
        }

        public void RegisterExistingFarm(FarmPlot plot)
        {
            if (plot == null || activeFarms.Contains(plot)) return;

            plot.InstalledOnWave = session != null ? session.State.CurrentWave : 0;
            plot.OnDestroyedByEnemy += HandleFarmDestroyed;
            activeFarms.Add(plot);
            OnFarmPlaced?.Invoke(plot);

            Debug.Log($"[FarmManager] 밭 설치됨 ({ActiveCount}/{MaxFarms})");
        }

        public bool IsPlacementAllowed()
        {
            if (!CanPlaceMore) return false;
            if (session == null) return true;
            return session.State.CurrentPhase == GamePhase.Preparation;
        }

        // ─────────────────────────────────────────────────────────────
        // 파괴
        // ─────────────────────────────────────────────────────────────

        private void HandleFarmDestroyed(FarmPlot plot)
        {
            if (plot == null) return;
            plot.OnDestroyedByEnemy -= HandleFarmDestroyed;
            activeFarms.Remove(plot);
            OnFarmDestroyed?.Invoke(plot);

            Debug.Log($"[FarmManager] 밭 파괴됨 — 누적 수익 손실 ({ActiveCount}/{MaxFarms})");
        }

        // ─────────────────────────────────────────────────────────────
        // 웨이브 종료 → 모든 밭에 누적
        // ─────────────────────────────────────────────────────────────

        private void HandleWaveEnded(int waveNumber)
        {
            // null 정리
            activeFarms.RemoveAll(f => f == null);

            int touched = 0;
            foreach (var plot in activeFarms)
            {
                if (plot == null) continue;
                plot.OnWavePassed();
                touched++;
            }

            if (touched > 0)
                Debug.Log($"[FarmManager] Wave {waveNumber} 종료 — {touched}개 밭에 수익 누적");
        }

        // ─────────────────────────────────────────────────────────────
        // 수확 (플레이어 F 키 → FarmPlot 이 호출)
        // ─────────────────────────────────────────────────────────────

        /// <summary>플레이어가 F 키로 수확 시도. FarmPlot 의 누적분을 팀 전원 지갑에 균등 분배.</summary>
        public void HarvestFarm(FarmPlot plot)
        {
            if (plot == null || !plot.HasYieldToHarvest) return;

            int yieldPerPlayer = plot.HarvestNow();
            if (yieldPerPlayer <= 0) return;

            var wallets = FindAllWallets();
            DistributeToTeam(wallets, yieldPerPlayer);

            OnFarmHarvested?.Invoke(plot, yieldPerPlayer);
            Debug.Log($"[FarmManager] 수확! +{yieldPerPlayer} × {wallets.Count}명");
        }

        private void DistributeToTeam(List<CurrencyWallet> wallets, int amountPerPlayer)
        {
            if (wallets == null || wallets.Count == 0) return;
            foreach (var w in wallets)
                if (w != null) w.Add(amountPerPlayer);
        }

        private List<CurrencyWallet> FindAllWallets()
        {
            var found = FindObjectsByType<CurrencyWallet>(FindObjectsSortMode.None);
            return new List<CurrencyWallet>(found);
        }
    }
}
