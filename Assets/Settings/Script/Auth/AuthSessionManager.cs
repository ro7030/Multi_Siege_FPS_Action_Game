using UnityEngine;

namespace ProjectM.Auth
{
    public enum AuthMode
    {
        None,
        Guest,
        PlayerAccount
    }

    // 로그인 세션 정보를 씬 전환 후에도 유지하는 싱글톤.
    public class AuthSessionManager : MonoBehaviour
    {
        public static AuthSessionManager Instance { get; private set; }

        public string Nickname { get; private set; } = string.Empty;
        public string PlayerId { get; private set; } = string.Empty;
        public bool IsGuest { get; private set; }
        public bool IsSignedIn { get; private set; }
        public AuthMode AuthMode { get; private set; } = AuthMode.None;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SetSession(string nickname, string playerId, bool isGuest)
        {
            Nickname = nickname ?? string.Empty;
            PlayerId = playerId ?? string.Empty;
            IsGuest = isGuest;
            IsSignedIn = !string.IsNullOrEmpty(PlayerId);
            AuthMode = isGuest ? AuthMode.Guest : AuthMode.PlayerAccount;
            Debug.Log($"[AuthSession] Saved nickname={Nickname}, playerId={PlayerId}, isGuest={IsGuest}");
        }

        public void ClearSession()
        {
            Nickname = string.Empty;
            PlayerId = string.Empty;
            IsGuest = false;
            IsSignedIn = false;
            AuthMode = AuthMode.None;
        }

        public static string ResolveNickname(string fallback = "player")
        {
            if (Instance != null && !string.IsNullOrWhiteSpace(Instance.Nickname))
                return Instance.Nickname.Trim();
            return fallback;
        }
    }
}
