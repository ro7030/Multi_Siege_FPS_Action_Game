using Unity.Netcode;
using UnityEngine;
using ProjectM.Core;
using ProjectM.Wave;

namespace ProjectM.Network
{
    /// <summary>
    /// 서버 매치 진행 상태를 클라이언트 UI에 미러링한다.
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

        private void HandleServerMatchStarted()
        {
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
            SyncMatchEndedClientRpc(cleared, netPhase.Value);
        }

        [ClientRpc]
        private void SyncMatchStartedClientRpc()
        {
            if (IsServer) return;
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
        private void SyncMatchEndedClientRpc(bool cleared, int phase)
        {
            if (IsServer) return;
            session?.MirrorMatchEnded(cleared, (GamePhase)phase);
            ApplyWaveManagerUiState();
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
