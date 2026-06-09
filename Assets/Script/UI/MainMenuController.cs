using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectM.UI
{
    /// <summary>
    /// 메인 메뉴 컨트롤러. 각 버튼에 OnClick으로 메서드를 연결한다.
    /// "게임 시작" 버튼은 즉시 씬을 로드하지 않고 방 생성 패널을 연다.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("씬 이름")]
        [SerializeField] private string gameplaySceneName = "Gameplay";

        [Header("패널 (선택)")]
        [SerializeField] private GameObject createRoomPanel;
        [SerializeField] private GameObject characterSelectPanel;
        [SerializeField] private GameObject roomListPanel;
        [SerializeField] private GameObject joinRoomPanel;
        [SerializeField] private GameObject settingsPanel;

        [Header("버튼 (Inspector에서 연결, OnClick으로도 가능)")]
        [Tooltip("기존 'startGameButton'. 이제 방 생성 패널을 여는 역할을 한다.")]
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button roomListButton;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private Button characterButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button exitButton;

        public string GameplaySceneName => gameplaySceneName;

        private void Start()
        {
            // 코드로 연결 (Inspector OnClick 사용 안 해도 됨)
            if (startGameButton != null)  startGameButton.onClick.AddListener(OnStartGame);
            if (roomListButton != null)   roomListButton.onClick.AddListener(OnRoomList);
            if (joinRoomButton != null)   joinRoomButton.onClick.AddListener(OnJoinRoom);
            if (characterButton != null)  characterButton.onClick.AddListener(OnCharacterSelect);
            if (settingsButton != null)   settingsButton.onClick.AddListener(OnSettings);
            if (exitButton != null)       exitButton.onClick.AddListener(OnExit);

            // 시작 시 모든 서브 패널 닫기
            HideAllPanels();
        }

        private void Update()
        {
            // ESC를 누르면 열려 있는 패널을 닫는다.
            // (열려 있는 패널이 여러 개일 일은 TogglePanel 로직상 없지만, 방어적으로 전부 닫는다)
            var kb = Keyboard.current;
            if (kb == null) return;
            if (!kb.escapeKey.wasPressedThisFrame) return;
            if (!IsAnyPanelOpen()) return;

            HideAllPanels();
        }

        // ── 버튼 동작 ──────────────────────────────────────────────
        public void OnStartGame()
        {
            Debug.Log("[MainMenu] 방 만들기 패널 열기");
            TogglePanel(createRoomPanel);
        }

        /// <summary>
        /// CreateRoomPanelController가 방 생성을 마치고 곧바로 게임플레이로 진입할 때 호출.
        /// </summary>
        public void LoadGameplayScene()
        {
            if (string.IsNullOrEmpty(gameplaySceneName))
            {
                Debug.LogError("[MainMenu] gameplaySceneName이 비어 있다.");
                return;
            }
            SceneManager.LoadScene(gameplaySceneName);
        }

        public void OnRoomList()
        {
            Debug.Log("[MainMenu] 방 목록 (미구현)");
            TogglePanel(roomListPanel);
        }

        public void OnJoinRoom()
        {
            Debug.Log($"[MainMenu] 방 참여 클릭. joinRoomPanel={(joinRoomPanel == null ? "NULL" : joinRoomPanel.name)}");
            TogglePanel(joinRoomPanel);
        }

        public void OnCharacterSelect()
        {
            Debug.Log("[MainMenu] 캐릭터 선택");
            TogglePanel(characterSelectPanel);
        }

        public void OnSettings()
        {
            Debug.Log("[MainMenu] 환경 설정");
            TogglePanel(settingsPanel);
        }

        public void OnExit()
        {
            Debug.Log("[MainMenu] 종료");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ── 패널 토글 ──────────────────────────────────────────────
        private void TogglePanel(GameObject panel)
        {
            if (panel == null) return;
            bool willOpen = !panel.activeSelf;
            HideAllPanels();
            panel.SetActive(willOpen);
        }

        public void CloseAllPanels() => HideAllPanels();

        private bool IsAnyPanelOpen()
        {
            if (createRoomPanel != null && createRoomPanel.activeSelf) return true;
            if (characterSelectPanel != null && characterSelectPanel.activeSelf) return true;
            if (roomListPanel != null && roomListPanel.activeSelf) return true;
            if (joinRoomPanel != null && joinRoomPanel.activeSelf) return true;
            if (settingsPanel != null && settingsPanel.activeSelf) return true;
            return false;
        }

        private void HideAllPanels()
        {
            if (createRoomPanel != null) createRoomPanel.SetActive(false);
            if (characterSelectPanel != null) characterSelectPanel.SetActive(false);
            if (roomListPanel != null) roomListPanel.SetActive(false);
            if (joinRoomPanel != null) joinRoomPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
        }
    }
}
