using System;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectM.Defense;

namespace ProjectM.Player
{
    /// <summary>
    /// 1인칭 무기 컨트롤러. MVP: 히트스캔(레이캐스트) 기반 사격 + 탄창/예비 탄약 + 재장전.
    /// 발사 판정은 로컬에서 즉시 수행하고, 네트워크 단계에서는 Host에 발사 요청 패킷으로 대체된다.
    /// </summary>
    public class WeaponController : MonoBehaviour
    {
        [Header("기준 카메라")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform muzzle;

        [Header("비주얼 (1인칭 뷰모델)")]
        [Tooltip("카메라 자식의 빈 GameObject. 무기 모델이 이 곳의 자식으로 인스턴스화된다.")]
        [SerializeField] private Transform viewModelSocket;
        private GameObject viewModelInstance;

        [Header("탄도")]
        [SerializeField] private float damage = 25f;
        [SerializeField] private float range = 200f;
        [SerializeField] private float fireRate = 8f; // 초당 발사
        [SerializeField] private bool isAutomatic = true;
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("탄약")]
        [SerializeField] private int magazineSize = 30;
        [SerializeField] private int reserveAmmo = 90;
        [SerializeField] private float reloadDuration = 1.5f;

        [Header("로컬 권한")]
        [SerializeField] private bool isLocalPlayer = true;

        [Header("키트/투척 시스템 연동")]
        [Tooltip("키트/투척무기가 장착되어 있으면 사격을 억제. 비워두면 자동 탐색.")]
        [SerializeField] private KitEquipper kitEquipper;
        [SerializeField] private ThrowableEquipper throwableEquipper;
        private PlayerCombatInputGate combatInputGate;

        public int CurrentMagazine { get; private set; }
        public int ReserveAmmo => reserveAmmo;
        public bool IsReloading { get; private set; }
        /// <summary>좌클릭 견착 유지(연발 중 fireRate 쿨다운 제외, 탄창·재장전 조건).</summary>
        public bool IsAimHeld { get; private set; }
        public bool IsLocalPlayer { get => isLocalPlayer; set => isLocalPlayer = value; }

        /// <summary>주무기 슬롯이 활성일 때만 true. PlayerArsenal 이 토글.</summary>
        public bool IsActive { get; set; } = true;
        public WeaponDefinition CurrentDefinition { get; private set; }

        /// <summary>레거시(비부착) 경로에서 현재 카메라 소켓에 인스턴스화된 뷰모델. 부착 모드에서는 null.</summary>
        public GameObject ViewModelInstance => viewModelInstance;
        public PlayerAttachedWeaponVisual AttachedVisual => attachedVisual;
        /// <summary>실제 히트스캔 레이가 나가는 기준 카메라. 총구 이펙트를 실제 탄도 방향에 맞추는 데 사용.</summary>
        public Camera ViewCamera => viewCamera;

        public event Action OnFired;
        public event Action OnReloadStart;
        public event Action OnReloadEnd;
        public event Action OnReserveAmmoChanged;
        public event Action<GameObject, float> OnHit; // 맞춘 대상, 데미지

        private float nextFireTime;
        private float reloadEndTime;
        private ReviveSystem revive;
        private PlayerAttachedWeaponVisual attachedVisual;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
            if (kitEquipper == null) kitEquipper = GetComponent<KitEquipper>();
            if (throwableEquipper == null) throwableEquipper = GetComponent<ThrowableEquipper>();
            if (combatInputGate == null) combatInputGate = GetComponent<PlayerCombatInputGate>();
            revive = GetComponent<ReviveSystem>();
            attachedVisual = GetComponent<PlayerAttachedWeaponVisual>();
            CurrentMagazine = magazineSize;
        }

        private void Update()
        {
            IsAimHeld = false;
            if (!isLocalPlayer) return;

            // 재장전 완료는 슬롯이 비활성(보조무기 사용 중)이어도 진행
            if (IsReloading && Time.time >= reloadEndTime) FinishReload();

            if (revive != null && (revive.IsDown || revive.IsDead)) return;
            if (!IsActive) return;

            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse == null) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;

            if (combatInputGate != null && combatInputGate.IsSuppressed) return;
            if (kitEquipper != null && (kitEquipper.IsSelecting || kitEquipper.IsKitEquipped)) return;
            if (throwableEquipper != null && throwableEquipper.SuppressesWeaponFire) return;
            if (throwableEquipper != null && (throwableEquipper.IsSelecting || throwableEquipper.IsThrowableEquipped)) return;

            bool wantsAim = mouse.leftButton.isPressed;
            IsAimHeld = wantsAim && !IsReloading && CurrentMagazine > 0;

            bool wantsFire = isAutomatic ? wantsAim : mouse.leftButton.wasPressedThisFrame;
            if (wantsFire && CanFire()) Fire();

            if (kb != null && kb.rKey.wasPressedThisFrame) StartReload();
        }

        public bool CanFire() => !IsReloading && CurrentMagazine > 0 && Time.time >= nextFireTime;

        public void Fire()
        {
            CurrentMagazine--;
            nextFireTime = Time.time + 1f / Mathf.Max(0.1f, fireRate);
            OnFired?.Invoke();

            if (viewCamera == null) return;
            var ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            if (PlayerRaycastUtility.TryRaycastFromView(transform, ray, out RaycastHit hit, range, hitMask))
            {
                // 방어 오브젝트(성문/베이스/밭)는 플레이어 공격으로 파괴 불가 — 데미지 적용 안 함
                bool isDefense = hit.collider.GetComponentInParent<DefenseObject>() != null;

                if (!isDefense)
                {
                    var dmg = hit.collider.GetComponentInParent<IDamageable>();
                    if (dmg != null && dmg.IsAlive)
                    {
                        dmg.TakeDamage(damage, gameObject);
                        OnHit?.Invoke(hit.collider.gameObject, damage);
                    }
                }
                Debug.DrawLine(ray.origin, hit.point, Color.red, 0.1f);
            }
            else
            {
                Debug.DrawRay(ray.origin, ray.direction * range, Color.yellow, 0.1f);
            }

            if (CurrentMagazine <= 0 && reserveAmmo > 0) StartReload();
        }

        public void StartReload()
        {
            if (IsReloading || CurrentMagazine >= magazineSize || reserveAmmo <= 0) return;
            IsReloading = true;
            reloadEndTime = Time.time + reloadDuration;
            OnReloadStart?.Invoke();
        }

        private void FinishReload()
        {
            int need = magazineSize - CurrentMagazine;
            int take = Mathf.Min(need, reserveAmmo);
            CurrentMagazine += take;
            reserveAmmo -= take;
            IsReloading = false;
            OnReloadEnd?.Invoke();
        }

        public void AddReserveAmmo(int amount)
        {
            if (amount <= 0) return;
            reserveAmmo += amount;
            OnReserveAmmoChanged?.Invoke();
        }

        /// <summary>무기 정의(단계)를 적용한다. PlayerArsenal 이 장착/업그레이드 시 호출.</summary>
        public void ApplyDefinition(WeaponDefinition def)
        {
            if (def == null) return;
            CurrentDefinition = def;
            damage = def.damage;
            fireRate = def.fireRate;
            range = def.range;
            isAutomatic = def.isAutomatic;
            magazineSize = def.magazineSize;
            reloadDuration = def.reloadDuration;
            if (CurrentMagazine > magazineSize) CurrentMagazine = magazineSize;

            SwapViewModel(def.viewModelPrefab);
        }

        private void SwapViewModel(GameObject prefab)
        {
            if (attachedVisual != null && attachedVisual.UseAttachedWeapons)
            {
                if (viewModelInstance != null) Destroy(viewModelInstance);
                viewModelInstance = null;
                attachedVisual.RefreshPresentation();
                return;
            }

            if (viewModelInstance != null) Destroy(viewModelInstance);
            viewModelInstance = null;

            if (prefab == null || viewModelSocket == null) return;

            viewModelInstance = Instantiate(prefab, viewModelSocket);
            viewModelInstance.transform.localPosition = Vector3.zero;
            viewModelInstance.transform.localRotation = Quaternion.identity;
            viewModelInstance.SetActive(IsActive);
        }

        public void SetViewModelVisible(bool visible)
        {
            if (attachedVisual != null && attachedVisual.UseAttachedWeapons)
            {
                attachedVisual.RefreshPresentation();
                return;
            }

            if (viewModelInstance != null) viewModelInstance.SetActive(visible);
        }
    }
}
