using System.Collections.Generic;
using UnityEngine;
using ProjectM.Network;
using ProjectM.Player;

namespace ProjectM.Economy
{
    /// <summary>
    /// 멀티플레이 골드 지급/지갑 탐색 유틸. 서버 권한 전용.
    /// </summary>
    public static class PlayerWalletUtility
    {
        public static void ServerAddToAllPlayers(int amount, string reason = null)
        {
            if (amount <= 0) return;
            if (NetworkSessionHelper.IsMultiplayerSession && !NetworkSessionHelper.IsServer)
                return;

            int count = 0;
            foreach (var wallet in FindAllPlayerWallets())
            {
                if (wallet == null) continue;
                ServerAdd(wallet, amount);
                count++;
            }

            if (!string.IsNullOrEmpty(reason))
                Debug.Log($"[Economy] {reason}: +{amount} × {count}명");
        }

        public static void ServerAdd(CurrencyWallet wallet, int amount)
        {
            if (wallet == null || amount <= 0) return;

            if (wallet.TryGetComponent<NetworkCurrencyWallet>(out var netWallet)
                && NetworkSessionHelper.IsMultiplayerSession)
            {
                netWallet.ServerAdd(amount);
                return;
            }

            wallet.Add(amount);
        }

        public static List<CurrencyWallet> FindAllPlayerWallets()
        {
            var wallets = new List<CurrencyWallet>();
            var seen = new HashSet<CurrencyWallet>();

            foreach (var player in NetworkPlayerRegistry.All)
            {
                if (player == null) continue;
                if (player.TryGetComponent(out CurrencyWallet wallet) && seen.Add(wallet))
                    wallets.Add(wallet);
            }

            foreach (var pc in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (pc == null) continue;
                if (pc.TryGetComponent(out CurrencyWallet wallet) && seen.Add(wallet))
                    wallets.Add(wallet);
            }

            return wallets;
        }
    }
}
