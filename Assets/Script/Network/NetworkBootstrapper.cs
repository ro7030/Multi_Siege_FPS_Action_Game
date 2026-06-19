using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace ProjectM.Network
{
    /// <summary>
    /// MainMenu 등에서 NetworkManager와 LobbyRelayService를 준비한다.
    /// </summary>
    public class NetworkBootstrapper : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManagerPrefab;
        [SerializeField] private bool createLobbyRelayService = true;

        private void Awake()
        {
            NetworkManager manager = ResolveNetworkManager();
            LobbyRelayService service = ResolveLobbyRelayService();

            if (service != null && manager != null)
                service.BindNetworkManager(manager);
        }

        private NetworkManager ResolveNetworkManager()
        {
            var existing = FindAnyObjectByType<NetworkManager>();
            if (existing != null)
            {
                NetworkPlayerSessionGuard.ApplyManagerSettings(existing);
                return existing;
            }

            if (networkManagerPrefab == null)
            {
                var go = new GameObject("NetworkManager");
                DontDestroyOnLoad(go);
                var manager = go.AddComponent<NetworkManager>();
                manager.NetworkConfig = new NetworkConfig();
                go.AddComponent<UnityTransport>();
                NetworkPlayerSessionGuard.ApplyManagerSettings(manager);
                return manager;
            }

            var instance = Instantiate(networkManagerPrefab);
            DontDestroyOnLoad(instance.gameObject);
            NetworkPlayerSessionGuard.ApplyManagerSettings(instance);
            return instance;
        }

        private LobbyRelayService ResolveLobbyRelayService()
        {
            if (!createLobbyRelayService) return LobbyRelayService.Instance;

            if (LobbyRelayService.Instance != null)
                return LobbyRelayService.Instance;

            var go = new GameObject(nameof(LobbyRelayService));
            DontDestroyOnLoad(go);
            return go.AddComponent<LobbyRelayService>();
        }
    }
}
