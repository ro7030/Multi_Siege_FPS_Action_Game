using System;
using UnityEngine;

namespace ProjectM.Economy
{
    /// <summary>
    /// 플레이어 화폐 지갑. 잔액 관리 및 이벤트 발행.
    /// MVP에서는 로컬에서 동작. 네트워크 단계에서는 Host 권한.
    /// </summary>
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
            balance += amount;
            OnAdded?.Invoke(amount);
            OnChanged?.Invoke(balance);
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0) return true;
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
            balance = Mathf.Max(0, value);
            OnChanged?.Invoke(balance);
        }
    }
}
