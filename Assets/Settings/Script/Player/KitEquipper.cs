using System;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectM.Defense;
using ProjectM.Economy;
using ProjectM.Network;
using ProjectM.UI;

namespace ProjectM.Player
{
    // 플레이어 키트 장착/사용 시스템 (배그식 탭 사이클).

    // 동작
    // - 3번키 "한 번 누름": 보유 중인 키트를 cycleOrder 순서대로 한 칸씩 순환 장착
    // · 미장착 상태에서 누르면 cycleOrder 의 첫 번째 보유 키트를 장착
    // · 이미 장착된 상태에서 누르면 다음 보유 키트로 교체 (한 종류만 있으면 그대로 유지)
    // · 보유 키트가 전혀 없으면 변경 없음
    // - 키트 장착 후 좌클릭: 장착한 키트 사용
    // · HealKit: 자신을 회복 / RepairKit: 시선 끝 방어물 수리 / FarmKit: 시선 끝 지면에 밭 설치
    // - 키트가 장착된 동안 WeaponController 가 사격을 억제
    // - 1/2 번 키로 무기 전환 시 PlayerArsenal 이 Holster() 를 호출하여 키트를 내려놓음
    [RequireComponent(typeof(KitInventory))]
    public class KitEquipper : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private KitInventory inventory;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private HealthSystem playerHealth;

        [Header("사용 거리/판정")]
        [SerializeField] private float useRange = 8f;
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("효과 수치 (Inspector 조절)")]
        [Tooltip("힐킷 단계 진행표가 비어있을 때 사용되는 폴백 값.")]
        [SerializeField] private float healAmount = 50f;
        [SerializeField] private float repairAmount = 50f;

        [Header("힐킷 티어")]
        [Tooltip("힐킷 단계 진행표. 비우면 위 healAmount/heldViewModels 매핑이 폴백으로 사용된다.")]
        [SerializeField] private HealKitProgression healProgression;
        [SerializeField] private int currentHealTier = 0;
        public int CurrentHealTier => currentHealTier;
        public HealKitProgression HealProgression => healProgression;
        public HealKitDefinition CurrentHealDef => healProgression != null ? healProgression.GetTier(currentHealTier) : null;
        public event Action<int> OnHealTierChanged;

        [Header("탭 사이클")]
        [Tooltip("3번키를 누를 때마다 이 순서대로 보유 키트를 순환 장착합니다. (Inspector 에서 자유롭게 재정렬)")]
        [SerializeField] private KitType[] cycleOrder = new KitType[]
        {
            KitType.HealKit,
            KitType.RepairKit,
            KitType.FarmKit,
        };
        [SerializeField] private Key cycleKey = Key.Digit3;

        [Header("로컬 권한")]
        [SerializeField] private bool isLocalPlayer = true;

        public bool IsLocalPlayer { get => isLocalPlayer; set => isLocalPlayer = value; }

        [Header("연동")]
        [SerializeField] private ThrowableEquipper throwableEquipper; // 키트 장착 시 투척 내려놓기

        [Header("비주얼 (1인칭 뷰모델)")]
        [Tooltip("카메라 자식의 빈 GameObject. 장착한 키트 모델이 이 곳의 자식으로 인스턴스화된다.")]
        [SerializeField] private Transform viewModelSocket;
        [Tooltip("KitType 별 들고 있는 모델 프리팹 매핑. None 은 무시.")]
        [SerializeField] private KitHeldVisual[] heldViewModels;
        private GameObject viewModelInstance;
        private KitType viewModelType = KitType.None;
        private PlayerAttachedWeaponVisual attachedVisual;

        [Serializable]
        public struct KitHeldVisual
        {
            public KitType type;
            public GameObject prefab;
        }

        // ── 장착 상태 ──
        public KitType EquippedKit { get; private set; } = KitType.None;
        public bool IsKitEquipped => EquippedKit != KitType.None;
        // 최근에 장착했던 키트 종류. 무기로 전환해도 유지되어 HUD 슬롯3 아이콘 복귀에 사용.
        public KitType LastSelected { get; private set; } = KitType.FarmKit;

        // ── (구) 휠 호환용 — 항상 비활성. 기존 KitWheelView 가 컴파일/실행은 되지만 표시되지 않음. ──
        public bool IsSelecting => false;
        public KitType HighlightedKit => KitType.None;
        public Vector2 SelectionDirection => Vector2.zero;
        public float SelectionDeadzone => 0f;
        public float SelectionMaxRadius => 1f;

        public event Action<KitType> OnEquippedChanged;
        public event Action<KitType, bool> OnKitUseAttempt;

        private PlayerCombatInputGate combatInputGate;

        private void Awake()
        {
            if (inventory == null) inventory = GetComponent<KitInventory>();
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
            if (playerHealth == null) playerHealth = GetComponent<HealthSystem>();
            if (throwableEquipper == null) throwableEquipper = GetComponent<ThrowableEquipper>();
            if (combatInputGate == null) combatInputGate = GetComponent<PlayerCombatInputGate>();
            attachedVisual = GetComponent<PlayerAttachedWeaponVisual>();
        }

        private void OnEnable()
        {
            if (inventory != null) inventory.OnCountChanged += HandleInventoryChanged;
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.OnCountChanged -= HandleInventoryChanged;
        }

        private void HandleInventoryChanged(KitType type, int newCount)
        {
            // 사용 중인 키트가 0이 되면 자동 해제
            if (EquippedKit == type && newCount <= 0)
                SetEquipped(KitType.None);

            if (newCount <= 0 && LastSelected == type)
                UpdateLastSelectedToOwnedKit();
        }

        private void UpdateLastSelectedToOwnedKit()
        {
            if (inventory == null || cycleOrder == null) return;

            foreach (var kit in cycleOrder)
            {
                if (kit == KitType.None) continue;
                if (!inventory.Has(kit)) continue;
                LastSelected = kit;
                return;
            }

            LastSelected = KitType.None;
        }

        // ─────────────────────────────────────────────────────────────
        private void Update()
        {
            if (!isLocalPlayer) return;

            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return;

            // 3번 키 한 번 누름: 보유 키트 사이클
            if (kb[cycleKey].wasPressedThisFrame)
                CycleEquipped();

            // 일반 키트 사용 (키트 장착 상태)
            if (!IsKitEquipped) return;
            if (mouse == null) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;

            if (mouse.leftButton.wasPressedThisFrame)
                UseEquippedKit();
        }

        // cycleOrder 에 정의된 순서대로, 현재 장착 키트 다음 칸부터 한 바퀴 돌며
        // 보유 중(인벤토리 ≥ 1)인 첫 키트를 장착한다.
        // 보유 키트가 하나도 없으면 변경 없음.
        public void CycleEquipped()
        {
            if (inventory == null || cycleOrder == null || cycleOrder.Length == 0)
            {
                Debug.Log("[Kit] 사이클 순서가 비어 있습니다.");
                return;
            }

            int startIdx = -1;
            for (int i = 0; i < cycleOrder.Length; i++)
                if (cycleOrder[i] == EquippedKit) { startIdx = i; break; }

            for (int step = 1; step <= cycleOrder.Length; step++)
            {
                int idx = ((startIdx + step) % cycleOrder.Length + cycleOrder.Length) % cycleOrder.Length;
                var next = cycleOrder[idx];
                if (next == KitType.None) continue;
                if (inventory.Has(next))
                {
                    LastSelected = next;
                    SetEquipped(next);
                    return;
                }
            }

            Debug.Log("[Kit] 보유한 키트가 없습니다.");
        }

        private void SetEquipped(KitType type)
        {
            if (EquippedKit == type) return;
            bool wasEquipped = EquippedKit != KitType.None;
            EquippedKit = type;
            if (type != KitType.None) throwableEquipper?.Holster(); // 투척과 배타
            SwapHeldViewModel(type);
            OnEquippedChanged?.Invoke(type);
            if (wasEquipped && type == KitType.None)
                combatInputGate?.Suppress();
            Debug.Log($"[Kit] 장착: {type}");
        }

        private void SwapHeldViewModel(KitType type)
        {
            if (attachedVisual != null && attachedVisual.UseAttachedWeapons)
            {
                if (viewModelInstance != null) Destroy(viewModelInstance);
                viewModelInstance = null;
                viewModelType = type;
                attachedVisual.RefreshPresentation();
                return;
            }

            if (viewModelType == type && viewModelInstance != null) return;

            if (viewModelInstance != null) Destroy(viewModelInstance);
            viewModelInstance = null;
            viewModelType = KitType.None;

            if (type == KitType.None || viewModelSocket == null) return;

            var prefab = GetHeldPrefab(type);
            if (prefab == null) return;

            viewModelInstance = Instantiate(prefab, viewModelSocket);
            viewModelInstance.transform.localPosition = Vector3.zero;
            viewModelInstance.transform.localRotation = Quaternion.identity;
            viewModelType = type;
        }

        public GameObject GetHeldViewModelPrefab(KitType type) => GetHeldPrefab(type);

        private GameObject GetHeldPrefab(KitType type)
        {
            // HealKit 은 현재 티어 정의의 prefab 을 우선 사용
            if (type == KitType.HealKit && CurrentHealDef != null && CurrentHealDef.heldViewModelPrefab != null)
                return CurrentHealDef.heldViewModelPrefab;

            if (heldViewModels == null) return null;
            for (int i = 0; i < heldViewModels.Length; i++)
                if (heldViewModels[i].type == type) return heldViewModels[i].prefab;
            return null;
        }

        // 키트를 내려놓는다(무기로 복귀). 무기 전환 시 PlayerArsenal 이 호출.
        public void Holster() => SetEquipped(KitType.None);

        // ── 힐킷 업그레이드 (PlayerArsenal.TryUpgrade 와 동일 패턴) ──────
        public HealKitDefinition NextHealTier()
            => healProgression != null ? healProgression.GetTier(currentHealTier + 1) : null;

        public bool CanUpgradeHealKit() => NextHealTier() != null;

        public int NextHealUpgradePrice()
        {
            var d = NextHealTier();
            return d != null ? d.price : 0;
        }

        // 다음 단계로 업그레이드(화폐 차감은 호출자 책임).
        public bool TryUpgradeHealKit()
        {
            if (!CanUpgradeHealKit()) return false;
            return TrySetHealTier(currentHealTier + 1);
        }

        // 지정 단계로 바로 설정(상점에서 임의 티어 구매).
        public bool TrySetHealTier(int tierIndex)
        {
            if (healProgression == null) return false;
            if (healProgression.GetTier(tierIndex) == null) return false;

            if (TryGetComponent<NetworkKitInventory>(out var netKit)
                && NetworkSessionHelper.IsMultiplayerSession
                && netKit.IsSpawned
                && NetworkSessionHelper.IsServer)
            {
                return netKit.ServerSetHealTier(tierIndex);
            }

            return ApplyNetworkHealTier(tierIndex);
        }

        // 네트워크 미러 또는 오프라인 로컬 적용.
        public bool ApplyNetworkHealTier(int tierIndex)
        {
            if (healProgression == null || healProgression.GetTier(tierIndex) == null)
                return false;

            currentHealTier = tierIndex;
            OnHealTierChanged?.Invoke(currentHealTier);

            if (EquippedKit == KitType.HealKit)
            {
                viewModelType = KitType.None;
                SwapHeldViewModel(KitType.HealKit);
            }

            Debug.Log($"[Kit] HealKit → 티어 {tierIndex} ({CurrentHealDef?.displayName})");
            return true;
        }

        public int GetHealTierPrice(int tierIndex)
        {
            var def = healProgression != null ? healProgression.GetTier(tierIndex) : null;
            return def != null ? def.price : 0;
        }

        // 아직 보유하지 않은 티어만 구매 가능 (0티어는 기본 지급).
        public bool CanPurchaseHealTier(int tierIndex)
        {
            if (healProgression == null || tierIndex <= 0) return false;
            if (tierIndex <= currentHealTier) return false;
            return healProgression.GetTier(tierIndex) != null;
        }

        // ─────────────────────────────────────────────────────────────
        // 사용
        // ─────────────────────────────────────────────────────────────

        public void UseEquippedKit()
        {
            bool ok = EquippedKit switch
            {
                KitType.HealKit   => UseHealKit(),
                KitType.RepairKit => UseRepairKit(),
                KitType.FarmKit   => UseFarmKit(),
                _ => false
            };
            if (ok)
                combatInputGate?.Suppress();
            OnKitUseAttempt?.Invoke(EquippedKit, ok);
        }

        private bool UseHealKit()
        {
            if (playerHealth == null || !playerHealth.IsAlive) return false;
            float amount = CurrentHealDef != null ? CurrentHealDef.healAmount : healAmount;

            if (NetworkSessionHelper.IsMultiplayerSession && !NetworkSessionHelper.IsServer)
            {
                if (TryGetComponent<NetworkKitInventory>(out var netKit) && netKit.IsSpawned)
                {
                    netKit.RequestUseHealKitFromOwner(amount);
                    Debug.Log($"[Kit] HealKit 사용 요청 (+{amount} HP, tier {currentHealTier})");
                    return true;
                }

                return false;
            }

            if (!inventory.TryConsume(KitType.HealKit)) return false;
            playerHealth.Heal(amount);
            Debug.Log($"[Kit] HealKit 사용 (+{amount} HP, tier {currentHealTier})");
            return true;
        }

        private bool UseRepairKit()
        {
            // RepairKit 은 게이트 설치 전용. 좌클릭 사용(방어물 즉시 수리)은 비활성화됨.
            // 게이트 슬롯 앞에서 F 키를 길게 누르면 KitInventory 에서 직접 소모된다.
            Debug.Log("[Kit] RepairKit 은 게이트 슬롯 앞에서 F 키로만 사용 가능합니다.");
            return false;
        }

        private bool UseFarmKit()
        {
            if (FarmManager.Instance == null) { Debug.LogWarning("[Kit] FarmKit: FarmManager 없음"); return false; }
            if (!FarmManager.Instance.IsPlacementAllowed())
            {
                string msg = FarmManager.Instance.GetPlacementBlockMessage();
                Debug.LogWarning($"[Kit] FarmKit: {msg}");
                NotificationBanner.Instance?.Show(msg, 2.5f);
                return false;
            }
            if (!TryRaycastFromView(out RaycastHit hit)) return false;

            Vector3 placePos = hit.point;
            float rotY = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up)).eulerAngles.y;

            if (NetworkSessionHelper.IsMultiplayerSession && !NetworkSessionHelper.IsServer)
            {
                var bridge = UnityEngine.Object.FindAnyObjectByType<NetworkFarmManagerBridge>();
                if (bridge == null)
                {
                    Debug.LogWarning("[Kit] FarmKit: NetworkFarmManagerBridge 없음");
                    return false;
                }

                bridge.RequestPlaceFarmServerRpc(placePos, rotY);
                Debug.Log($"[Kit] FarmKit 설치 요청 @ {placePos}");
                return true;
            }

            if (!inventory.TryConsume(KitType.FarmKit)) return false;

            if (FarmManager.Instance.TryPlaceFarm(placePos, Quaternion.Euler(0f, rotY, 0f), out _))
            {
                Debug.Log($"[Kit] FarmKit 사용 — 밭 설치 @ {placePos}");
                return true;
            }
            inventory.Add(KitType.FarmKit, 1);
            return false;
        }

        private bool TryRaycastFromView(out RaycastHit hit)
        {
            hit = default;
            if (viewCamera == null) return false;
            var ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            return PlayerRaycastUtility.TryRaycastFromView(transform, ray, out hit, useRange, hitMask);
        }
    }
}
