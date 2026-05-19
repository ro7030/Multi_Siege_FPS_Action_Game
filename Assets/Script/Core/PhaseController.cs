using System;
using UnityEngine;

namespace ProjectM.Core
{
    public class PhaseController
    {
        public event Action<GamePhase, GamePhase> OnPhaseChanged;

        private readonly MatchState state;

        public PhaseController(MatchState state)
        {
            this.state = state;
        }

        public void ChangePhase(GamePhase next)
        {
            if (state.CurrentPhase == next) return;

            if (!CanTransition(state.CurrentPhase, next))
            {
                Debug.LogWarning($"[PhaseController] Invalid transition: {state.CurrentPhase} -> {next}");
                return;
            }

            GamePhase prev = state.CurrentPhase;
            state.CurrentPhase = next;
            OnPhaseChanged?.Invoke(prev, next);
            Debug.Log($"[PhaseController] Phase changed: {prev} -> {next}");
        }

        private bool CanTransition(GamePhase from, GamePhase to)
        {
            switch (from)
            {
                case GamePhase.None:         return to == GamePhase.Lobby;
                case GamePhase.Lobby:        return to == GamePhase.Preparation;
                case GamePhase.Preparation:  return to == GamePhase.Wave;
                case GamePhase.Wave:         return to == GamePhase.WaveCleared || to == GamePhase.Result;
                case GamePhase.WaveCleared:  return to == GamePhase.Preparation || to == GamePhase.Result;
                case GamePhase.Result:       return to == GamePhase.Lobby;
                default:                     return false;
            }
        }
    }
}
