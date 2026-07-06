using Unity.Netcode;
using UnityEngine;
using ProjectM.Economy;

namespace ProjectM.Network
{
    // 플레이어 지갑 잔액을 서버 권한으로 NGO 동기화한다.
    [RequireComponent(typeof(CurrencyWallet))]
    public class NetworkCurrencyWallet : NetworkBehaviour
    {
        private readonly NetworkVariable<int> netBalance = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private CurrencyWallet wallet;

        private void Awake() => wallet = GetComponent<CurrencyWallet>();

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                netBalance.Value = wallet.Balance;
                wallet.NotifyBalance(netBalance.Value);
            }
            else
            {
                netBalance.OnValueChanged += HandleBalanceChanged;
                wallet.NotifyBalance(netBalance.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer)
                netBalance.OnValueChanged -= HandleBalanceChanged;
        }

        public void ServerAdd(int amount)
        {
            if (!IsServer || amount <= 0)
                return;

            if (!IsSpawned)
            {
                wallet.NotifyBalance(wallet.Balance + amount, added: amount);
                return;
            }

            netBalance.Value += amount;
            wallet.NotifyBalance(netBalance.Value, added: amount);
        }

        public bool ServerTrySpend(int amount)
        {
            if (!IsServer || amount <= 0)
                return true;

            if (!IsSpawned)
                return wallet.Balance >= amount;

            if (netBalance.Value < amount)
                return false;

            netBalance.Value -= amount;
            wallet.NotifyBalance(netBalance.Value, spent: amount);
            return true;
        }

        private void HandleBalanceChanged(int _, int value) => wallet.NotifyBalance(value);
    }
}
