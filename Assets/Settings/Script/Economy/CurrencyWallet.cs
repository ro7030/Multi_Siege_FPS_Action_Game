using System;
using UnityEngine;
using ProjectM.Network;

namespace ProjectM.Economy
{
    // 플레이어 화폐 지갑. 잔액 관리 및 이벤트 발행.
    // MVP에서는 로컬에서 동작. 네트워크 단계에서는 Host 권한.
    public class CurrencyWallet : MonoBehaviour
    {
        [SerializeField] private int startingBalance = 0;
        [SerializeField] private int balance = 0;

        public int Balance => balance;

        public event Action<int> OnChanged;   // 새 잔액
        public event Action<int> OnAdded;     // 증가량
        public event Action<int> OnSpent;     // 차감량
        public event Action<int> OnSpendFailed; // 시도 차감량 (잔액 부족)

        private void Awake()
        {
            balance = Mathf.Max(0, startingBalance);
        }

        public void Add(int amount)
        {
            if (amount <= 0) return;

            if (TryGetComponent<NetworkCurrencyWallet>(out var netWallet)
                && NetworkSessionHelper.IsMultiplayerSession
                && netWallet.IsSpawned)
            {
                if (NetworkSessionHelper.IsServer)
                    netWallet.ServerAdd(amount);
                return;
            }

            balance += amount;
            OnAdded?.Invoke(amount);
            OnChanged?.Invoke(balance);
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0) return true;

            if (TryGetComponent<NetworkCurrencyWallet>(out var netWallet)
                && NetworkSessionHelper.IsMultiplayerSession
                && netWallet.IsSpawned)
            {
                if (NetworkSessionHelper.IsServer)
                    return netWallet.ServerTrySpend(amount);

                OnSpendFailed?.Invoke(amount);
                return false;
            }

            if (balance < amount)
            {
                OnSpendFailed?.Invoke(amount);
                return false;
            }
            balance -= amount;
            OnSpent?.Invoke(amount);
            OnChanged?.Invoke(balance);
            return true;
        }

        public void SetBalance(int value)
        {
            NotifyBalance(value);
        }

        // 서버/네트워크 동기화용. 잔액 갱신 + 이벤트 발행.
        public void NotifyBalance(int value, int added = 0, int spent = 0)
        {
            balance = Mathf.Max(0, value);
            if (added > 0) OnAdded?.Invoke(added);
            if (spent > 0) OnSpent?.Invoke(spent);
            OnChanged?.Invoke(balance);
        }
    }
}
