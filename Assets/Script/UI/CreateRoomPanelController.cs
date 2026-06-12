using System.Text;
using ProjectM.Auth;
using ProjectM.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectM.UI
{
    /// <summary>
    /// 방 만들기 패널 — Unity Lobby + Relay + NGO Host.
    /// </summary>
    public class CreateRoomPanelController : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private MainMenuController mainMenu;
        [SerializeField] private LobbyRelayService lobbyRelayService;

        [Header("입력 필드")]
        [SerializeField] private TMP_InputField roomNameInput;
        [SerializeField] private TMP_InputField passwordInput;

        [Header("닉네임 (선택)")]
        [SerializeField] private TMP_InputField nicknameInput;

        [Header("토글")]
        [SerializeField] private Toggle publicToggle;
        [SerializeField] private Toggle privateToggle;
        [SerializeField] private Toggle showPasswordToggle;

        [Header("버튼")]
        [SerializeField] private Button createButton;
        [SerializeField] private Button cancelButton;

        [Header("상태")]
        [SerializeField] private TMP_Text statusText;

        [Header("옵션")]
        [SerializeField] private int passwordMaxLength = LobbyRelayService.LobbyPasswordLength;

        private const TMP_InputField.ContentType MaskedContent = TMP_InputField.ContentType.Pin;
        private const TMP_InputField.ContentType VisibleContent = TMP_InputField.ContentType.IntegerNumber;

        private bool isBusy;

        private void Awake()
        {
            if (lobbyRelayService == null) lobbyRelayService = LobbyRelayService.Instance;
            if (mainMenu == null) mainMenu = FindAnyObjectByType<MainMenuController>();
        }

        private void OnEnable()
        {
            if (passwordInput != null)
            {
                passwordInput.characterLimit = passwordMaxLength;
                passwordInput.contentType = MaskedContent;
                passwordInput.onValueChanged.RemoveListener(OnPasswordValueChanged);
                passwordInput.onValueChanged.AddListener(OnPasswordValueChanged);
                passwordInput.ForceLabelUpdate();
            }

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
            if (showPasswordToggle != null)
            {
                showPasswordToggle.onValueChanged.RemoveListener(OnShowPasswordToggleChanged);
                showPasswordToggle.SetIsOnWithoutNotify(false);
                showPasswordToggle.onValueChanged.AddListener(OnShowPasswordToggleChanged);
            }

            UpdatePasswordAreaInteractable();
            ApplyShowPassword(false);
            SetStatus(string.Empty);

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

            PrefillNicknameFromSession();
        }

        private void PrefillNicknameFromSession()
        {
            if (nicknameInput == null) return;
            string nickname = AuthSessionManager.ResolveNickname(string.Empty);
            if (!string.IsNullOrEmpty(nickname))
                nicknameInput.text = nickname;
        }

        private void OnDisable()
        {
            if (passwordInput != null)
            {
                passwordInput.contentType = MaskedContent;
                passwordInput.ForceLabelUpdate();
            }
            if (showPasswordToggle != null) showPasswordToggle.SetIsOnWithoutNotify(false);
        }

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
            bool isPrivate = privateToggle != null && privateToggle.isOn;
            if (passwordInput != null) passwordInput.interactable = isPrivate;
            if (showPasswordToggle != null) showPasswordToggle.interactable = isPrivate;
        }

        private void OnShowPasswordToggleChanged(bool isOn) => ApplyShowPassword(isOn);

        private void ApplyShowPassword(bool show)
        {
            if (passwordInput == null) return;
            int caret = passwordInput.caretPosition;
            string current = passwordInput.text;
            passwordInput.contentType = show ? VisibleContent : MaskedContent;
            passwordInput.characterLimit = passwordMaxLength;
            passwordInput.text = SanitizeDigits(current, passwordMaxLength);
            passwordInput.caretPosition = Mathf.Clamp(caret, 0, passwordInput.text.Length);
            passwordInput.ForceLabelUpdate();
        }

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

        private async void OnCreateClicked()
        {
            if (isBusy) return;

            string roomName = roomNameInput != null ? roomNameInput.text?.Trim() : "";
            bool publicOn = publicToggle != null && publicToggle.isOn;
            bool privateOn = privateToggle != null && privateToggle.isOn;
            bool isPublic = publicOn;
            string password = privateOn && passwordInput != null
                ? SanitizeDigits(passwordInput.text, passwordMaxLength)
                : string.Empty;

            if (string.IsNullOrEmpty(roomName))
            {
                SetStatus("방 이름을 입력해 주세요.");
                return;
            }
            if (!publicOn && !privateOn)
            {
                SetStatus("공개 또는 비공개를 선택해 주세요.");
                return;
            }
            if (privateOn && string.IsNullOrEmpty(password))
            {
                SetStatus("비공개 방은 비밀번호가 필요합니다.");
                return;
            }
            if (privateOn && password.Length != LobbyRelayService.LobbyPasswordLength)
            {
                SetStatus("비밀번호 8자리를 입력해 주세요.");
                return;
            }

            if (lobbyRelayService == null)
            {
                lobbyRelayService = LobbyRelayService.Instance;
                if (lobbyRelayService == null)
                {
                    SetStatus("네트워크 서비스를 찾을 수 없습니다.");
                    return;
                }
            }

            SetBusy(true, "방 생성 중...");

            try
            {
                await lobbyRelayService.CreateRoomAsync(roomName, isPublic, password);
                gameObject.SetActive(false);
                if (mainMenu != null) mainMenu.LoadCharacterSelectScene();
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                SetStatus($"방 생성 실패: {ex.Message}");
                SetBusy(false);
            }
        }

        private void OnCancelClicked() => gameObject.SetActive(false);

        private void SetBusy(bool busy, string message = null)
        {
            isBusy = busy;
            if (createButton != null) createButton.interactable = !busy;
            if (cancelButton != null) cancelButton.interactable = !busy;
            if (!string.IsNullOrEmpty(message)) SetStatus(message);
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }
    }
}
