using System.Collections.Generic;
using ProjectM.Player;

namespace ProjectM.Network
{
    /// <summary>
    /// 스폰된 NetworkPlayer 목록. LocalPlayerUtility 등에서 참조.
    /// </summary>
    public static class NetworkPlayerRegistry
    {
        private static readonly List<NetworkPlayer> players = new();

        public static IReadOnlyList<NetworkPlayer> All => players;

        public static NetworkPlayer LocalPlayer
        {
            get
            {
                foreach (var p in players)
                {
                    if (p != null && p.IsOwner) return p;
                }
                return null;
            }
        }

        internal static void Register(NetworkPlayer player)
        {
            if (player == null || players.Contains(player)) return;
            players.Add(player);
        }

        internal static void Unregister(NetworkPlayer player) => players.Remove(player);
    }
}
