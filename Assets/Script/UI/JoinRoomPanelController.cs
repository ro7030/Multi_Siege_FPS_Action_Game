using System.Collections.Generic;
using System.Text;
using ProjectM.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectM.UI
{
    /// <summary>
    /// 방 참여 패널.
    /// - 목록에 RoomListEntry들을 행으로 표시.
    /// - 행을 클릭하면 그 방이 선택되고, 비밀번호가 있는 방이면 비밀번호 입력 영역이 활성화된다.
    /// - "번호 표시" 토글로 마스킹 on/off (이 패널 안에서만, 닫으면 자동 마스킹).
    /// - "참여"는 선택된 방에 RoomManager.JoinRoom으로 접속한 뒤 게임플레이 씬을 로드.
    /// </summary>
    public class JoinRoomPanelController : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private MainMenuController mainMenu;
        [SerializeField] private RoomManager roomManager;

        [Header("목록")]
        [Tooltip("행 프리팹. JoinRoomItem 컴포넌트가 부착되어 있어야 한다.")]
        [SerializeField] private JoinRoomItem rowPrefab;
        [Tooltip("프리팹 인스턴스가 들어갈 부모 (Content). VerticalLayoutGroup 권장.")]
        [SerializeField] private Transform listContent;

        [Header("비밀번호 영역")]
        [Tooltip("비밀번호 입력 영역 전체. 비밀번호 있는 방을 선택했을 때만 활성화.")]
        [SerializeField] private GameObject passwordSection;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private Toggle showPasswordToggle;

        [Header("닉네임 (선택)")]
        [SerializeField] private TMP_InputField nicknameInput;

        [Header("버튼")]
        [SerializeField] private Button joinButton;
        [SerializeField] private Button cancelButton;

        [Header("옵션")]
        [SerializeField] private int passwordMaxLength = 4;
        [Tooltip("테스트용 더미 데이터. 실제 매치메이킹이 들어가기 전까지 목록을 비우면 이걸로 채운다.")]
        [SerializeField] private List<RoomListEntry> sampleEntries = new();

        private const TMP_InputField.ContentType MaskedContent  = TMP_InputField.ContentType.Pin;
        private const TMP_InputField.ContentType VisibleContent = TMP_InputField.ContentType.IntegerNumber;

        private readonly List<JoinRoomItem> spawnedRows = new();
        private JoinRoomItem selectedRow;

        private void Awake()
        {
            if (roomManager == null) roomManager = FindObjectOfType<RoomManager>();
            if (mainMenu == null)    mainMenu    = FindObjectOfType<MainMenuController>();
        }

        private void OnEnable()
        {
            // 비밀번호 입력 초기화
            if (passwordInput != null)
            {
                passwordInput.characterLimit = passwordMaxLength;
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

            // 버튼
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

            // 목록이 비어 있다면 sampleEntries로 채워서 UI 테스트 가능
            if (spawnedRows.Count == 0 && sampleEntries != null && sampleEntries.Count > 0)
                SetEntries(sampleEntries);

            UpdatePasswordSection();
            UpdateJoinButton();
        }

        private void OnDisable()
        {
            // 닫으면 마스킹 상태로 되돌리고 비밀번호 비우기
            if (passwordInput != null)
            {
                passwordInput.contentType = MaskedContent;
                passwordInput.text = "";
                passwordInput.ForceLabelUpdate();
            }
            if (showPasswordToggle != null) showPasswordToggle.SetIsOnWithoutNotify(false);
        }

        // ── 외부에서 목록 채우기 ───────────────────────────────────
        public void SetEntries(IList<RoomListEntry> entries)
        {
            ClearRows();
            if (entries == null || rowPrefab == null || listContent == null) { UpdateJoinButton(); return; }

            foreach (var e in entries)
            {
                var row = Instantiate(rowPrefab, listContent);
                row.Bind(e);
                row.OnSelected += HandleRowSelected;
                spawnedRows.Add(row);
            }
            selectedRow = null;
            UpdatePasswordSection();
            UpdateJoinButton();
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

        // ── 선택 ──────────────────────────────────────────────────
        private void HandleRowSelected(JoinRoomItem row)
        {
            if (selectedRow == row) return;
            if (selectedRow != null) selectedRow.SetSelected(false);
            selectedRow = row;
            if (selectedRow != null) selectedRow.SetSelected(true);

            if (passwordInput != null) passwordInput.text = "";
            UpdatePasswordSection();
            UpdateJoinButton();
        }

        private void UpdatePasswordSection()
        {
            bool needsPassword = selectedRow != null && selectedRow.Entry != null && selectedRow.Entry.hasPassword;
            if (passwordSection != null) passwordSection.SetActive(needsPassword);
            if (passwordInput != null) passwordInput.interactable = needsPassword;
            if (showPasswordToggle != null) showPasswordToggle.interactable = needsPassword;
        }

        private void UpdateJoinButton()
        {
            if (joinButton == null) return;
            bool hasSelection = selectedRow != null && selectedRow.Entry != null;
            bool needsPw = hasSelection && selectedRow.Entry.hasPassword;
            bool pwOk = !needsPw || (passwordInput != null && SanitizeDigits(passwordInput.text, passwordMaxLength).Length > 0);
            joinButton.interactable = hasSelection && pwOk;
        }

        // ── 비밀번호 입력 동작 ────────────────────────────────────
        private void OnPasswordValueChanged(string value)
        {
            string sanitized = SanitizeDigits(value, passwordMaxLength);
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
            passwordInput.characterLimit = passwordMaxLength;
            passwordInput.text = SanitizeDigits(current, passwordMaxLength);
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

        // ── 버튼 ──────────────────────────────────────────────────
        private void OnJoinClicked()
        {
            if (selectedRow == null || selectedRow.Entry == null)
            {
                Debug.LogWarning("[JoinRoomPanel] 선택된 방이 없다.");
                return;
            }
            if (roomManager == null)
            {
                Debug.LogError("[JoinRoomPanel] RoomManager 참조가 없다.");
                return;
            }

            var entry = selectedRow.Entry;
            string nickname = nicknameInput != null ? nicknameInput.text?.Trim() : "";
            string password = entry.hasPassword && passwordInput != null
                ? SanitizeDigits(passwordInput.text, passwordMaxLength)
                : string.Empty;

            string ip = string.IsNullOrEmpty(entry.hostIp) ? "127.0.0.1" : entry.hostIp;

            bool ok = roomManager.JoinRoom(ip, nickname, password);
            if (!ok)
            {
                Debug.LogError($"[JoinRoomPanel] 접속 실패: {entry.roomName} @ {ip}");
                return;
            }

            Debug.Log($"[JoinRoomPanel] 참여: name='{entry.roomName}' ip={ip} hasPw={entry.hasPassword}");

            gameObject.SetActive(false);
            if (mainMenu != null) mainMenu.LoadGameplayScene();
        }

        private void OnCancelClicked() => gameObject.SetActive(false);
    }
}
