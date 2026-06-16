using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace ProjectM.CharacterSelect
{
    public class CharacterSelectManager : MonoBehaviour
    {
        [Header("Services")]
        [SerializeField] private RoomServiceBootstrapper roomServiceBootstrapper;
        [SerializeField] private MonoBehaviour roomServiceObject;
        [SerializeField] private CharacterDatabase database;

        [Header("Slot UI (index = slot index)")]
        [SerializeField] private PlayerSlotUI[] slotUIs;

        [Header("Buttons")]
        [SerializeField] private Button leftArrowButton;
        [SerializeField] private Button rightArrowButton;
        [SerializeField] private Button readyButton;
        [SerializeField] private Button exitRoomButton;
        [SerializeField] private TMP_Text readyButtonLabel;

        [Header("Scene Flow")]
        [SerializeField] private string gameplaySceneName = "GamePlay";

        [Header("Button Labels")]
        [SerializeField] private string startLabel = "Start";
        [SerializeField] private string readyLabel = "Ready";
        [SerializeField] private string cancelLabel = "Cancel";

        private IRoomService _room;

        private void Awake()
        {
            ResolveRoomService();
            if (_room == null)
                Debug.LogWarning($"[{nameof(CharacterSelectManager)}] IRoomService not ready in Awake; will retry in Start.", this);

            if (leftArrowButton != null)  leftArrowButton.onClick.AddListener(OnLeftArrow);
            if (rightArrowButton != null) rightArrowButton.onClick.AddListener(OnRightArrow);
            if (readyButton != null)      readyButton.onClick.AddListener(OnReadyClicked);
            if (exitRoomButton != null)   exitRoomButton.onClick.AddListener(OnExitClicked);
        }

        private void ResolveRoomService()
        {
            if (roomServiceBootstrapper == null)
                roomServiceBootstrapper = FindAnyObjectByType<RoomServiceBootstrapper>();

            if (roomServiceBootstrapper != null && roomServiceBootstrapper.ActiveRoomService != null)
                roomServiceObject = roomServiceBootstrapper.ActiveRoomService;

            _room = roomServiceObject as IRoomService;
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
            if (_room == null)
            {
                ResolveRoomService();
                if (_room != null)
                {
                    _room.OnPlayerChanged += HandlePlayerChanged;
                    _room.OnAllPlayersReady += HandleAllReady;
                }
            }

            if (_room == null) return;
            for (int i = 0; i < _room.MaxSlots; i++) HandlePlayerChanged(i);
            UpdateReadyButtonLabel();
            UpdateArrowButtons();
        }

        private void OnLeftArrow()
        {
            var data = _room.GetPlayer(_room.LocalSlotIndex);
            if (data.IsReady) return;
            _room.SelectCharacter(data.CharacterIndex - 1);
        }

        private void OnRightArrow()
        {
            var data = _room.GetPlayer(_room.LocalSlotIndex);
            if (data.IsReady) return;
            _room.SelectCharacter(data.CharacterIndex + 1);
        }

        private void OnReadyClicked()
        {
            if (_room.IsLocalHost)
            {
                _room.TryStartGame();
                return;
            }

            var data = _room.GetPlayer(_room.LocalSlotIndex);
            _room.SetReady(!data.IsReady);
        }

        private void OnExitClicked()
        {
            _room.LeaveRoom();
        }

        private void HandlePlayerChanged(int slotIndex)
        {
            if (slotUIs == null) return;
            for (int i = 0; i < slotUIs.Length; i++)
            {
                if (slotUIs[i] == null) continue;
                if (slotUIs[i].SlotIndex != slotIndex) continue;
                var data = _room.GetPlayer(slotIndex);
                slotUIs[i].Bind(data, slotIndex == _room.LocalSlotIndex);
            }

            if (slotIndex == _room.LocalSlotIndex)
            {
                UpdateReadyButtonLabel();
                UpdateArrowButtons();
            }
            else if (_room.IsLocalHost)
            {
                UpdateReadyButtonLabel();
            }
        }

        private void UpdateReadyButtonLabel()
        {
            if (readyButtonLabel == null) return;

            if (_room.IsLocalHost)
            {
                readyButtonLabel.text = startLabel;
                if (readyButton != null) readyButton.interactable = _room.CanStartGame;
                return;
            }

            var data = _room.GetPlayer(_room.LocalSlotIndex);
            readyButtonLabel.text = data.IsReady ? cancelLabel : readyLabel;
            if (readyButton != null) readyButton.interactable = data.IsOccupied;
        }

        private void UpdateArrowButtons()
        {
            var data = _room.GetPlayer(_room.LocalSlotIndex);
            bool canChange = data.IsOccupied && !data.IsReady;
            if (leftArrowButton != null) leftArrowButton.interactable = canChange;
            if (rightArrowButton != null) rightArrowButton.interactable = canChange;
        }

        private void HandleAllReady()
        {
            if (_room is NetworkRoomService) return;
            if (string.IsNullOrEmpty(gameplaySceneName)) return;
            SceneManager.LoadScene(gameplaySceneName);
        }
    }
}
