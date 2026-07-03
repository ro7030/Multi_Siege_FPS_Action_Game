using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ProjectM.Combat;
using ProjectM.Defense;
using ProjectM.Network;

namespace ProjectM.Player
{
    /// <summary>
    /// 던져진 투척무기. fuseTime 후(또는 충돌 시) 폭발하여 반경 내 적에게 효과를 적용한다.
    /// 방어물/플레이어/던진 사람은 피해 대상에서 제외 (적만 타격).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ThrowableProjectile : MonoBehaviour
    {
        [SerializeField] private bool explodeOnContact = false;
        [SerializeField] private bool explodeOnGroundContact;
        [SerializeField] private float groundContactNormalMinY = 0.65f;
        [SerializeField] private LayerMask hitMask = ~0;
        [Tooltip("폭발 시 재생할 VFX 프리팹 (프리팹 레벨 — 종류별로 다르게 지정. 예: 섬광탄만 지정).")]
        [SerializeField] private GameObject explosionVfxPrefab;
        [SerializeField] private float explosionVfxLifetime = 2f;
        [Tooltip("scale=1일 때 VFX가 대략 커버하는 게임플레이 반경(m). 0이면 스케일 조정 안 함.")]
        [SerializeField] private float explosionVfxReferenceRadius;
        [Tooltip("Instantiate 후 적용할 추가 회전(도).")]
        [SerializeField] private Vector3 explosionVfxRotationEuler;

        /// <summary>Explode()가 실제로 실행될 때(피해 판정 권한이 있는 쪽) 1회 발생 — NGO 게스트 가시성 브리지용.</summary>
        public event Action OnExploded;

        private ThrowableDefinition def;
        private GameObject thrower;
        private Rigidbody rb;
        private bool exploded;
        private bool serverOnlyExplosion;
        private Coroutine throwerIgnoreRoutine;

        public void Launch(
            ThrowableDefinition definition,
            GameObject owner,
            Vector3 velocity,
            bool serverOnlyExplosion = false)
        {
            def = definition;
            thrower = owner;
            this.serverOnlyExplosion = serverOnlyExplosion;
            rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();

            if (serverOnlyExplosion && !NetworkSessionHelper.IsServer)
                return;

            BeginThrowerCollisionIgnore();
            ApplyVelocity(velocity);

            if (def != null && def.fuseTime > 0f)
                Invoke(nameof(Explode), def.fuseTime);
        }

        private void ApplyVelocity(Vector3 velocity)
        {
            if (rb == null)
                return;

            rb.WakeUp();
            rb.linearVelocity = velocity;
        }

        private void BeginThrowerCollisionIgnore()
        {
            if (thrower == null)
                return;

            if (throwerIgnoreRoutine != null)
                StopCoroutine(throwerIgnoreRoutine);

            throwerIgnoreRoutine = StartCoroutine(IgnoreThrowerCollisionsTemporary(0.35f));
        }

        private System.Collections.IEnumerator IgnoreThrowerCollisionsTemporary(float duration)
        {
            var selfColliders = GetComponentsInChildren<Collider>();
            var throwerColliders = thrower.GetComponentsInChildren<Collider>();
            for (int i = 0; i < selfColliders.Length; i++)
            {
                for (int j = 0; j < throwerColliders.Length; j++)
                {
                    if (selfColliders[i] == null || throwerColliders[j] == null) continue;
                    Physics.IgnoreCollision(selfColliders[i], throwerColliders[j], true);
                }
            }

            yield return new WaitForSeconds(duration);

            for (int i = 0; i < selfColliders.Length; i++)
            {
                for (int j = 0; j < throwerColliders.Length; j++)
                {
                    if (selfColliders[i] == null || throwerColliders[j] == null) continue;
                    Physics.IgnoreCollision(selfColliders[i], throwerColliders[j], false);
                }
            }

            throwerIgnoreRoutine = null;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (explodeOnGroundContact && IsGroundCollision(collision))
            {
                CancelInvoke(nameof(Explode));
                Explode();
                return;
            }

            if (explodeOnContact)
                Explode();
        }

        private bool IsGroundCollision(Collision collision)
        {
            if (collision == null || collision.contactCount == 0)
                return false;

            for (int i = 0; i < collision.contactCount; i++)
            {
                if (collision.GetContact(i).normal.y >= groundContactNormalMinY)
                    return true;
            }

            return false;
        }

        private void StopProjectileMotion()
        {
            if (rb == null)
                rb = GetComponent<Rigidbody>();
            if (rb == null)
                return;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        private void Explode()
        {
            if (exploded)
                return;

            if (serverOnlyExplosion && !NetworkSessionHelper.IsServer)
                return;

            if (def == null)
            {
                DestroySelf();
                return;
            }

            exploded = true;
            StopProjectileMotion();
            SpawnExplosionVfx();

            var center = transform.position;

            if (def.effect == ThrowableEffect.Stun)
            {
                ApplyStunInRadius(center, def.radius, def.effectDuration, thrower, hitMask);
            }
            else if (def.effect == ThrowableEffect.Fire)
            {
                ApplyDamageInRadius(center, def.radius, def.damage, thrower, hitMask);
                FireDamageZone.Spawn(
                    center,
                    def.radius,
                    def.effectDuration,
                    def.fireTickInterval,
                    def.fireTickDamage,
                    thrower,
                    hitMask);
            }
            else if (def.damage > 0f)
            {
                ApplyDamageInRadius(center, def.radius, def.damage, thrower, hitMask);
            }

            OnExploded?.Invoke();
            DestroySelf();
        }

        public static void ApplyDamageInRadius(
            Vector3 center,
            float radius,
            float damageAmount,
            GameObject thrower,
            LayerMask hitMask)
        {
            if (damageAmount <= 0f)
                return;

            var cols = Physics.OverlapSphere(center, radius, hitMask, QueryTriggerInteraction.Ignore);
            var hitDamageable = new HashSet<IDamageable>();

            foreach (var c in cols)
            {
                if (thrower != null && c.transform.IsChildOf(thrower.transform)) continue;
                if (c.GetComponentInParent<DefenseObject>() != null) continue;
                if (c.GetComponentInParent<PlayerController>() != null) continue;

                var dmg = c.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive || hitDamageable.Contains(dmg)) continue;
                hitDamageable.Add(dmg);
                dmg.TakeDamage(damageAmount, thrower);
            }
        }

        private static void ApplyStunInRadius(
            Vector3 center,
            float radius,
            float duration,
            GameObject thrower,
            LayerMask hitMask)
        {
            if (duration <= 0f)
                return;

            var cols = Physics.OverlapSphere(center, radius, hitMask, QueryTriggerInteraction.Ignore);
            var hitStunnable = new HashSet<IStunnable>();

            foreach (var c in cols)
            {
                if (thrower != null && c.transform.IsChildOf(thrower.transform)) continue;
                if (c.GetComponentInParent<DefenseObject>() != null) continue;
                if (c.GetComponentInParent<PlayerController>() != null) continue;

                var stunnable = c.GetComponentInParent<IStunnable>();
                if (stunnable == null || hitStunnable.Contains(stunnable)) continue;
                hitStunnable.Add(stunnable);
                stunnable.ApplyStun(duration, thrower);
            }
        }

        /// <summary>로컬에서 폭발 VFX만 재생한다(피해 판정 없음). NGO 게스트 클라이언트의 ClientRpc 수신 측에서 사용.</summary>
        public void PlayExplosionVfxOnly()
        {
            SpawnExplosionVfx();
        }

        private void SpawnExplosionVfx()
        {
            if (explosionVfxPrefab == null)
                return;

            var rot = Quaternion.Euler(explosionVfxRotationEuler);
            var vfx = Instantiate(explosionVfxPrefab, transform.position, rot);

            if (def != null && explosionVfxReferenceRadius > 0f)
            {
                float scale = def.radius / explosionVfxReferenceRadius;
                vfx.transform.localScale = Vector3.one * scale;
            }

            Destroy(vfx, explosionVfxLifetime);
        }

        private void DestroySelf()
        {
            if (TryGetComponent<NetworkObject>(out var netObj) && netObj.IsSpawned)
            {
                if (NetworkSessionHelper.IsServer)
                    netObj.Despawn(true);
                return;
            }

            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            if (def == null) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, def.radius);
        }
    }
}
