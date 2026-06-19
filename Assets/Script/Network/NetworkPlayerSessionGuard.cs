using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectM.Network
{
    /// <summary>
    /// GamePlay가 아닌 씬(MainMenu, CharacterSelect 등)에서는 NetworkPlayer(PlayerObject)를 두지 않는다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class NetworkPlayerSessionGuard : MonoBehaviour
    {
        [SerializeField] private string gameplaySceneName = "GamePlay";

        private NetworkManager networkManager;
        private bool coreEventsSubscribed;
        private bool networkSceneEventsSubscribed;

        public static bool IsGameplayScene(string sceneName, string gameplayScene = "GamePlay") =>
            sceneName == gameplayScene;

        public static void ApplyManagerSettings(NetworkManager manager)
        {
            if (manager == null) return;

            manager.NetworkConfig.PlayerPrefab = null;
            manager.NetworkConfig.AutoSpawnPlayerPrefabClientSide = false;

            if (manager.GetComponent<NetworkPlayerSessionGuard>() == null)
                manager.gameObject.AddComponent<NetworkPlayerSessionGuard>();
        }

        public static void EnforceGameplayOnlySpawn(NetworkManager manager, string gameplayScene = "GamePlay")
        {
            if (manager == null || !manager.IsServer) return;
            if (IsGameplayScene(SceneManager.GetActiveScene().name, gameplayScene)) return;

            foreach (ulong clientId in manager.ConnectedClientsIds)
                DespawnPlayerObject(manager, clientId);

            foreach (var player in Object.FindObjectsByType<NetworkPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (player == null) continue;
                var netObj = player.NetworkObject;
                if (netObj != null && netObj.IsSpawned)
                    netObj.Despawn(true);
            }
        }

        private void Awake()
        {
            networkManager = GetComponent<NetworkManager>();
            if (networkManager == null)
                networkManager = NetworkManager.Singleton;
        }

        private void OnEnable()
        {
            TrySubscribeCoreEvents();
            TrySubscribeNetworkSceneEvents();
        }

        private void Start()
        {
            TrySubscribeCoreEvents();
            TrySubscribeNetworkSceneEvents();
            ApplyManagerSettings(networkManager);
            EnforceGameplayOnlySpawn(networkManager, gameplaySceneName);
        }

        private void Update()
        {
            TrySubscribeNetworkSceneEvents();
        }

        private void OnDisable()
        {
            if (networkManager != null)
            {
                if (coreEventsSubscribed)
                {
                    networkManager.OnClientConnectedCallback -= HandleClientConnected;
                    networkManager.OnServerStarted -= HandleServerStarted;
                }

                if (networkSceneEventsSubscribed && networkManager.SceneManager != null)
                    networkManager.SceneManager.OnLoadEventCompleted -= HandleNetworkSceneLoaded;
            }

            if (coreEventsSubscribed)
                SceneManager.sceneLoaded -= HandleUnitySceneLoaded;

            coreEventsSubscribed = false;
            networkSceneEventsSubscribed = false;
        }

        private void TrySubscribeCoreEvents()
        {
            if (coreEventsSubscribed) return;

            networkManager ??= GetComponent<NetworkManager>();
            networkManager ??= NetworkManager.Singleton;
            if (networkManager == null) return;

            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnServerStarted += HandleServerStarted;
            SceneManager.sceneLoaded += HandleUnitySceneLoaded;
            coreEventsSubscribed = true;
        }

        private void TrySubscribeNetworkSceneEvents()
        {
            if (networkSceneEventsSubscribed) return;

            networkManager ??= GetComponent<NetworkManager>();
            networkManager ??= NetworkManager.Singleton;
            if (networkManager == null) return;

            var sceneManager = networkManager.SceneManager;
            if (sceneManager == null) return;

            sceneManager.OnLoadEventCompleted += HandleNetworkSceneLoaded;
            networkSceneEventsSubscribed = true;
        }

        private void HandleServerStarted()
        {
            ApplyManagerSettings(networkManager);
            EnforceGameplayOnlySpawn(networkManager, gameplaySceneName);
        }

        private void HandleClientConnected(ulong clientId)
        {
            StartCoroutine(EnforceNextFrame());
        }

        private void HandleNetworkSceneLoaded(
            string sceneName,
            LoadSceneMode loadSceneMode,
            System.Collections.Generic.List<ulong> clientsCompleted,
            System.Collections.Generic.List<ulong> clientsTimedOut)
        {
            if (IsGameplayScene(sceneName, gameplaySceneName)) return;
            EnforceGameplayOnlySpawn(networkManager, gameplaySceneName);
        }

        private void HandleUnitySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsGameplayScene(scene.name, gameplaySceneName)) return;
            EnforceGameplayOnlySpawn(networkManager, gameplaySceneName);
        }

        private IEnumerator EnforceNextFrame()
        {
            yield return null;
            EnforceGameplayOnlySpawn(networkManager, gameplaySceneName);
        }

        private static void DespawnPlayerObject(NetworkManager manager, ulong clientId)
        {
            if (manager == null || !manager.IsServer) return;
            if (!manager.ConnectedClients.TryGetValue(clientId, out var client)) return;
            if (client.PlayerObject == null) return;

            client.PlayerObject.Despawn(true);
        }
    }
}
