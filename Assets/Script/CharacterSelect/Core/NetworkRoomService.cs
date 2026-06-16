using System;
using ProjectM.Network;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectM.CharacterSelect
{
    /// <summary>
    /// LobbyRelayService NGO 세션용 IRoomService 구현.
    /// </summary>
    public class NetworkRoomService : MonoBehaviour, IRoomService
    {
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private CharacterDatabase database;

        private CharacterLobbyNetwork lobby;
        private bool subscribed;

        public int LocalSlotIndex => lobby != null ? lobby.GetLocalSlotIndex() : -1;
        public int MaxSlots => CharacterLobbyNetwork.MaxSlots;
        public bool IsLocalHost => LobbyRelayService.Instance != null && LobbyRelayService.Instance.IsHost;
        public bool CanStartGame => lobby != null && lobby.CanStartGame();

        public event Action<int> OnPlayerChanged;
        public event Action OnAllPlayersReady;

        private void OnEnable()
        {
            TryBindLobby();
        }

        private void Update()
        {
            if (!subscribed) TryBindLobby();
        }

        private void OnDisable()
        {
            UnsubscribeLobby();
        }

        private void TryBindLobby()
        {
            var candidate = CharacterLobbyNetwork.Instance;
            if (candidate == null) candidate = FindAnyObjectByType<CharacterLobbyNetwork>();
            if (candidate == null || !candidate.IsSpawned) return;
            if (lobby == candidate && subscribed) return;

            UnsubscribeLobby();
            lobby = candidate;
            lobby.OnPlayerChanged += HandlePlayerChanged;
            lobby.OnGameStarting += HandleGameStarting;
            subscribed = true;

            for (int i = 0; i < MaxSlots; i++)
                HandlePlayerChanged(i);
        }

        private void UnsubscribeLobby()
        {
            if (!subscribed || lobby == null) return;
            lobby.OnPlayerChanged -= HandlePlayerChanged;
            lobby.OnGameStarting -= HandleGameStarting;
            subscribed = false;
        }

        public RoomPlayerData GetPlayer(int slotIndex)
        {
            if (lobby == null) return RoomPlayerData.Empty;
            return lobby.GetPlayer(slotIndex);
        }

        public void SelectCharacter(int characterIndex)
        {
            if (lobby == null) return;
            var data = GetPlayer(LocalSlotIndex);
            if (!data.IsOccupied || data.IsReady) return;

            int wrapped = database != null ? database.Wrap(characterIndex) : characterIndex;
            if (data.CharacterIndex == wrapped) return;
            lobby.RequestSelectCharacter(wrapped);
        }

        public void SetReady(bool ready)
        {
            if (lobby == null || IsLocalHost) return;
            lobby.RequestSetReady(ready);
        }

        public void TryStartGame()
        {
            if (lobby == null || !IsLocalHost) return;
            lobby.RequestStartGame();
        }

        public async void LeaveRoom()
        {
            UnsubscribeLobby();

            if (LobbyRelayService.Instance != null)
                await LobbyRelayService.Instance.LeaveSessionAsync();

            if (!string.IsNullOrEmpty(mainMenuSceneName))
                SceneManager.LoadScene(mainMenuSceneName);
        }

        private void HandlePlayerChanged(int slotIndex)
        {
            OnPlayerChanged?.Invoke(slotIndex);
        }

        private void HandleGameStarting()
        {
            OnAllPlayersReady?.Invoke();
        }
    }
}
