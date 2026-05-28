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
    ///   - keyIcon        : F키 아이콘 이미지 (Inspector 에서 Sprite 교체)
    ///   - keyIconOutline : 키 아이콘 외곽 링. 홀드 진행도(0~1)에 맞춰 Radial360 으로 채워짐.
    ///                      반드시 Image.Type = Filled, FillMethod = Radial360 으로 사용.
    ///                      시각적으로는 가운데가 비어 있는 "링" 모양 스프라이트를 권장.
    ///   - messageText  : 메시지 텍스트 (내용은 대상이 정함, 폰트/색/크기는 여기서 편집)
    ///   - progressFill : 홀드형 진행바 (선택, 기존 하단 가로 바)
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
        [SerializeField] private GameObject promptRoot;   // 전체 묶음 (거리 되면 활성화)
        [SerializeField] private Image keyIcon;           // F키 이미지 (직접 만든 이미지 사용)
        [Tooltip("키 아이콘 외곽 링. Filled + Radial360 으로 설정되어 있어야 하며, 상호작용 시간에 맞춰 0→1 로 채워집니다. 가운데가 비어 있는 링 스프라이트를 넣어 주세요.")]
        [SerializeField] private Image keyIconOutline;    // F키 외곽 링 (홀드 진행도 게이지)
        [SerializeField] private TMP_Text messageText;    // 상호작용 메시지
        [SerializeField] private Image progressFill;      // 홀드 진행바 (선택)

        [Header("외곽 링 동작")]
        [Tooltip("끄면 비홀드(단발) 대상에서는 외곽 링을 숨김. 켜면 비홀드 대상에서도 항상 표시(가득 찬 상태).")]
        [SerializeField] private bool outlineAlwaysVisible = false;
        [Tooltip("비홀드(단발) 대상일 때 외곽 링이 보일 경우의 fillAmount. outlineAlwaysVisible 가 켜졌을 때만 적용.")]
        [Range(0f, 1f)]
        [SerializeField] private float outlineNonHoldFill = 1f;

        [Header("디버그")]
        [SerializeField] private bool debugLog = false;

        [Header("배치")]
        [Tooltip("켜면 대상 머리 위(월드)를 따라다님. 끄면 Canvas 에 디자인한 고정 위치 유지.")]
        [SerializeField] private bool followTarget = true;
        [Tooltip("대상 앵커 기준 추가 높이(월드 단위)")]
        [SerializeField] private float worldHeightOffset = 1.6f;
        [SerializeField] private Vector2 screenOffset = new(0, 0);

        private RectTransform rootRt;
        /// <summary>promptRoot가 이 오브젝트와 같을 때 true. SetActive(self)는 스크립트를 꺼버리므로 자식만 토글한다.</summary>
        private bool promptRootIsSelf;

        private void Awake()
        {
            if (interactor == null) interactor = FindAnyObjectByType<PlayerInteractor>();
            if (worldCamera == null) worldCamera = Camera.main;
        }

        private void Start()
        {
            if (UIRoot.Instance == null)
            {
                Debug.LogWarning("[InteractionPrompt] UIRoot.Instance 없음 — 스크립트는 유지하지만 UI 자동 생성은 건너뜁니다.");
            }
            else
            {
                BuildMissing();
            }

            promptRootIsSelf = promptRoot != null && promptRoot == gameObject;
            if (promptRootIsSelf)
                Debug.LogWarning("[InteractionPrompt] Prompt Root가 이 오브젝트와 같습니다. " +
                                 "자식 UI만 켜고 끕니다. 권장: 빈 자식 'PromptContent'를 만들고 그걸 Prompt Root로 지정하세요.", this);

            SetPromptVisible(false);

            if (interactor == null)
                Debug.LogWarning("[InteractionPrompt] Interactor(PlayerInteractor)를 찾지 못했습니다. " +
                                 "플레이어에 PlayerInteractor 컴포넌트가 있는지, 또는 Interactor 슬롯을 연결했는지 확인하세요.");
        }

        private void LateUpdate()
        {
            if (promptRoot == null) return;

            var target = interactor != null ? interactor.Current : null;
            bool show = target != null;

            if (IsPromptVisible() != show)
            {
                SetPromptVisible(show);
                if (debugLog) Debug.Log($"[Prompt] visible={show} — target={(target as MonoBehaviour)?.name}");
            }
            if (!show) return;

            // 내용 갱신
            if (messageText != null) messageText.text = target.PromptText;
            if (keyIcon != null && target.PromptIcon != null) keyIcon.sprite = target.PromptIcon;

            // 외곽 링 — 상호작용 시간에 맞춰 Radial360 으로 0→1 채움
            if (keyIconOutline != null)
            {
                bool hold = target.IsHold;
                if (hold)
                {
                    if (!keyIconOutline.gameObject.activeSelf) keyIconOutline.gameObject.SetActive(true);
                    keyIconOutline.fillAmount = Mathf.Clamp01(target.HoldProgress01);
                }
                else if (outlineAlwaysVisible)
                {
                    if (!keyIconOutline.gameObject.activeSelf) keyIconOutline.gameObject.SetActive(true);
                    keyIconOutline.fillAmount = Mathf.Clamp01(outlineNonHoldFill);
                }
                else
                {
                    if (keyIconOutline.gameObject.activeSelf) keyIconOutline.gameObject.SetActive(false);
                }
            }

            // 진행바 (홀드형만)
            if (progressFill != null)
            {
                bool hold = target.IsHold;
                progressFill.transform.parent.gameObject.SetActive(hold);
                if (hold) progressFill.fillAmount = Mathf.Clamp01(target.HoldProgress01);
            }

            // 위치: followTarget 이 켜져 있을 때만 월드 앵커를 따라감 (끄면 디자인한 고정 위치 유지)
            if (followTarget)
            {
                var anchor = target.PromptAnchor;
                if (anchor != null && worldCamera != null && rootRt != null)
                {
                    Vector3 worldPos = anchor.position + Vector3.up * worldHeightOffset;
                    Vector3 sp = worldCamera.WorldToScreenPoint(worldPos);
                    // 카메라 뒤면 숨김
                    if (sp.z < 0f) { SetPromptVisible(false); return; }
                    rootRt.position = new Vector3(sp.x + screenOffset.x, sp.y + screenOffset.y, 0f);
                }
            }
        }

        private bool IsPromptVisible()
        {
            if (promptRoot == null) return false;
            if (promptRootIsSelf)
            {
                foreach (Transform child in transform)
                {
                    if (child.gameObject.activeSelf) return true;
                }
                return false;
            }
            return promptRoot.activeSelf;
        }

        private void SetPromptVisible(bool show)
        {
            if (promptRoot == null) return;

            if (promptRootIsSelf)
            {
                foreach (Transform child in transform)
                    child.gameObject.SetActive(show);
                return;
            }

            promptRoot.SetActive(show);
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

                // 외곽 링 (자동 생성 시 흰 박스 — 가운데가 비어 있는 링 이미지로 교체 권장)
                // 키 아이콘보다 먼저 만들어서 아이콘 뒤에 깔리도록 함.
                if (keyIconOutline == null)
                {
                    keyIconOutline = UIRoot.CreatePanel("KeyIconOutline", rootRt, new Color(1, 1, 1, 0.85f));
                    var ort = keyIconOutline.rectTransform;
                    ort.anchorMin = ort.anchorMax = new Vector2(0, 0.5f);
                    ort.pivot = new Vector2(0, 0.5f);
                    ort.sizeDelta = new Vector2(58, 58);             // 아이콘(48) 보다 살짝 크게
                    ort.anchoredPosition = new Vector2(-5, 0);       // 아이콘 중심에 맞춰 살짝 좌측으로 (피벗 보정)
                    keyIconOutline.type = Image.Type.Filled;
                    keyIconOutline.fillMethod = Image.FillMethod.Radial360;
                    keyIconOutline.fillOrigin = (int)Image.Origin360.Top;
                    keyIconOutline.fillClockwise = true;
                    keyIconOutline.fillAmount = 0f;
                    keyIconOutline.raycastTarget = false;
                }

                // F 아이콘 (자동 생성 시 흰 박스 — 직접 이미지로 교체 권장)
                if (keyIcon == null)
                {
                    keyIcon = UIRoot.CreatePanel("KeyIcon", rootRt, new Color(1, 1, 1, 0.95f));
                    var krt = keyIcon.rectTransform;
                    krt.anchorMin = krt.anchorMax = new Vector2(0, 0.5f);
                    krt.pivot = new Vector2(0, 0.5f);
                    krt.sizeDelta = new Vector2(48, 48);
                    krt.anchoredPosition = new Vector2(0, 0);
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
