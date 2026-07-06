using Unity.Netcode;
using UnityEngine;
using ProjectM.Network;

namespace ProjectM.Player
{
    public enum AttachedWeaponDisplayKind
    {
        None,
        Primary,
        Secondary,
        Kit,
        Throwable
    }

    /// <summary>
    /// useAttachedWeapons=true 일 때 총/검/키트/투척을 몽키 Hand_r_equipment 단일 소켓에 부착한다.
    /// false 이면 기존 카메라 소켓 경로(WeaponController 등)를 그대로 사용한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerAttachedWeaponVisual : MonoBehaviour
    {
        [Header("실험 토글 — false 로 롤백")]
        [SerializeField] private bool useAttachedWeapons = true;

        [Header("참조")]
        [SerializeField] private PlayerArsenal arsenal;
        [SerializeField] private CharacterVisualBinder visualBinder;
        [SerializeField] private KitEquipper kitEquipper;
        [SerializeField] private ThrowableEquipper throwableEquipper;

        [Header("3인칭 손 오프셋 (원격 / 정렬 전)")]
        [SerializeField] private Vector3 rangedLocalPosition = new(0.02f, 0.04f, 0.08f);
        [SerializeField] private Vector3 rangedLocalEuler = new(-10f, 90f, 0f);
        [SerializeField] private Vector3 rangedLocalScale = new(0.6f, 0.6f, 0.6f);

        [SerializeField] private Vector3 meleeLocalPosition = new(0.04f, 0.02f, 0.06f);
        [SerializeField] private Vector3 meleeLocalEuler = new(0f, 90f, 0f);
        [SerializeField] private Vector3 meleeLocalScale = new(0.7f, 0.7f, 0.7f);

        [SerializeField] private Vector3 heldLocalPosition = new(0.03f, 0.03f, 0.07f);
        [SerializeField] private Vector3 heldLocalEuler = new(-5f, 90f, 0f);
        [SerializeField] private Vector3 heldLocalScale = new(0.55f, 0.55f, 0.55f);

        private NetworkObject networkObject;
        private Transform equipmentSocket;

        private GameObject primaryInstance;
        private GameObject secondaryInstance;
        private GameObject kitInstance;
        private GameObject throwableInstance;

        private int primaryTierIndex = -1;
        private int secondaryTierIndex = -1;
        private KitType kitVisualType = KitType.None;
        private ThrowableType throwableVisualType = ThrowableType.None;

        private AttachedWeaponDisplayKind activeDisplay = AttachedWeaponDisplayKind.None;

        public bool UseAttachedWeapons => useAttachedWeapons;
        public AttachedWeaponDisplayKind ActiveDisplay => activeDisplay;
        public ThrowableType ActiveThrowableType =>
            activeDisplay == AttachedWeaponDisplayKind.Throwable ? throwableVisualType : ThrowableType.None;

        public GameObject ActiveDisplayedInstance =>
            activeDisplay switch
            {
                AttachedWeaponDisplayKind.Primary => primaryInstance,
                AttachedWeaponDisplayKind.Secondary => secondaryInstance,
                AttachedWeaponDisplayKind.Kit => kitInstance,
                AttachedWeaponDisplayKind.Throwable => throwableInstance,
                _ => null
            };

        private void Awake()
        {
            if (arsenal == null) arsenal = GetComponent<PlayerArsenal>();
            if (visualBinder == null) visualBinder = GetComponentInChildren<CharacterVisualBinder>(true);
            if (kitEquipper == null) kitEquipper = GetComponent<KitEquipper>();
            if (throwableEquipper == null) throwableEquipper = GetComponent<ThrowableEquipper>();
            networkObject = GetComponent<NetworkObject>();
        }

        private void OnEnable()
        {
            if (visualBinder != null)
            {
                visualBinder.OnVisualApplied += HandleVisualApplied;
                if (visualBinder.HandEquipmentSocket != null)
                    equipmentSocket = visualBinder.HandEquipmentSocket;
            }

            if (arsenal != null)
            {
                arsenal.OnSlotChanged += HandleArsenalChanged;
                arsenal.OnTierChanged += HandleTierChanged;
            }

            if (kitEquipper != null)
                kitEquipper.OnEquippedChanged += HandleKitChanged;
            if (throwableEquipper != null)
                throwableEquipper.OnEquippedChanged += HandleThrowableChanged;
        }

        private void OnDisable()
        {
            if (visualBinder != null)
                visualBinder.OnVisualApplied -= HandleVisualApplied;
            if (arsenal != null)
            {
                arsenal.OnSlotChanged -= HandleArsenalChanged;
                arsenal.OnTierChanged -= HandleTierChanged;
            }
            if (kitEquipper != null)
                kitEquipper.OnEquippedChanged -= HandleKitChanged;
            if (throwableEquipper != null)
                throwableEquipper.OnEquippedChanged -= HandleThrowableChanged;
        }

        private void Start()
        {
            RefreshPresentation();
        }

        /// <summary>
        /// 카메라 스냅 이후에도 손 본 추적이 필요할 때(검 등) 로컬 그립 오프셋을 복원한다.
        /// </summary>
        public void RestoreActiveHandOffset()
        {
            var instance = ActiveDisplayedInstance;
            if (instance == null)
                return;

            switch (activeDisplay)
            {
                case AttachedWeaponDisplayKind.Secondary:
                    ApplyHandOffset(instance.transform, WeaponKind.Melee, useHeldOffset: false);
                    break;
                case AttachedWeaponDisplayKind.Kit:
                case AttachedWeaponDisplayKind.Throwable:
                    ApplyHandOffset(instance.transform, WeaponKind.Melee, useHeldOffset: true);
                    break;
            }
        }

        public void RefreshPresentation()
        {
            if (!useAttachedWeapons)
                return;

            if (equipmentSocket == null && visualBinder != null)
                equipmentSocket = visualBinder.HandEquipmentSocket;

            EnsurePrimaryInstance();
            EnsureSecondaryInstance();
            EnsureKitInstance();
            EnsureThrowableInstance();
            ApplyOwnerWeaponLayers();
            ApplyVisibility();
        }

        private void HandleVisualApplied(GameObject _, Transform socket)
        {
            equipmentSocket = socket != null ? socket : visualBinder?.HandEquipmentSocket;
            primaryTierIndex = -1;
            secondaryTierIndex = -1;
            kitVisualType = KitType.None;
            throwableVisualType = ThrowableType.None;
            DestroyInstance(ref primaryInstance);
            DestroyInstance(ref secondaryInstance);
            DestroyInstance(ref kitInstance);
            DestroyInstance(ref throwableInstance);
            RefreshPresentation();
        }

        private void HandleArsenalChanged(WeaponSlot _) => RefreshPresentation();
        private void HandleTierChanged(WeaponSlot slot, int _)
        {
            if (slot == WeaponSlot.Primary) primaryTierIndex = -1;
            else secondaryTierIndex = -1;
            RefreshPresentation();
        }

        private void HandleKitChanged(KitType _) => RefreshPresentation();
        private void HandleThrowableChanged(ThrowableType _) => RefreshPresentation();

        private void EnsurePrimaryInstance()
        {
            if (arsenal == null || equipmentSocket == null) return;

            int tier = arsenal.CurrentTierIndex(WeaponSlot.Primary);
            if (primaryInstance != null && primaryTierIndex == tier) return;

            DestroyInstance(ref primaryInstance);
            primaryTierIndex = tier;

            var def = arsenal.CurrentDefinition(WeaponSlot.Primary);
            var prefab = def != null ? def.ResolveWorldModelPrefab() : null;
            primaryInstance = CreateAttachedInstance(prefab, def?.kind ?? WeaponKind.Ranged);
        }

        private void EnsureSecondaryInstance()
        {
            if (arsenal == null || equipmentSocket == null) return;

            int tier = arsenal.CurrentTierIndex(WeaponSlot.Secondary);
            if (secondaryInstance != null && secondaryTierIndex == tier) return;

            DestroyInstance(ref secondaryInstance);
            secondaryTierIndex = tier;

            var def = arsenal.CurrentDefinition(WeaponSlot.Secondary);
            var prefab = def != null ? def.ResolveWorldModelPrefab() : null;
            secondaryInstance = CreateAttachedInstance(prefab, def?.kind ?? WeaponKind.Melee);
        }

        private void EnsureKitInstance()
        {
            if (kitEquipper == null || equipmentSocket == null) return;

            var type = kitEquipper.EquippedKit;
            if (kitInstance != null && kitVisualType == type) return;

            DestroyInstance(ref kitInstance);
            kitVisualType = type;

            if (type == KitType.None) return;

            var prefab = kitEquipper.GetHeldViewModelPrefab(type);
            kitInstance = CreateAttachedInstance(prefab, WeaponKind.Melee, useHeldOffset: true);
        }

        private void EnsureThrowableInstance()
        {
            if (throwableEquipper == null || equipmentSocket == null) return;

            var type = throwableEquipper.EquippedThrowable;
            if (throwableInstance != null && throwableVisualType == type) return;

            DestroyInstance(ref throwableInstance);
            throwableVisualType = type;

            if (type == ThrowableType.None) return;

            var def = throwableEquipper.GetDefinition(type);
            throwableInstance = CreateAttachedInstance(def?.heldViewModelPrefab, WeaponKind.Melee, useHeldOffset: true);
        }

        private GameObject CreateAttachedInstance(GameObject prefab, WeaponKind kind, bool useHeldOffset = false)
        {
            if (prefab == null || equipmentSocket == null) return null;

            var instance = Instantiate(prefab, equipmentSocket);
            ApplyHandOffset(instance.transform, kind, useHeldOffset);
            instance.SetActive(false);

            if (IsLocalOwner())
                LocalFirstPersonVisual.ApplyOwnerWeaponLayer(instance, true);

            return instance;
        }

        private void ApplyOwnerWeaponLayers()
        {
            if (!IsLocalOwner())
                return;

            LocalFirstPersonVisual.ApplyOwnerWeaponLayer(primaryInstance, true);
            LocalFirstPersonVisual.ApplyOwnerWeaponLayer(secondaryInstance, true);
            LocalFirstPersonVisual.ApplyOwnerWeaponLayer(kitInstance, true);
            LocalFirstPersonVisual.ApplyOwnerWeaponLayer(throwableInstance, true);
        }

        private void ApplyHandOffset(Transform t, WeaponKind kind, bool useHeldOffset)
        {
            if (useHeldOffset)
            {
                t.localPosition = heldLocalPosition;
                t.localRotation = Quaternion.Euler(heldLocalEuler);
                t.localScale = heldLocalScale;
                return;
            }

            if (kind == WeaponKind.Melee)
            {
                t.localPosition = meleeLocalPosition;
                t.localRotation = Quaternion.Euler(meleeLocalEuler);
                t.localScale = meleeLocalScale;
                return;
            }

            t.localPosition = rangedLocalPosition;
            t.localRotation = Quaternion.Euler(rangedLocalEuler);
            t.localScale = rangedLocalScale;
        }

        private void ApplyVisibility()
        {
            activeDisplay = ResolveActiveDisplay();

            SetInstanceActive(primaryInstance, activeDisplay == AttachedWeaponDisplayKind.Primary);
            SetInstanceActive(secondaryInstance, activeDisplay == AttachedWeaponDisplayKind.Secondary);
            SetInstanceActive(kitInstance, activeDisplay == AttachedWeaponDisplayKind.Kit);
            SetInstanceActive(throwableInstance, activeDisplay == AttachedWeaponDisplayKind.Throwable);
        }

        private AttachedWeaponDisplayKind ResolveActiveDisplay()
        {
            if (kitEquipper != null && kitEquipper.IsKitEquipped)
                return AttachedWeaponDisplayKind.Kit;

            if (throwableEquipper != null && throwableEquipper.IsThrowableEquipped)
                return AttachedWeaponDisplayKind.Throwable;

            if (arsenal == null || !arsenal.AreWeaponsVisible())
                return AttachedWeaponDisplayKind.None;

            return arsenal.ActiveSlot == WeaponSlot.Primary
                ? AttachedWeaponDisplayKind.Primary
                : AttachedWeaponDisplayKind.Secondary;
        }

        private bool IsLocalOwner()
        {
            if (networkObject != null && networkObject.IsSpawned)
                return networkObject.IsOwner;

            return arsenal != null && arsenal.IsLocalPlayer;
        }

        private static void SetInstanceActive(GameObject instance, bool active)
        {
            if (instance != null && instance.activeSelf != active)
                instance.SetActive(active);
        }

        private static void DestroyInstance(ref GameObject instance)
        {
            if (instance == null) return;
            Destroy(instance);
            instance = null;
        }
    }
}
