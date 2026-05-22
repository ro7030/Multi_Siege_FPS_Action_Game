using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectM.Player;

namespace ProjectM.UI
{
    /// <summary>
    /// "(F) 메시지" 상호작용 프롬프트. PlayerInteractor 의 현재 대상을 따라 월드 위치에 표시한다.
    ///
    /// [편집 가능]
    ///   - keyIcon   : F키 아이콘 이미지 (Inspector 에서 Sprite 교체)
    ///   - keyLabel  : 아이콘 위 글자 ("F")
    ///   - messageText : 메시지 텍스트 (내용은 대상이 정함, 폰트/색/크기는 여기서 편집)
    ///   - progressFill : 홀드형 진행바 (부활 등)
    ///
    /// 자동 생성형이지만, 직접 Canvas 로 만든 요소를 Inspector 슬롯에 연결하면 그것을 사용한다.
    /// (null 인 슬롯만 자동 생성)
    /// </summary>
    public class InteractionPromptView : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private Camera worldCamera;

        [Header("UI 요소 — 비워두면 자동 생성")]
        [SerializeField] private GameObject promptRoot;   // 전체 묶음
        [SerializeField] private Image keyIcon;           // F키 배경 아이콘 (이미지 교체 가능)
        [SerializeField] private TMP_Text keyLabel;           // "F"
        [SerializeField] private TMP_Text messageText;        // 메시지
        [SerializeField] private Image progressFill;      // 홀드 진행바

        [Header("배치")]
        [Tooltip("대상 앵커 기준 추가 높이(월드 단위)")]
        [SerializeField] private float worldHeightOffset = 1.6f;
        [SerializeField] private Vector2 screenOffset = new(0, 0);

        [Header("기본 외형")]
        [SerializeField] private string defaultKeyLabel = "F";

        private RectTransform rootRt;

        private void Awake()
        {
            if (interactor == null) interactor = FindAnyObjectByType<PlayerInteractor>();
            if (worldCamera == null) worldCamera = Camera.main;
        }

        private void Start()
        {
            if (UIRoot.Instance == null) { enabled = false; return; }
            BuildMissing();
            if (promptRoot != null) promptRoot.SetActive(false);
        }

        private void LateUpdate()
        {
            if (interactor == null || promptRoot == null) return;

            var target = interactor.Current;
            bool show = target != null;

            if (promptRoot.activeSelf != show) promptRoot.SetActive(show);
            if (!show) return;

            // 내용 갱신
            if (messageText != null) messageText.text = target.PromptText;
            if (keyIcon != null && target.PromptIcon != null) keyIcon.sprite = target.PromptIcon;

            // 진행바 (홀드형만)
            if (progressFill != null)
            {
                bool hold = target.IsHold;
                progressFill.transform.parent.gameObject.SetActive(hold);
                if (hold) progressFill.fillAmount = Mathf.Clamp01(target.HoldProgress01);
            }

            // 위치: 대상 월드 앵커 → 스크린
            var anchor = target.PromptAnchor;
            if (anchor != null && worldCamera != null && rootRt != null)
            {
                Vector3 worldPos = anchor.position + Vector3.up * worldHeightOffset;
                Vector3 sp = worldCamera.WorldToScreenPoint(worldPos);
                // 카메라 뒤면 숨김
                if (sp.z < 0f) { promptRoot.SetActive(false); return; }
                rootRt.position = new Vector3(sp.x + screenOffset.x, sp.y + screenOffset.y, 0f);
            }
        }

        // ─────────────────────────────────────────────────────────────
        private void BuildMissing()
        {
            var root = UIRoot.Instance.RootTransform;

            if (promptRoot == null)
            {
                var go = UIRoot.CreateChild("InteractionPrompt", root);
                promptRoot = go;
                rootRt = go.GetComponent<RectTransform>();
                rootRt.sizeDelta = new Vector2(360, 64);
                rootRt.pivot = new Vector2(0.5f, 0.5f);

                // F 아이콘 (동그란 배경)
                if (keyIcon == null)
                {
                    keyIcon = UIRoot.CreatePanel("KeyIcon", rootRt, new Color(1, 1, 1, 0.95f));
                    var krt = keyIcon.rectTransform;
                    krt.anchorMin = krt.anchorMax = new Vector2(0, 0.5f);
                    krt.pivot = new Vector2(0, 0.5f);
                    krt.sizeDelta = new Vector2(48, 48);
                    krt.anchoredPosition = new Vector2(0, 0);
                }
                if (keyLabel == null)
                {
                    keyLabel = UIRoot.CreateText("KeyLabel", keyIcon.rectTransform, 26, TextAnchor.MiddleCenter);
                    keyLabel.color = Color.black;
                    keyLabel.fontStyle = FontStyles.Bold;
                    keyLabel.text = defaultKeyLabel;
                    var lrt = keyLabel.rectTransform;
                    lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                    lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
                }

                // 메시지
                if (messageText == null)
                {
                    messageText = UIRoot.CreateText("Message", rootRt, 24, TextAnchor.MiddleLeft);
                    messageText.color = Color.white;
                    messageText.fontStyle = FontStyles.Bold;
                    var mrt = messageText.rectTransform;
                    mrt.anchorMin = new Vector2(0, 0); mrt.anchorMax = new Vector2(1, 1);
                    mrt.offsetMin = new Vector2(60, 0); mrt.offsetMax = new Vector2(0, 0);
                }

                // 진행바 (홀드형용)
                if (progressFill == null)
                {
                    var barBg = UIRoot.CreatePanel("ProgressBg", rootRt, new Color(0, 0, 0, 0.6f));
                    var bgrt = barBg.rectTransform;
                    bgrt.anchorMin = new Vector2(0, 0); bgrt.anchorMax = new Vector2(1, 0);
                    bgrt.pivot = new Vector2(0.5f, 1f);
                    bgrt.sizeDelta = new Vector2(-60, 8);
                    bgrt.anchoredPosition = new Vector2(30, -2);

                    progressFill = UIRoot.CreatePanel("ProgressFill", barBg.rectTransform, new Color(0.4f, 0.9f, 1f, 1f));
                    progressFill.type = Image.Type.Filled;
                    progressFill.fillMethod = Image.FillMethod.Horizontal;
                    progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                    var frt = progressFill.rectTransform;
                    frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
                    frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
                    barBg.gameObject.SetActive(false);
                }
            }
            else
            {
                rootRt = promptRoot.GetComponent<RectTransform>();
            }
        }
    }
}
