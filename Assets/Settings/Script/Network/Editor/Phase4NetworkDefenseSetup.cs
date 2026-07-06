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
using ProjectM.Player;

namespace ProjectM.Network.Editor
{
    // Phase 4: 방어/밭/게이트/지갑 NGO 동기화 컴포넌트 연결.
    // 메뉴: ProjectM/Setup Phase 4 Network Defense & Economy
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
            var zones = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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

            if (root.GetComponent<NetworkKitInventory>() == null)
                root.AddComponent<NetworkKitInventory>();

            if (root.GetComponent<NetworkThrowableInventory>() == null)
                root.AddComponent<NetworkThrowableInventory>();

            if (root.GetComponent<NetworkPlayerArsenal>() == null)
                root.AddComponent<NetworkPlayerArsenal>();

            PrefabUtility.SaveAsPrefabAsset(root, NetworkPlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        [MenuItem("ProjectM/Setup Phase 4 W3 Player Inventory NGO")]
        public static void SetupW3PlayerInventory()
        {
            ConfigureNetworkPlayerWallet();
            AssetDatabase.SaveAssets();
            Debug.Log("[Phase4] W3 NetworkThrowableInventory + NetworkPlayerArsenal — NetworkPlayer prefab 적용 완료");
        }

        [MenuItem("ProjectM/Setup Phase 4 W2 Network Kit Inventory")]
        public static void SetupNetworkKitInventoryOnly()
        {
            ConfigureNetworkPlayerWallet();
            AssetDatabase.SaveAssets();
            Debug.Log("[Phase4] W2 NetworkKitInventory — NetworkPlayer prefab 적용 완료");
        }

        private static void SetupGamePlayScene()
        {
            if (SceneManager.GetActiveScene().path != GamePlayScenePath)
                EditorSceneManager.OpenScene(GamePlayScenePath);

            RemoveLegacyRepairZones();
            SetupNetworkGameplayBridge();
            SetupSceneDefenses();
            SetupGateDefenseBodies();
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
            int count = 0;
            foreach (var defense in Object.FindObjectsByType<DefenseObject>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (defense == null) continue;
                EnsureDefenseNetworkComponents(defense.gameObject);
                count++;
            }

            foreach (var plot in Object.FindObjectsByType<FarmPlot>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (plot == null) continue;
                EnsureDefenseNetworkComponents(plot.gameObject);
                if (plot.GetComponent<NetworkFarmBridge>() == null)
                    plot.gameObject.AddComponent<NetworkFarmBridge>();
            }

            Debug.Log($"[Phase4] 씬 DefenseObject NGO 적용: {count}개 (비활성 포함)");
        }

        // GateInstaller가 참조하는 Gate Body(비활성)에 NGO HP 브릿지를 붙인다.
        private static void SetupGateDefenseBodies()
        {
            int count = 0;
            foreach (var installer in Object.FindObjectsByType<GateInstaller>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (installer == null) continue;
                var gateBody = GetGateObjectReference(installer);
                if (gateBody == null) continue;

                EnsureGateBodyNetworkComponents(gateBody);
                count++;
            }

            Debug.Log($"[Phase4] Gate Body NGO 적용: {count}개");
        }

        private static void EnsureGateBodyNetworkComponents(GameObject go)
        {
            EnsureDefenseNetworkComponents(go);

            if (go.GetComponent<NetworkGateBodyBridge>() == null)
                go.AddComponent<NetworkGateBodyBridge>();

            if (go.TryGetComponent<HealthSystem>(out var health))
            {
                var so = new SerializedObject(health);
                so.FindProperty("destroyOnDeath").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static GameObject GetGateObjectReference(GateInstaller installer)
        {
            var so = new SerializedObject(installer);
            return so.FindProperty("gateObject").objectReferenceValue as GameObject;
        }

        [MenuItem("ProjectM/Setup Phase 4 W2 Gate Defense NGO")]
        public static void SetupGateDefenseOnly()
        {
            if (SceneManager.GetActiveScene().path != GamePlayScenePath)
                EditorSceneManager.OpenScene(GamePlayScenePath);

            SetupSceneDefenses();
            SetupGateDefenseBodies();

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("[Phase4] W2 Gate Defense NGO 씬 적용 완료");
        }

        private static void SetupGateInstallers()
        {
            foreach (var installer in Object.FindObjectsByType<GateInstaller>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
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
