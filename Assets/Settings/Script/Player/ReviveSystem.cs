using System;
using UnityEngine;
using ProjectM.Network;

namespace ProjectM.Player
{
    // 다운/부활 상태 관리. HealthSystem이 0이 되면 즉시 Death가 아닌 Down 상태로 진입한다.
    // 일정 시간 안에 동료가 부활시키지 못하면 완전 사망으로 전환된다.
    [RequireComponent(typeof(HealthSystem))]
    public class ReviveSystem : MonoBehaviour
    {
        [SerializeField] private float downDuration = 30f;       // 다운 후 완전 사망까지의 시간
        [SerializeField] private float reviveDuration = 3f;      // 부활 인터랙션 진행 시간
        [SerializeField] private float reviveHpRatio = 0.5f;     // 부활 시 회복 비율

        public bool IsDown { get; private set; }
        public bool IsDead { get; private set; }
        public float DownTimer { get; private set; }
        public float ReviveProgress { get; private set; }
        public float ReviveDuration => reviveDuration;

        public event Action OnDowned;
        public event Action OnReviveStarted;
        public event Action OnRevived;
        public event Action OnFullDeath;

        private HealthSystem health;
        private bool reviveInProgress;

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
        }

        private void OnEnable() => health.OnDied += HandleHealthDied;
        private void OnDisable() => health.OnDied -= HandleHealthDied;

        private void HandleHealthDied(GameObject _)
        {
            if (!ShouldSimulateLocally()) return;
            if (IsDead || IsDown) return;
            EnterDownState();
        }

        private void EnterDownState()
        {
            IsDown = true;
            DownTimer = downDuration;
            ReviveProgress = 0f;
            reviveInProgress = false;
            OnDowned?.Invoke();
            Debug.Log($"[Revive] {name} 다운 상태 진입 ({downDuration}초 남음)");
        }

        private void Update()
        {
            if (!ShouldSimulateLocally()) return;
            if (!IsDown || IsDead) return;

            if (!reviveInProgress)
            {
                DownTimer -= Time.deltaTime;
                if (DownTimer <= 0f) EnterFullDeath();
            }
        }

        public void ProgressRevive(float delta)
        {
            if (!ShouldSimulateLocally()) return;
            if (!IsDown || IsDead) return;
            if (!reviveInProgress)
            {
                reviveInProgress = true;
                OnReviveStarted?.Invoke();
            }
            ReviveProgress += delta;
            if (ReviveProgress >= reviveDuration) FinishRevive();
        }

        public void CancelRevive()
        {
            if (!reviveInProgress) return;
            reviveInProgress = false;
            ReviveProgress = 0f;
        }

        private void FinishRevive()
        {
            IsDown = false;
            reviveInProgress = false;
            ReviveProgress = 0f;
            health.ResetHp();
            health.SetMaxHp(health.MaxHp, refill: false);
            // 절반만 회복
            float target = health.MaxHp * reviveHpRatio;
            health.Heal(target - health.CurrentHp);
            OnRevived?.Invoke();
            Debug.Log($"[Revive] {name} 부활 완료");
        }

        private void EnterFullDeath()
        {
            if (!ShouldSimulateLocally()) return;
            IsDown = false;
            IsDead = true;
            OnFullDeath?.Invoke();
            Debug.Log($"[Revive] {name} 완전 사망");
        }

        public void ApplyNetworkLifeState(byte state)
        {
            if (ShouldSimulateLocally()) return;

            const byte lifeAlive = 0;
            const byte lifeDown = 1;
            const byte lifeDead = 2;

            switch (state)
            {
                case lifeAlive:
                    if (IsDown || IsDead)
                    {
                        IsDown = false;
                        IsDead = false;
                        reviveInProgress = false;
                        ReviveProgress = 0f;
                        OnRevived?.Invoke();
                    }
                    break;
                case lifeDown:
                    if (!IsDown && !IsDead)
                    {
                        IsDown = true;
                        IsDead = false;
                        DownTimer = downDuration;
                        ReviveProgress = 0f;
                        reviveInProgress = false;
                        OnDowned?.Invoke();
                    }
                    else if (IsDown)
                    {
                        IsDead = false;
                    }
                    break;
                case lifeDead:
                    if (!IsDead)
                    {
                        IsDown = false;
                        IsDead = true;
                        reviveInProgress = false;
                        ReviveProgress = 0f;
                        OnFullDeath?.Invoke();
                    }
                    break;
            }
        }

        public void ApplyNetworkReviveProgress(float progress)
        {
            if (ShouldSimulateLocally()) return;
            ReviveProgress = Mathf.Max(0f, progress);
            reviveInProgress = ReviveProgress > 0f && IsDown && !IsDead;
        }

        private static bool ShouldSimulateLocally()
        {
            return !NetworkSessionHelper.IsMultiplayerSession || NetworkSessionHelper.IsServer;
        }
    }
}
