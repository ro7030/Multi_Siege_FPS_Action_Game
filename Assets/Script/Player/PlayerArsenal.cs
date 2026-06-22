using System;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectM.Network;
using ProjectM.UI;

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

        public WeaponProgression Progression => progression;

        [Header("무기 컴포넌트")]
        [SerializeField] private WeaponController rangedWeapon; // 주무기
        [SerializeField] private MeleeWeapon meleeWeapon;       // 보조무기

        [Header("연동")]
        [SerializeField] private KitEquipper kitEquipper;
        [SerializeField] private ThrowableEquipper throwableEquipper;
        [SerializeField] private bool isLocalPlayer = true;

        public bool IsLocalPlayer { get => isLocalPlayer; set => isLocalPlayer = value; }

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

        private void OnEnable()
        {
            if (kitEquipper != null)       kitEquipper.OnEquippedChanged       += HandleKitChanged;
            if (throwableEquipper != null) throwableEquipper.OnEquippedChanged += HandleThrowableChanged;
        }

        private void OnDisable()
        {
            if (kitEquipper != null)       kitEquipper.OnEquippedChanged       -= HandleKitChanged;
            if (throwableEquipper != null) throwableEquipper.OnEquippedChanged -= HandleThrowableChanged;
        }

        private void HandleKitChanged(KitType _)
        {
            SyncWeaponInputState();
            SyncWeaponViewModels();
        }

        private void HandleThrowableChanged(ThrowableType _)
        {
            SyncWeaponInputState();
            SyncWeaponViewModels();
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
            if (UIInputModal.IsBlockingGameplayInput) return;

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

            SyncWeaponInputState();
            SyncWeaponViewModels();

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
            return TrySetTier(slot, CurrentTierIndex(slot) + 1);
        }

        /// <summary>지정 단계로 바로 설정(상점에서 임의 티어 구매).</summary>
        public bool TrySetTier(WeaponSlot slot, int tierIndex)
        {
            if (progression == null) return false;
            if (progression.GetTier(slot, tierIndex) == null) return false;

            if (TryGetComponent<NetworkPlayerArsenal>(out var netArsenal)
                && NetworkSessionHelper.IsMultiplayerSession
                && netArsenal.IsSpawned
                && NetworkSessionHelper.IsServer)
            {
                return netArsenal.ServerSetTier(slot, tierIndex);
            }

            return ApplyNetworkTier(slot, tierIndex);
        }

        /// <summary>네트워크 미러 또는 오프라인 로컬 적용.</summary>
        public bool ApplyNetworkTier(WeaponSlot slot, int tierIndex)
        {
            if (progression == null || progression.GetTier(slot, tierIndex) == null)
                return false;

            if (slot == WeaponSlot.Primary) primaryTierIndex = tierIndex;
            else                            secondaryTierIndex = tierIndex;

            ApplySlot(slot);
            OnTierChanged?.Invoke(slot, CurrentTierIndex(slot));
            ResyncWeaponActivation();

            Debug.Log($"[Arsenal] {slot} → 티어 {tierIndex} ({CurrentDefinition(slot)?.displayName})");
            return true;
        }

        /// <summary>ActiveSlot 기준 IsActive만 재동기화(Holster·뷰모델 없음).</summary>
        private void ResyncWeaponActivation() => SyncWeaponInputState();

        public int GetTierPrice(WeaponSlot slot, int tierIndex)
        {
            var def = progression != null ? progression.GetTier(slot, tierIndex) : null;
            return def != null ? def.price : 0;
        }

        /// <summary>아직 보유하지 않은 티어만 구매 가능 (0티어는 기본 지급).</summary>
        public bool CanPurchaseTier(WeaponSlot slot, int tierIndex)
        {
            if (progression == null || tierIndex <= 0) return false;
            if (tierIndex <= CurrentTierIndex(slot)) return false;
            return progression.GetTier(slot, tierIndex) != null;
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

        private bool AreWeaponsInputAllowed()
        {
            bool kitOn       = kitEquipper       != null && kitEquipper.IsKitEquipped;
            bool throwableOn = throwableEquipper != null && throwableEquipper.IsThrowableEquipped;
            return !kitOn && !throwableOn;
        }

        /// <summary>ActiveSlot + 키트/투척 상태 기준으로 IsActive만 갱신(유일한 진입점).</summary>
        private void SyncWeaponInputState()
        {
            bool weaponsActive = AreWeaponsInputAllowed();

            if (rangedWeapon != null)
                rangedWeapon.IsActive = weaponsActive && ActiveSlot == WeaponSlot.Primary;
            if (meleeWeapon != null)
                meleeWeapon.IsActive = weaponsActive && ActiveSlot == WeaponSlot.Secondary;
        }

        /// <summary>ActiveSlot + 키트/투척 상태 기준으로 1인칭 뷰모델 표시만 갱신.</summary>
        private void SyncWeaponViewModels()
        {
            bool weaponsVisible = AreWeaponsInputAllowed();

            if (rangedWeapon != null)
                rangedWeapon.SetViewModelVisible(weaponsVisible && ActiveSlot == WeaponSlot.Primary);
            if (meleeWeapon != null)
                meleeWeapon.SetViewModelVisible(weaponsVisible && ActiveSlot == WeaponSlot.Secondary);
        }
    }
}
