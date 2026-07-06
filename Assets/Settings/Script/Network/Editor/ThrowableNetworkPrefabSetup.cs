#if UNITY_EDITOR
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using ProjectM.Network;
using ProjectM.Player;

namespace ProjectM.Network.Editor
{
    // 투척 투사체 prefab에 NGO 컴포넌트를 추가하고 DefaultNetworkPrefabs에 등록한다.
    public static class ThrowableNetworkPrefabSetup
    {
        private const string DefaultNetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";

        private static readonly string[] ThrowablePrefabPaths =
        {
            "Assets/Prefab/Weapon/Throwable/Grenade.prefab",
            "Assets/Prefab/Weapon/Throwable/Flash.prefab",
            "Assets/Prefab/Weapon/Throwable/Molotov.prefab",
        };

        [MenuItem("ProjectM/Network/Setup Throwable NGO Prefabs")]
        public static void SetupAll()
        {
            int updated = 0;
            foreach (var path in ThrowablePrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogWarning($"[ThrowableNetworkPrefabSetup] prefab 없음: {path}");
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(path);
                if (EnsureNetworkComponents(root))
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    RegisterDefaultNetworkPrefab(root);
                    updated++;
                    Debug.Log($"[ThrowableNetworkPrefabSetup] 적용: {path}");
                }

                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ThrowableNetworkPrefabSetup] 완료 — {updated}개 prefab");
        }

        private static bool EnsureNetworkComponents(GameObject root)
        {
            bool changed = false;

            if (root.GetComponent<Rigidbody>() == null)
            {
                var rb = root.AddComponent<Rigidbody>();
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                changed = true;
            }
            else
            {
                var rb = root.GetComponent<Rigidbody>();
                if (!rb.useGravity || rb.collisionDetectionMode != CollisionDetectionMode.ContinuousDynamic)
                {
                    rb.useGravity = true;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    changed = true;
                }
            }

            if (root.GetComponent<ThrowableProjectile>() == null)
            {
                root.AddComponent<ThrowableProjectile>();
                changed = true;
            }

            if (root.GetComponent<NetworkObject>() == null)
            {
                root.AddComponent<NetworkObject>();
                changed = true;
            }

            var netObj = root.GetComponent<NetworkObject>();
            if (netObj != null && netObj.SynchronizeTransform)
            {
                netObj.SynchronizeTransform = false;
                changed = true;
            }

            if (root.GetComponent<NetworkThrowableProjectile>() == null)
            {
                root.AddComponent<NetworkThrowableProjectile>();
                changed = true;
            }

            if (root.GetComponent<ServerNetworkTransform>() == null)
            {
                root.AddComponent<ServerNetworkTransform>();
                changed = true;
            }

            return changed;
        }

        private static void RegisterDefaultNetworkPrefab(GameObject prefab)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(DefaultNetworkPrefabsPath);
            if (list == null)
            {
                Debug.LogWarning($"[ThrowableNetworkPrefabSetup] NetworkPrefabsList 없음: {DefaultNetworkPrefabsPath}");
                return;
            }

            foreach (var entry in list.PrefabList)
            {
                if (entry.Prefab == prefab)
                    return;
            }

            list.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(list);
        }
    }
}
#endif
