#if UNITY_EDITOR
using ProjectM.Auth;
using ProjectM.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectM.UI.Editor
{
    public static class LoginSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Login.unity";
        private const string FontPath = "Assets/Resources/Fonts/Jalnan2/Jalnan2TTF SDF.asset";

        [MenuItem("ProjectM/Scenes/Create Login Scene")]
        public static void CreateLoginScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var root = new GameObject("Login");
            var controller = root.AddComponent<LoginController>();
            root.AddComponent<UnityAuthService>();

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(root.transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = CreatePanel("LoginPanel", canvasGo.transform, new Color(0.08f, 0.1f, 0.16f, 0.92f));
            Stretch(panel.GetComponent<RectTransform>(), 0.25f, 0.2f, 0.75f, 0.8f);

            var title = CreateText("TitleText", panel.transform, "project m", 48, font);
            Anchor(title.rectTransform, 0.1f, 0.78f, 0.9f, 0.95f);

            var nicknameLabel = CreateText("NicknameLabel", panel.transform, "닉네임", 28, font);
            Anchor(nicknameLabel.rectTransform, 0.1f, 0.66f, 0.9f, 0.76f);
            nicknameLabel.alignment = TextAlignmentOptions.MidlineLeft;

            var nicknameInput = CreateInputField("NicknameInput", panel.transform, "닉네임을 입력하세요", font);
            Anchor(nicknameInput.GetComponent<RectTransform>(), 0.1f, 0.54f, 0.9f, 0.64f);

            var loginButton = CreateButton("LoginButton", panel.transform, "로그인", font);
            Anchor(loginButton.GetComponent<RectTransform>(), 0.1f, 0.38f, 0.9f, 0.48f);

            var guestButton = CreateButton("GuestButton", panel.transform, "게스트 로그인", font);
            Anchor(guestButton.GetComponent<RectTransform>(), 0.1f, 0.26f, 0.9f, 0.36f);

            var statusText = CreateText("StatusText", panel.transform, string.Empty, 22, font);
            Anchor(statusText.rectTransform, 0.1f, 0.1f, 0.9f, 0.22f);
            statusText.color = new Color(1f, 0.75f, 0.75f, 1f);

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var so = new SerializedObject(controller);
            so.FindProperty("uiFont").objectReferenceValue = font;
            so.FindProperty("nicknameInput").objectReferenceValue = nicknameInput;
            so.FindProperty("loginButton").objectReferenceValue = loginButton;
            so.FindProperty("guestButton").objectReferenceValue = guestButton;
            so.FindProperty("statusText").objectReferenceValue = statusText;
            so.FindProperty("mainMenuSceneName").stringValue = "MainMenu";
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"[LoginSceneSetup] Saved {ScenePath}");
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, int size, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.font = font;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return tmp;
        }

        private static TMP_InputField CreateInputField(string name, Transform parent, string placeholder, TMP_FontAsset font)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            root.transform.SetParent(parent, false);
            root.GetComponent<Image>().color = new Color(0.15f, 0.18f, 0.24f, 1f);

            var textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(root.transform, false);
            Stretch(textArea.GetComponent<RectTransform>(), 0.05f, 0.1f, 0.95f, 0.9f);

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
            placeholderGo.transform.SetParent(textArea.transform, false);
            var placeholderText = placeholderGo.GetComponent<TextMeshProUGUI>();
            placeholderText.text = placeholder;
            placeholderText.font = font;
            placeholderText.fontSize = 24;
            placeholderText.color = new Color(1f, 1f, 1f, 0.45f);
            Stretch(placeholderText.rectTransform, 0f, 0f, 1f, 1f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(textArea.transform, false);
            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = 24;
            text.color = Color.white;
            Stretch(text.rectTransform, 0f, 0f, 1f, 1f);

            var input = root.GetComponent<TMP_InputField>();
            input.textViewport = textArea.GetComponent<RectTransform>();
            input.textComponent = text;
            input.placeholder = placeholderText;
            input.characterLimit = 16;
            return input;
        }

        private static Button CreateButton(string name, Transform parent, string label, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = new Color(0.18f, 0.32f, 0.55f, 1f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            var text = CreateText("Label", go.transform, label, 26, font);
            Stretch(text.rectTransform, 0f, 0f, 1f, 1f);
            return button;
        }

        private static void Stretch(RectTransform rt, float minX, float minY, float maxX, float maxY)
        {
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rt, float minX, float minY, float maxX, float maxY)
        {
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
#endif
