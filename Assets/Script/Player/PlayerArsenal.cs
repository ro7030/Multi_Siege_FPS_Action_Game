using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectM.Player
{
    /// <summary>
    /// 플레이어 무기고. 주무기(총)/보조무기(칼) 슬롯 전환 + 단계(업그레이드) 관리.
    ///
    /// 입력
    ///   - 1번키: 주 무기 장착 / 2번키: 보조 무기 장착 (기획서 8-2)
    ///   - 무기 전환 시 키트는 자동으로 내려놓음
    ///
    /// 업그레이드는 ShopController.TryUpgradeWeapon 이 화폐 차감 후 TryUpgrade 를 호출한다.
    /// 단계 데이터는 WeaponProgression(SO) 에서 읽으며, Unity 에서 자유롭게 추가/삭제 가능.
    /// </summary>
    public class PlayerArsenal : MonoBehaviour
    {
        [Header("데이터")]
        [SerializeField] private WeaponProgression progression;

        [Header("무기 컴포넌트")]
        [SerializeField] private WeaponController rangedWeapon; // 주무기
        [SerializeField] private MeleeWeapon meleeWeapon;       // 보조무기

        [Header("연동")]
        [SerializeField] private KitEquipper kitEquipper;
        [SerializeField] private ThrowableEquipper throwableEquipper;
        [SerializeField] private bool isLocalPlayer = true;

        [Header("전환 키 (Inspector 조절)")]
        [Tooltip("주 무기(라이플)로 전환할 키")]
        [SerializeField] private Key primaryKey = Key.Digit2;   // 2번 = 라이플
        [Tooltip("보조 무기(근접)로 전환할 키")]
        [SerializeField] private Key secondaryKey = Key.Digit1; // 1번 = 근접

        [Header("상태 (읽기 전용)")]
        [SerializeField] private int primaryTierIndex = 0;
        [SerializeField] private int secondaryTierIndex = 0;

        public WeaponSlot ActiveSlot { get; private set; } = WeaponSlot.Primary;
        public int PrimaryTierIndex => primaryTierIndex;
        public int SecondaryTierIndex => secondaryTierIndex;

        public event Action<WeaponSlot> OnSlotChanged;
        public event Action<WeaponSlot, int> OnTierChanged;

        private void Awake()
        {
            if (rangedWeapon == null) rangedWeapon = GetComponent<WeaponController>();
            if (meleeWeapon == null) meleeWeapon = GetComponent<MeleeWeapon>();
            if (kitEquipper == null) kitEquipper = GetComponent<KitEquipper>();
            if (throwableEquipper == null) throwableEquipper = GetComponent<ThrowableEquipper>();
        }

        private void Start()
        {
            ApplySlot(WeaponSlot.Primary);
            ApplySlot(WeaponSlot.Secondary);
            SetActiveSlot(WeaponSlot.Primary);
        }

        private void Update()
        {
            if (!isLocalPlayer) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb[primaryKey].wasPressedThisFrame) SetActiveSlot(WeaponSlot.Primary);
            if (kb[secondaryKey].wasPressedThisFrame) SetActiveSlot(WeaponSlot.Secondary);
        }

        // ── 슬롯 전환 ─────────────────────────────────────────────
        public void SetActiveSlot(WeaponSlot slot)
        {
            ActiveSlot = slot;

            // 무기로 전환하면 키트/투척은 내려놓음
            if (kitEquipper != null) kitEquipper.Holster();
            if (throwableEquipper != null) throwableEquipper.Holster();

            if (rangedWeapon != null) rangedWeapon.IsActive = (slot == WeaponSlot.Primary);
            if (meleeWeapon != null)  meleeWeapon.IsActive  = (slot == WeaponSlot.Secondary);

            OnSlotChanged?.Invoke(slot);
            Debug.Log($"[Arsenal] 슬롯 전환: {slot}");
        }

        // ── 업그레이드 조회/실행 ──────────────────────────────────
        public int CurrentTierIndex(WeaponSlot slot)
            => slot == WeaponSlot.Primary ? primaryTierIndex : secondaryTierIndex;

        public WeaponDefinition CurrentDefinition(WeaponSlot slot)
            => progression != null ? progression.GetTier(slot, CurrentTierIndex(slot)) : null;

        public WeaponDefinition NextTier(WeaponSlot slot)
            => progression != null ? progression.GetTier(slot, CurrentTierIndex(slot) + 1) : null;

        public bool CanUpgrade(WeaponSlot slot) => NextTier(slot) != null;

        public int NextUpgradePrice(WeaponSlot slot)
        {
            var d = NextTier(slot);
            return d != null ? d.price : 0;
        }

        /// <summary>다음 단계로 업그레이드(화폐 차감은 호출자 책임).</summary>
        public bool TryUpgrade(WeaponSlot slot)
        {
            if (!CanUpgrade(slot)) return false;

            if (slot == WeaponSlot.Primary) primaryTierIndex++;
            else                            secondaryTierIndex++;

            ApplySlot(slot);
            OnTierChanged?.Invoke(slot, CurrentTierIndex(slot));
            Debug.Log($"[Arsenal] {slot} 업그레이드 → {CurrentDefinition(slot)?.displayName}");
            return true;
        }

        // ── 적용 ──────────────────────────────────────────────────
        private void ApplySlot(WeaponSlot slot)
        {
            var def = CurrentDefinition(slot);
            if (def == null) return;

            if (slot == WeaponSlot.Primary && rangedWeapon != null)
                rangedWeapon.ApplyDefinition(def);
            else if (slot == WeaponSlot.Secondary && meleeWeapon != null)
                meleeWeapon.ApplyDefinition(def);
        }
    }
}
