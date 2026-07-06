using System.Collections;
using ProjectM.Auth;
using ProjectM.Core;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectM.Network
{
    // ResultView Retry/Home → orchestrated rematch(새 Lobby/Relay) 또는 MainMenu 복귀 조율.
    // LobbyRelayService DontDestroyOnLoad 오브젝트에 부착.
    public class MatchRematchCoordinator : MonoBehaviour
    {
        public const string CharacterSelectScene = "CharacterSelect";
        public const string MainMenuScene = "MainMenu";

        public static MatchRematchCoordinator Instance { get; private set; }

        public static event System.Action HostReturnedHome;
        public static event System.Action RematchTransitionStarted;
        public static event System.Action RematchOrchestrationFailed;

        private bool isBusy;
        private bool rematchLeaveCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // 오프라인 Retry — 로컬 CharacterSelect.
        public void RequestRematchOffline()
        {
            if (isBusy) return;
            StartCoroutine(OfflineRematchRoutine());
        }

        public void RequestHome()
        {
            StartCoroutine(HomeRoutine());
        }

        // NetworkMatchDirector — 전원 Retry 확인 후 서버에서 1회 호출.
        public void BeginOrchestratedRematch(
            string rematchHostAuthPlayerId,
            string rematchGroupId,
            ulong rematchHostClientId)
        {
            if (isBusy) return;
            StartCoroutine(BeginOrchestratedRematchRoutine(
                rematchHostAuthPlayerId,
                rematchGroupId,
                rematchHostClientId));
        }

        // Guest rematch host — NetworkMatchDirector ClientRpc.
        public void CreateRematchRoomAsOwner(string rematchGroupId, string roomName)
        {
            if (string.IsNullOrEmpty(rematchGroupId)) return;
            StartCoroutine(CreateRematchRoomAsOwnerRoutine(rematchGroupId, roomName));
        }

        // NetworkMatchDirector SyncRematchJoinClientRpc (연결 유지 시).
        // Leave 이후에는 Lobby 검색으로 Join한다.
        public void JoinOrchestratedRematch(string lobbyId)
        {
            if (string.IsNullOrEmpty(lobbyId)) return;

            var relay = LobbyRelayService.Instance;
            if (relay != null && relay.IsHost && relay.CurrentLobbyId == lobbyId)
                return;

            StartCoroutine(GuestJoinOrchestratedRematchRoutine(lobbyId));
        }

        // NetworkMatchDirector SyncRematchTransitionClientRpc — 전원 UI 잠금 + Leave.
        public void HandleRematchTransitionStarted()
        {
            Time.timeScale = 1f;
            isBusy = true;
            rematchLeaveCompleted = false;
            RematchTransitionStarted?.Invoke();
            StartCoroutine(ClientOrchestratedRematchRoutine());
        }

        // NetworkMatchDirector — Host Home 알림.
        public void HandleHostReturnedHome()
        {
            MatchPartyContext.HostLeftViaHome = true;
            HostReturnedHome?.Invoke();
            Debug.Log("[Rematch] Host returned home.");
        }

        private IEnumerator BeginOrchestratedRematchRoutine(
            string rematchHostAuthPlayerId,
            string rematchGroupId,
            ulong rematchHostClientId)
        {
            isBusy = true;
            rematchLeaveCompleted = false;
            Time.timeScale = 1f;

            try
            {
                var director = FindAnyObjectByType<NetworkMatchDirector>();
                director?.SyncRematchTransitionClientRpc();

                float wait = 0f;
                while (!rematchLeaveCompleted && wait < 30f)
                {
                    wait += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (!rematchLeaveCompleted)
                {
                    FailOrchestrationAndNotify("Timed out waiting for clients to leave session.");
                    yield break;
                }

                ulong localClientId = NetworkManager.Singleton != null
                    ? NetworkManager.Singleton.LocalClientId
                    : 0;
                bool isRematchHost = rematchHostClientId != 0
                    ? localClientId == rematchHostClientId
                    : IsLocalRematchHostByAuth(rematchHostAuthPlayerId);

                if (isRematchHost)
                {
                    yield return CreateRematchRoomLocallyRoutine(rematchGroupId, director);
                    yield break;
                }

                if (NetworkSessionHelper.IsServer && director != null && rematchHostClientId != 0)
                {
                    string roomName = $"{AuthSessionManager.ResolveNickname("player")} rematch";
                    director.RequestRematchRoomCreationOnClient(
                        rematchHostClientId,
                        rematchGroupId,
                        roomName);
                }
            }
            finally
            {
                isBusy = false;
            }
        }

        private IEnumerator CreateRematchRoomAsOwnerRoutine(string rematchGroupId, string roomName)
        {
            isBusy = true;
            try
            {
                yield return CreateRematchRoomLocallyRoutine(rematchGroupId, FindAnyObjectByType<NetworkMatchDirector>());
            }
            finally
            {
                isBusy = false;
            }
        }

        private IEnumerator CreateRematchRoomLocallyRoutine(
            string rematchGroupId,
            NetworkMatchDirector director)
        {
            var relay = LobbyRelayService.Instance;
            if (relay == null)
            {
                FailOrchestrationAndNotify("LobbyRelayService missing — cannot create rematch room.");
                yield break;
            }

            string roomName = $"{AuthSessionManager.ResolveNickname("player")} rematch";
            Debug.Log($"[Rematch] Create rematch room group={rematchGroupId}");

            var createTask = relay.CreateRematchRoomAsync(rematchGroupId, roomName);
            while (!createTask.IsCompleted)
                yield return null;

            if (createTask.IsFaulted)
            {
                FailOrchestrationAndNotify(
                    $"Create rematch room failed: {createTask.Exception?.GetBaseException().Message}");
                yield break;
            }

            string newLobbyId = relay.CurrentLobbyId;
            MatchPartyContext.RematchLobbyId = newLobbyId;
            MatchPartyContext.HostLeftViaHome = false;

            if (director != null && director.IsSpawned)
            {
                if (NetworkSessionHelper.IsServer)
                    director.SyncRematchJoinClientRpc(new FixedString64Bytes(newLobbyId));
                else
                    director.ReportRematchLobbyCreated(newLobbyId);
            }

            CompleteRematchHostFlow();
        }

        private void CompleteRematchHostFlow()
        {
            GameSessionManager.Instance?.ReturnToLobby();
            MatchPartyContext.ResetRematchSession();
            LoadCharacterSelectViaNgo();
        }

        private IEnumerator ClientOrchestratedRematchRoutine()
        {
            yield return LeaveSessionRoutine();

            if (IsLocalRematchPlayerLeftHome())
                yield break;

            ulong localClientId = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.LocalClientId
                : 0;
            ulong rematchHostClientId = MatchPartyContext.RematchHostClientId;
            bool isRematchHost = rematchHostClientId != 0
                ? localClientId == rematchHostClientId
                : IsLocalRematchHostByAuth(MatchPartyContext.RematchHostAuthPlayerId);

            if (isRematchHost)
                yield break;

            yield return DiscoverAndJoinRematchLobbyRoutine();
        }

        private IEnumerator GuestJoinOrchestratedRematchRoutine(string lobbyId)
        {
            if (!rematchLeaveCompleted)
                yield return LeaveSessionRoutine();

            yield return JoinRematchLobbyAndWaitForSceneSync(lobbyId);
        }

        private IEnumerator DiscoverAndJoinRematchLobbyRoutine()
        {
            var relay = LobbyRelayService.Instance;
            if (relay == null)
            {
                FailOrchestrationAndNotify("LobbyRelayService missing — cannot discover rematch lobby.");
                yield break;
            }

            string groupId = MatchPartyContext.RematchGroupId;
            if (string.IsNullOrEmpty(groupId))
            {
                FailOrchestrationAndNotify("RematchGroupId missing — cannot discover rematch lobby.");
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < 45f)
            {
                if (!string.IsNullOrEmpty(MatchPartyContext.RematchLobbyId))
                {
                    yield return JoinRematchLobbyAndWaitForSceneSync(MatchPartyContext.RematchLobbyId);
                    yield break;
                }

                var findTask = relay.TryFindRematchLobbyAsync(groupId);
                while (!findTask.IsCompleted)
                    yield return null;

                if (!string.IsNullOrEmpty(findTask.Result))
                {
                    yield return JoinRematchLobbyAndWaitForSceneSync(findTask.Result);
                    yield break;
                }

                elapsed += 0.75f;
                yield return new WaitForSecondsRealtime(0.75f);
            }

            FailOrchestrationAndNotify("Timed out waiting for rematch lobby discovery.");
        }

        private IEnumerator JoinRematchLobbyAndWaitForSceneSync(string lobbyId)
        {
            var relay = LobbyRelayService.Instance;
            if (relay == null)
            {
                FailOrchestrationAndNotify("LobbyRelayService missing — cannot join rematch.");
                yield break;
            }

            if (relay.IsInSession && relay.CurrentLobbyId == lobbyId)
            {
                Debug.Log($"[Rematch] Already in rematch lobby {lobbyId}");
                yield break;
            }

            var joinTask = relay.JoinRoomAsync(lobbyId, string.Empty);
            while (!joinTask.IsCompleted)
                yield return null;

            if (joinTask.IsFaulted)
            {
                FailOrchestrationAndNotify(
                    $"Join rematch failed: {joinTask.Exception?.GetBaseException().Message}");
                yield break;
            }

            Debug.Log($"[Rematch] Joined rematch lobby {lobbyId} — waiting for NGO scene sync.");
        }

        private IEnumerator LeaveSessionRoutine()
        {
            var relay = LobbyRelayService.Instance;
            if (relay != null && relay.IsInSession)
            {
                var leaveTask = relay.LeaveSessionAsync();
                while (!leaveTask.IsCompleted)
                    yield return null;
            }
            else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            rematchLeaveCompleted = true;
        }

        private IEnumerator OfflineRematchRoutine()
        {
            isBusy = true;
            Time.timeScale = 1f;

            try
            {
                GameSessionManager.Instance?.ReturnToLobby();
                SceneManager.LoadScene(CharacterSelectScene);
            }
            finally
            {
                isBusy = false;
            }

            yield break;
        }

        private static void LoadCharacterSelectViaNgo()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsHost && nm.IsListening)
            {
                nm.SceneManager.LoadScene(CharacterSelectScene, LoadSceneMode.Single);
                Debug.Log("[Rematch] Orchestrated rematch host → CharacterSelect (NGO)");
                return;
            }

            Debug.LogWarning("[Rematch] NGO host not listening — cannot load CharacterSelect via scene manager.");
        }

        private IEnumerator HomeRoutine()
        {
            while (isBusy && MatchPartyContext.RematchOrchestrationStarted)
                yield return null;

            isBusy = true;
            Time.timeScale = 1f;

            try
            {
                if (NetworkSessionHelper.IsMultiplayerSession)
                {
                    var director = FindAnyObjectByType<NetworkMatchDirector>();
                    if (director != null && director.IsSpawned)
                    {
                        director.UnregisterRematchPlayer();

                        if (NetworkSessionHelper.IsServer)
                            director.NotifyHostReturningHome();
                    }
                }

                if (MatchPartyContext.RematchOrchestrationStarted)
                {
                    float orchestrationWait = 0f;
                    while (MatchPartyContext.RematchOrchestrationStarted && orchestrationWait < 12f)
                    {
                        orchestrationWait += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                GameSessionManager.Instance?.ReturnToLobby();

                var relay = LobbyRelayService.Instance;
                if (relay != null)
                {
                    var leaveTask = relay.LeaveSessionForHomeAsync();
                    while (!leaveTask.IsCompleted)
                        yield return null;
                }
                else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.Shutdown();
                }

                if (!MatchPartyContext.RematchOrchestrationStarted)
                    MatchPartyContext.Clear();

                SceneManager.LoadScene(MainMenuScene);
            }
            finally
            {
                isBusy = false;
            }
        }

        private static bool IsLocalRematchHostByAuth(string rematchHostAuthPlayerId)
        {
            if (string.IsNullOrEmpty(rematchHostAuthPlayerId))
                return NetworkSessionHelper.IsServer;

            return ResolveLocalAuthPlayerId() == rematchHostAuthPlayerId;
        }

        private static bool IsLocalRematchPlayerLeftHome()
        {
            if (!NetworkSessionHelper.IsMultiplayerSession || NetworkManager.Singleton == null)
                return false;

            ulong localClientId = NetworkManager.Singleton.LocalClientId;
            var payload = MatchPartyContext.LastStatusPayload;

            for (int i = 0; i < payload.PlayerCount; i++)
            {
                var entry = payload.GetPlayer(i);
                if (entry.OwnerClientId != localClientId) continue;
                return entry.PlayerState == RematchPlayerState.LeftHome;
            }

            return false;
        }

        private static void FailOrchestrationAndNotify(string reason)
        {
            MatchPartyContext.FailOrchestration(reason);
            RematchOrchestrationFailed?.Invoke();
        }

        private static string ResolveLocalAuthPlayerId()
        {
            if (AuthSessionManager.Instance != null && !string.IsNullOrEmpty(AuthSessionManager.Instance.PlayerId))
                return AuthSessionManager.Instance.PlayerId;

            var local = NetworkPlayerRegistry.LocalPlayer;
            if (local != null && !string.IsNullOrEmpty(local.AuthPlayerId))
                return local.AuthPlayerId;

            return $"client:{NetworkManager.Singleton?.LocalClientId ?? 0}";
        }
    }
}
