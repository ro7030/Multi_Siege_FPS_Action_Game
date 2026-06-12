#if UNITY_EDITOR
using ProjectM.Network;
using ProjectM.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectM.UI.Editor
{
    public static class MainMenuSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";
        private const string NetworkPrefabPath = "Assets/Prefab/Network/NetworkManager.prefab";
        private const string FontPath = "Assets/Resources/Fonts/Jalnan2/Jalnan2TTF SDF.asset";

        [MenuItem("ProjectM/Scenes/Wire MainMenu Relay Lobby")]
        public static void WireMainMenuScene()
        {
            EnsureNetworkManagerPrefab();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var networkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPrefabPath);

            var canvas = FindInScene("Canvas");
            if (canvas != null && canvas.GetComponent<MainMenuFontBinder>() == null)
            {
                var binder = canvas.AddComponent<MainMenuFontBinder>();
                var binderSo = new SerializedObject(binder);
                binderSo.FindProperty("uiFont").objectReferenceValue = font;
                binderSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var bootstrapper = Object.FindAnyObjectByType<NetworkBootstrapper>();
            if (bootstrapper == null)
            {
                var go = new GameObject("NetworkBootstrapper");
                bootstrapper = go.AddComponent<NetworkBootstrapper>();
            }

            var bootstrapperSo = new SerializedObject(bootstrapper);
            bootstrapperSo.FindProperty("networkManagerPrefab").objectReferenceValue = networkPrefab;
            bootstrapperSo.FindProperty("createLobbyRelayService").boolValue = true;
            bootstrapperSo.ApplyModifiedPropertiesWithoutUndo();

            WireMainMenuController();
            WireCreateRoomPanel(font);
            WireJoinRoomPanel(font);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[MainMenuSceneSetup] MainMenu wired for Relay/Lobby.");
        }

        [MenuItem("ProjectM/Network/Create NetworkManager Prefab")]
        public static void EnsureNetworkManagerPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPrefabPath);
            if (existing != null) return;

            EnsureFolder("Assets/Prefab/Network");

            var go = new GameObject("NetworkManager");
            var manager = go.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig();
            go.AddComponent<UnityTransport>();

            PrefabUtility.SaveAsPrefabAsset(go, NetworkPrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.Refresh();
            Debug.Log($"[MainMenuSceneSetup] Created {NetworkPrefabPath}");
        }

        private static GameObject FindInScene(string name)
        {
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                var transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in transforms)
                {
                    if (t.name == name)
                        return t.gameObject;
                }
            }
            return null;
        }

        private static void WireMainMenuController()
        {
            var controller = Object.FindAnyObjectByType<MainMenuController>();
            if (controller == null) return;

            var so = new SerializedObject(controller);
            so.FindProperty("characterSelectSceneName").stringValue = "CharacterSelect";
            so.FindProperty("loginSceneName").stringValue = "Login";
            so.ApplyModifiedPropertiesWithoutUndo();

            var exitButton = so.FindProperty("exitButton").objectReferenceValue as Button;
            if (exitButton != null)
            {
                var label = exitButton.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = "로그아웃";
                    EditorUtility.SetDirty(label);
                }
            }
        }

        private static void WireCreateRoomPanel(TMP_FontAsset font)
        {
            var controller = Object.FindAnyObjectByType<CreateRoomPanelController>();
            if (controller == null) return;

            var panel = FindInScene("CreateRoomPanel");
            var mainMenu = Object.FindAnyObjectByType<MainMenuController>();

            Button cancelButton = FindButton(panel, "CancelButton");
            if (cancelButton == null) cancelButton = FindButton(panel, "CloseButton");

            TMP_Text statusText = FindOrCreateStatusText(panel, "CreateStatusText", font,
                new Vector2(0.1f, 0.05f), new Vector2(0.9f, 0.12f));

            var so = new SerializedObject(controller);
            so.FindProperty("mainMenu").objectReferenceValue = mainMenu;
            so.FindProperty("lobbyRelayService").objectReferenceValue = LobbyRelayService.Instance;
            if (cancelButton != null)
                so.FindProperty("cancelButton").objectReferenceValue = cancelButton;
            so.FindProperty("statusText").objectReferenceValue = statusText;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireJoinRoomPanel(TMP_FontAsset font)
        {
            var controller = Object.FindAnyObjectByType<JoinRoomPanelController>();
            if (controller == null) return;

            var panel = FindInScene("JoinRoomPanel");
            var mainMenu = Object.FindAnyObjectByType<MainMenuController>();

            var scrollView = panel != null ? panel.transform.Find("Scroll View") : null;
            ScrollRect scrollRect = scrollView != null ? scrollView.GetComponent<ScrollRect>() : null;
            Transform content = scrollView != null ? scrollView.Find("Viewport/Content") : null;

            if (content != null)
            {
                EnsureScrollContentLayout(content.gameObject);
                if (scrollRect != null && scrollRect.content == null)
                {
                    scrollRect.content = content.GetComponent<RectTransform>();
                    EditorUtility.SetDirty(scrollRect);
                }
            }

            GameObject passwordSection = EnsurePasswordSection(panel);
            TMP_Text statusText = FindOrCreateStatusText(panel, "JoinStatusText", font,
                new Vector2(0.1f, 0.02f), new Vector2(0.9f, 0.08f));

            Button cancelButton = FindButton(panel, "CancelButton");
            if (cancelButton == null)
            {
                var joinBtn = FindButton(panel, "JoinButton");
                if (joinBtn != null)
                {
                    cancelButton = FindSiblingCancelButton(panel, joinBtn.transform);
                }
            }

            var so = new SerializedObject(controller);
            so.FindProperty("mainMenu").objectReferenceValue = mainMenu;
            so.FindProperty("lobbyRelayService").objectReferenceValue = LobbyRelayService.Instance;
            if (scrollRect != null)
                so.FindProperty("scrollRect").objectReferenceValue = scrollRect;
            if (content != null)
                so.FindProperty("listContent").objectReferenceValue = content;
            so.FindProperty("passwordSection").objectReferenceValue = passwordSection;
            if (cancelButton != null)
                so.FindProperty("cancelButton").objectReferenceValue = cancelButton;
            so.FindProperty("statusText").objectReferenceValue = statusText;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject EnsurePasswordSection(GameObject panel)
        {
            if (panel == null) return null;

            var existing = panel.transform.Find("PasswordSection");
            if (existing != null) return existing.gameObject;

            var section = new GameObject("PasswordSection", typeof(RectTransform));
            section.transform.SetParent(panel.transform, false);
            var sectionRt = section.GetComponent<RectTransform>();
            sectionRt.anchorMin = new Vector2(0.5f, 0.5f);
            sectionRt.anchorMax = new Vector2(0.5f, 0.5f);
            sectionRt.anchoredPosition = Vector2.zero;
            sectionRt.sizeDelta = Vector2.zero;

            ReparentIfFound(panel.transform, section.transform, "PasswordInputField");
            ReparentIfFound(panel.transform, section.transform, "PasswordToggle");

            return section;
        }

        private static void ReparentIfFound(Transform panelRoot, Transform section, string childName)
        {
            var child = panelRoot.Find(childName);
            if (child != null)
                child.SetParent(section, true);
        }

        private static void EnsureScrollContentLayout(GameObject content)
        {
            if (content.GetComponent<VerticalLayoutGroup>() == null)
            {
                var layout = content.AddComponent<VerticalLayoutGroup>();
                layout.childControlHeight = true;
                layout.childControlWidth = true;
                layout.childForceExpandHeight = false;
                layout.childForceExpandWidth = true;
                layout.spacing = 4f;
            }

            if (content.GetComponent<ContentSizeFitter>() == null)
            {
                var fitter = content.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            }
        }

        private static TMP_Text FindOrCreateStatusText(GameObject panel, string name, TMP_FontAsset font,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            if (panel == null) return null;

            var existing = panel.transform.Find(name);
            if (existing != null)
                return existing.GetComponent<TMP_Text>();

            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(panel.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.fontSize = 22;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.75f, 0.75f, 1f);
            return tmp;
        }

        private static Button FindButton(GameObject root, string name)
        {
            if (root == null) return null;
            var t = root.transform.Find(name);
            return t != null ? t.GetComponent<Button>() : null;
        }

        private static Button FindSiblingCancelButton(GameObject panel, Transform reference)
        {
            foreach (Transform child in panel.transform)
            {
                if (child == reference) continue;
                var btn = child.GetComponent<Button>();
                if (btn != null && child.name.Contains("Cancel"))
                    return btn;
            }
            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
