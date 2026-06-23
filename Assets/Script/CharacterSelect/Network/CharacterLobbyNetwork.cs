using System.Collections.Generic;
using ProjectM.Auth;
using ProjectM.Network;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectM.CharacterSelect
{
    public struct LobbySlotState : INetworkSerializable, System.IEquatable<LobbySlotState>
    {
        public FixedString64Bytes Nickname;
        public FixedString64Bytes AuthPlayerId;
        public int CharacterIndex;
        public bool IsReady;
        public bool IsOccupied;
        public ulong OwnerClientId;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Nickname);
            serializer.SerializeValue(ref AuthPlayerId);
            serializer.SerializeValue(ref CharacterIndex);
            serializer.SerializeValue(ref IsReady);
            serializer.SerializeValue(ref IsOccupied);
            serializer.SerializeValue(ref OwnerClientId);
        }

        public bool Equals(LobbySlotState other)
        {
            return Nickname.Equals(other.Nickname)
                   && AuthPlayerId.Equals(other.AuthPlayerId)
                   && CharacterIndex == other.CharacterIndex
                   && IsReady == other.IsReady
                   && IsOccupied == other.IsOccupied
                   && OwnerClientId == other.OwnerClientId;
        }
    }

    /// <summary>
    /// NGO 캐릭터 선택 로비 상태 — 4슬롯, Ready, Start → GamePlay.
    /// rematch 시 OriginalHostAuthPlayerId 기준 슬롯 0 복원.
    /// </summary>
    public class CharacterLobbyNetwork : NetworkBehaviour
    {
        public const int MaxSlots = 4;
        public const int HostSlotIndex = 0;

        [SerializeField] private string gameplaySceneName = "GamePlay";

        public static CharacterLobbyNetwork Instance { get; private set; }

        private NetworkList<LobbySlotState> slots;

        public event System.Action<int> OnPlayerChanged;
        public event System.Action OnGameStarting;

        private void Awake()
        {
            slots = new NetworkList<LobbySlotState>(
                default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
        }

        public override void OnNetworkSpawn()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[CharacterLobbyNetwork] Duplicate instance detected.");
            }
            else
            {
                Instance = this;
            }

            slots.OnListChanged += HandleSlotsChanged;

            if (IsServer)
            {
                InitializeServerSlots();
                EnsureAllConnectedClientsRegistered();
                NetworkManager.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            }

            RegisterLocalPlayer();
            BroadcastAllSlots();
        }

        public override void OnNetworkDespawn()
        {
            slots.OnListChanged -= HandleSlotsChanged;

            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            }

            if (Instance == this) Instance = null;
        }

        private void RegisterLocalPlayer()
        {
            string authId = AuthSessionManager.Instance != null
                ? AuthSessionManager.Instance.PlayerId ?? string.Empty
                : string.Empty;

            RegisterLocalPlayerServerRpc(
                AuthSessionManager.ResolveNickname("Player"),
                NetworkManager.LocalClientId,
                new FixedString64Bytes(authId));
        }

        public int GetLocalSlotIndex()
        {
            if (NetworkManager == null) return -1;
            ulong localId = NetworkManager.LocalClientId;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsOccupied && slots[i].OwnerClientId == localId)
                    return i;
            }
            return -1;
        }

        public RoomPlayerData GetPlayer(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Count) return RoomPlayerData.Empty;
            var s = slots[slotIndex];
            if (!s.IsOccupied) return RoomPlayerData.Empty;
            return new RoomPlayerData
            {
                IsOccupied = true,
                Nickname = s.Nickname.ToString(),
                CharacterIndex = s.CharacterIndex,
                IsReady = s.IsReady,
                Score = 0
            };
        }

        public string GetNicknameForClient(ulong clientId)
        {
            int slot = FindSlotByClientId(clientId);
            if (slot < 0) return string.Empty;
            return slots[slot].Nickname.ToString();
        }

        public bool CanStartGame()
        {
            if (NetworkManager == null || slots == null) return false;

            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (!s.IsOccupied) continue;
                if (s.OwnerClientId == NetworkManager.ServerClientId) continue;
                if (!s.IsReady) return false;
            }
            return true;
        }

        public void RequestSelectCharacter(int characterIndex)
        {
            SelectCharacterServerRpc(characterIndex, NetworkManager.LocalClientId);
        }

        public void RequestSetReady(bool ready)
        {
            SetReadyServerRpc(ready, NetworkManager.LocalClientId);
        }

        public void RequestStartGame()
        {
            if (NetworkManager == null || !NetworkManager.IsHost) return;
            StartGameServerRpc(NetworkManager.LocalClientId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RegisterLocalPlayerServerRpc(
            FixedString64Bytes nickname,
            ulong clientId,
            FixedString64Bytes authPlayerId)
        {
            int slot = FindSlotByClientId(clientId);
            if (slot < 0) slot = AssignClientToSlot(clientId, authPlayerId);
            if (slot < 0) return;

            var state = slots[slot];
            state.IsOccupied = true;
            state.OwnerClientId = clientId;
            state.Nickname = nickname;
            if (!authPlayerId.IsEmpty)
                state.AuthPlayerId = authPlayerId;
            if (state.CharacterIndex < 0) state.CharacterIndex = 0;
            if (clientId == NetworkManager.ServerClientId) state.IsReady = false;
            slots[slot] = state;

            RebalanceHostSlot();
        }

        [ServerRpc(RequireOwnership = false)]
        private void SelectCharacterServerRpc(int characterIndex, ulong clientId)
        {
            int slot = FindSlotByClientId(clientId);
            if (slot < 0) return;

            var state = slots[slot];
            if (!state.IsOccupied || state.IsReady) return;

            state.CharacterIndex = characterIndex;
            slots[slot] = state;
        }

        [ServerRpc(RequireOwnership = false)]
        private void SetReadyServerRpc(bool ready, ulong clientId)
        {
            if (clientId == NetworkManager.ServerClientId) return;

            int slot = FindSlotByClientId(clientId);
            if (slot < 0) return;

            var state = slots[slot];
            if (!state.IsOccupied) return;

            state.IsReady = ready;
            slots[slot] = state;
        }

        [ServerRpc(RequireOwnership = false)]
        private void StartGameServerRpc(ulong clientId)
        {
            if (clientId != NetworkManager.ServerClientId) return;
            if (!CanStartGame()) return;

            OnGameStarting?.Invoke();
            if (string.IsNullOrEmpty(gameplaySceneName)) return;
            NetworkManager.SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
        }

        private void InitializeServerSlots()
        {
            slots.Clear();
            for (int i = 0; i < MaxSlots; i++)
            {
                slots.Add(new LobbySlotState
                {
                    Nickname = default,
                    AuthPlayerId = default,
                    CharacterIndex = 0,
                    IsReady = false,
                    IsOccupied = false,
                    OwnerClientId = 0
                });
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (clientId == NetworkManager.ServerClientId) return;
            if (FindSlotByClientId(clientId) >= 0) return;
            AssignClientToSlot(clientId, default);
            RebalanceHostSlot();
        }

        private void EnsureAllConnectedClientsRegistered()
        {
            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                if (FindSlotByClientId(clientId) >= 0) continue;
                AssignClientToSlot(clientId, default);
            }

            RebalanceHostSlot();
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            int slot = FindSlotByClientId(clientId);
            if (slot < 0) return;

            slots[slot] = EmptySlot();
            RebalanceHostSlot();
        }

        private int AssignClientToSlot(ulong clientId, FixedString64Bytes authPlayerId)
        {
            int existing = FindSlotByClientId(clientId);
            if (existing >= 0) return existing;

            int slot = ResolvePreferredSlot(clientId, authPlayerId);
            if (slot < 0) return -1;

            if (slots[slot].IsOccupied && slots[slot].OwnerClientId != clientId)
                slot = FindFirstFreeGuestSlot();

            if (slot < 0) return -1;

            var state = slots[slot];
            state.IsOccupied = true;
            state.OwnerClientId = clientId;
            if (state.Nickname.IsEmpty) state.Nickname = default;
            if (!authPlayerId.IsEmpty) state.AuthPlayerId = authPlayerId;
            state.CharacterIndex = 0;
            state.IsReady = false;
            slots[slot] = state;
            return slot;
        }

        private int ResolvePreferredSlot(ulong clientId, FixedString64Bytes authPlayerId)
        {
            string originalHost = MatchPartyContext.OriginalHostAuthPlayerId;
            if (!string.IsNullOrEmpty(originalHost)
                && !authPlayerId.IsEmpty
                && authPlayerId.ToString() == originalHost
                && !IsOriginalHostExcluded())
            {
                return HostSlotIndex;
            }

            if (string.IsNullOrEmpty(originalHost) && clientId == NetworkManager.ServerClientId)
                return HostSlotIndex;

            return FindFirstFreeGuestSlot();
        }

        private static bool IsOriginalHostExcluded()
        {
            string originalHost = MatchPartyContext.OriginalHostAuthPlayerId;
            if (string.IsNullOrEmpty(originalHost)) return false;

            var payload = MatchPartyContext.LastStatusPayload;
            for (int i = 0; i < payload.PlayerCount; i++)
            {
                var entry = payload.GetPlayer(i);
                if (entry.AuthPlayerId.ToString() != originalHost) continue;
                return entry.PlayerState == RematchPlayerState.LeftHome;
            }

            return false;
        }

        private void RebalanceHostSlot()
        {
            string originalHost = MatchPartyContext.OriginalHostAuthPlayerId;
            if (string.IsNullOrEmpty(originalHost) || IsOriginalHostExcluded()) return;

            ulong hostClientId = FindClientIdByAuth(originalHost);
            if (hostClientId == ulong.MaxValue) return;

            int currentHostSlot = FindSlotByClientId(hostClientId);
            if (currentHostSlot == HostSlotIndex) return;

            if (slots[HostSlotIndex].IsOccupied && slots[HostSlotIndex].OwnerClientId != hostClientId)
            {
                ulong displaced = slots[HostSlotIndex].OwnerClientId;
                MoveClientToGuestSlot(displaced);
            }

            MoveClientToSlot(hostClientId, HostSlotIndex);
        }

        private ulong FindClientIdByAuth(string authPlayerId)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsOccupied) continue;
                if (slots[i].AuthPlayerId.ToString() == authPlayerId)
                    return slots[i].OwnerClientId;
            }

            return ulong.MaxValue;
        }

        private void MoveClientToGuestSlot(ulong clientId)
        {
            int guestSlot = FindFirstFreeGuestSlot();
            if (guestSlot < 0) return;
            MoveClientToSlot(clientId, guestSlot);
        }

        private void MoveClientToSlot(ulong clientId, int targetSlot)
        {
            int sourceSlot = FindSlotByClientId(clientId);
            if (sourceSlot < 0 || targetSlot < 0 || sourceSlot == targetSlot) return;

            var moving = slots[sourceSlot];
            if (!moving.IsOccupied || moving.OwnerClientId != clientId) return;

            if (slots[targetSlot].IsOccupied && slots[targetSlot].OwnerClientId != clientId)
                return;

            slots[sourceSlot] = EmptySlot();
            moving.IsReady = targetSlot == HostSlotIndex ? false : moving.IsReady;
            slots[targetSlot] = moving;
        }

        private static LobbySlotState EmptySlot()
        {
            return new LobbySlotState
            {
                Nickname = default,
                AuthPlayerId = default,
                CharacterIndex = 0,
                IsReady = false,
                IsOccupied = false,
                OwnerClientId = 0
            };
        }

        private int FindFirstFreeGuestSlot()
        {
            for (int i = 1; i < MaxSlots; i++)
            {
                if (!slots[i].IsOccupied) return i;
            }
            return -1;
        }

        private int FindSlotByClientId(ulong clientId)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsOccupied && slots[i].OwnerClientId == clientId)
                    return i;
            }
            return -1;
        }

        private void HandleSlotsChanged(NetworkListEvent<LobbySlotState> changeEvent)
        {
            int index = changeEvent.Index;
            if (index >= 0 && index < MaxSlots)
                OnPlayerChanged?.Invoke(index);
            else
                BroadcastAllSlots();
        }

        private void BroadcastAllSlots()
        {
            for (int i = 0; i < MaxSlots; i++)
                OnPlayerChanged?.Invoke(i);
        }
    }
}
