#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectM.Core;
using ProjectM.Data;
using ProjectM.Economy;
using ProjectM.Network;
using ProjectM.UI;

namespace ProjectM.Network.Editor
{
    /// <summary>
    /// Phase 5: 매치 통계 NGO, 결과 UI, TCP 레거시 제거.
    /// 메뉴: ProjectM/Setup Phase 5 Match Results
    /// </summary>
    public static class Phase5MatchResultsSetup
    {
        private const string GamePlayScenePath = "Assets/Scenes/GamePlay.unity";
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("ProjectM/Setup Phase 5 Match Results")]
        public static void Setup()
        {
            SetupGamePlayScene();
            SetupMainMenuScene();

            AssetDatabase.SaveAssets();
            Debug.Log("[Phase5] Match Results NGO 설정 및 TCP 레거시 제거 완료");
        }

        private static void SetupGamePlayScene()
        {
            if (SceneManager.GetActiveScene().path != GamePlayScenePath)
                EditorSceneManager.OpenScene(GamePlayScenePath);

            RemoveLegacyTcpObjects();
            SetupNetworkMatchStats();
            DeduplicateResultViews();
            EnsureDbApiClient();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        private static void SetupMainMenuScene()
        {
            var activePath = SceneManager.GetActiveScene().path;
            EditorSceneManager.OpenScene(MainMenuScenePath);

            RemoveObjectsByName("RoomManager");
            StripMissingScripts();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

            if (!string.IsNullOrEmpty(activePath) && activePath != MainMenuScenePath)
                EditorSceneManager.OpenScene(activePath);
        }

        private static void RemoveLegacyTcpObjects()
        {
            RemoveObjectsByName("Network");
            RemoveObjectsByName("RoomManager");
            StripMissingScripts();
        }

        private static void RemoveObjectsByName(string objectName)
        {
            var all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            int removed = 0;
            foreach (var go in all)
            {
                if (go == null || go.name != objectName) continue;
                Object.DestroyImmediate(go);
                removed++;
            }

            if (removed > 0)
                Debug.Log($"[Phase5] {objectName} GameObject {removed}개 제거");
        }

        private static void StripMissingScripts()
        {
            int stripped = 0;
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go == null) continue;
                int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (count <= 0) continue;
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                stripped += count;
            }

            if (stripped > 0)
                Debug.Log($"[Phase5] Missing Script {stripped}개 제거");
        }

        private static void SetupNetworkMatchStats()
        {
            var root = GameObject.Find("NetworkGameplay");
            if (root == null)
            {
                Debug.LogError("[Phase5] NetworkGameplay 오브젝트를 찾을 수 없습니다.");
                return;
            }

            var stats = root.GetComponent<NetworkMatchStats>() ?? root.AddComponent<NetworkMatchStats>();
            var session = Object.FindAnyObjectByType<GameSessionManager>();

            var so = new SerializedObject(stats);
            so.FindProperty("session").objectReferenceValue = session;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[Phase5] NetworkMatchStats → NetworkGameplay 연결 완료");
        }

        private static void DeduplicateResultViews()
        {
            var views = Object.FindObjectsByType<ResultView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            ResultView keep = null;

            foreach (var view in views)
            {
                if (view == null) continue;
                var so = new SerializedObject(view);
                var panel = so.FindProperty("panelRoot").objectReferenceValue;
                if (panel != null)
                {
                    keep = view;
                    break;
                }
            }

            keep ??= views.Length > 0 ? views[0] : null;
            if (keep == null)
            {
                Debug.LogWarning("[Phase5] ResultView를 찾을 수 없습니다.");
                return;
            }

            foreach (var view in views)
            {
                if (view == null || view == keep) continue;
                var goName = view.gameObject.name;
                Object.DestroyImmediate(view);
                Debug.Log($"[Phase5] 중복 ResultView 제거: {goName}");
            }

            WireResultViewReferences(keep);
        }

        private static void WireResultViewReferences(ResultView view)
        {
            var so = new SerializedObject(view);
            so.FindProperty("session").objectReferenceValue = Object.FindAnyObjectByType<GameSessionManager>();
            so.FindProperty("wallet").objectReferenceValue = null;
            so.FindProperty("reward").objectReferenceValue = Object.FindAnyObjectByType<RewardCalculator>();
            so.FindProperty("stats").objectReferenceValue = Object.FindAnyObjectByType<PlayerStatsTracker>();
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[Phase5] ResultView 참조 자동 연결 완료");
        }

        private static void EnsureDbApiClient()
        {
            if (Object.FindAnyObjectByType<DbApiClient>() != null)
                return;

            var managers = GameObject.Find("GameManagers");
            if (managers == null)
            {
                Debug.Log("[Phase5] DbApiClient 추가 생략 (GameManagers 없음)");
                return;
            }

            managers.AddComponent<DbApiClient>();
            Debug.Log("[Phase5] DbApiClient → GameManagers 추가");
        }
    }
}
#endif
