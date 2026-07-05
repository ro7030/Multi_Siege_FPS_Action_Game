#if UNITY_EDITOR
using ProjectM.CharacterSelect;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectM.CharacterSelect.Editor
{
    public static class CharacterSelectSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/CharacterSelect.unity";
        private const string FontPath = "Assets/Resources/Fonts/Jalnan2/Jalnan2TTF SDF.asset";
        private const string DatabasePath = "Assets/CharacterSelect/CharacterDatabase.asset";
        private const string PreviewAnimatorControllerPath = "Assets/Animations/Player/PlayerGunAnimator.controller";

        private const string ResLeftArrow = "캐릭터 선택/캐릭터선택 좌";
        private const string ResRightArrow = "캐릭터 선택/캐릭터선택 우";
        private const string ResActionButton = "캐릭터 선택/Ready및Start 버튼";
        private const string ResNameBox = "캐릭터 선택/플레이어 이름박스";
        private const string ResReadyBadge = "캐릭터 선택/Ready 표시";

        private static readonly string[] ResBackButtonCandidates =
        {
            "캐릭터 선택/뒤로가기 버튼",
            "캐릭터 선택/뒤로가기",
            "캐릭터 선택/세션 나가기 버튼",
            "ESC를 누르면 나오는 창/나가기",
            "캐릭터 선택/Ready및Start 버튼"
        };

        private static readonly Vector3[] SlotPositions =
        {
            new(-3f, 1f, 1.5f),
            new(-1f, 1f, 1.5f),
            new(1f, 1f, 1.5f),
            new(3f, 1f, 1.5f)
        };

        [MenuItem("ProjectM/Setup CharacterSelect Scene")]
        public static void SetupCharacterSelectScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var database = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(DatabasePath);

            RemoveLegacyUi();

            var slotViews = CreateCharacterSlots(font);
            var uiController = CreateScreenUi(font, slotViews);
            EnsureManagers(database, slotViews, uiController);

            if (Object.FindAnyObjectByType<EventSystem>() == null)
                CreateEventSystem();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[CharacterSelectSceneSetup] CharacterSelect scene rebuilt (LobbyScene layout).");
        }

        private static void RemoveLegacyUi()
        {
            DestroyIfExists("UICanvas");
            DestroyIfExists("Main Camera");
            DestroyIfExists("Stage");
            DestroyIfExists("CharacterSlots");
            DestroyIfExists("CharacterSelectUIRoot");
            DestroyIfExists("LobbyUICanvas");
            DestroyIfExists("LobbyCamera");
            DestroyIfExists("Ground");
            DestroyIfExists("LobbyBackdrop");
            DestroyIfExists("BackButton");
        }

        private static void DestroyIfExists(string name)
        {
            var go = GameObject.Find(name);
            if (go != null)
                Object.DestroyImmediate(go);
        }

        private static CharacterSelectSlotView[] CreateCharacterSlots(TMP_FontAsset font)
        {
            var spriteNameBox = Resources.Load<Sprite>(ResNameBox);
            var spriteReady = Resources.Load<Sprite>(ResReadyBadge);

            var slotsRoot = new GameObject("CharacterSlots");
            var views = new CharacterSelectSlotView[SlotPositions.Length];

            for (int i = 0; i < SlotPositions.Length; i++)
            {
                views[i] = CreateSlot(slotsRoot.transform, i, SlotPositions[i], spriteNameBox, spriteReady, font);
            }

            return views;
        }

        private static CharacterSelectSlotView CreateSlot(
            Transform parent, int slotIndex, Vector3 worldPos,
            Sprite nameBoxSprite, Sprite readySprite, TMP_FontAsset font)
        {
            var slotRoot = new GameObject($"CharacterSlot_{slotIndex + 1}");
            slotRoot.transform.SetParent(parent, false);
            slotRoot.transform.position = worldPos;

            var previewAnchor = new GameObject("PreviewAnchor");
            previewAnchor.transform.SetParent(slotRoot.transform, false);
            previewAnchor.transform.localPosition = Vector3.zero;

            var nameCanvasGo = new GameObject("NameTagCanvas");
            nameCanvasGo.transform.SetParent(slotRoot.transform, false);
            nameCanvasGo.transform.localPosition = new Vector3(0f, 2.15f, 0f);
            var canvas = nameCanvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = nameCanvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(280f, 120f);
            canvasRect.localScale = new Vector3(0.012f, 0.012f, 0.012f);

            var nameTagRoot = CreateUiImage(nameCanvasGo.transform, "NameTagRoot", nameBoxSprite,
                new Vector2(0.05f, 0.15f), new Vector2(0.95f, 0.85f));
            var nameText = CreateTmpText(nameTagRoot.transform, "NameTagText", "waiting...",
                28, TextAlignmentOptions.Center, font, new Color(0.35f, 0.95f, 0.35f, 1f),
                Vector2.zero, Vector2.one);

            var readyBadge = CreateUiImage(nameCanvasGo.transform, "ReadyBadge", readySprite,
                new Vector2(0.35f, 0.78f), new Vector2(0.65f, 0.98f));
            readyBadge.SetActive(false);

            nameTagRoot.SetActive(false);

            var slotView = slotRoot.AddComponent<CharacterSelectSlotView>();
            var so = new SerializedObject(slotView);
            so.FindProperty("slotIndex").intValue = slotIndex;
            so.FindProperty("previewAnchor").objectReferenceValue = previewAnchor.transform;
            so.FindProperty("nameTagRoot").objectReferenceValue = nameTagRoot;
            so.FindProperty("nameTagText").objectReferenceValue = nameText;
            so.FindProperty("readyBadge").objectReferenceValue = readyBadge;
            so.ApplyModifiedPropertiesWithoutUndo();

            return slotView;
        }

        private static CharacterSelectLobbyUIController CreateScreenUi(
            TMP_FontAsset font, CharacterSelectSlotView[] slotViews)
        {
            CreateLobbyCamera();
            CreateGround();

            var canvasGo = new GameObject("LobbyUICanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            canvasGo.GetComponent<CanvasScaler>().matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var binder = canvasGo.AddComponent<CharacterSelectFontBinder>();
            var binderSo = new SerializedObject(binder);
            binderSo.FindProperty("uiFont").objectReferenceValue = font;
            binderSo.ApplyModifiedPropertiesWithoutUndo();

            var canvasRect = canvasGo.GetComponent<RectTransform>();

            var controlPanel = CreateUiPanel(canvasRect, "ControlPanel", Color.clear,
                new Vector2(0.36f, 0.04f), new Vector2(0.64f, 0.22f));
            var controlRect = controlPanel.GetComponent<RectTransform>();

            var prevBtn = CreateSpriteButton(controlRect, "PreviousCharacterButton",
                Resources.Load<Sprite>(ResLeftArrow),
                new Vector2(0.18f, 0.58f), new Vector2(0.38f, 0.92f));
            var nextBtn = CreateSpriteButton(controlRect, "NextCharacterButton",
                Resources.Load<Sprite>(ResRightArrow),
                new Vector2(0.62f, 0.58f), new Vector2(0.82f, 0.92f));
            var actionBtn = CreateSpriteButton(controlRect, "ActionButton",
                Resources.Load<Sprite>(ResActionButton),
                new Vector2(0.12f, 0.08f), new Vector2(0.88f, 0.5f));
            var actionLabel = CreateTmpText(actionBtn.transform, "Label", "start",
                36, TextAlignmentOptions.Center, font, Color.black,
                Vector2.zero, Vector2.one);

            var statusText = CreateTmpText(canvasRect, "LobbyStatusText",
                "select a character.", 20, TextAlignmentOptions.Bottom,
                font, new Color(0.9f, 0.9f, 0.9f, 1f),
                new Vector2(0.2f, 0.01f), new Vector2(0.8f, 0.05f));

            var backBtn = CreateBackButton(canvasRect, font);

            var uiRoot = new GameObject("CharacterSelectUIRoot");
            var controller = uiRoot.AddComponent<CharacterSelectLobbyUIController>();
            var so = new SerializedObject(controller);
            so.FindProperty("slotViews").arraySize = slotViews.Length;
            for (int i = 0; i < slotViews.Length; i++)
                so.FindProperty("slotViews").GetArrayElementAtIndex(i).objectReferenceValue = slotViews[i];
            so.FindProperty("previousCharacterButton").objectReferenceValue = prevBtn;
            so.FindProperty("nextCharacterButton").objectReferenceValue = nextBtn;
            so.FindProperty("actionButton").objectReferenceValue = actionBtn;
            so.FindProperty("actionButtonLabel").objectReferenceValue = actionLabel;
            so.FindProperty("backButton").objectReferenceValue = backBtn;
            so.FindProperty("backButtonLabel").objectReferenceValue = backBtn.GetComponentInChildren<TMP_Text>();
            so.FindProperty("statusText").objectReferenceValue = statusText;
            so.ApplyModifiedPropertiesWithoutUndo();

            return controller;
        }

        [MenuItem("ProjectM/CharacterSelect/Add Back Button")]
        public static void AddBackButtonToScene()
        {
            var canvas = GameObject.Find("LobbyUICanvas")?.GetComponent<RectTransform>();
            if (canvas == null)
            {
                Debug.LogError("[CharacterSelectSceneSetup] LobbyUICanvas not found.");
                return;
            }

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            DestroyIfExists("BackButton");

            var backBtn = CreateBackButton(canvas, font);
            var controller = Object.FindAnyObjectByType<CharacterSelectLobbyUIController>();
            if (controller != null)
            {
                var so = new SerializedObject(controller);
                so.FindProperty("backButton").objectReferenceValue = backBtn;
                so.FindProperty("backButtonLabel").objectReferenceValue = backBtn.GetComponentInChildren<TMP_Text>();
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[CharacterSelectSceneSetup] BackButton added to CharacterSelect scene.");
        }

        private static Button CreateBackButton(RectTransform canvas, TMP_FontAsset font)
        {
            return CreateSpriteButton(canvas, "BackButton", LoadBackButtonSprite(),
                new Vector2(0.02f, 0.90f), new Vector2(0.16f, 0.98f));
        }

        private static Sprite LoadBackButtonSprite()
        {
            foreach (var path in ResBackButtonCandidates)
            {
                var sprite = Resources.Load<Sprite>(path);
                if (sprite != null) return sprite;
            }

            return Resources.Load<Sprite>(ResLeftArrow);
        }

        private static void CreateLobbyCamera()
        {
            var camGo = new GameObject("LobbyCamera");
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 2.2f, -6.5f);
            camGo.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
        }

        private static void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(2.5f, 1f, 2f);
            ground.transform.position = Vector3.zero;
        }

        private static void EnsureManagers(
            CharacterDatabase database,
            CharacterSelectSlotView[] slotViews,
            CharacterSelectLobbyUIController uiController)
        {
            var managers = GameObject.Find("Managers");
            if (managers == null)
                managers = new GameObject("Managers");

            RemoveComponentIfExists<CharacterSelectManager>(managers);
            RemoveComponentIfExists<CharacterPreviewSpawner>(managers);

            EnsureComponent<LocalRoomService>(managers);
            EnsureComponent<NetworkRoomService>(managers);
            EnsureComponent<RoomServiceBootstrapper>(managers);
            EnsureComponent<CharacterLobbyNetwork>(managers);
            EnsureComponent<NetworkObject>(managers);

            var spawner = EnsureComponent<CharacterPreviewSpawner>(managers);
            var bootstrapper = managers.GetComponent<RoomServiceBootstrapper>();
            var localSvc = managers.GetComponent<LocalRoomService>();
            var networkSvc = managers.GetComponent<NetworkRoomService>();

            var anchors = new Transform[slotViews.Length];
            for (int i = 0; i < slotViews.Length; i++)
                anchors[i] = slotViews[i].PreviewAnchor;

            var fallbackController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PreviewAnimatorControllerPath);

            var spawnerSo = new SerializedObject(spawner);
            spawnerSo.FindProperty("database").objectReferenceValue = database;
            spawnerSo.FindProperty("roomServiceBootstrapper").objectReferenceValue = bootstrapper;
            spawnerSo.FindProperty("slotAnchors").arraySize = anchors.Length;
            for (int i = 0; i < anchors.Length; i++)
                spawnerSo.FindProperty("slotAnchors").GetArrayElementAtIndex(i).objectReferenceValue = anchors[i];
            spawnerSo.FindProperty("fallbackAnimatorController").objectReferenceValue = fallbackController;
            spawnerSo.ApplyModifiedPropertiesWithoutUndo();

            var bootstrapSo = new SerializedObject(bootstrapper);
            bootstrapSo.FindProperty("localRoomService").objectReferenceValue = localSvc;
            bootstrapSo.FindProperty("networkRoomService").objectReferenceValue = networkSvc;
            bootstrapSo.ApplyModifiedPropertiesWithoutUndo();

            var networkSo = new SerializedObject(networkSvc);
            networkSo.FindProperty("database").objectReferenceValue = database;
            networkSo.ApplyModifiedPropertiesWithoutUndo();

            var localSo = new SerializedObject(localSvc);
            localSo.FindProperty("database").objectReferenceValue = database;
            localSo.ApplyModifiedPropertiesWithoutUndo();

            var uiSo = new SerializedObject(uiController);
            uiSo.FindProperty("roomServiceBootstrapper").objectReferenceValue = bootstrapper;
            uiSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateEventSystem()
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private static void RemoveComponentIfExists<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c != null) Object.DestroyImmediate(c);
        }

        private static GameObject CreateUiPanel(RectTransform parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            SetAnchors(rect, anchorMin, anchorMax);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return go;
        }

        private static GameObject CreateUiImage(Transform parent, string name, Sprite sprite,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            SetAnchors(rect, anchorMin, anchorMax);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return go;
        }

        private static Button CreateSpriteButton(RectTransform parent, string name, Sprite sprite,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            SetAnchors(rect, anchorMin, anchorMax);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            return btn;
        }

        private static TMP_Text CreateTmpText(Transform parent, string name, string text, float fontSize,
            TextAlignmentOptions alignment, TMP_FontAsset font, Color color,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            SetAnchors(rect, anchorMin, anchorMax);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = color;
            if (font != null) tmp.font = font;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
#endif
