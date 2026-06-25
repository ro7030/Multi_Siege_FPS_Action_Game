using System;
using UnityEngine;
using ProjectM.Network;
using ProjectM.Player;

namespace ProjectM.Defense
{
    /// <summary>
    /// 농작물 밭. 기획서 7-3 + 10-2 + 사용자 보정 기준.
    /// - 적 공격 대상 (DefenseObject + HealthSystem)
    /// - 매 웨이브 종료 시 yieldPerWave 만큼 1인당 수익이 누적됨
    /// - 플레이어가 밭 근처에서 F 키(PlayerInteractor) → 누적분을 팀 전체 분배 (FarmManager.HarvestFarm)
    /// - 파괴되면 누적분은 0 으로 손실
    /// </summary>
    [RequireComponent(typeof(DefenseObject))]
    public class FarmPlot : MonoBehaviour, IInteractable
    {
        public enum FarmState { Active, Destroyed }

        [Header("수확량 (Inspector 조절)")]
        [Tooltip("웨이브 1회 통과 시 1인당 누적되는 재화량")]
        [SerializeField] private int yieldPerWave = 25;

        [Header("상호작용 프롬프트")]
        [Tooltip("F 프롬프트에 표시할 메시지")]
        [SerializeField] private string promptText = "수확";
        [SerializeField] private Sprite promptIcon;
        [SerializeField] private Transform promptAnchor; // 비우면 자기 위치
        [Tooltip("F키를 누르고 있어야 하는 시간(초). 완료되면 수확된다.")]
        [SerializeField] private float holdDuration = 1f;

        private float holdProgress;

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

        private void Start()
        {
            // 씬에 미리 놓인 밭(인스펙터 배치)도 FarmManager 에 자동 등록.
            // MP 서버는 PlaceFarmInternal/RegisterExistingFarm 으로 이미 등록됨.
            if (State == FarmState.Active && Economy.FarmManager.Instance != null)
            {
                if (NetworkSessionHelper.IsMultiplayerSession && NetworkSessionHelper.IsServer)
                    return;

                Economy.FarmManager.Instance.RegisterExistingFarm(this);
            }
        }

        private void OnDisable()
        {
            if (defense != null) defense.OnDestroyed -= HandleDestroyed;
        }

        private void HandleDestroyed(DefenseObject _)
        {
            State = FarmState.Destroyed;
            AccumulatedYield = 0;
            ApplyVisual();
            ApplyDestroyedPresentation();
            OnDestroyedByEnemy?.Invoke(this);

            if (TryGetComponent<NetworkFarmBridge>(out var bridge) && NetworkSessionHelper.IsServer)
                bridge.ServerSyncFromPlot();
        }

        // ─────────────────────────────────────────────────────────────
        // IInteractable (F키 수확)
        // ─────────────────────────────────────────────────────────────
        public bool CanInteract(GameObject interactor) => HasYieldToHarvest;
        public string PromptText => $"{promptText} (+{AccumulatedYield})";
        public Sprite PromptIcon => promptIcon;
        public bool IsHold => true;
        public float HoldProgress01 => holdDuration > 0f ? Mathf.Clamp01(holdProgress / holdDuration) : 0f;
        public Transform PromptAnchor => promptAnchor != null ? promptAnchor : transform;

        public void Interact(GameObject interactor) { } // 홀드형이므로 단발 입력은 사용 안 함

        public void InteractHold(GameObject interactor, float deltaTime)
        {
            if (!HasYieldToHarvest) { holdProgress = 0f; return; }
            holdProgress += deltaTime;
            if (holdProgress < holdDuration) return;

            // 완료 — FarmManager 에 위임 (실제 지급은 매니저가 팀 분배)
            holdProgress = 0f;
            if (TryGetComponent<NetworkFarmBridge>(out var bridge))
                bridge.RequestHarvest();
            else
                Economy.FarmManager.Instance?.HarvestFarm(this);
        }

        public void InteractHoldCancel() { holdProgress = 0f; }

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

            if (TryGetComponent<NetworkFarmBridge>(out var bridge) && NetworkSessionHelper.IsServer)
                bridge.ServerSyncFromPlot();
        }

        /// <summary>수확 실행. 누적분 반환 후 0 으로 초기화. 실제 지갑 지급은 FarmManager 가 수행.</summary>
        public int HarvestNow()
        {
            int amount = AccumulatedYield;
            AccumulatedYield = 0;
            OnHarvested?.Invoke(this, amount);
            ApplyVisual();

            if (TryGetComponent<NetworkFarmBridge>(out var bridge) && NetworkSessionHelper.IsServer)
                bridge.ServerSyncFromPlot();

            return amount;
        }

        /// <summary>클라이언트 NGO 미러용. 서버 로직은 변경하지 않는다.</summary>
        internal void ApplyNetworkMirror(int accumulatedYield, FarmState state)
        {
            if (NetworkSessionHelper.IsGameplayAuthority)
                return;

            bool wasActive = State == FarmState.Active;
            AccumulatedYield = accumulatedYield;
            State = state;
            ApplyVisual();

            if (state == FarmState.Destroyed)
            {
                ApplyDestroyedPresentation();
                if (wasActive)
                    Economy.FarmManager.Instance?.NotifyFarmDestroyedFromMirror(this);
            }
        }

        /// <summary>파괴 시 비주얼·콜라이더 비활성화 (서버/클라이언트 공통).</summary>
        internal void ApplyDestroyedPresentation()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;

            foreach (var collider in GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
        }

        // ─────────────────────────────────────────────────────────────
        // 내부 헬퍼
        // ─────────────────────────────────────────────────────────────

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
    }
}
