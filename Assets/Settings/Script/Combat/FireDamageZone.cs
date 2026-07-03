using System.Collections;
using UnityEngine;
using ProjectM.Network;
using ProjectM.Player;

namespace ProjectM.Combat
{
    /// <summary>
    /// 서버 전용 화염 장판. duration 동안 tickInterval마다 반경 내 적에게 tickDamage를 적용한다.
    /// </summary>
    public class FireDamageZone : MonoBehaviour
    {
        public static void Spawn(
            Vector3 center,
            float radius,
            float duration,
            float tickInterval,
            float tickDamage,
            GameObject thrower,
            LayerMask hitMask)
        {
            if (duration <= 0f || tickInterval <= 0f || tickDamage <= 0f)
                return;

            if (NetworkSessionHelper.IsMultiplayerSession && !NetworkSessionHelper.IsServer)
                return;

            var go = new GameObject("FireDamageZone");
            go.transform.position = center;
            var zone = go.AddComponent<FireDamageZone>();
            zone.StartZone(radius, duration, tickInterval, tickDamage, thrower, hitMask);
        }

        private float radius;
        private float duration;
        private float tickInterval;
        private float tickDamage;
        private GameObject thrower;
        private LayerMask hitMask;

        private void StartZone(
            float radius,
            float duration,
            float tickInterval,
            float tickDamage,
            GameObject thrower,
            LayerMask hitMask)
        {
            this.radius = radius;
            this.duration = duration;
            this.tickInterval = tickInterval;
            this.tickDamage = tickDamage;
            this.thrower = thrower;
            this.hitMask = hitMask;
            StartCoroutine(TickRoutine());
        }

        private IEnumerator TickRoutine()
        {
            float endTime = Time.time + duration;
            float nextTickTime = Time.time + tickInterval;

            while (Time.time < endTime)
            {
                if (Time.time >= nextTickTime)
                {
                    ThrowableProjectile.ApplyDamageInRadius(
                        transform.position,
                        radius,
                        tickDamage,
                        thrower,
                        hitMask);
                    nextTickTime += tickInterval;
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
