using System;
using ProjectM.Auth;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectM.UI
{
    public class LoginController : MonoBehaviour
    {
        private const string DefaultFontResourcePath = "Fonts/Jalnan2/Jalnan2TTF SDF";

        [Header("Font")]
        [SerializeField] private TMP_FontAsset uiFont;

        [Header("UI")]
        [SerializeField] private TMP_InputField nicknameInput;
        [SerializeField] private Button loginButton;
        [SerializeField] private Button guestButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject loadingOverlay;

        [Header("Scene")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("Validation")]
        [SerializeField] private int minNicknameLength = 2;
        [SerializeField] private int maxNicknameLength = 16;

        private UnityAuthService authService;
        private bool isBusy;

        private void Awake()
        {
            if (uiFont == null)
                uiFont = Resources.Load<TMP_FontAsset>(DefaultFontResourcePath);

            ApplyFontToUi();
            authService = GetComponent<UnityAuthService>();
            if (authService == null)
                authService = gameObject.AddComponent<UnityAuthService>();
        }

        private void Start()
        {
            if (loginButton != null) loginButton.onClick.AddListener(OnLoginClicked);
            if (guestButton != null) guestButton.onClick.AddListener(OnGuestClicked);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
            SetStatus(string.Empty);
            SetBusy(false);
        }

        private void OnQuitClicked()
        {
            if (isBusy) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ApplyFontToUi()
        {
            if (uiFont == null) return;

            ApplyFontToText(statusText);
            ApplyFontToInput(nicknameInput);

            if (loginButton != null) ApplyFontToText(loginButton.GetComponentInChildren<TMP_Text>());
            if (guestButton != null) ApplyFontToText(guestButton.GetComponentInChildren<TMP_Text>());
            if (quitButton != null) ApplyFontToText(quitButton.GetComponentInChildren<TMP_Text>());

            foreach (var text in GetComponentsInChildren<TMP_Text>(true))
                ApplyFontToText(text);
            foreach (var input in GetComponentsInChildren<TMP_InputField>(true))
                ApplyFontToInput(input);
        }

        private void ApplyFontToText(TMP_Text text)
        {
            if (text == null || uiFont == null) return;
            text.font = uiFont;
        }

        private void ApplyFontToInput(TMP_InputField input)
        {
            if (input == null || uiFont == null) return;
            if (input.textComponent != null) input.textComponent.font = uiFont;
            if (input.placeholder is TMP_Text placeholder) placeholder.font = uiFont;
        }

        private async void OnLoginClicked()
        {
            if (isBusy) return;
            if (!TryValidateNickname(out string nickname)) return;

            await RunSignInAsync(
                nickname,
                isGuest: false,
                busyMessage: "Unity 계정으로 로그인 중...",
                signInAction: () => authService.SignInWithPlayerAccountAsync());
        }

        private async void OnGuestClicked()
        {
            if (isBusy) return;
            if (!TryValidateNickname(out string nickname)) return;

            await RunSignInAsync(
                nickname,
                isGuest: true,
                busyMessage: "게스트 로그인 중...",
                signInAction: () => authService.SignInAsGuestAsync());
        }

        private async System.Threading.Tasks.Task RunSignInAsync(
            string nickname,
            bool isGuest,
            string busyMessage,
            Func<System.Threading.Tasks.Task<string>> signInAction)
        {
            SetBusy(true, busyMessage);

            try
            {
                string playerId = await signInAction();
                EnsureAuthSessionManager();
                AuthSessionManager.Instance.SetSession(nickname, playerId, isGuest);
                SceneManager.LoadScene(mainMenuSceneName);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                SetStatus(isGuest
                    ? $"게스트 로그인 실패: {ex.Message}"
                    : $"로그인 실패: {ex.Message}");
                SetBusy(false);
            }
        }

        private bool TryValidateNickname(out string nickname)
        {
            nickname = nicknameInput != null ? nicknameInput.text?.Trim() : string.Empty;

            if (string.IsNullOrEmpty(nickname))
            {
                SetStatus("닉네임을 입력해 주세요.");
                return false;
            }

            if (nickname.Length < minNicknameLength)
            {
                SetStatus($"닉네임은 {minNicknameLength}자 이상이어야 합니다.");
                return false;
            }

            if (nickname.Length > maxNicknameLength)
            {
                SetStatus($"닉네임은 {maxNicknameLength}자 이하로 입력해 주세요.");
                return false;
            }

            SetStatus(string.Empty);
            return true;
        }

        private static void EnsureAuthSessionManager()
        {
            if (AuthSessionManager.Instance != null) return;
            var go = new GameObject(nameof(AuthSessionManager));
            go.AddComponent<AuthSessionManager>();
        }

        private void SetBusy(bool busy, string message = null)
        {
            isBusy = busy;
            if (loginButton != null) loginButton.interactable = !busy;
            if (guestButton != null) guestButton.interactable = !busy;
            if (nicknameInput != null) nicknameInput.interactable = !busy;
            if (loadingOverlay != null) loadingOverlay.SetActive(busy);
            if (!string.IsNullOrEmpty(message)) SetStatus(message);
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }
    }
}
