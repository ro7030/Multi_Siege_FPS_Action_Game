using System;

namespace ProjectM.Core
{
    public enum GamePhase
    {
        None,
        Lobby,
        Preparation,
        Wave,
        WaveCleared,
        Result
    }

    [Serializable]
    public class MatchState
    {
        public GamePhase CurrentPhase = GamePhase.None;
        public int CurrentWave = 0;
        public int MaxWave = 10;
        public int PlayerCount = 0;
        public bool IsCleared = false;
        public bool IsFailed = false;

        public float MatchStartTime = 0f;
        public float CurrentWaveStartTime = 0f;

        public void Reset()
        {
            CurrentPhase = GamePhase.None;
            CurrentWave = 0;
            PlayerCount = 0;
            IsCleared = false;
            IsFailed = false;
            MatchStartTime = 0f;
            CurrentWaveStartTime = 0f;
        }
    }
}
