using UnityEngine;
using ProjectM.Defense;

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
        [SerializeField] private LayerMask hitMask = ~0;

        private ThrowableDefinition def;
        private GameObject thrower;
        private Rigidbody rb;
        private bool exploded;

        public void Launch(ThrowableDefinition definition, GameObject owner, Vector3 velocity)
        {
            def = definition;
            thrower = owner;
            rb = GetComponent<Rigidbody>();
            rb.linearVelocity = velocity;

            if (def != null && def.fuseTime > 0f)
                Invoke(nameof(Explode), def.fuseTime);
        }

        private void OnCollisionEnter(Collision _)
        {
            if (explodeOnContact) Explode();
        }

        private void Explode()
        {
            if (exploded || def == null) { if (def == null) Destroy(gameObject); return; }
            exploded = true;

            var cols = Physics.OverlapSphere(transform.position, def.radius, hitMask, QueryTriggerInteraction.Ignore);
            var hit = new System.Collections.Generic.HashSet<IDamageable>();

            foreach (var c in cols)
            {
                if (thrower != null && c.transform.IsChildOf(thrower.transform)) continue; // 던진 사람 제외
                if (c.GetComponentInParent<DefenseObject>() != null) continue;              // 방어물 제외
                if (c.GetComponentInParent<PlayerController>() != null) continue;            // 플레이어(아군) 제외

                var dmg = c.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive || hit.Contains(dmg)) continue;
                hit.Add(dmg);

                // MVP: 효과별 분기 — Damage/Fire 는 피해, Stun(섬광)은 피해 0 (스턴은 추후 EnemyAI 지원 시)
                if (def.effect != ThrowableEffect.Stun && def.damage > 0f)
                    dmg.TakeDamage(def.damage, thrower);
            }

            // TODO: 화염 지속존(Molotov), 스턴(Flash) 효과 + 폭발 VFX 는 추후 확장
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
