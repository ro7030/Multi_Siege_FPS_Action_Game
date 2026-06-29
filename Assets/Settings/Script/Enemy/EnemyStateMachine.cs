using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectM.Enemy
{
    public enum EnemyState { Idle, Chase, Attack, Dead }

    /// <summary>
    /// 가벼운 상태 머신. 상태별 Enter/Update/Exit 콜백을 외부에서 등록한다.
    /// EnemyAIController가 소유하고 매 프레임 Tick()을 호출한다.
    /// </summary>
    public class EnemyStateMachine
    {
        public EnemyState Current { get; private set; } = EnemyState.Idle;

        public event Action<EnemyState, EnemyState> OnStateChanged;

        private readonly Dictionary<EnemyState, Action> onEnter = new();
        private readonly Dictionary<EnemyState, Action> onTick = new();
        private readonly Dictionary<EnemyState, Action> onExit = new();

        public void Bind(EnemyState state, Action enter = null, Action tick = null, Action exit = null)
        {
            if (enter != null) onEnter[state] = enter;
            if (tick != null) onTick[state] = tick;
            if (exit != null) onExit[state] = exit;
        }

        public void ChangeState(EnemyState next)
        {
            if (Current == next) return;
            var prev = Current;
            if (onExit.TryGetValue(prev, out var exit)) exit?.Invoke();
            Current = next;
            if (onEnter.TryGetValue(next, out var enter)) enter?.Invoke();
            OnStateChanged?.Invoke(prev, next);
        }

        public void Tick()
        {
            if (onTick.TryGetValue(Current, out var tick)) tick?.Invoke();
        }
    }
}
