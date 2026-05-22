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

        public int CurrentMagazine { get; private set; }
        public int ReserveAmmo => reserveAmmo;
        public bool IsReloading { get; private set; }
        public bool IsLocalPlayer { get => isLocalPlayer; set => isLocalPlayer = value; }

        /// <summary>주무기 슬롯이 활성일 때만 true. PlayerArsenal 이 토글.</summary>
        public bool IsActive { get; set; } = true;
        public WeaponDefinition CurrentDefinition { get; private set; }

        public event Action OnFired;
        public event Action OnReloadStart;
        public event Action OnReloadEnd;
        public event Action<GameObject, float> OnHit; // 맞춘 대상, 데미지

        private float nextFireTime;
        private float reloadEndTime;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
            if (kitEquipper == null) kitEquipper = GetComponent<KitEquipper>();
            if (throwableEquipper == null) throwableEquipper = GetComponent<ThrowableEquipper>();
            CurrentMagazine = magazineSize;
        }

        private void Update()
        {
            if (!isLocalPlayer) return;

            // 재장전 완료는 슬롯이 비활성(보조무기 사용 중)이어도 진행
            if (IsReloading && Time.time >= reloadEndTime) FinishReload();

            if (!IsActive) return; // 보조무기 활성 중이면 주무기 입력 무시

            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse == null) return;

            // 커서가 잠겨 있을 때만 입력 수신 (디버그 UI 클릭과 충돌 방지)
            if (Cursor.lockState != CursorLockMode.Locked) return;

            // 키트/투척 휠 선택 중이거나 장착 중이면 좌클릭은 그쪽 용도 — 사격 억제
            if (kitEquipper != null && (kitEquipper.IsSelecting || kitEquipper.IsKitEquipped)) return;
            if (throwableEquipper != null && (throwableEquipper.IsSelecting || throwableEquipper.IsThrowableEquipped)) return;

            bool wantsFire = isAutomatic ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;
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
            if (Physics.Raycast(ray, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
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
        }
    }
}
