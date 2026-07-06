using UnityEngine;
using ProjectM.Auth;

namespace ProjectM.Network
{
    // 플레이어 표시 이름. Login(AuthSession) → NetworkPlayer.DisplayName 순으로 해석한다.
    public static class PlayerDisplayNameUtility
    {
        public static string GetDisplayName(GameObject playerRoot, string fallback = "player")
        {
            if (playerRoot == null) return fallback;

            if (playerRoot.TryGetComponent<NetworkPlayer>(out var networkPlayer))
            {
                string synced = networkPlayer.DisplayName;
                if (!string.IsNullOrWhiteSpace(synced))
                    return synced.Trim();
            }

            return AuthSessionManager.ResolveNickname(fallback);
        }

        public static string GetDisplayName(Component component, string fallback = "player")
        {
            return component != null ? GetDisplayName(component.gameObject, fallback) : fallback;
        }
    }
}
