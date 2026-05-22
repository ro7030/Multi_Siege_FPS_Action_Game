using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

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

        public static TMP_Text CreateText(string name, RectTransform parent, int fontSize = 24, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = CreateChild(name, parent);
            var txt = go.AddComponent<TextMeshProUGUI>();
            // 폰트는 TMP_Settings.defaultFontAsset 사용 (Project Settings > TextMeshPro). 따로 지정 안 함.
            txt.fontSize = fontSize;
            txt.alignment = ToTMPAlign(anchor);
            txt.color = Color.white;
            txt.raycastTarget = false;
            return txt;
        }

        /// <summary>레거시 TextAnchor → TMP TextAlignmentOptions 변환.</summary>
        public static TextAlignmentOptions ToTMPAlign(TextAnchor anchor) => anchor switch
        {
            TextAnchor.UpperLeft    => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter  => TextAlignmentOptions.Top,
            TextAnchor.UpperRight   => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft   => TextAlignmentOptions.Left,
            TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
            TextAnchor.MiddleRight  => TextAlignmentOptions.Right,
            TextAnchor.LowerLeft    => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter  => TextAlignmentOptions.Bottom,
            TextAnchor.LowerRight   => TextAlignmentOptions.BottomRight,
            _                       => TextAlignmentOptions.Center
        };

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
