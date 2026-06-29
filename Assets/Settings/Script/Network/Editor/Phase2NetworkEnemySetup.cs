#if UNITY_EDITOR
using System.IO;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectM.Core;
using ProjectM.Network;
using ProjectM.Wave;

namespace ProjectM.Network.Editor
{
    /// <summary>
    /// Phase 2: 적 프리팹 NGO 등록 + GamePlay NetworkMatchDirector 연결.
    /// 메뉴: ProjectM/Setup Phase 2 Network Enemies
    /// </summary>
    public static class Phase2NetworkEnemySetup
    {
        private const string DefaultNetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";
        private const string GamePlayScenePath = "Assets/Scenes/GamePlay.unity";

        private static readonly string[] EnemyPrefabPaths =
        {
            "Assets/Prefab/Enemy/Enemy_Normal.prefab",
            "Assets/Prefab/Enemy/Enemy_Runner.prefab",
            "Assets/Prefab/Enemy/Enemy_DPS.prefab",
            "Assets/Prefab/Enemy/Enemy_Tank.prefab",
            "Assets/Prefab/Enemy/Enemy_Boss.prefab",
        };

        [MenuItem("ProjectM/Setup Phase 2 Network Enemies")]
        public static void Setup()
        {
            foreach (string path in EnemyPrefabPaths)
                ConfigureEnemyPrefab(path);

            if (SceneManager.GetActiveScene().path != GamePlayScenePath)
                EditorSceneManager.OpenScene(GamePlayScenePath);

            SetupNetworkMatchDirector();

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("[Phase2] NetworkEnemy 프리팹 + NetworkMatchDirector 설정 완료");
        }

        private static void ConfigureEnemyPrefab(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[Phase2] 프리팹 없음: {path}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            EnsureEnemyNetworkComponents(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            RegisterDefaultNetworkPrefab(prefab);
        }

        private static void EnsureEnemyNetworkComponents(GameObject go)
        {
            if (go.GetComponent<NetworkObject>() == null)
                go.AddComponent<NetworkObject>();

            if (go.GetComponent<ServerNetworkTransform>() == null)
                go.AddComponent<ServerNetworkTransform>();

            if (go.GetComponent<NetworkEnemy>() == null)
                go.AddComponent<NetworkEnemy>();

            if (go.GetComponent<NetworkDamageBridge>() == null)
                go.AddComponent<NetworkDamageBridge>();
        }

        private static void RegisterDefaultNetworkPrefab(GameObject prefab)
        {
            if (prefab == null) return;

            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(DefaultNetworkPrefabsPath);
            if (list == null) return;

            foreach (var entry in list.PrefabList)
            {
                if (entry.Prefab == prefab) return;
            }

            list.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(list);
        }

        private static void SetupNetworkMatchDirector()
        {
            var root = GameObject.Find("NetworkGameplay");
            if (root == null)
            {
                Debug.LogError("[Phase2] NetworkGameplay 오브젝트를 찾을 수 없습니다. Phase 1 Setup을 먼저 실행하세요.");
                return;
            }

            var director = root.GetComponent<NetworkMatchDirector>() ?? root.AddComponent<NetworkMatchDirector>();
            var session = Object.FindAnyObjectByType<GameSessionManager>();
            var bootstrapper = Object.FindAnyObjectByType<MatchBootstrapper>();
            var waveManager = Object.FindAnyObjectByType<WaveManager>();

            var so = new SerializedObject(director);
            so.FindProperty("session").objectReferenceValue = session;
            so.FindProperty("bootstrapper").objectReferenceValue = bootstrapper;
            so.FindProperty("waveManager").objectReferenceValue = waveManager;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
#endif
