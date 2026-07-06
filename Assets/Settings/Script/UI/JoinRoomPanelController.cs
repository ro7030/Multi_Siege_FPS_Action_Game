using System.Collections.Generic;
using System.Text;
using ProjectM.Auth;
using ProjectM.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectM.UI
{
    // 방 참여 패널 — Lobby 목록 + ScrollView + 비밀번호 검증 + Relay Client.
    public class JoinRoomPanelController : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private MainMenuController mainMenu;
        [SerializeField] private LobbyRelayService lobbyRelayService;

        [Header("목록")]
        [SerializeField] private JoinRoomItem rowPrefab;
        [SerializeField] private Transform listContent;
        [SerializeField] private ScrollRect scrollRect;

        [Header("비밀번호 영역")]
        [SerializeField] private GameObject passwordSection;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private Toggle showPasswordToggle;

        [Header("닉네임 (선택)")]
        [SerializeField] private TMP_InputField nicknameInput;

        [Header("버튼")]
        [SerializeField] private Button joinButton;
        [SerializeField] private Button cancelButton;

        [Header("상태")]
        [SerializeField] private TMP_Text statusText;

        private const TMP_InputField.ContentType MaskedContent = TMP_InputField.ContentType.Pin;
        private const TMP_InputField.ContentType VisibleContent = TMP_InputField.ContentType.IntegerNumber;

        private readonly List<JoinRoomItem> spawnedRows = new();
        private JoinRoomItem selectedRow;
        private string selectedLobbyId;
        private bool isBusy;

        private static int PasswordLength => LobbyRelayService.LobbyPasswordLength;

        private void Awake()
        {
            if (lobbyRelayService == null) lobbyRelayService = LobbyRelayService.Instance;
            if (mainMenu == null) mainMenu = FindAnyObjectByType<MainMenuController>();
            EnsureListContentLayout();
        }

        // 방 참여 패널이 열릴 때 MainMenuController에서 호출.
        public void RefreshNow() => RefreshLobbyListAsync();

        private void OnEnable()
        {
            EnsureListContentLayout();
            if (passwordInput != null)
            {
                passwordInput.characterLimit = PasswordLength;
                passwordInput.contentType = MaskedContent;
                passwordInput.text = "";
                passwordInput.onValueChanged.RemoveListener(OnPasswordValueChanged);
                passwordInput.onValueChanged.AddListener(OnPasswordValueChanged);
                passwordInput.ForceLabelUpdate();
            }
            if (showPasswordToggle != null)
            {
                showPasswordToggle.SetIsOnWithoutNotify(false);
                showPasswordToggle.onValueChanged.RemoveListener(OnShowPasswordToggleChanged);
                showPasswordToggle.onValueChanged.AddListener(OnShowPasswordToggleChanged);
            }

            if (joinButton != null)
            {
                joinButton.onClick.RemoveListener(OnJoinClicked);
                joinButton.onClick.AddListener(OnJoinClicked);
            }
            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(OnCancelClicked);
                cancelButton.onClick.AddListener(OnCancelClicked);
            }

            PrefillNicknameFromSession();
            UpdatePasswordSection();
            UpdateJoinButton();
            RefreshLobbyListAsync();
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
                passwordInput.text = "";
                passwordInput.ForceLabelUpdate();
            }
            if (showPasswordToggle != null) showPasswordToggle.SetIsOnWithoutNotify(false);
        }

        private void EnsureListContentLayout()
        {
            if (listContent == null) return;

            var contentRect = listContent as RectTransform;
            if (contentRect != null)
            {
                contentRect.anchorMin = new Vector2(0f, 1f);
                contentRect.anchorMax = new Vector2(1f, 1f);
                contentRect.pivot = new Vector2(0.5f, 1f);
                contentRect.anchoredPosition = Vector2.zero;
            }

            var layout = listContent.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 8f;
                layout.padding = new RectOffset(8, 8, 8, 8);
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
            }

            if (listContent.GetComponent<ContentSizeFitter>() == null)
            {
                var fitter = listContent.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            if (scrollRect != null)
            {
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                if (scrollRect.content == null && contentRect != null)
                    scrollRect.content = contentRect;
            }
        }

        private async void RefreshLobbyListAsync()
        {
            if (lobbyRelayService == null)
            {
                lobbyRelayService = LobbyRelayService.Instance;
                if (lobbyRelayService == null)
                {
                    SetStatus("네트워크 서비스를 찾을 수 없습니다.");
                    return;
                }
            }

            SetBusy(true, "방 목록 불러오는 중...");
            try
            {
                var entries = await lobbyRelayService.QueryLobbiesAsync();
                SetEntries(entries);
                SetStatus(entries.Count == 0 ? "참여 가능한 방이 없습니다." : string.Empty);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                SetStatus($"목록 조회 실패: {ex.Message}");
                ClearRows();
            }
            finally
            {
                SetBusy(false);
            }
        }

        public void SetEntries(IList<RoomListEntry> entries)
        {
            string reselectId = selectedLobbyId;
            ClearRows();
            if (entries == null || rowPrefab == null || listContent == null)
            {
                UpdateJoinButton();
                return;
            }

            foreach (var e in entries)
            {
                var row = Instantiate(rowPrefab, listContent);
                row.gameObject.SetActive(true);
                row.Bind(e);
                row.OnSelected += HandleRowSelected;
                spawnedRows.Add(row);
            }

            RebuildListLayout();
            Debug.Log($"[JoinRoomPanel] Scroll list updated: {spawnedRows.Count} row(s).");

            selectedRow = null;
            if (!string.IsNullOrEmpty(reselectId))
            {
                foreach (var row in spawnedRows)
                {
                    if (row.Entry != null && row.Entry.lobbyId == reselectId)
                    {
                        selectedRow = row;
                        selectedLobbyId = reselectId;
                        row.SetSelected(true);
                        break;
                    }
                }
            }

            UpdatePasswordSection();
            UpdateJoinButton();
        }

        private void RebuildListLayout()
        {
            if (listContent is RectTransform contentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

            Canvas.ForceUpdateCanvases();

            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 1f;
        }

        private void ClearRows()
        {
            foreach (var row in spawnedRows)
            {
                if (row == null) continue;
                row.OnSelected -= HandleRowSelected;
                Destroy(row.gameObject);
            }
            spawnedRows.Clear();
            selectedRow = null;
        }

        private void HandleRowSelected(JoinRoomItem row)
        {
            if (selectedRow == row) return;
            if (selectedRow != null) selectedRow.SetSelected(false);
            selectedRow = row;
            selectedLobbyId = row.Entry?.lobbyId;
            if (selectedRow != null) selectedRow.SetSelected(true);

            if (passwordInput != null) passwordInput.text = "";
            SetStatus(string.Empty);
            UpdatePasswordSection();
            UpdateJoinButton();
        }

        private void UpdatePasswordSection()
        {
            bool needsPassword = selectedRow != null && selectedRow.Entry != null && selectedRow.Entry.hasPassword;
            if (passwordSection != null)
            {
                passwordSection.SetActive(needsPassword);
            }
            else
            {
                if (passwordInput != null) passwordInput.gameObject.SetActive(needsPassword);
                if (showPasswordToggle != null) showPasswordToggle.gameObject.SetActive(needsPassword);
            }
            if (passwordInput != null) passwordInput.interactable = needsPassword;
            if (showPasswordToggle != null) showPasswordToggle.interactable = needsPassword;
        }

        private void UpdateJoinButton()
        {
            if (joinButton == null) return;
            bool hasSelection = selectedRow != null && selectedRow.Entry != null;
            bool needsPw = hasSelection && selectedRow.Entry.hasPassword;
            bool pwOk = !needsPw || (passwordInput != null
                && SanitizeDigits(passwordInput.text, PasswordLength).Length == PasswordLength);
            joinButton.interactable = hasSelection && pwOk && !isBusy;
        }

        private void OnPasswordValueChanged(string value)
        {
            string sanitized = SanitizeDigits(value, PasswordLength);
            if (sanitized != value)
            {
                passwordInput.SetTextWithoutNotify(sanitized);
                passwordInput.caretPosition = sanitized.Length;
            }
            UpdateJoinButton();
        }

        private void OnShowPasswordToggleChanged(bool isOn)
        {
            if (passwordInput == null) return;
            int caret = passwordInput.caretPosition;
            string current = passwordInput.text;
            passwordInput.contentType = isOn ? VisibleContent : MaskedContent;
            passwordInput.characterLimit = PasswordLength;
            passwordInput.text = SanitizeDigits(current, PasswordLength);
            passwordInput.caretPosition = Mathf.Clamp(caret, 0, passwordInput.text.Length);
            passwordInput.ForceLabelUpdate();
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

        private async void OnJoinClicked()
        {
            if (isBusy || selectedRow == null || selectedRow.Entry == null) return;

            if (lobbyRelayService == null)
            {
                lobbyRelayService = LobbyRelayService.Instance;
                if (lobbyRelayService == null)
                {
                    SetStatus("네트워크 서비스를 찾을 수 없습니다.");
                    return;
                }
            }

            var entry = selectedRow.Entry;
            string password = entry.hasPassword && passwordInput != null
                ? SanitizeDigits(passwordInput.text, PasswordLength)
                : string.Empty;

            if (entry.hasPassword && password.Length != PasswordLength)
            {
                SetStatus("비밀번호 8자리를 입력해 주세요.");
                return;
            }

            SetBusy(true, "방 참여 중...");

            try
            {
                await lobbyRelayService.JoinRoomAsync(entry.lobbyId, password);
                gameObject.SetActive(false);
                if (mainMenu != null) mainMenu.LoadCharacterSelectScene();
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                SetStatus(ex.Message.Contains("비밀번호")
                    ? "비밀번호가 올바르지 않습니다."
                    : $"참여 실패: {ex.Message}");
                SetBusy(false);
            }
        }

        private void OnCancelClicked() => gameObject.SetActive(false);

        private void SetBusy(bool busy, string message = null)
        {
            isBusy = busy;
            if (!string.IsNullOrEmpty(message)) SetStatus(message);
            UpdateJoinButton();
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }
    }
}
