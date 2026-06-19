using Unity.Netcode;
using UnityEngine;
using ProjectM.Economy;

namespace ProjectM.Network
{
    /// <summary>
    /// 플레이어 지갑 잔액을 서버 권한으로 NGO 동기화한다.
    /// </summary>
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
            }
            else
            {
                netBalance.OnValueChanged += HandleBalanceChanged;
                wallet.SetBalance(netBalance.Value);
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

            netBalance.Value += amount;
            wallet.SetBalance(netBalance.Value);
        }

        public bool ServerTrySpend(int amount)
        {
            if (!IsServer || amount <= 0)
                return true;

            if (netBalance.Value < amount)
                return false;

            netBalance.Value -= amount;
            wallet.SetBalance(netBalance.Value);
            return true;
        }

        private void HandleBalanceChanged(int _, int value) => wallet.SetBalance(value);
    }
}
