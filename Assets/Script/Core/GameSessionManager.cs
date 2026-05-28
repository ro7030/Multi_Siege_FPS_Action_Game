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

            bool isFinal = State.CurrentWave >= State.MaxWave;

            // 페이즈를 먼저 전환한 뒤 OnWaveEnded 를 발화한다.
            // (구독자 MatchBootstrapper / BannerEventBridge 등이 CurrentPhase == Preparation 을
            //  검사해 정비 코루틴 시작·정비 안내 배너 표시 등을 결정하기 때문)
            if (!isFinal)
            {
                Phase.ChangePhase(GamePhase.WaveCleared);
                Phase.ChangePhase(GamePhase.Preparation);
                Debug.Log($"[GameSession] Wave {State.CurrentWave} cleared. Next preparation.");
            }

            OnWaveEnded?.Invoke(State.CurrentWave);

            if (isFinal)
            {
                // 마지막 웨이브: OnWaveEnded 보상 처리가 끝난 뒤 매치 종료 (Result 페이즈로 전환).
                EndMatch(true);
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
