#if UNITY_EDITOR
using System.IO;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectM.Network;

namespace ProjectM.Network.Editor
{
    /// <summary>
    /// Phase 1: NetworkPlayer 프리팹, 중앙 스폰 포인트, GamePlay NGO 스폰 설정.
    /// 메뉴: ProjectM/Setup Phase 1 Network Player
    /// </summary>
    public static class Phase1NetworkPlayerSetup
    {
        private const string PrefabPath = "Assets/Prefab/Network/NetworkPlayer.prefab";
        private const string NetworkManagerPrefabPath = "Assets/Prefab/Network/NetworkManager.prefab";
        private const string DefaultNetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";
        private const string GamePlayScenePath = "Assets/Scenes/GamePlay.unity";
        private const string SpawnRootName = "PlayerSpawnRoot";
        private static readonly Vector3 MapCenter = new(0f, 0.5f, 0f);
        private const float SpawnRadius = 3f;

        [MenuItem("ProjectM/Setup Phase 1 Network Player")]
        public static void Setup()
        {
            if (SceneManager.GetActiveScene().path != GamePlayScenePath)
                EditorSceneManager.OpenScene(GamePlayScenePath);

            GameObject player = FindOrCreateOfflinePlayer();
            if (player == null)
            {
                Debug.LogError("[Phase1] 오프라인 Player 준비 실패");
                return;
            }

            EnsureNetworkComponents(player);
            var prefab = SavePlayerPrefab(player);
            if (prefab == null) return;

            RegisterDefaultNetworkPrefab(prefab);
            ConfigureNetworkManagerPrefab();
            Transform[] spawnPoints = CreatePlayerSpawnPoints();
            SetupGamePlayScene(prefab, player, spawnPoints);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("[Phase1] NetworkPlayer + 중앙 스폰 포인트 설정 완료");
        }

        private static GameObject FindOrCreateOfflinePlayer()
        {
            foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
            {
                if (go.GetComponent<NetworkObject>() == null)
                    return go;
            }

            var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existingPrefab == null)
                return null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(existingPrefab);
            instance.name = "OfflinePlayer";
            instance.tag = "Player";
            RemoveNetworkComponents(instance);
            return instance;
        }

        private static void RemoveNetworkComponents(GameObject go)
        {
            var netObj = go.GetComponent<NetworkObject>();
            if (netObj != null) Object.DestroyImmediate(netObj);
            var np = go.GetComponent<NetworkPlayer>();
            if (np != null) Object.DestroyImmediate(np);
            var nt = go.GetComponent<OwnerNetworkTransform>();
            if (nt != null) Object.DestroyImmediate(nt);
        }

        private static void EnsureNetworkComponents(GameObject player)
        {
            if (player.GetComponent<NetworkObject>() == null)
                player.AddComponent<NetworkObject>();
            if (player.GetComponent<OwnerNetworkTransform>() == null)
                player.AddComponent<OwnerNetworkTransform>();
            if (player.GetComponent<NetworkPlayer>() == null)
                player.AddComponent<NetworkPlayer>();
            if (player.GetComponent<ProjectM.Player.PlayerAnimationController>() == null)
                player.AddComponent<ProjectM.Player.PlayerAnimationController>();
            if (player.GetComponent<NetworkPlayerAnimationBridge>() == null)
                player.AddComponent<NetworkPlayerAnimationBridge>();
        }

        private static GameObject SavePlayerPrefab(GameObject player)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath) ?? "Assets/Prefab/Network");

            var temp = Object.Instantiate(player);
            temp.name = "NetworkPlayer";

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
            else
                PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);

            Object.DestroyImmediate(temp);
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        private static void RegisterDefaultNetworkPrefab(GameObject prefab)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(DefaultNetworkPrefabsPath);
            if (list == null) return;

            foreach (var entry in list.PrefabList)
            {
                if (entry.Prefab == prefab) return;
            }

            list.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(list);
        }

        private static void ConfigureNetworkManagerPrefab()
        {
            var nmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkManagerPrefabPath);
            if (nmPrefab == null) return;

            var nm = nmPrefab.GetComponent<NetworkManager>();
            if (nm == null) return;

            // CharacterSelect 등에서 자동 스폰 방지 — GamePlay Spawner만 사용
            nm.NetworkConfig.PlayerPrefab = null;
            nm.NetworkConfig.AutoSpawnPlayerPrefabClientSide = false;

            if (nmPrefab.GetComponent<NetworkPlayerSessionGuard>() == null)
                nmPrefab.AddComponent<NetworkPlayerSessionGuard>();

            EditorUtility.SetDirty(nmPrefab);
        }

        private static Transform[] CreatePlayerSpawnPoints()
        {
            var oldRoot = GameObject.Find(SpawnRootName);
            if (oldRoot != null)
                Object.DestroyImmediate(oldRoot);

            var root = new GameObject(SpawnRootName);
            root.transform.position = MapCenter;

            Vector3[] offsets =
            {
                new(0f, 0f, SpawnRadius),
                new(SpawnRadius, 0f, 0f),
                new(0f, 0f, -SpawnRadius),
                new(-SpawnRadius, 0f, 0f),
            };

            var points = new Transform[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                var go = new GameObject($"PlayerSpawn_{i + 1}");
                go.transform.SetParent(root.transform, false);
                go.transform.position = MapCenter + offsets[i];
                points[i] = go.transform;
            }

            return points;
        }

        private static void SetupGamePlayScene(GameObject networkPlayerPrefab, GameObject scenePlayer, Transform[] spawnPoints)
        {
            var root = GameObject.Find("NetworkGameplay") ?? new GameObject("NetworkGameplay");
            if (root.GetComponent<NetworkObject>() == null)
                root.AddComponent<NetworkObject>();

            var spawner = root.GetComponent<NetworkPlayerSpawner>() ?? root.AddComponent<NetworkPlayerSpawner>();
            var so = new SerializedObject(spawner);
            so.FindProperty("networkPlayerPrefab").objectReferenceValue = networkPlayerPrefab;
            so.FindProperty("offlineScenePlayer").objectReferenceValue = scenePlayer;
            so.FindProperty("playerSpawnPoints").arraySize = spawnPoints.Length;
            for (int i = 0; i < spawnPoints.Length; i++)
                so.FindProperty("playerSpawnPoints").GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            if (spawnPoints.Length > 0)
                scenePlayer.transform.SetPositionAndRotation(spawnPoints[0].position, spawnPoints[0].rotation);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
#endif
