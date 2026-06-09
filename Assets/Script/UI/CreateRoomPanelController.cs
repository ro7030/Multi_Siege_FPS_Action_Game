using System.Text;
using ProjectM.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectM.UI
{
    /// <summary>
    /// 방 만들기 패널 컨트롤러.
    /// - 방 이름 입력
    /// - 공개 / 비공개 토글 (둘 중 하나만 선택)
    /// - 비밀번호 입력 (최대 4자리 숫자, 비공개일 때만 사용)
    /// - "번호 표시" 토글: 비밀번호 InputField의 마스킹을 끄거나 켠다. (이 패널 안에서만 적용)
    /// - "생성" 버튼: RoomManager.CreateRoom() 호출 후 게임플레이 씬 로드.
    /// </summary>
    public class CreateRoomPanelController : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private MainMenuController mainMenu;
        [SerializeField] private RoomManager roomManager;

        [Header("입력 필드")]
        [SerializeField] private TMP_InputField roomNameInput;
        [SerializeField] private TMP_InputField passwordInput;

        [Header("닉네임 (선택)")]
        [Tooltip("닉네임 입력 필드. 비워두면 'Host'가 기본값.")]
        [SerializeField] private TMP_InputField nicknameInput;

        [Header("토글")]
        [Tooltip("공개 토글. 체크되면 공개, 해제되면 비공개.")]
        [SerializeField] private Toggle publicToggle;
        [Tooltip("비공개 토글. publicToggle과 상호 배타.")]
        [SerializeField] private Toggle privateToggle;
        [Tooltip("'번호 표시' 토글. 체크 시 비밀번호 입력값이 마스킹되지 않고 그대로 보인다.")]
        [SerializeField] private Toggle showPasswordToggle;

        [Header("버튼")]
        [SerializeField] private Button createButton;
        [SerializeField] private Button cancelButton;

        [Header("옵션")]
        [Tooltip("비밀번호 최대 자릿수.")]
        [SerializeField] private int passwordMaxLength = 4;

        private const TMP_InputField.ContentType MaskedContent  = TMP_InputField.ContentType.Pin;     // 숫자만 + 마스킹
        private const TMP_InputField.ContentType VisibleContent = TMP_InputField.ContentType.IntegerNumber; // 숫자만 + 평문

        private void Awake()
        {
            if (roomManager == null) roomManager = FindObjectOfType<RoomManager>();
            if (mainMenu == null)    mainMenu    = FindObjectOfType<MainMenuController>();
        }

        private void OnEnable()
        {
            // 입력 필드 초기화
            if (passwordInput != null)
            {
                passwordInput.characterLimit = passwordMaxLength;
                passwordInput.contentType = MaskedContent;
                passwordInput.onValueChanged.RemoveListener(OnPasswordValueChanged);
                passwordInput.onValueChanged.AddListener(OnPasswordValueChanged);
                passwordInput.ForceLabelUpdate();
            }

            // 공개/비공개 토글: 상호 배타. 패널이 열릴 때마다 둘 다 OFF로 시작 → 사용자가 직접 선택.
            if (publicToggle != null)
            {
                publicToggle.onValueChanged.RemoveListener(OnPublicToggleChanged);
                publicToggle.SetIsOnWithoutNotify(false);
                publicToggle.onValueChanged.AddListener(OnPublicToggleChanged);
            }
            if (privateToggle != null)
            {
                privateToggle.onValueChanged.RemoveListener(OnPrivateToggleChanged);
                privateToggle.SetIsOnWithoutNotify(false);
                privateToggle.onValueChanged.AddListener(OnPrivateToggleChanged);
            }

            // 번호 표시 토글도 OFF로 시작 → 비밀번호 영역은 비공개가 켜졌을 때만 의미가 있다
            if (showPasswordToggle != null)
            {
                showPasswordToggle.onValueChanged.RemoveListener(OnShowPasswordToggleChanged);
                showPasswordToggle.SetIsOnWithoutNotify(false);
                showPasswordToggle.onValueChanged.AddListener(OnShowPasswordToggleChanged);
            }

            // 초기 상태: 둘 다 꺼져 있으므로 비밀번호 비활성
            UpdatePasswordAreaInteractable();
            ApplyShowPassword(false);

            // 버튼
            if (createButton != null)
            {
                createButton.onClick.RemoveListener(OnCreateClicked);
                createButton.onClick.AddListener(OnCreateClicked);
            }
            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(OnCancelClicked);
                cancelButton.onClick.AddListener(OnCancelClicked);
            }
        }

        private void OnDisable()
        {
            // 패널을 닫을 때는 비밀번호가 다시 보이지 않도록 마스킹으로 복귀
            if (passwordInput != null)
            {
                passwordInput.contentType = MaskedContent;
                passwordInput.ForceLabelUpdate();
            }
            if (showPasswordToggle != null) showPasswordToggle.SetIsOnWithoutNotify(false);
        }

        // ── 토글 핸들러 ───────────────────────────────────────────
        // 공개/비공개는 둘 중 하나만 켜질 수 있지만, "둘 다 꺼짐" 도 허용한다 (초기 상태).
        // 사용자가 둘 다 꺼두면 생성 버튼 단계에서 막는다.
        private void OnPublicToggleChanged(bool isOn)
        {
            if (isOn && privateToggle != null) privateToggle.SetIsOnWithoutNotify(false);
            UpdatePasswordAreaInteractable();
        }

        private void OnPrivateToggleChanged(bool isOn)
        {
            if (isOn && publicToggle != null) publicToggle.SetIsOnWithoutNotify(false);
            UpdatePasswordAreaInteractable();
        }

        private void UpdatePasswordAreaInteractable()
        {
            // 비공개 토글이 켜졌을 때만 비밀번호 입력/표시 가능.
            bool isPrivate = privateToggle != null && privateToggle.isOn;
            if (passwordInput != null) passwordInput.interactable = isPrivate;
            if (showPasswordToggle != null) showPasswordToggle.interactable = isPrivate;
        }

        private void OnShowPasswordToggleChanged(bool isOn) => ApplyShowPassword(isOn);

        private void ApplyShowPassword(bool show)
        {
            if (passwordInput == null) return;
            // 캐럿 위치 보존해서 contentType 교체
            int caret = passwordInput.caretPosition;
            string current = passwordInput.text;
            passwordInput.contentType = show ? VisibleContent : MaskedContent;
            passwordInput.characterLimit = passwordMaxLength;
            // contentType을 바꾸면 text가 리셋되는 경우가 있어 명시적으로 다시 세팅
            passwordInput.text = SanitizeDigits(current, passwordMaxLength);
            passwordInput.caretPosition = Mathf.Clamp(caret, 0, passwordInput.text.Length);
            passwordInput.ForceLabelUpdate();
        }

        // ── 입력 검증 ─────────────────────────────────────────────
        private void OnPasswordValueChanged(string value)
        {
            string sanitized = SanitizeDigits(value, passwordMaxLength);
            if (sanitized != value)
            {
                passwordInput.SetTextWithoutNotify(sanitized);
                passwordInput.caretPosition = sanitized.Length;
            }
        }

        private static string SanitizeDigits(string input, int maxLen)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var sb = new StringBuilder(maxLen);
            foreach (var ch in input)
            {
                if (ch >= '0' && ch <= '9')
                {
                    sb.Append(ch);
                    if (sb.Length >= maxLen) break;
                }
            }
            return sb.ToString();
        }

        // ── 버튼 핸들러 ───────────────────────────────────────────
        private void OnCreateClicked()
        {
            string roomName = roomNameInput != null ? roomNameInput.text?.Trim() : "";
            string nickname = nicknameInput != null ? nicknameInput.text?.Trim() : "";
            bool publicOn  = publicToggle  != null && publicToggle.isOn;
            bool privateOn = privateToggle != null && privateToggle.isOn;
            bool isPublic  = publicOn;
            string password = privateOn && passwordInput != null
                ? SanitizeDigits(passwordInput.text, passwordMaxLength)
                : string.Empty;

            if (string.IsNullOrEmpty(roomName))
            {
                Debug.LogWarning("[CreateRoomPanel] 방 이름이 비어 있다.");
                // TODO: 사용자에게 토스트/배너로 알림. 지금은 진행 막기.
                return;
            }
            if (!publicOn && !privateOn)
            {
                Debug.LogWarning("[CreateRoomPanel] 공개/비공개 중 하나를 선택해야 한다.");
                return;
            }
            if (privateOn && string.IsNullOrEmpty(password))
            {
                Debug.LogWarning("[CreateRoomPanel] 비공개 방인데 비밀번호가 비어 있다.");
                return;
            }

            if (roomManager == null)
            {
                Debug.LogError("[CreateRoomPanel] RoomManager 참조가 없다.");
                return;
            }

            bool ok = roomManager.CreateRoom(nickname, roomName, isPublic, password);
            if (!ok)
            {
                Debug.LogError("[CreateRoomPanel] 방 생성 실패.");
                return;
            }

            Debug.Log($"[CreateRoomPanel] 방 생성: name='{roomName}' public={isPublic} hasPw={!string.IsNullOrEmpty(password)}");

            gameObject.SetActive(false);
            if (mainMenu != null) mainMenu.LoadGameplayScene();
        }

        private void OnCancelClicked()
        {
            gameObject.SetActive(false);
        }
    }
}
