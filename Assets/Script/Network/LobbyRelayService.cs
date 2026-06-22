using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectM.Auth;
using ProjectM.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace ProjectM.Network
{
    /// <summary>
    /// Unity Lobby + Relay + NGO Host/Client 세션 관리.
    /// </summary>
    public class LobbyRelayService : MonoBehaviour
    {
        public const string DataRoomName = "roomName";
        public const string DataRelayJoinCode = "relayJoinCode";
        public const string DataHasPassword = "hasPassword";
        public const int LobbyPasswordLength = 8;

        public static LobbyRelayService Instance { get; private set; }

        [SerializeField] private int maxPlayers = 4;
        [SerializeField] private float lobbyHeartbeatSeconds = 10f;

        public bool IsInSession { get; private set; }
        public bool IsHost { get; private set; }
        public string CurrentLobbyId { get; private set; } = string.Empty;
        public string RelayJoinCode { get; private set; } = string.Empty;
        public string RoomName { get; private set; } = string.Empty;

        private NetworkManager networkManager;
        private UnityTransport transport;
        private Coroutine heartbeatCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            ResolveNetworkComponents();
        }

        private void ResolveNetworkComponents()
        {
            networkManager = FindAnyObjectByType<NetworkManager>();
            if (networkManager == null)
            {
                Debug.LogWarning("[LobbyRelay] NetworkManager not found in scene.");
                return;
            }

            transport = networkManager.GetComponent<UnityTransport>();
            if (transport == null)
                transport = networkManager.gameObject.AddComponent<UnityTransport>();
        }

        public void BindNetworkManager(NetworkManager manager)
        {
            networkManager = manager;
            transport = manager != null ? manager.GetComponent<UnityTransport>() : null;
            if (networkManager != null && transport == null)
                transport = networkManager.gameObject.AddComponent<UnityTransport>();
        }

        private async Task EnsureServicesReadyAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                throw new InvalidOperationException("Authentication required. Please log in first.");

            ResolveNetworkComponents();
            if (networkManager == null)
                throw new InvalidOperationException("NetworkManager is not available.");
        }

        public async Task CreateRoomAsync(string roomName, bool isPublic, string password)
        {
            await EnsureServicesReadyAsync();

            if (IsInSession)
                await LeaveSessionAsync();

            await CleanupRemoteLobbyMembershipAsync();

            int connectionCount = Mathf.Max(1, maxPlayers - 1);
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(connectionCount);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            bool hasPassword = !isPublic && !string.IsNullOrEmpty(password);
            if (hasPassword && password.Length < LobbyPasswordLength)
                throw new InvalidOperationException("비밀번호는 8자리 숫자여야 합니다.");

            string displayName = string.IsNullOrWhiteSpace(roomName)
                ? $"{AuthSessionManager.ResolveNickname("Host")}의 방"
                : roomName.Trim();

            var lobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = false,
                Password = hasPassword ? password : null,
                Data = BuildLobbyData(displayName, joinCode, hasPassword)
            };

            string nickname = AuthSessionManager.ResolveNickname("Host");
            var lobby = await LobbyService.Instance.CreateLobbyAsync(displayName, maxPlayers, lobbyOptions);

            ApplyRelayServerData(allocation);
            NetworkPlayerSessionGuard.ApplyManagerSettings(networkManager);
            if (!networkManager.StartHost())
                throw new InvalidOperationException("Failed to start NGO Host.");

            NetworkPlayerSessionGuard.EnforceGameplayOnlySpawn(networkManager);

            IsInSession = true;
            IsHost = true;
            CurrentLobbyId = lobby.Id;
            RelayJoinCode = joinCode;
            RoomName = displayName;

            await SendHeartbeatAsync();
            StartHostHeartbeat();

            Debug.Log($"[LobbyRelay] Room created: {displayName} lobbyId={lobby.Id} relay={joinCode}");
        }

        public async Task<List<RoomListEntry>> QueryLobbiesAsync()
        {
            await EnsureServicesReadyAsync();

            var options = new QueryLobbiesOptions
            {
                Count = 25,
                Filters = new List<QueryFilter>
                {
                    new(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                },
                Order = new List<QueryOrder>
                {
                    new QueryOrder(false, QueryOrder.FieldOptions.Created)
                }
            };

            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);
            var entries = new List<RoomListEntry>();

            Debug.Log($"[LobbyRelay] QueryLobbies returned {response.Results.Count} lobby(ies).");

            foreach (var lobby in response.Results)
            {
                string name = lobby.Name;
                if (lobby.Data != null && lobby.Data.TryGetValue(DataRoomName, out var nameData))
                    name = nameData.Value;

                bool hasPassword = lobby.Data != null
                                   && lobby.Data.TryGetValue(DataHasPassword, out var pwFlag)
                                   && pwFlag.Value == "1";

                entries.Add(new RoomListEntry
                {
                    roomName = name,
                    currentPlayers = lobby.Players.Count,
                    maxPlayers = lobby.MaxPlayers,
                    hasPassword = hasPassword || lobby.HasPassword,
                    lobbyId = lobby.Id,
                    lobbyCode = lobby.LobbyCode
                });
            }

            return entries;
        }

        public async Task JoinRoomAsync(string lobbyId, string password)
        {
            await EnsureServicesReadyAsync();

            if (IsInSession)
                await LeaveSessionAsync();

            ShutdownNetworkIfListening();

            if (string.IsNullOrEmpty(lobbyId))
                throw new ArgumentException("Lobby id is required.");

            if (!string.IsNullOrEmpty(password) && password.Length < LobbyPasswordLength)
                throw new InvalidOperationException("비밀번호는 8자리 숫자여야 합니다.");

            JoinLobbyByIdOptions joinOptions = null;
            if (!string.IsNullOrEmpty(password))
                joinOptions = new JoinLobbyByIdOptions { Password = password };

            await CleanupRemoteLobbyMembershipAsync(exceptLobbyId: lobbyId);

            Lobby lobby = await ResolveLobbyMembershipAsync(lobbyId, joinOptions);

            if (lobby.Data == null || !lobby.Data.TryGetValue(DataRelayJoinCode, out var relayData)
                || string.IsNullOrEmpty(relayData.Value))
            {
                await SafeRemovePlayerAsync(lobby.Id);
                throw new InvalidOperationException("Relay join code not found in lobby data.");
            }

            string relayJoinCode = relayData.Value;
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);

            ApplyRelayServerData(allocation);
            NetworkPlayerSessionGuard.ApplyManagerSettings(networkManager);
            if (!networkManager.StartClient())
            {
                await SafeRemovePlayerAsync(lobby.Id);
                throw new InvalidOperationException("Failed to start NGO Client.");
            }

            NetworkPlayerSessionGuard.EnforceGameplayOnlySpawn(networkManager);

            IsInSession = true;
            IsHost = false;
            CurrentLobbyId = lobby.Id;
            RelayJoinCode = relayJoinCode;
            RoomName = lobby.Name;
            StopHostHeartbeat();

            Debug.Log($"[LobbyRelay] Joined room: {lobby.Name} lobbyId={lobby.Id}");
        }

        private async Task<Lobby> ResolveLobbyMembershipAsync(string lobbyId, JoinLobbyByIdOptions joinOptions)
        {
            if (await IsAlreadyInLobbyAsync(lobbyId))
            {
                Debug.Log("[LobbyRelay] 이미 로비 멤버 — GetLobbyAsync로 재연결합니다.");
                return await LobbyService.Instance.GetLobbyAsync(lobbyId);
            }

            try
            {
                return joinOptions != null
                    ? await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, joinOptions)
                    : await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId);
            }
            catch (LobbyServiceException ex) when (IsAlreadyMemberError(ex))
            {
                Debug.Log("[LobbyRelay] JoinLobby conflict — 이미 멤버, GetLobbyAsync로 재연결합니다.");
                return await LobbyService.Instance.GetLobbyAsync(lobbyId);
            }
            catch (LobbyServiceException ex) when (IsIncorrectPasswordError(ex))
            {
                throw new InvalidOperationException("비밀번호가 올바르지 않습니다.", ex);
            }
            catch (LobbyServiceException ex)
            {
                throw new InvalidOperationException("방에 참여할 수 없습니다.", ex);
            }
        }

        private static bool IsAlreadyMemberError(LobbyServiceException ex)
        {
            if (ex.Reason == LobbyExceptionReason.LobbyConflict
                || ex.Reason == LobbyExceptionReason.Conflict)
                return true;

            return ex.Message != null
                   && ex.Message.IndexOf("already a member", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsIncorrectPasswordError(LobbyServiceException ex)
        {
            if (ex.Reason == LobbyExceptionReason.IncorrectPassword)
                return true;

            return ex.Message != null
                   && ex.Message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0
                   && ex.Message.IndexOf("incorrect", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private async Task<bool> IsAlreadyInLobbyAsync(string lobbyId)
        {
            try
            {
                var joined = await LobbyService.Instance.GetJoinedLobbiesAsync();
                return joined != null && joined.Contains(lobbyId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyRelay] GetJoinedLobbiesAsync failed: {ex.Message}");
                return false;
            }
        }

        private async Task CleanupRemoteLobbyMembershipAsync(string exceptLobbyId = null)
        {
            if (!AuthenticationService.Instance.IsSignedIn)
                return;

            try
            {
                var joined = await LobbyService.Instance.GetJoinedLobbiesAsync();
                if (joined == null) return;

                foreach (string lobbyId in joined)
                {
                    if (!string.IsNullOrEmpty(exceptLobbyId) && lobbyId == exceptLobbyId)
                        continue;

                    await SafeRemovePlayerAsync(lobbyId);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyRelay] CleanupRemoteLobbyMembership failed: {ex.Message}");
            }
        }

        private async Task SafeRemovePlayerAsync(string lobbyId)
        {
            if (string.IsNullOrEmpty(lobbyId) || !AuthenticationService.Instance.IsSignedIn)
                return;

            try
            {
                await LobbyService.Instance.RemovePlayerAsync(lobbyId, AuthenticationService.Instance.PlayerId);
                Debug.Log($"[LobbyRelay] Removed lobby membership: {lobbyId}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyRelay] RemovePlayer({lobbyId}) warning: {ex.Message}");
            }
        }

        private void ShutdownNetworkIfListening()
        {
            if (networkManager != null && networkManager.IsListening)
                networkManager.Shutdown();
        }

        public async Task LeaveSessionAsync()
        {
            StopHostHeartbeat();
            ShutdownNetworkIfListening();

            if (!string.IsNullOrEmpty(CurrentLobbyId) && AuthenticationService.Instance.IsSignedIn)
            {
                try
                {
                    if (IsHost)
                        await LobbyService.Instance.DeleteLobbyAsync(CurrentLobbyId);
                    else
                        await SafeRemovePlayerAsync(CurrentLobbyId);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LobbyRelay] Leave lobby warning: {ex.Message}");
                }
            }
            else
            {
                await CleanupRemoteLobbyMembershipAsync();
            }

            IsInSession = false;
            IsHost = false;
            CurrentLobbyId = string.Empty;
            RelayJoinCode = string.Empty;
            RoomName = string.Empty;
        }

        private void ApplyRelayServerData(Allocation allocation)
        {
            if (transport == null)
                throw new InvalidOperationException("UnityTransport is missing.");

            var relayData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            transport.SetRelayServerData(relayData);
        }

        private void ApplyRelayServerData(JoinAllocation allocation)
        {
            if (transport == null)
                throw new InvalidOperationException("UnityTransport is missing.");

            var relayData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            transport.SetRelayServerData(relayData);
        }

        private static Dictionary<string, DataObject> BuildLobbyData(string roomName, string relayJoinCode, bool hasPassword)
        {
            return new Dictionary<string, DataObject>
            {
                { DataRoomName, new DataObject(DataObject.VisibilityOptions.Public, roomName) },
                { DataRelayJoinCode, new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) },
                { DataHasPassword, new DataObject(DataObject.VisibilityOptions.Public, hasPassword ? "1" : "0") }
            };
        }

        private void StartHostHeartbeat()
        {
            StopHostHeartbeat();
            if (!IsHost || string.IsNullOrEmpty(CurrentLobbyId)) return;
            heartbeatCoroutine = StartCoroutine(HostHeartbeatRoutine());
        }

        private void StopHostHeartbeat()
        {
            if (heartbeatCoroutine != null)
            {
                StopCoroutine(heartbeatCoroutine);
                heartbeatCoroutine = null;
            }
        }

        private IEnumerator HostHeartbeatRoutine()
        {
            var wait = new WaitForSecondsRealtime(lobbyHeartbeatSeconds);
            while (IsHost && !string.IsNullOrEmpty(CurrentLobbyId))
            {
                yield return wait;

                if (!IsHost || string.IsNullOrEmpty(CurrentLobbyId)) yield break;

                var task = SendHeartbeatAsync();
                while (!task.IsCompleted) yield return null;
            }
        }

        private async Task SendHeartbeatAsync()
        {
            if (string.IsNullOrEmpty(CurrentLobbyId)) return;

            try
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(CurrentLobbyId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyRelay] Heartbeat failed: {ex.Message}");
            }
        }
    }
}
