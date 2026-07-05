using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectM.Defense;
using ProjectM.UI;

namespace ProjectM.Player
{
    /// <summary>
    /// 근접 무기(칼). 좌클릭으로 정면 부채꼴(AOE 슬래시) 범위 안 모든 적을 타격한다.
    /// WeaponDefinition(kind=Melee) 으로 수치가 주입된다. PlayerArsenal 이 IsActive 를 토글.
    /// 방어 오브젝트는 플레이어 공격으로 파괴 불가 (총과 동일 규칙).
    /// </summary>
    public class MeleeWeapon : MonoBehaviour
    {
        [Header("기준 카메라")]
        [SerializeField] private Camera viewCamera;

        [Header("비주얼 (1인칭 뷰모델)")]
        [Tooltip("카메라 자식의 빈 GameObject. 무기 모델이 이 곳의 자식으로 인스턴스화된다.")]
        [SerializeField] private Transform viewModelSocket;
        private GameObject viewModelInstance;

        [Header("스탯 (WeaponDefinition 으로 덮어씀)")]
        [SerializeField] private float damage = 15f;
        [SerializeField] private float attackInterval = 2.5f;
        [SerializeField] private float meleeRange = 2.5f;
        [SerializeField] private float meleeAngle = 100f;
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("연동")]
        [SerializeField] private bool isLocalPlayer = true;
        [SerializeField] private KitEquipper kitEquipper;
        [SerializeField] private ThrowableEquipper throwableEquipper;

        public bool IsLocalPlayer { get => isLocalPlayer; set => isLocalPlayer = value; }
        public bool IsActive { get; set; } = false;
        public WeaponDefinition CurrentDefinition { get; private set; }

        public Transform FirstPersonViewTransform =>
            viewModelInstance != null ? viewModelInstance.transform : null;

        public event Action OnAttack;
        public event Action<GameObject, float> OnHit;

        private float nextAttackTime;
        private ReviveSystem revive;
        private PlayerAttachedWeaponVisual attachedVisual;

        private void Awake()
        {
            ResolveViewCamera();
            if (kitEquipper == null) kitEquipper = GetComponent<KitEquipper>();
            if (throwableEquipper == null) throwableEquipper = GetComponent<ThrowableEquipper>();
            revive = GetComponent<ReviveSystem>();
            attachedVisual = GetComponent<PlayerAttachedWeaponVisual>();
        }

        private void ResolveViewCamera()
        {
            if (viewCamera == null)
                viewCamera = GetComponentInChildren<Camera>(true);
        }

        private void ResolveCombatRefs()
        {
            if (kitEquipper == null) kitEquipper = GetComponent<KitEquipper>();
            if (throwableEquipper == null) throwableEquipper = GetComponent<ThrowableEquipper>();
            if (revive == null) revive = GetComponent<ReviveSystem>();
        }

        public void ApplyDefinition(WeaponDefinition def)
        {
            if (def == null) return;
            CurrentDefinition = def;
            damage = def.damage;
            attackInterval = def.attackInterval;
            meleeRange = def.meleeRange;
            meleeAngle = def.meleeAngle;

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

        private void Update()
        {
            if (!isLocalPlayer || !IsActive) return;
            if (UIInputModal.IsBlockingGameplayInput) return;

            ResolveCombatRefs();
            if (revive != null && (revive.IsDown || revive.IsDead)) return;

            ResolveViewCamera();

            var mouse = Mouse.current;
            if (mouse == null) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;

            // 키트/투척 장착 중에는 좌클릭이 그쪽 용도 — 근접 공격 억제
            if (kitEquipper != null && (kitEquipper.IsSelecting || kitEquipper.IsKitEquipped)) return;
            if (throwableEquipper != null && (throwableEquipper.IsSelecting || throwableEquipper.IsThrowableEquipped)) return;

            if (mouse.leftButton.wasPressedThisFrame && Time.time >= nextAttackTime)
                Attack();
        }

        public bool CanAttack() => Time.time >= nextAttackTime;

        /// <summary>부채꼴 범위 내 모든 적에게 데미지(AOE 슬래시).</summary>
        public void Attack()
        {
            nextAttackTime = Time.time + Mathf.Max(0.1f, attackInterval);
            OnAttack?.Invoke();

            ResolveViewCamera();
            if (viewCamera == null) return;

            Vector3 origin = transform.position + Vector3.up * 1f;
            Vector3 fwd = viewCamera.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f)
                fwd = transform.forward;
            fwd.y = 0f;
            fwd.Normalize();

            var hitTargets = new HashSet<IDamageable>();
            TryRayAssistHit(origin, fwd, hitTargets);
            ApplyDamageToTargetsInArc(origin, fwd, hitTargets);
        }

        /// <summary>OverlapSphere + 부채꼴 필터로 범위 안 모든 IDamageable에 데미지.</summary>
        private void ApplyDamageToTargetsInArc(Vector3 origin, Vector3 fwd, HashSet<IDamageable> hitTargets)
        {
            var cols = Physics.OverlapSphere(origin, meleeRange, hitMask, QueryTriggerInteraction.Ignore);
            float halfAngle = meleeAngle * 0.5f;

            foreach (var c in cols)
            {
                if (c.transform.IsChildOf(transform)) continue;
                if (c.GetComponentInParent<DefenseObject>() != null) continue;

                var dmg = c.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive) continue;
                if (hitTargets.Contains(dmg)) continue;

                Vector3 to = c.bounds.center - origin; to.y = 0;
                if (Vector3.Angle(fwd, to) > halfAngle) continue;

                hitTargets.Add(dmg);
                dmg.TakeDamage(damage, gameObject);
                OnHit?.Invoke(c.gameObject, damage);
            }
        }

        /// <summary>카메라 정면 레이로 근접 miss 보완(OverlapSphere 전 1타 우선).</summary>
        private void TryRayAssistHit(Vector3 origin, Vector3 fwd, HashSet<IDamageable> hitTargets)
        {
            var ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            if (!PlayerRaycastUtility.TryRaycastFromView(transform, ray, out RaycastHit hit, meleeRange, hitMask))
                return;

            if (hit.collider.GetComponentInParent<DefenseObject>() != null) return;

            var dmg = hit.collider.GetComponentInParent<IDamageable>();
            if (dmg == null || !dmg.IsAlive) return;

            Vector3 to = hit.collider.bounds.center - origin; to.y = 0;
            if (Vector3.Angle(fwd, to) > meleeAngle * 0.5f) return;

            hitTargets.Add(dmg);
            dmg.TakeDamage(damage, gameObject);
            OnHit?.Invoke(hit.collider.gameObject, damage);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (viewCamera == null)
                viewCamera = GetComponentInChildren<Camera>(true);

            Vector3 origin = transform.position + Vector3.up * 1f;
            Vector3 fwd = viewCamera != null ? viewCamera.transform.forward : transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = transform.forward;
            fwd.Normalize();

            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(origin, meleeRange);

            float halfAngle = meleeAngle * 0.5f;
            Vector3 left = Quaternion.AngleAxis(-halfAngle, Vector3.up) * fwd * meleeRange;
            Vector3 right = Quaternion.AngleAxis(halfAngle, Vector3.up) * fwd * meleeRange;
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
            Gizmos.DrawLine(origin, origin + left);
            Gizmos.DrawLine(origin, origin + right);
            Gizmos.DrawLine(origin, origin + fwd * meleeRange);
        }
#endif
    }
}
