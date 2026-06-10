using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace ProjectM.CharacterSelect
{
    public class CharacterSelectManager : MonoBehaviour
    {
        [Header("Services")]
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
        [SerializeField] private string readyLabel = "Ready";
        [SerializeField] private string cancelLabel = "Cancel";

        private IRoomService _room;

        private void Awake()
        {
            _room = roomServiceObject as IRoomService;
            if (_room == null)
            {
                Debug.LogError($"[{nameof(CharacterSelectManager)}] roomServiceObject must implement IRoomService.", this);
                enabled = false;
                return;
            }

            if (leftArrowButton != null)  leftArrowButton.onClick.AddListener(OnLeftArrow);
            if (rightArrowButton != null) rightArrowButton.onClick.AddListener(OnRightArrow);
            if (readyButton != null)      readyButton.onClick.AddListener(OnReadyClicked);
            if (exitRoomButton != null)   exitRoomButton.onClick.AddListener(OnExitClicked);
        }

        private void OnEnable()
        {
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
            if (_room == null) return;
            for (int i = 0; i < _room.MaxSlots; i++) HandlePlayerChanged(i);
            UpdateReadyButtonLabel();
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

            if (slotIndex == _room.LocalSlotIndex) UpdateReadyButtonLabel();
        }

        private void UpdateReadyButtonLabel()
        {
            if (readyButtonLabel == null) return;
            var data = _room.GetPlayer(_room.LocalSlotIndex);
            readyButtonLabel.text = data.IsReady ? cancelLabel : readyLabel;
        }

        private void HandleAllReady()
        {
            if (string.IsNullOrEmpty(gameplaySceneName)) return;
            SceneManager.LoadScene(gameplaySceneName);
        }
    }
}
