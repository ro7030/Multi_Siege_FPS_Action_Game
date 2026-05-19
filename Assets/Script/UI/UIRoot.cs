using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectM.UI
{
    /// <summary>
    /// 정식 게임 UI 루트. Canvas + CanvasScaler + GraphicRaycaster + EventSystem을 자동 생성한다.
    /// 다른 Presenter들은 UIRoot.Instance.RootTransform에 자식으로 추가된다.
    /// </summary>
    public class UIRoot : MonoBehaviour
    {
        public static UIRoot Instance { get; private set; }

        [SerializeField] private Canvas canvas;
        [SerializeField] private Vector2 referenceResolution = new(1920, 1080);

        public Canvas Canvas => canvas;
        public RectTransform RootTransform => canvas != null ? canvas.transform as RectTransform : null;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (canvas == null) BuildCanvas();
            EnsureEventSystem();
        }

        private void BuildCanvas()
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(go);
        }

        // ── 공용 빌더 헬퍼 ─────────────────────────────────────────
        public static GameObject CreateChild(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static Text CreateText(string name, RectTransform parent, int fontSize = 24, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = CreateChild(name, parent);
            var txt = go.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = fontSize;
            txt.alignment = anchor;
            txt.color = Color.white;
            txt.raycastTarget = false;
            return txt;
        }

        public static Image CreatePanel(string name, RectTransform parent, Color color)
        {
            var go = CreateChild(name, parent);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Button CreateButton(string name, RectTransform parent, string label)
        {
            var go = CreateChild(name, parent);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.32f, 0.55f, 1f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var txt = CreateText("Label", go.GetComponent<RectTransform>(), 18);
            txt.text = label;
            txt.color = Color.white;
            var trt = txt.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            return btn;
        }
    }
}
