using System;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectM.Defense;
using ProjectM.Economy;

namespace ProjectM.Player
{
    /// <summary>
    /// 플레이어 키트 장착/사용 시스템.
    ///
    /// 동작
    ///   - 3번키: 보유 중인 키트 중 다음 종류로 사이클 (HealKit → RepairKit → FarmKit → 해제)
    ///   - 좌클릭: 장착한 키트 사용
    ///       · HealKit: 자신을 회복
    ///       · RepairKit: 시선 끝 방어물 수리
    ///       · FarmKit: 시선 끝 지면에 밭 설치 (정비 시간만)
    ///   - 키트 장착 중에는 WeaponController 가 사격을 억제 (IsKitEquipped 체크)
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
        [Tooltip("HealKit 좌클릭 시 회복량")]
        [SerializeField] private float healAmount = 50f;
        [Tooltip("RepairKit 좌클릭 시 즉시 수리량 (기획서: 문 HP의 10%)")]
        [SerializeField] private float repairAmount = 50f;

        [Header("로컬 권한")]
        [SerializeField] private bool isLocalPlayer = true;

        public KitType EquippedKit { get; private set; } = KitType.None;
        public bool IsKitEquipped => EquippedKit != KitType.None;

        public event Action<KitType> OnEquippedChanged;
        public event Action<KitType, bool> OnKitUseAttempt; // (kit, success)

        private void Awake()
        {
            if (inventory == null) inventory = GetComponent<KitInventory>();
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
            if (playerHealth == null) playerHealth = GetComponent<HealthSystem>();
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
            // 사용 중인 키트가 0이 되면 다음 가능한 키트로 자동 이동
            if (EquippedKit == type && newCount <= 0)
                CycleKit();
        }

        // ─────────────────────────────────────────────────────────────
        private void Update()
        {
            if (!isLocalPlayer) return;

            var kb = Keyboard.current;
            var mouse = Mouse.current;

            // 3번키: 키트 사이클
            if (kb != null && kb.digit3Key.wasPressedThisFrame)
                CycleKit();

            if (!IsKitEquipped) return;
            if (mouse == null) return;
            if (Cursor.lockState != CursorLockMode.Locked) return; // UI 열려있을 땐 무시

            if (mouse.leftButton.wasPressedThisFrame)
                UseEquippedKit();
        }

        // ─────────────────────────────────────────────────────────────
        // 사이클
        // ─────────────────────────────────────────────────────────────

        /// <summary>보유 중인 다음 키트 종류로 이동. 모두 없으면 해제(None).</summary>
        public void CycleKit()
        {
            // 순서: 현재 → HealKit → RepairKit → FarmKit → None
            KitType[] order = { KitType.HealKit, KitType.RepairKit, KitType.FarmKit };

            int startIdx = Array.IndexOf(order, EquippedKit);
            // None 이면 -1, 다음은 0부터 시작
            for (int step = 1; step <= order.Length; step++)
            {
                int idx = (startIdx + step) % (order.Length + 1); // +1 = None 슬롯
                if (idx >= order.Length)
                {
                    // None 슬롯
                    SetEquipped(KitType.None);
                    return;
                }
                if (inventory != null && inventory.Has(order[idx]))
                {
                    SetEquipped(order[idx]);
                    return;
                }
            }
            SetEquipped(KitType.None);
        }

        private void SetEquipped(KitType type)
        {
            if (EquippedKit == type) return;
            EquippedKit = type;
            OnEquippedChanged?.Invoke(type);
            Debug.Log($"[Kit] 장착: {type}");
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

            OnKitUseAttempt?.Invoke(EquippedKit, ok);
        }

        private bool UseHealKit()
        {
            if (playerHealth == null) { Debug.LogWarning("[Kit] HealKit: HealthSystem 없음"); return false; }
            if (!playerHealth.IsAlive) return false;

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
            else
            {
                // 매니저 거부 시 키트 환불
                inventory.Add(KitType.FarmKit, 1);
                return false;
            }
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
