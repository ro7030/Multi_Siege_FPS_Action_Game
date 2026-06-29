using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace ProjectM.CharacterSelect
{
    /// <summary>
    /// LobbyScene 방식 UI — Host Start / Guest Ready·Cancel, 슬롯 World Space 갱신.
    /// </summary>
    public class CharacterSelectLobbyUIController : MonoBehaviour
    {
        private const string HostActionLabel = "Start";
        private const string GuestActionLabel = "Ready";
        private const string GuestCancelLabel = "Cancel";

        [Header("Services")]
        [SerializeField] private RoomServiceBootstrapper roomServiceBootstrapper;
        [SerializeField] private MonoBehaviour roomServiceObject;

        [Header("Slots")]
        [SerializeField] private CharacterSelectSlotView[] slotViews;

        [Header("Control Panel")]
        [SerializeField] private Button previousCharacterButton;
        [SerializeField] private Button nextCharacterButton;
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionButtonLabel;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text backButtonLabel;
        [SerializeField] private TMP_Text statusText;

        [Header("Scene Flow")]
        [SerializeField] private string gameplaySceneName = "GamePlay";

        private IRoomService _room;

        private void Awake()
        {
            ResolveRoomService();
            WireButtons();
        }

        private void OnEnable()
        {
            if (_room == null) ResolveRoomService();
            if (_room == null) return;
            _room.OnPlayerChanged += HandlePlayerChanged;
            _room.OnAllPlayersReady += HandleAllReady;
        }

        private void OnDisable()
        {
            if (_room == null) return;
            _room.OnPlayerChanged -= HandlePlayerChanged;
            _room.OnAllPlayersReady -= HandleAllReady;
        }

        private void Start()
        {
            if (_room == null) ResolveRoomService();
            if (_room == null) return;
            RefreshAllSlots();
            RefreshActionButton();
            RefreshCharacterButtons();
            RefreshBackButton();
            SetStatus(_room.IsLocalHost
                ? "All players ready? Press Start to begin."
                : "Select a character and press Ready.");
        }

        private void Update()
        {
            if (_room != null) return;
            ResolveRoomService();
            if (_room == null) return;
            _room.OnPlayerChanged += HandlePlayerChanged;
            _room.OnAllPlayersReady += HandleAllReady;
            RefreshAllSlots();
            RefreshActionButton();
            RefreshBackButton();
        }

        private void ResolveRoomService()
        {
            if (roomServiceBootstrapper == null)
                roomServiceBootstrapper = FindAnyObjectByType<RoomServiceBootstrapper>();

            if (roomServiceBootstrapper != null && roomServiceBootstrapper.ActiveRoomService != null)
                roomServiceObject = roomServiceBootstrapper.ActiveRoomService;

            _room = roomServiceObject as IRoomService;
        }

        private void WireButtons()
        {
            if (previousCharacterButton != null)
                previousCharacterButton.onClick.AddListener(OnPreviousCharacter);
            if (nextCharacterButton != null)
                nextCharacterButton.onClick.AddListener(OnNextCharacter);
            if (actionButton != null)
                actionButton.onClick.AddListener(OnActionClicked);
            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);
        }

        private void OnBackClicked()
        {
            _room?.LeaveRoom();
        }

        private void OnPreviousCharacter()
        {
            if (_room == null) return;
            var data = _room.GetPlayer(_room.LocalSlotIndex);
            if (!data.IsOccupied || data.IsReady) return;
            _room.SelectCharacter(data.CharacterIndex - 1);
        }

        private void OnNextCharacter()
        {
            if (_room == null) return;
            var data = _room.GetPlayer(_room.LocalSlotIndex);
            if (!data.IsOccupied || data.IsReady) return;
            _room.SelectCharacter(data.CharacterIndex + 1);
        }

        private void OnActionClicked()
        {
            if (_room == null) return;

            if (_room.IsLocalHost)
            {
                if (!_room.CanStartGame)
                {
                    SetStatus("All guests must be Ready before Start.");
                    return;
                }
                _room.TryStartGame();
                SetStatus("Starting game...");
                return;
            }

            var data = _room.GetPlayer(_room.LocalSlotIndex);
            _room.SetReady(!data.IsReady);
        }

        private void HandlePlayerChanged(int slotIndex)
        {
            RefreshSlot(slotIndex);
            if (slotIndex == _room.LocalSlotIndex || _room.IsLocalHost)
            {
                RefreshActionButton();
                RefreshCharacterButtons();
                RefreshBackButton();
            }
        }

        private void HandleAllReady()
        {
            if (_room is NetworkRoomService) return;
            if (string.IsNullOrEmpty(gameplaySceneName)) return;
            SceneManager.LoadScene(gameplaySceneName);
        }

        private void RefreshAllSlots()
        {
            if (_room == null || slotViews == null) return;
            foreach (var view in slotViews)
            {
                if (view != null) view.ClearSlot();
            }
            for (int i = 0; i < _room.MaxSlots; i++)
                RefreshSlot(i);
        }

        private void RefreshSlot(int slotIndex)
        {
            if (_room == null || slotViews == null) return;
            var data = _room.GetPlayer(slotIndex);
            foreach (var view in slotViews)
            {
                if (view != null && view.SlotIndex == slotIndex)
                {
                    view.ApplyPlayer(data);
                    break;
                }
            }
        }

        private void RefreshActionButton()
        {
            if (_room == null || actionButtonLabel == null) return;

            if (_room.IsLocalHost)
            {
                actionButtonLabel.text = HostActionLabel;
                if (actionButton != null) actionButton.interactable = _room.CanStartGame;
                return;
            }

            var data = _room.GetPlayer(_room.LocalSlotIndex);
            actionButtonLabel.text = data.IsReady ? GuestCancelLabel : GuestActionLabel;
            if (actionButton != null) actionButton.interactable = data.IsOccupied;

            if (data.IsReady)
                SetStatus("Ready! Waiting for host to Start...");
            else
                SetStatus("Select a character and press Ready.");
        }

        private void RefreshCharacterButtons()
        {
            if (_room == null) return;
            var data = _room.GetPlayer(_room.LocalSlotIndex);
            bool canChange = data.IsOccupied && !data.IsReady;
            if (previousCharacterButton != null) previousCharacterButton.interactable = canChange;
            if (nextCharacterButton != null) nextCharacterButton.interactable = canChange;
        }

        private void RefreshBackButton()
        {
            if (backButtonLabel == null || _room == null) return;
            backButtonLabel.text = _room.IsLocalHost ? "세션 종료" : "세션 나가기";
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }
    }
}
