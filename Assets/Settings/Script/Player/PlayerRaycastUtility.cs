using System;
using UnityEngine;

namespace ProjectM.Player
{
    // 1인칭 레이캐스트 공통 처리. 자기 CharacterController/비주얼 히트를 건너뛴다.
    public static class PlayerRaycastUtility
    {
        private const float DefaultOriginForwardOffset = 0.1f;

        public static bool TryRaycastFromView(
            Transform shooterRoot,
            Ray ray,
            out RaycastHit hit,
            float maxDistance,
            LayerMask mask,
            float originForwardOffset = DefaultOriginForwardOffset)
        {
            hit = default;
            if (originForwardOffset > 0f)
                ray.origin += ray.direction * originForwardOffset;

            var hits = Physics.RaycastAll(ray, maxDistance, mask, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0)
                return false;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            Transform root = shooterRoot != null ? shooterRoot.root : null;
            foreach (var candidate in hits)
            {
                if (root != null && candidate.collider.transform.root == root)
                    continue;

                hit = candidate;
                return true;
            }

            return false;
        }
    }
}
