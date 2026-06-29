using ProjectM.Auth;
using ProjectM.Network;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectM.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("씬 이름")]
        [SerializeField] private string characterSelectSceneName = "CharacterSelect";
        [SerializeField] private string loginSceneName = "Login";

        [Header("패널 (선택)")]
        [SerializeField] private GameObject createRoomPanel;
        [SerializeField] private GameObject characterSelectPanel;
        [SerializeField] private GameObject roomListPanel;
        [SerializeField] private GameObject joinRoomPanel;
        [SerializeField] private GameObject settingsPanel;

        [Header("버튼")]
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button roomListButton;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private Button characterButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button exitButton;

        public string CharacterSelectSceneName => characterSelectSceneName;

        private void Start()
        {
            if (startGameButton != null) startGameButton.onClick.AddListener(OnStartGame);
            if (roomListButton != null) roomListButton.onClick.AddListener(OnRoomList);
            if (joinRoomButton != null) joinRoomButton.onClick.AddListener(OnJoinRoom);
            if (characterButton != null) characterButton.onClick.AddListener(OnCharacterSelect);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettings);
            if (exitButton != null) exitButton.onClick.AddListener(OnLogout);

            HideAllPanels();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (!kb.escapeKey.wasPressedThisFrame) return;
            if (!IsAnyPanelOpen()) return;
            HideAllPanels();
        }

        public void OnStartGame()
        {
            TogglePanel(createRoomPanel);
        }

        public void LoadCharacterSelectScene()
        {
            if (string.IsNullOrEmpty(characterSelectSceneName))
            {
                Debug.LogError("[MainMenu] characterSelectSceneName is empty.");
                return;
            }

            var lobby = LobbyRelayService.Instance;
            var nm = NetworkManager.Singleton;

            if (lobby != null && lobby.IsInSession && nm != null && nm.IsListening)
            {
                if (nm.IsHost)
                    nm.SceneManager.LoadScene(characterSelectSceneName, LoadSceneMode.Single);
                return;
            }

            SceneManager.LoadScene(characterSelectSceneName);
        }

        public void OnRoomList()
        {
            TogglePanel(roomListPanel);
        }

        public void OnJoinRoom()
        {
            TogglePanel(joinRoomPanel);
            if (joinRoomPanel != null && joinRoomPanel.activeSelf)
            {
                var joinCtrl = FindAnyObjectByType<JoinRoomPanelController>();
                joinCtrl?.RefreshNow();
            }
        }

        public void OnCharacterSelect()
        {
            TogglePanel(characterSelectPanel);
        }

        public void OnSettings()
        {
            TogglePanel(settingsPanel);
        }

        public async void OnLogout()
        {
            if (LobbyRelayService.Instance != null)
                await LobbyRelayService.Instance.LeaveSessionAsync();

            if (AuthenticationService.Instance.IsSignedIn)
                AuthenticationService.Instance.SignOut(true);

            AuthSessionManager.Instance?.ClearSession();

            if (string.IsNullOrEmpty(loginSceneName))
            {
                Debug.LogError("[MainMenu] loginSceneName is empty.");
                return;
            }

            SceneManager.LoadScene(loginSceneName);
        }

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
