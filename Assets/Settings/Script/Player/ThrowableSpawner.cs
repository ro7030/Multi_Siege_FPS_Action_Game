using Unity.Netcode;
using UnityEngine;
using ProjectM.Network;

namespace ProjectM.Player
{
    /// <summary>
    /// 서버/로컬 공통 투척무기 투사체 생성.
    /// </summary>
    public static class ThrowableSpawner
    {
        public static bool SpawnProjectile(
            ThrowableDefinition def,
            GameObject thrower,
            Vector3 origin,
            Vector3 velocity)
        {
            if (def == null || thrower == null) return false;

            GameObject go = def.projectilePrefab != null
                ? Object.Instantiate(def.projectilePrefab, origin, Quaternion.identity)
                : CreateDefaultProjectile(origin);

            if (go.GetComponent<ThrowableProjectile>() == null)
                go.AddComponent<ThrowableProjectile>();

            if (NetworkSessionHelper.IsMultiplayerSession && NetworkSessionHelper.IsServer)
            {
                var netObj = go.GetComponent<NetworkObject>();
                var netProjectile = go.GetComponent<NetworkThrowableProjectile>();
                if (netObj == null || netProjectile == null)
                {
                    Debug.LogError(
                        $"[ThrowableSpawner] NGO 투척 prefab에 NetworkObject/NetworkThrowableProjectile 필요: {def.displayName}");
                    Object.Destroy(go);
                    return false;
                }

                netProjectile.PrepareServerLaunch(def, thrower, velocity);
                netObj.Spawn();
                return true;
            }

            go.GetComponent<ThrowableProjectile>().Launch(def, thrower, velocity);
            return true;
        }

        private static GameObject CreateDefaultProjectile(Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.3f;
            if (go.GetComponent<Rigidbody>() == null)
                go.AddComponent<Rigidbody>();
            return go;
        }
    }
}
