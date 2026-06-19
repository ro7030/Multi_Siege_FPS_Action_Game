#if UNITY_EDITOR
using System.IO;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectM.Defense;
using ProjectM.Economy;
using ProjectM.Network;

namespace ProjectM.Network.Editor
{
    /// <summary>
    /// Phase 4: 방어/밭/게이트/지갑 NGO 동기화 컴포넌트 연결.
    /// 메뉴: ProjectM/Setup Phase 4 Network Defense & Economy
    /// </summary>
    public static class Phase4NetworkDefenseSetup
    {
        private const string DefaultNetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";
        private const string GamePlayScenePath = "Assets/Scenes/GamePlay.unity";
        private const string FarmPrefabPath = "Assets/Prefab/Farm/Farm_01.prefab";
        private const string NetworkPlayerPrefabPath = "Assets/Prefab/Network/NetworkPlayer.prefab";

        [MenuItem("ProjectM/Setup Phase 4 Network Defense & Economy")]
        public static void Setup()
        {
            RemoveLegacyRepairZones();
            ConfigureFarmPrefab();
            ConfigureNetworkPlayerWallet();
            SetupGamePlayScene();

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("[Phase4] 방어/밭/게이트/지갑 NGO 설정 완료");
        }

        [MenuItem("ProjectM/Remove Defense Repair Zones")]
        public static void RemoveLegacyRepairZonesMenu()
        {
            if (SceneManager.GetActiveScene().path != GamePlayScenePath)
                EditorSceneManager.OpenScene(GamePlayScenePath);

            RemoveLegacyRepairZones();
            StripRepairZonesFromFarmPrefab();

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("[Phase4] E키 수리용 RepairZone 제거 완료");
        }

        private static void RemoveLegacyRepairZones()
        {
            var zones = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            int removed = 0;
            foreach (var t in zones)
            {
                if (t == null || t.name != "RepairZone") continue;
                Object.DestroyImmediate(t.gameObject);
                removed++;
            }

            if (removed > 0)
                Debug.Log($"[Phase4] GamePlay RepairZone {removed}개 제거");
        }

        private static void StripRepairZonesFromFarmPrefab()
        {
            if (!File.Exists(FarmPrefabPath)) return;

            var root = PrefabUtility.LoadPrefabContents(FarmPrefabPath);
            int removed = RemoveNamedChildren(root.transform, "InteractZone", "RepairZone");
            if (removed > 0)
                Debug.Log($"[Phase4] Farm_01 레거시 수리 존 {removed}개 제거");

            PrefabUtility.SaveAsPrefabAsset(root, FarmPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static int RemoveNamedChildren(Transform parent, params string[] names)
        {
            int removed = 0;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                foreach (var name in names)
                {
                    if (child.name != name) continue;
                    Object.DestroyImmediate(child.gameObject);
                    removed++;
                    break;
                }
            }

            return removed;
        }

        private static void ConfigureFarmPrefab()
        {
            if (!File.Exists(FarmPrefabPath))
            {
                Debug.LogWarning($"[Phase4] 프리팹 없음: {FarmPrefabPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(FarmPrefabPath);
            RemoveNamedChildren(root.transform, "InteractZone", "RepairZone");
            EnsureDefenseNetworkComponents(root);
            if (root.GetComponent<NetworkFarmBridge>() == null)
                root.AddComponent<NetworkFarmBridge>();

            PrefabUtility.SaveAsPrefabAsset(root, FarmPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FarmPrefabPath);
            RegisterDefaultNetworkPrefab(prefab);
        }

        private static void ConfigureNetworkPlayerWallet()
        {
            var root = PrefabUtility.LoadPrefabContents(NetworkPlayerPrefabPath);
            if (root.GetComponent<NetworkCurrencyWallet>() == null)
                root.AddComponent<NetworkCurrencyWallet>();

            PrefabUtility.SaveAsPrefabAsset(root, NetworkPlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void SetupGamePlayScene()
        {
            if (SceneManager.GetActiveScene().path != GamePlayScenePath)
                EditorSceneManager.OpenScene(GamePlayScenePath);

            RemoveLegacyRepairZones();
            SetupNetworkGameplayBridge();
            SetupSceneDefenses();
            SetupGateInstallers();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static void SetupNetworkGameplayBridge()
        {
            var root = GameObject.Find("NetworkGameplay");
            if (root == null)
            {
                Debug.LogError("[Phase4] NetworkGameplay 없음. Phase 1 Setup을 먼저 실행하세요.");
                return;
            }

            if (root.GetComponent<NetworkObject>() == null)
                root.AddComponent<NetworkObject>();

            if (root.GetComponent<NetworkFarmManagerBridge>() == null)
                root.AddComponent<NetworkFarmManagerBridge>();
        }

        private static void SetupSceneDefenses()
        {
            foreach (var defense in Object.FindObjectsByType<DefenseObject>(FindObjectsSortMode.None))
            {
                if (defense == null) continue;
                EnsureDefenseNetworkComponents(defense.gameObject);
            }

            foreach (var plot in Object.FindObjectsByType<FarmPlot>(FindObjectsSortMode.None))
            {
                if (plot == null) continue;
                EnsureDefenseNetworkComponents(plot.gameObject);
                if (plot.GetComponent<NetworkFarmBridge>() == null)
                    plot.gameObject.AddComponent<NetworkFarmBridge>();
            }
        }

        private static void SetupGateInstallers()
        {
            foreach (var installer in Object.FindObjectsByType<GateInstaller>(FindObjectsSortMode.None))
            {
                if (installer == null) continue;

                if (installer.GetComponent<NetworkObject>() == null)
                    installer.gameObject.AddComponent<NetworkObject>();

                if (installer.GetComponent<NetworkGateInstaller>() == null)
                    installer.gameObject.AddComponent<NetworkGateInstaller>();
            }
        }

        private static void EnsureDefenseNetworkComponents(GameObject go)
        {
            if (go.GetComponent<NetworkObject>() == null)
                go.AddComponent<NetworkObject>();

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
    }
}
#endif
