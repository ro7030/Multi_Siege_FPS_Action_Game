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
using TMPro;

namespace ProjectM.Network.Editor
{
    // Phase 5: 매치 통계 NGO, 결과 UI, TCP 레거시 제거.
    // 메뉴: ProjectM/Setup Phase 5 Match Results
    public static class Phase5MatchResultsSetup
    {
        private const string GamePlayScenePath = "Assets/Scenes/GamePlay.unity";
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("ProjectM/Setup Phase 5 Match Results")]
        public static void Setup()
        {
            SetupGamePlayScene();
            SetupMainMenuScene();
            EnsureRematchCoordinatorOnLobbyRelay();

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

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Resources/Fonts/Jalnan2/Jalnan2TTF SDF.asset");
            if (font != null)
                so.FindProperty("rematchStatusFont").objectReferenceValue = font;

            EnsureRematchStatusText(view, so, font);
            EnsureResultViewSceneDefaults(view);
            so.ApplyModifiedPropertiesWithoutUndo();

            // ResultView 루트는 평소 비활성. Awake 구독은 동작하지만 Show()가 루트를 켠다.
            view.gameObject.SetActive(false);

            Debug.Log("[Phase5] ResultView 참조 자동 연결 완료");
        }

        private static void EnsureRematchStatusText(ResultView view, SerializedObject so, TMP_FontAsset font)
        {
            var panelProp = so.FindProperty("panelRoot");
            var panel = panelProp.objectReferenceValue as GameObject;
            if (panel == null) return;

            var existing = panel.transform.Find("RematchStatusText");
            TMP_Text text = existing != null ? existing.GetComponent<TMP_Text>() : null;

            if (text == null)
            {
                var go = new GameObject("RematchStatusText", typeof(RectTransform));
                go.transform.SetParent(panel.transform, false);

                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, -120f);
                rect.sizeDelta = new Vector2(480f, 160f);

                text = go.AddComponent<TextMeshProUGUI>();
                text.fontSize = 18f;
                text.alignment = TextAlignmentOptions.TopLeft;
                text.color = new Color(0.85f, 0.9f, 1f);
                text.raycastTarget = false;
                if (font != null) text.font = font;

                var retry = panel.transform.Find("Retry_Button");
                if (retry != null)
                    go.transform.SetSiblingIndex(retry.GetSiblingIndex());
            }
            else if (font != null)
            {
                text.font = font;
                text.raycastTarget = false;
            }

            so.FindProperty("rematchStatusText").objectReferenceValue = text;
        }

        private static void EnsureResultViewSceneDefaults(ResultView view)
        {
            var so = new SerializedObject(view);
            var panel = so.FindProperty("panelRoot").objectReferenceValue as GameObject;
            if (panel != null && panel.TryGetComponent(out UnityEngine.UI.Image panelImage))
            {
                var c = panelImage.color;
                panelImage.color = new Color(c.r, c.g, c.b, 0.85f);
            }

            var canvas = view.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var rt = canvas.GetComponent<RectTransform>();
            if (rt != null && rt.localScale.sqrMagnitude < 0.01f)
                rt.localScale = Vector3.one;

            if (canvas.sortingOrder < 100)
                canvas.sortingOrder = 100;
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

        private static void EnsureRematchCoordinatorOnLobbyRelay()
        {
            var relay = Object.FindAnyObjectByType<LobbyRelayService>(FindObjectsInactive.Include);
            if (relay == null)
            {
                Debug.Log("[Phase5] MatchRematchCoordinator — MainMenu Play 후 LobbyRelayService 생성됨 (NetworkBootstrapper)");
                return;
            }

            if (relay.GetComponent<MatchRematchCoordinator>() == null)
                relay.gameObject.AddComponent<MatchRematchCoordinator>();

            Debug.Log("[Phase5] MatchRematchCoordinator → LobbyRelayService 연결 완료");
        }
    }
}
#endif
