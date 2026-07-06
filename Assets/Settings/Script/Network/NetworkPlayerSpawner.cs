using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectM.Network
{
    // GamePlay 씬에서만 NetworkPlayer를 스폰한다. 오프라인은 offlineScenePlayer 사용.
    public class NetworkPlayerSpawner : NetworkBehaviour
    {
        [SerializeField] private string gameplaySceneName = "GamePlay";
        [SerializeField] private GameObject networkPlayerPrefab;
        [SerializeField] private Transform[] playerSpawnPoints;
        [SerializeField] private GameObject offlineScenePlayer;

        private bool hasSpawnedForSession;

        private void Start()
        {
            if (NetworkSessionHelper.IsMultiplayerSession) return;
            EnableOfflinePlayer();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            if (!IsGameplayScene()) return;

            StartCoroutine(SpawnPlayersNextFrame());
        }

        public override void OnNetworkDespawn()
        {
            hasSpawnedForSession = false;
        }

        private IEnumerator SpawnPlayersNextFrame()
        {
            yield return null;

            if (!IsGameplayScene()) yield break;
            if (hasSpawnedForSession) yield break;

            if (offlineScenePlayer != null)
                offlineScenePlayer.SetActive(false);

            if (networkPlayerPrefab == null)
            {
                Debug.LogError("[NetworkPlayerSpawner] networkPlayerPrefab 미할당");
                yield break;
            }

            if (playerSpawnPoints == null || playerSpawnPoints.Length == 0)
            {
                Debug.LogError("[NetworkPlayerSpawner] playerSpawnPoints 미설정");
                yield break;
            }

            int index = 0;
            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                DespawnExistingPlayer(clientId);

                Transform sp = playerSpawnPoints[index % playerSpawnPoints.Length];
                index++;

                var go = Instantiate(networkPlayerPrefab, sp.position, sp.rotation);
                var netObj = go.GetComponent<NetworkObject>();
                if (netObj == null)
                {
                    Debug.LogError("[NetworkPlayerSpawner] prefab에 NetworkObject 없음");
                    Destroy(go);
                    continue;
                }

                netObj.SpawnAsPlayerObject(clientId, true);
                Debug.Log($"[NetworkPlayerSpawner] client {clientId} @ {sp.name} ({sp.position})");
            }

            hasSpawnedForSession = true;
        }

        private void EnableOfflinePlayer()
        {
            if (offlineScenePlayer == null)
            {
                offlineScenePlayer = GameObject.FindGameObjectWithTag("Player");
                if (offlineScenePlayer != null && offlineScenePlayer.GetComponent<NetworkObject>() != null)
                    offlineScenePlayer = null;
            }

            if (offlineScenePlayer == null) return;

            Transform sp = playerSpawnPoints != null && playerSpawnPoints.Length > 0
                ? playerSpawnPoints[0]
                : null;

            if (sp != null)
            {
                offlineScenePlayer.transform.SetPositionAndRotation(sp.position, sp.rotation);
            }

            offlineScenePlayer.SetActive(true);
            ApplyOfflineCharacterVisual(offlineScenePlayer);
        }

        private static void ApplyOfflineCharacterVisual(GameObject player)
        {
            if (player == null) return;

            var binder = player.GetComponentInChildren<CharacterVisualBinder>(true);
            if (binder == null) return;

            int index = MatchLoadoutContext.GetOfflineCharacterIndex();
            binder.ApplyCharacter(index);

            if (binder.CurrentVisual != null)
            {
                LocalFirstPersonVisual.ApplyOwnerVisual(binder.CurrentVisual, true);
                foreach (var cam in player.GetComponentsInChildren<Camera>(true))
                    LocalFirstPersonVisual.ConfigureLocalCamera(cam, true);
            }
        }

        private bool IsGameplayScene()
        {
            return SceneManager.GetActiveScene().name == gameplaySceneName;
        }

        private static void DespawnExistingPlayer(ulong clientId)
        {
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return;
            if (client.PlayerObject == null) return;
            client.PlayerObject.Despawn(true);
        }
    }
}
