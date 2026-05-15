using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectM.UI
{
    /// <summary>
    /// 메인 메뉴 컨트롤러. 각 버튼에 OnClick으로 메서드를 연결한다.
    /// MVP에서는 "게임 시작"만 활성화, 나머지는 추후 확장.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("씬 이름")]
        [SerializeField] private string gameplaySceneName = "Gameplay";

        [Header("패널 (선택)")]
        [SerializeField] private GameObject characterSelectPanel;
        [SerializeField] private GameObject roomListPanel;
        [SerializeField] private GameObject joinRoomPanel;
        [SerializeField] private GameObject settingsPanel;

        [Header("버튼 (Inspector에서 연결, OnClick으로도 가능)")]
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button roomListButton;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private Button characterButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button exitButton;

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

        // ── 버튼 동작 ──────────────────────────────────────────────
        public void OnStartGame()
        {
            Debug.Log("[MainMenu] 게임 시작");
            SceneManager.LoadScene(gameplaySceneName);
        }

        public void OnRoomList()
        {
            Debug.Log("[MainMenu] 방 목록 (미구현)");
            TogglePanel(roomListPanel);
        }

        public void OnJoinRoom()
        {
            Debug.Log("[MainMenu] 방 참여 (미구현)");
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

        private void HideAllPanels()
        {
            if (characterSelectPanel != null) characterSelectPanel.SetActive(false);
            if (roomListPanel != null) roomListPanel.SetActive(false);
            if (joinRoomPanel != null) joinRoomPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
        }
    }
}
