using System;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectM.Defense;
using ProjectM.Economy;

namespace ProjectM.Player
{
    /// <summary>
    /// 플레이어 키트 장착/사용 시스템 (배그식 탭 사이클).
    ///
    /// 동작
    ///   - 3번키 "한 번 누름": 보유 중인 키트를 cycleOrder 순서대로 한 칸씩 순환 장착
    ///       · 미장착 상태에서 누르면 cycleOrder 의 첫 번째 보유 키트를 장착
    ///       · 이미 장착된 상태에서 누르면 다음 보유 키트로 교체 (한 종류만 있으면 그대로 유지)
    ///       · 보유 키트가 전혀 없으면 변경 없음
    ///   - 키트 장착 후 좌클릭: 장착한 키트 사용
    ///       · HealKit: 자신을 회복 / RepairKit: 시선 끝 방어물 수리 / FarmKit: 시선 끝 지면에 밭 설치
    ///   - 키트가 장착된 동안 WeaponController 가 사격을 억제
    ///   - 1/2 번 키로 무기 전환 시 PlayerArsenal 이 Holster() 를 호출하여 키트를 내려놓음
    /// </summary>
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
        [SerializeField] private float healAmount = 50f;
        [SerializeField] private float repairAmount = 50f;

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

        [Header("연동")]
        [SerializeField] private ThrowableEquipper throwableEquipper; // 키트 장착 시 투척 내려놓기

        // ── 장착 상태 ──
        public KitType EquippedKit { get; private set; } = KitType.None;
        public bool IsKitEquipped => EquippedKit != KitType.None;

        // ── (구) 휠 호환용 — 항상 비활성. 기존 KitWheelView 가 컴파일/실행은 되지만 표시되지 않음. ──
        public bool IsSelecting => false;
        public KitType HighlightedKit => KitType.None;
        public Vector2 SelectionDirection => Vector2.zero;
        public float SelectionDeadzone => 0f;
        public float SelectionMaxRadius => 1f;

        public event Action<KitType> OnEquippedChanged;
        public event Action<KitType, bool> OnKitUseAttempt;

        private void Awake()
        {
            if (inventory == null) inventory = GetComponent<KitInventory>();
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
            if (playerHealth == null) playerHealth = GetComponent<HealthSystem>();
            if (throwableEquipper == null) throwableEquipper = GetComponent<ThrowableEquipper>();
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

        /// <summary>
        /// cycleOrder 에 정의된 순서대로, 현재 장착 키트 다음 칸부터 한 바퀴 돌며
        /// 보유 중(인벤토리 ≥ 1)인 첫 키트를 장착한다.
        /// 보유 키트가 하나도 없으면 변경 없음.
        /// </summary>
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
                    SetEquipped(next);
                    return;
                }
            }

            Debug.Log("[Kit] 보유한 키트가 없습니다.");
        }

        private void SetEquipped(KitType type)
        {
            if (EquippedKit == type) return;
            EquippedKit = type;
            if (type != KitType.None) throwableEquipper?.Holster(); // 투척과 배타
            OnEquippedChanged?.Invoke(type);
            Debug.Log($"[Kit] 장착: {type}");
        }

        /// <summary>키트를 내려놓는다(무기로 복귀). 무기 전환 시 PlayerArsenal 이 호출.</summary>
        public void Holster() => SetEquipped(KitType.None);

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
            OnKitUseAttempt?.Invoke(EquippedKit, ok);
        }

        private bool UseHealKit()
        {
            if (playerHealth == null || !playerHealth.IsAlive) return false;
            if (!inventory.TryConsume(KitType.HealKit)) return false;
            playerHealth.Heal(healAmount);
            Debug.Log($"[Kit] HealKit 사용 (+{healAmount} HP)");
            return true;
        }

        private bool UseRepairKit()
        {
            if (!TryRaycastFromView(out RaycastHit hit)) return false;
            var defense = hit.collider.GetComponentInParent<DefenseObject>();
            if (defense == null) { Debug.Log("[Kit] RepairKit: 방어물이 없음"); return false; }

            if (!inventory.TryConsume(KitType.RepairKit)) return false;
            defense.RepairInstant(repairAmount);
            Debug.Log($"[Kit] RepairKit 사용 (+{repairAmount} → {defense.DisplayName})");
            return true;
        }

        private bool UseFarmKit()
        {
            if (FarmManager.Instance == null) { Debug.LogWarning("[Kit] FarmKit: FarmManager 없음"); return false; }
            if (!FarmManager.Instance.IsPlacementAllowed())
            {
                Debug.LogWarning("[Kit] FarmKit: 정비 시간이 아니거나 최대 개수 도달");
                return false;
            }
            if (!TryRaycastFromView(out RaycastHit hit)) return false;

            Vector3 placePos = hit.point;
            Quaternion placeRot = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up));

            if (!inventory.TryConsume(KitType.FarmKit)) return false;

            if (FarmManager.Instance.TryPlaceFarm(placePos, placeRot, out _))
            {
                Debug.Log($"[Kit] FarmKit 사용 — 밭 설치 @ {placePos}");
                return true;
            }
            inventory.Add(KitType.FarmKit, 1); // 거부 시 환불
            return false;
        }

        private bool TryRaycastFromView(out RaycastHit hit)
        {
            hit = default;
            if (viewCamera == null) return false;
            var ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            return Physics.Raycast(ray, out hit, useRange, hitMask, QueryTriggerInteraction.Ignore);
        }
    }
}
