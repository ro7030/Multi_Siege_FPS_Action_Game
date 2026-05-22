using System;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectM.Defense;
using ProjectM.Economy;

namespace ProjectM.Player
{
    /// <summary>
    /// 플레이어 키트 장착/사용 시스템 (GTA식 라디얼 휠).
    ///
    /// 동작
    ///   - 3번키를 "꾹 누른 상태": 라디얼 휠이 열림 (KitWheelView 가 표시)
    ///       · 마우스를 움직여 방향을 가리킴 (위=회복 / 좌하=수리 / 우하=밭)
    ///       · 좌클릭 또는 3번키 떼기 → 가리킨 키트 장착 (가운데/이동 없음 = 장착 해제)
    ///   - 키트 장착 후 좌클릭: 장착한 키트 사용
    ///       · HealKit: 자신을 회복 / RepairKit: 시선 끝 방어물 수리 / FarmKit: 시선 끝 지면에 밭 설치
    ///   - 휠이 열려있거나 키트가 장착된 동안 WeaponController 가 사격을 억제
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

        [Header("라디얼 휠")]
        [Tooltip("마우스 이동이 이 값을 넘어야 방향이 선택됨 (픽셀)")]
        [SerializeField] private float selectionDeadzone = 40f;
        [Tooltip("선택 방향 벡터 누적 최대 크기 (픽셀)")]
        [SerializeField] private float selectionMaxRadius = 200f;
        [SerializeField] private float mouseSensitivity = 1f;

        [Header("로컬 권한")]
        [SerializeField] private bool isLocalPlayer = true;

        [Header("연동")]
        [SerializeField] private ThrowableEquipper throwableEquipper; // 키트 장착 시 투척 내려놓기

        // ── 장착 상태 ──
        public KitType EquippedKit { get; private set; } = KitType.None;
        public bool IsKitEquipped => EquippedKit != KitType.None;

        // ── 휠 선택 상태 (UI 가 폴링) ──
        public bool IsSelecting { get; private set; }
        public KitType HighlightedKit { get; private set; } = KitType.None;
        /// <summary>현재 누적된 선택 방향 (UI 포인터 표시용). 길이 0 ~ selectionMaxRadius.</summary>
        public Vector2 SelectionDirection { get; private set; }
        public float SelectionDeadzone => selectionDeadzone;
        public float SelectionMaxRadius => selectionMaxRadius;

        public event Action<KitType> OnEquippedChanged;
        public event Action<KitType, bool> OnKitUseAttempt;
        public event Action<bool> OnSelectionStateChanged;

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

            // 휠 열기
            if (kb.digit3Key.wasPressedThisFrame)
                BeginSelection();

            if (IsSelecting)
            {
                UpdateSelection(mouse);

                bool confirm = (mouse != null && mouse.leftButton.wasPressedThisFrame)
                               || kb.digit3Key.wasReleasedThisFrame;
                if (confirm) EndSelection();
                return; // 선택 중에는 일반 키트 사용 입력 무시
            }

            // 일반 키트 사용 (휠 닫힘 + 키트 장착 상태)
            if (!IsKitEquipped) return;
            if (mouse == null) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;

            if (mouse.leftButton.wasPressedThisFrame)
                UseEquippedKit();
        }

        // ─────────────────────────────────────────────────────────────
        // 라디얼 휠 선택
        // ─────────────────────────────────────────────────────────────

        private void BeginSelection()
        {
            IsSelecting = true;
            SelectionDirection = Vector2.zero;
            HighlightedKit = KitType.None; // 이동 없이 확정하면 장착 해제
            OnSelectionStateChanged?.Invoke(true);
        }

        private void UpdateSelection(Mouse mouse)
        {
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
                Vector2 v = SelectionDirection + delta;
                if (v.magnitude > selectionMaxRadius)
                    v = v.normalized * selectionMaxRadius;
                SelectionDirection = v;
            }

            if (SelectionDirection.magnitude >= selectionDeadzone)
                HighlightedKit = DirectionToKit(SelectionDirection);
            else
                HighlightedKit = KitType.None;
        }

        private void EndSelection()
        {
            IsSelecting = false;
            OnSelectionStateChanged?.Invoke(false);

            // 가리킨 키트가 보유 중이면 장착, 아니면 해제
            if (HighlightedKit != KitType.None && inventory != null && inventory.Has(HighlightedKit))
                SetEquipped(HighlightedKit);
            else
                SetEquipped(KitType.None);
        }

        /// <summary>방향 벡터 → 키트. 위=Heal, 좌하=Repair, 우하=Farm (120°씩).</summary>
        public static KitType DirectionToKit(Vector2 dir)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg; // -180..180
            if (ang < 0) ang += 360f;                              // 0..360

            // Heal: 위(90°) 중심 30~150
            if (ang >= 30f && ang < 150f) return KitType.HealKit;
            // Repair: 좌하(210°) 중심 150~270
            if (ang >= 150f && ang < 270f) return KitType.RepairKit;
            // Farm: 우하(330°) 중심 270~360, 0~30
            return KitType.FarmKit;
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
