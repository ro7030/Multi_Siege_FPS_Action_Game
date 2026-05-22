using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectM.Defense;

namespace ProjectM.Player
{
    /// <summary>
    /// 근접 무기(칼). 좌클릭으로 정면 부채꼴 범위 안 가장 가까운 적을 타격한다.
    /// WeaponDefinition(kind=Melee) 으로 수치가 주입된다. PlayerArsenal 이 IsActive 를 토글.
    /// 방어 오브젝트는 플레이어 공격으로 파괴 불가 (총과 동일 규칙).
    /// </summary>
    public class MeleeWeapon : MonoBehaviour
    {
        [Header("기준 카메라")]
        [SerializeField] private Camera viewCamera;

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

        public bool IsActive { get; set; } = false;
        public WeaponDefinition CurrentDefinition { get; private set; }

        public event Action OnAttack;
        public event Action<GameObject, float> OnHit;

        private float nextAttackTime;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
            if (kitEquipper == null) kitEquipper = GetComponent<KitEquipper>();
            if (throwableEquipper == null) throwableEquipper = GetComponent<ThrowableEquipper>();
        }

        public void ApplyDefinition(WeaponDefinition def)
        {
            if (def == null) return;
            CurrentDefinition = def;
            damage = def.damage;
            attackInterval = def.attackInterval;
            meleeRange = def.meleeRange;
            meleeAngle = def.meleeAngle;
        }

        private void Update()
        {
            if (!isLocalPlayer || !IsActive) return;

            var mouse = Mouse.current;
            if (mouse == null) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;

            // 키트/투척 휠·장착 중에는 좌클릭이 그쪽 용도 — 근접 공격 억제
            if (kitEquipper != null && (kitEquipper.IsSelecting || kitEquipper.IsKitEquipped)) return;
            if (throwableEquipper != null && (throwableEquipper.IsSelecting || throwableEquipper.IsThrowableEquipped)) return;

            if (mouse.leftButton.wasPressedThisFrame && Time.time >= nextAttackTime)
                Attack();
        }

        public bool CanAttack() => Time.time >= nextAttackTime;

        public void Attack()
        {
            nextAttackTime = Time.time + Mathf.Max(0.1f, attackInterval);
            OnAttack?.Invoke();
            if (viewCamera == null) return;

            Vector3 origin = transform.position + Vector3.up * 1f;
            Vector3 fwd = viewCamera.transform.forward; fwd.y = 0; fwd.Normalize();

            var cols = Physics.OverlapSphere(origin, meleeRange, hitMask, QueryTriggerInteraction.Ignore);

            // 같은 대상이 여러 콜라이더로 잡히는 중복 타격 방지
            var hitTargets = new HashSet<IDamageable>();

            foreach (var c in cols)
            {
                if (c.transform.IsChildOf(transform)) continue;          // 자기 자신 제외
                if (c.GetComponentInParent<DefenseObject>() != null) continue; // 방어물 제외

                var dmg = c.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive) continue;
                if (hitTargets.Contains(dmg)) continue;                  // 이미 이번 스윙에 맞은 대상

                Vector3 to = c.bounds.center - origin; to.y = 0;
                if (Vector3.Angle(fwd, to) > meleeAngle * 0.5f) continue; // 부채꼴 밖 제외

                hitTargets.Add(dmg);
                dmg.TakeDamage(damage, gameObject);
                OnHit?.Invoke(c.gameObject, damage);
            }
        }
    }
}
