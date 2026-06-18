using Unity.Netcode;
using UnityEngine;

namespace ProjectM.Network
{
    /// <summary>
    /// NGO 세션 여부 및 Host/Server 판별.
    /// </summary>
    public static class NetworkSessionHelper
    {
        public static NetworkManager Manager => NetworkManager.Singleton;

        public static bool IsMultiplayerSession =>
            Manager != null && Manager.IsListening;

        public static bool IsServer =>
            IsMultiplayerSession && Manager.IsServer;

        /// <summary>오프라인이거나 NGO 서버(Host)일 때 게임플레이 권한 보유.</summary>
        public static bool IsGameplayAuthority =>
            !IsMultiplayerSession || IsServer;
    }
}
