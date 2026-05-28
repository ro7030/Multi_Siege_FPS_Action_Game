using UnityEngine;
using ProjectM.Core;
using ProjectM.Player;

namespace ProjectM.Economy
{
    /// <summary>
    /// 웨이브 종료 시 보상을 계산하여 CurrencyWallet에 지급한다.
    /// MVP 공식: baseReward + (waveNumber * waveBonus)
    /// </summary>
    public class RewardCalculator : MonoBehaviour
    {
        [SerializeField] private GameSessionManager session;
        [SerializeField] private CurrencyWallet wallet;

        [Header("보상 공식")]
        [SerializeField] private int baseReward = 30;
        [SerializeField] private int waveBonus = 10;
        [SerializeField] private int bossWaveBonus = 100;
        [SerializeField] private int finalClearBonus = 300;

        public int LastReward { get; private set; }

        private void Awake()
        {
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
            if (wallet == null) wallet = LocalPlayerUtility.FindLocalCurrencyWallet();
        }

        private void OnEnable()
        {
            if (session == null) return;
            session.OnWaveEnded += HandleWaveEnded;
            session.OnMatchEnded += HandleMatchEnded;
        }

        private void OnDisable()
        {
            if (session == null) return;
            session.OnWaveEnded -= HandleWaveEnded;
            session.OnMatchEnded -= HandleMatchEnded;
        }

        private void HandleWaveEnded(int waveNumber)
        {
            int reward = baseReward + waveBonus * waveNumber;
            // 보스 웨이브 보정: WaveConfig.isBossWave는 Wave 모듈에 있으므로 단순화하여 마지막 웨이브를 보스로 간주
            if (session != null && waveNumber >= session.State.MaxWave) reward += bossWaveBonus;
            ApplyReward(reward, $"Wave {waveNumber} 보상");
        }

        private void HandleMatchEnded(bool cleared)
        {
            if (cleared) ApplyReward(finalClearBonus, "전체 클리어 보너스");
        }

        private void ApplyReward(int amount, string reason)
        {
            if (wallet == null) return;
            wallet.Add(amount);
            LastReward = amount;
            Debug.Log($"[Reward] {reason}: +{amount} (잔액 {wallet.Balance})");
        }
    }
}
