using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectM.Defense
{
    /// <summary>
    /// 농작물 밭. 기획서 7-3 + 10-2 + 사용자 보정 기준.
    /// - 적 공격 대상 (DefenseObject + HealthSystem)
    /// - 매 웨이브 종료 시 yieldPerWave 만큼 1인당 수익이 누적됨
    /// - 플레이어가 밭 근처에서 F 키 → 누적분을 팀 전체 분배 (FarmManager.HarvestFarm 호출)
    /// - 파괴되면 누적분은 0 으로 손실
    /// </summary>
    [RequireComponent(typeof(DefenseObject))]
    public class FarmPlot : MonoBehaviour
    {
        public enum FarmState { Active, Destroyed }

        [Header("수확량 (Inspector 조절)")]
        [Tooltip("웨이브 1회 통과 시 1인당 누적되는 재화량")]
        [SerializeField] private int yieldPerWave = 25;

        [Header("상호작용")]
        [Tooltip("플레이어가 이 거리 안에서 F 키를 누르면 수확")]
        [SerializeField] private float interactRange = 2.5f;
        [SerializeField] private string playerTag = "Player";

        [Header("외형 (선택)")]
        [Tooltip("[0]=비어있음, [1]=수확물 있음 등 자유 배치. AccumulatedYield 가 0보다 크면 마지막 인덱스로 전환")]
        [SerializeField] private GameObject[] stageVisuals;

        public FarmState State { get; private set; } = FarmState.Active;
        public int YieldPerWave => yieldPerWave;
        public int AccumulatedYield { get; private set; }
        public bool HasYieldToHarvest => AccumulatedYield > 0 && State == FarmState.Active;

        /// <summary>설치된 웨이브 번호 (FarmManager 가 설정).</summary>
        public int InstalledOnWave { get; set; }

        public event Action<FarmPlot, int> OnYieldAdded;     // (plot, addedAmount)
        public event Action<FarmPlot, int> OnHarvested;      // (plot, totalHarvested)
        public event Action<FarmPlot> OnDestroyedByEnemy;

        private DefenseObject defense;
        private Transform playerCache;

        // ─────────────────────────────────────────────────────────────
        private void Awake()
        {
            defense = GetComponent<DefenseObject>();
            ApplyVisual();
        }

        private void OnEnable()
        {
            if (defense != null) defense.OnDestroyed += HandleDestroyed;
        }

        private void OnDisable()
        {
            if (defense != null) defense.OnDestroyed -= HandleDestroyed;
        }

        private void HandleDestroyed(DefenseObject _)
        {
            State = FarmState.Destroyed;
            AccumulatedYield = 0;  // 파괴 시 누적분 손실
            ApplyVisual();
            OnDestroyedByEnemy?.Invoke(this);
        }

        private void Update()
        {
            if (State != FarmState.Active) return;
            if (!HasYieldToHarvest) return;
            if (!IsPlayerNear()) return;
            if (!IsHarvestKeyPressed()) return;

            // FarmManager 에 위임 (실제 지급은 매니저가 팀 분배)
            Economy.FarmManager.Instance?.HarvestFarm(this);
        }

        // ─────────────────────────────────────────────────────────────
        // FarmManager 가 호출
        // ─────────────────────────────────────────────────────────────

        /// <summary>웨이브가 1회 지났을 때 호출. 누적 수익 증가.</summary>
        public void OnWavePassed()
        {
            if (State != FarmState.Active) return;
            AccumulatedYield += yieldPerWave;
            OnYieldAdded?.Invoke(this, yieldPerWave);
            ApplyVisual();
        }

        /// <summary>수확 실행. 누적분 반환 후 0 으로 초기화. 실제 지갑 지급은 FarmManager 가 수행.</summary>
        public int HarvestNow()
        {
            int amount = AccumulatedYield;
            AccumulatedYield = 0;
            OnHarvested?.Invoke(this, amount);
            ApplyVisual();
            return amount;
        }

        // ─────────────────────────────────────────────────────────────
        // 내부 헬퍼
        // ─────────────────────────────────────────────────────────────

        private bool IsPlayerNear()
        {
            if (playerCache == null)
            {
                try
                {
                    var go = GameObject.FindGameObjectWithTag(playerTag);
                    if (go != null) playerCache = go.transform;
                }
                catch (UnityException) { /* 태그 미정의 */ }
            }
            if (playerCache == null) return false;
            return Vector3.Distance(playerCache.position, transform.position) <= interactRange;
        }

        private bool IsHarvestKeyPressed()
        {
            var kb = Keyboard.current;
            if (kb != null) return kb.fKey.wasPressedThisFrame;
            return Input.GetKeyDown(KeyCode.F);
        }

        private void ApplyVisual()
        {
            if (stageVisuals == null || stageVisuals.Length == 0) return;

            int idx;
            if (State == FarmState.Destroyed)         idx = 0;
            else if (AccumulatedYield > 0)            idx = stageVisuals.Length - 1;
            else                                       idx = 0;

            for (int i = 0; i < stageVisuals.Length; i++)
                if (stageVisuals[i] != null) stageVisuals[i].SetActive(i == idx);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactRange);
        }
    }
}
