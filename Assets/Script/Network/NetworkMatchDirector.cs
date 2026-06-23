using System.Collections.Generic;
using ProjectM.Auth;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using ProjectM.Core;
using ProjectM.UI;
using ProjectM.Wave;

namespace ProjectM.Network
{
    /// <summary>
    /// 서버 매치 진행 상태를 클라이언트 UI에 미러링하고 rematch intent를 집계한다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkMatchDirector : NetworkBehaviour
    {
        [SerializeField] private GameSessionManager session;
        [SerializeField] private MatchBootstrapper bootstrapper;
        [SerializeField] private WaveManager waveManager;

        private NetworkVariable<int> netWave = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<int> netPhase = new((int)GamePhase.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<float> netPrepRemaining = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<int> netTotalToSpawn = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<int> netSpawnedCount = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<bool> netIsSpawning = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly List<RematchPlayerEntry> rematchPlayers = new();

        public float SyncedPrepRemaining => netPrepRemaining.Value;

        public static event System.Action<RematchStatusPayload> RematchStatusUpdated;

        private void Awake()
        {
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
            if (bootstrapper == null) bootstrapper = FindAnyObjectByType<MatchBootstrapper>();
            if (waveManager == null) waveManager = FindAnyObjectByType<WaveManager>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                if (session != null)
                {
                    session.OnMatchStarted += HandleServerMatchStarted;
                    session.OnWaveStarted += HandleServerWaveStarted;
                    session.OnWaveEnded += HandleServerWaveEnded;
                    session.OnMatchEnded += HandleServerMatchEnded;
                }
            }
            else
            {
                netWave.OnValueChanged += (_, _) => ApplyClientSnapshot(false);
                netPhase.OnValueChanged += (_, _) => ApplyClientSnapshot(false);
                netPrepRemaining.OnValueChanged += (_, _) => ApplyClientSnapshot(false);
                netTotalToSpawn.OnValueChanged += (_, _) => ApplyClientSnapshot(false);
                netSpawnedCount.OnValueChanged += (_, _) => ApplyClientSnapshot(false);
                netIsSpawning.OnValueChanged += (_, _) => ApplyClientSnapshot(false);
                ApplyClientSnapshot(forceEvents: true);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (session != null)
            {
                session.OnMatchStarted -= HandleServerMatchStarted;
                session.OnWaveStarted -= HandleServerWaveStarted;
                session.OnWaveEnded -= HandleServerWaveEnded;
                session.OnMatchEnded -= HandleServerMatchEnded;
            }
        }

        private void Update()
        {
            if (!IsServer) return;

            if (bootstrapper != null)
                netPrepRemaining.Value = bootstrapper.PreparationRemaining;

            if (session != null)
            {
                netWave.Value = session.State.CurrentWave;
                netPhase.Value = (int)session.State.CurrentPhase;
            }

            if (waveManager != null)
            {
                netTotalToSpawn.Value = waveManager.TotalToSpawn;
                netSpawnedCount.Value = waveManager.SpawnedCount;
                netIsSpawning.Value = waveManager.IsSpawning;
            }
        }

        public void RegisterRematchIntent()
        {
            if (!NetworkSessionHelper.IsMultiplayerSession || !IsSpawned)
            {
                MatchRematchCoordinator.Instance?.RequestRematchOffline();
                return;
            }

            RegisterRematchIntentServerRpc();
        }

        public void UnregisterRematchPlayer()
        {
            if (!NetworkSessionHelper.IsMultiplayerSession || !IsSpawned)
                return;

            UnregisterRematchPlayerServerRpc();
        }

        private void HandleServerMatchStarted()
        {
            rematchPlayers.Clear();
            MatchPartyContext.Clear();

            if (session != null)
            {
                netWave.Value = session.State.CurrentWave;
                netPhase.Value = (int)session.State.CurrentPhase;
            }

            SyncMatchStartedClientRpc();
        }

        private void HandleServerWaveStarted(int wave)
        {
            netWave.Value = wave;
            netPhase.Value = (int)GamePhase.Wave;
            SyncWaveStartedClientRpc(wave);
        }

        private void HandleServerWaveEnded(int wave)
        {
            netWave.Value = wave;
            netPhase.Value = (int)session.State.CurrentPhase;
            SyncWaveEndedClientRpc(wave, netPhase.Value);
        }

        private void HandleServerMatchEnded(bool cleared)
        {
            netPhase.Value = (int)session.State.CurrentPhase;

            MatchPartyContext.ResetRematchSession();

            string baseLobbyId = LobbyRelayService.Instance != null
                ? LobbyRelayService.Instance.CurrentLobbyId
                : string.Empty;
            string rematchGroupId = string.IsNullOrEmpty(baseLobbyId)
                ? System.Guid.NewGuid().ToString("N")
                : $"{baseLobbyId}:{System.Guid.NewGuid():N}";

            InitializeRematchPartyOnServer();
            var payload = BuildRematchStatusPayload();

            ShowResultUiForAllClients(cleared, rematchGroupId, payload);
            SyncMatchEndedClientRpc(cleared, netPhase.Value, rematchGroupId, payload);
            RematchStatusUpdatedClientRpc(payload);
        }

        private static void ShowResultUiForAllClients(bool cleared, string rematchGroupId, RematchStatusPayload payload)
        {
            MatchPartyContext.SetRematchGroup(rematchGroupId);
            MatchPartyContext.ApplyStatusPayload(payload);

            var resultView = Object.FindAnyObjectByType<ResultView>(FindObjectsInactive.Include);
            resultView?.Show(cleared);
        }

        private void InitializeRematchPartyOnServer()
        {
            rematchPlayers.Clear();

            string hostAuthId = ResolveServerHostAuthPlayerId();
            MatchPartyContext.SetOriginalHost(hostAuthId);

            foreach (var player in NetworkPlayerRegistry.All)
            {
                if (player == null) continue;

                string authId = player.AuthPlayerId;
                if (string.IsNullOrEmpty(authId))
                    authId = $"client:{player.OwnerClientId}";

                rematchPlayers.Add(new RematchPlayerEntry
                {
                    OwnerClientId = player.OwnerClientId,
                    AuthPlayerId = new FixedString64Bytes(authId),
                    Nickname = new FixedString64Bytes(player.DisplayName),
                    State = (byte)RematchPlayerState.Pending
                });
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RegisterRematchIntentServerRpc(ServerRpcParams rpcParams = default)
        {
            if (MatchPartyContext.RematchOrchestrationStarted) return;

            ulong senderClientId = rpcParams.Receive.SenderClientId;
            if (IsOriginalHostLeftHome() && senderClientId != NetworkManager.ServerClientId)
                return;
            if (MatchPartyContext.HostLeftViaHome && senderClientId != NetworkManager.ServerClientId)
                return;
            int index = FindRematchPlayerIndexByClientId(senderClientId);
            if (index < 0)
            {
                rematchPlayers.Add(new RematchPlayerEntry
                {
                    OwnerClientId = senderClientId,
                    AuthPlayerId = ResolveAuthPlayerIdForClient(senderClientId),
                    Nickname = ResolveNicknameForClient(senderClientId),
                    State = (byte)RematchPlayerState.RetryReady
                });
            }
            else
            {
                var entry = rematchPlayers[index];
                entry.State = (byte)RematchPlayerState.RetryReady;
                if (entry.Nickname.IsEmpty)
                    entry.Nickname = ResolveNicknameForClient(senderClientId);
                rematchPlayers[index] = entry;
            }

            BroadcastRematchStatus();
            TryBeginOrchestratedRematch();
        }

        [ServerRpc(RequireOwnership = false)]
        private void UnregisterRematchPlayerServerRpc(ServerRpcParams rpcParams = default)
        {
            if (MatchPartyContext.RematchOrchestrationStarted) return;

            ulong senderClientId = rpcParams.Receive.SenderClientId;
            int index = FindRematchPlayerIndexByClientId(senderClientId);
            if (index < 0) return;

            var entry = rematchPlayers[index];
            entry.State = (byte)RematchPlayerState.LeftHome;
            rematchPlayers[index] = entry;

            BroadcastRematchStatus();
            TryBeginOrchestratedRematchAfterDeparture();
        }

        private void TryBeginOrchestratedRematchAfterDeparture()
        {
            var payload = BuildRematchStatusPayload();
            if (payload.RequiredCount <= 0 || payload.RegisteredCount < payload.RequiredCount)
                return;

            TryBeginOrchestratedRematch();
        }

        private bool IsOriginalHostLeftHome()
        {
            string hostAuth = MatchPartyContext.OriginalHostAuthPlayerId;
            if (string.IsNullOrEmpty(hostAuth)) return false;

            foreach (var entry in rematchPlayers)
            {
                if (entry.AuthPlayerId.ToString() != hostAuth) continue;
                return entry.PlayerState == RematchPlayerState.LeftHome;
            }

            return false;
        }

        private void BroadcastRematchStatus()
        {
            var payload = BuildRematchStatusPayload();
            MatchPartyContext.ApplyStatusPayload(payload);
            RematchStatusUpdatedClientRpc(payload);
        }

        private void TryBeginOrchestratedRematch()
        {
            var payload = BuildRematchStatusPayload();
            if (payload.RequiredCount <= 0) return;
            if (payload.RegisteredCount < payload.RequiredCount) return;
            if (MatchPartyContext.RematchOrchestrationStarted) return;

            MatchPartyContext.RematchOrchestrationStarted = true;
            string rematchHostAuthId = ResolveRematchHostAuthId();
            ulong rematchHostClientId = ResolveRematchHostClientId();
            MatchPartyContext.RematchHostAuthPlayerId = rematchHostAuthId;
            MatchPartyContext.RematchHostClientId = rematchHostClientId;
            MatchRematchCoordinator.Instance?.BeginOrchestratedRematch(
                rematchHostAuthId,
                MatchPartyContext.RematchGroupId,
                rematchHostClientId);
        }

        private ulong ResolveRematchHostClientId()
        {
            string preferredAuthId = ResolveRematchHostAuthId();
            foreach (var entry in rematchPlayers)
            {
                if (entry.PlayerState != RematchPlayerState.RetryReady) continue;
                if (entry.AuthPlayerId.ToString() != preferredAuthId) continue;
                return entry.OwnerClientId;
            }

            foreach (var entry in rematchPlayers)
            {
                if (entry.PlayerState != RematchPlayerState.RetryReady) continue;
                return entry.OwnerClientId;
            }

            return NetworkManager != null ? NetworkManager.ServerClientId : 0;
        }

        private string ResolveRematchHostAuthId()
        {
            string original = MatchPartyContext.OriginalHostAuthPlayerId;
            if (!string.IsNullOrEmpty(original))
            {
                foreach (var entry in rematchPlayers)
                {
                    if (entry.AuthPlayerId.ToString() != original) continue;
                    if (entry.PlayerState == RematchPlayerState.RetryReady)
                        return original;
                    break;
                }
            }

            foreach (var entry in rematchPlayers)
            {
                if (entry.PlayerState != RematchPlayerState.RetryReady) continue;
                string authId = entry.AuthPlayerId.ToString();
                if (!string.IsNullOrEmpty(authId))
                    return authId;
            }

            return original;
        }

        private RematchStatusPayload BuildRematchStatusPayload()
        {
            int registered = 0;
            int required = 0;

            foreach (var entry in rematchPlayers)
            {
                if (entry.PlayerState == RematchPlayerState.LeftHome) continue;
                required++;
                if (entry.PlayerState == RematchPlayerState.RetryReady)
                    registered++;
            }

            var payload = new RematchStatusPayload
            {
                RegisteredCount = registered,
                RequiredCount = required,
                PlayerCount = rematchPlayers.Count
            };

            for (int i = 0; i < rematchPlayers.Count && i < RematchStatusPayload.MaxPlayers; i++)
                payload.SetPlayer(i, rematchPlayers[i]);

            return payload;
        }

        private int FindRematchPlayerIndexByClientId(ulong clientId)
        {
            for (int i = 0; i < rematchPlayers.Count; i++)
            {
                if (rematchPlayers[i].OwnerClientId == clientId)
                    return i;
            }

            return -1;
        }

        private static FixedString64Bytes ResolveAuthPlayerIdForClient(ulong clientId)
        {
            foreach (var player in NetworkPlayerRegistry.All)
            {
                if (player == null || player.OwnerClientId != clientId) continue;
                if (!string.IsNullOrEmpty(player.AuthPlayerId))
                    return new FixedString64Bytes(player.AuthPlayerId);
            }

            return new FixedString64Bytes($"client:{clientId}");
        }

        private static FixedString64Bytes ResolveNicknameForClient(ulong clientId)
        {
            foreach (var player in NetworkPlayerRegistry.All)
            {
                if (player == null || player.OwnerClientId != clientId) continue;
                if (!string.IsNullOrEmpty(player.DisplayName))
                    return new FixedString64Bytes(player.DisplayName);
            }

            return new FixedString64Bytes($"Player{clientId}");
        }

        private static string ResolveServerHostAuthPlayerId()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                ulong hostClientId = NetworkManager.ServerClientId;
                foreach (var player in NetworkPlayerRegistry.All)
                {
                    if (player == null || player.OwnerClientId != hostClientId) continue;
                    if (!string.IsNullOrEmpty(player.AuthPlayerId))
                        return player.AuthPlayerId;
                }
            }

            return AuthSessionManager.Instance?.PlayerId ?? string.Empty;
        }

        [ClientRpc]
        private void SyncMatchStartedClientRpc()
        {
            if (IsServer) return;
            MatchPartyContext.Clear();
            session?.MirrorMatchStarted();
            ApplyWaveManagerUiState();
        }

        [ClientRpc]
        private void SyncWaveStartedClientRpc(int wave)
        {
            if (IsServer) return;
            session?.MirrorWaveStarted(wave);
            ApplyWaveManagerUiState();
        }

        [ClientRpc]
        private void SyncWaveEndedClientRpc(int wave, int phase)
        {
            if (IsServer) return;
            session?.MirrorWaveEnded(wave, (GamePhase)phase);
            ApplyWaveManagerUiState();
        }

        [ClientRpc]
        private void SyncMatchEndedClientRpc(bool cleared, int phase, string rematchGroupId, RematchStatusPayload payload)
        {
            if (IsServer) return;
            session?.MirrorMatchEnded(cleared, (GamePhase)phase);
            ShowResultUiForAllClients(cleared, rematchGroupId, payload);
            ApplyWaveManagerUiState();
        }

        [ClientRpc]
        private void RematchStatusUpdatedClientRpc(RematchStatusPayload payload)
        {
            MatchPartyContext.ApplyStatusPayload(payload);
            RematchStatusUpdated?.Invoke(payload);
        }

        [ClientRpc]
        public void SyncRematchTransitionClientRpc()
        {
            MatchRematchCoordinator.Instance?.HandleRematchTransitionStarted();
        }

        [ClientRpc]
        public void SyncRematchJoinClientRpc(FixedString64Bytes lobbyId)
        {
            MatchRematchCoordinator.Instance?.JoinOrchestratedRematch(lobbyId.ToString());
        }

        public void NotifyHostReturningHome()
        {
            if (!IsServer) return;
            NotifyHostReturnedHomeClientRpc();
        }

        [ClientRpc]
        private void NotifyHostReturnedHomeClientRpc()
        {
            MatchRematchCoordinator.Instance?.HandleHostReturnedHome();
        }

        [ClientRpc]
        private void RequestCreateRematchRoomClientRpc(
            FixedString64Bytes rematchGroupId,
            FixedString64Bytes roomName,
            ClientRpcParams rpcParams = default)
        {
            MatchRematchCoordinator.Instance?.CreateRematchRoomAsOwner(
                rematchGroupId.ToString(),
                roomName.ToString());
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportRematchLobbyCreatedServerRpc(
            FixedString64Bytes lobbyId,
            ServerRpcParams rpcParams = default)
        {
            if (!NetworkSessionHelper.IsServer) return;
            if (!MatchPartyContext.RematchOrchestrationStarted) return;

            string lobbyIdStr = lobbyId.ToString();
            MatchPartyContext.RematchLobbyId = lobbyIdStr;
            Debug.Log($"[Rematch] Rematch lobby reported by client {rpcParams.Receive.SenderClientId}: {lobbyIdStr}");
            SyncRematchJoinClientRpc(lobbyId);
        }

        public void RequestRematchRoomCreationOnClient(ulong targetClientId, string rematchGroupId, string roomName)
        {
            if (!NetworkSessionHelper.IsServer || !IsSpawned) return;

            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { targetClientId }
                }
            };

            RequestCreateRematchRoomClientRpc(
                new FixedString64Bytes(rematchGroupId),
                new FixedString64Bytes(roomName),
                rpcParams);
        }

        public void ReportRematchLobbyCreated(string lobbyId)
        {
            if (!IsSpawned || string.IsNullOrEmpty(lobbyId)) return;
            ReportRematchLobbyCreatedServerRpc(new FixedString64Bytes(lobbyId));
        }

        private void ApplyClientSnapshot(bool forceEvents)
        {
            if (IsServer || session == null) return;

            int syncedWave = Mathf.Max(session.State.CurrentWave, netWave.Value);
            session.MirrorPhase((GamePhase)netPhase.Value, syncedWave);
            ApplyWaveManagerUiState();

            if (forceEvents
                && netWave.Value > 0
                && (GamePhase)netPhase.Value == GamePhase.Wave
                && session.State.CurrentWave != netWave.Value)
            {
                session.MirrorWaveStarted(netWave.Value);
            }
        }

        private void ApplyWaveManagerUiState()
        {
            if (waveManager == null) return;

            waveManager.ApplyRemoteUiState(
                netTotalToSpawn.Value,
                netSpawnedCount.Value,
                netIsSpawning.Value);
        }
    }
}
