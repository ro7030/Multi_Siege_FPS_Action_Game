using UnityEngine;

namespace ProjectM.Player
{
    /// <summary>
    /// 서버/로컬 공통 투척무기 투사체 생성.
    /// </summary>
    public static class ThrowableSpawner
    {
        public static void SpawnProjectile(
            ThrowableDefinition def,
            GameObject thrower,
            Vector3 origin,
            Vector3 velocity)
        {
            if (def == null || thrower == null) return;

            GameObject go = def.projectilePrefab != null
                ? Object.Instantiate(def.projectilePrefab, origin, Quaternion.identity)
                : CreateDefaultProjectile(origin);

            var proj = go.GetComponent<ThrowableProjectile>();
            if (proj == null) proj = go.AddComponent<ThrowableProjectile>();
            proj.Launch(def, thrower, velocity);
        }

        private static GameObject CreateDefaultProjectile(Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.3f;
            if (go.GetComponent<Rigidbody>() == null) go.AddComponent<Rigidbody>();
            return go;
        }
    }
}
