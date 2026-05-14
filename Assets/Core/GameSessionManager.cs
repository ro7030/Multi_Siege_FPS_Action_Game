using System;
using UnityEngine;

namespace ProjectM.Core
{
    public class GameSessionManager : MonoBehaviour
    {
        public static GameSessionManager Instance { get; private set; }

        [SerializeField] private int maxWave = 10;

        public MatchState State { get; private set; }
        public PhaseController Phase { get; private set; }

        public event Action OnMatchStarted;
        public event Action<int> OnWaveStarted;
        public event Action<int> OnWaveEnded;
        public event Action<bool> OnMatchEnded; // true = cleared, false = failed

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            State = new MatchState { MaxWave = maxWave };
            Phase = new PhaseController(State);

            Phase.ChangePhase(GamePhase.Lobby);
        }

        public void StartMatch()
        {
            if (State.CurrentPhase != GamePhase.Lobby)
            {
                Debug.LogWarning("[GameSession] StartMatch called outside Lobby phase.");
                return;
            }

            State.Reset();
            State.MaxWave = maxWave;
            State.MatchStartTime = Time.time;

            Phase.ChangePhase(GamePhase.Lobby);
            Phase.ChangePhase(GamePhase.Preparation);
            OnMatchStarted?.Invoke();
            Debug.Log("[GameSession] Match started.");
        }

        public void StartWave()
        {
            if (State.CurrentPhase != GamePhase.Preparation)
            {
                Debug.LogWarning("[GameSession] StartWave called outside Preparation phase.");
                return;
            }

            State.CurrentWave++;
            State.CurrentWaveStartTime = Time.time;
            Phase.ChangePhase(GamePhase.Wave);
            OnWaveStarted?.Invoke(State.CurrentWave);
            Debug.Log($"[GameSession] Wave {State.CurrentWave} started.");
        }

        public void EndWave()
        {
            if (State.CurrentPhase != GamePhase.Wave)
            {
                Debug.LogWarning("[GameSession] EndWave called outside Wave phase.");
                return;
            }

            OnWaveEnded?.Invoke(State.CurrentWave);

            if (State.CurrentWave >= State.MaxWave)
            {
                EndMatch(true);
            }
            else
            {
                Phase.ChangePhase(GamePhase.WaveCleared);
                Phase.ChangePhase(GamePhase.Preparation);
                Debug.Log($"[GameSession] Wave {State.CurrentWave} cleared. Next preparation.");
            }
        }

        public void EndMatch(bool cleared)
        {
            State.IsCleared = cleared;
            State.IsFailed = !cleared;
            Phase.ChangePhase(GamePhase.Result);
            OnMatchEnded?.Invoke(cleared);
            Debug.Log($"[GameSession] Match ended. Cleared = {cleared}");
        }

        public void ReturnToLobby()
        {
            Phase.ChangePhase(GamePhase.Lobby);
            State.Reset();
        }
    }
}
