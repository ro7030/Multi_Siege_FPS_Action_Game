using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using ProjectM.Audio;
using ProjectM.Core;
using ProjectM.Economy;
using ProjectM.Player;
using ProjectM.UI;

namespace ProjectM.Network
{
    /// <summary>
    /// 클라이언트 밭 설치 요청을 서버 FarmManager 로 전달하고,
    /// 팀 활성 밭 개수를 NetworkVariable 로 복제한다.
    /// </summary>
    public class NetworkFarmManagerBridge : NetworkBehaviour
    {
        public static NetworkFarmManagerBridge Instance { get; private set; }

        private const float FailureBannerDuration = 2.5f;

        [SerializeField] private GameSessionManager session;

        private readonly NetworkVariable<int> netActiveFarmCount = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public int SyncedActiveCount => netActiveFarmCount.Value;

        private void Awake()
        {
            if (session == null)
                session = FindAnyObjectByType<GameSessionManager>();
        }

        public override void OnNetworkSpawn()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning("[NetworkFarmManagerBridge] Duplicate instance detected.");
            else
                Instance = this;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>서버 FarmManager 가 활성 밭 수 변경 시 호출.</summary>
        public void ServerSyncActiveFarmCount(int count)
        {
            if (!IsServer)
                return;

            netActiveFarmCount.Value = count;
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestPlaceFarmServerRpc(Vector3 position, float rotationY, ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            var manager = FarmManager.Instance;
            if (manager == null)
            {
                NotifyPlaceFarmResult(clientId, false, "no_manager");
                return;
            }

            if (!manager.IsPlacementAllowed())
            {
                string reason = !manager.CanPlaceMore ? "max_farms" : "wrong_phase";
                NotifyPlaceFarmResult(clientId, false, reason);
                return;
            }

            var player = ResolvePlayer(clientId);
            if (player == null)
            {
                NotifyPlaceFarmResult(clientId, false, "player_missing");
                return;
            }

            var inventory = player.GetComponent<KitInventory>();
            if (inventory == null || !inventory.TryConsume(KitType.FarmKit))
            {
                NotifyPlaceFarmResult(clientId, false, "consume_failed");
                return;
            }

            var rotation = Quaternion.Euler(0f, rotationY, 0f);
            if (!manager.PlaceFarmInternal(position, rotation, out _))
            {
                inventory.Add(KitType.FarmKit, 1);
                NotifyPlaceFarmResult(clientId, false, "place_failed");
                return;
            }

            NotifyPlaceFarmResult(clientId, true, "ok");
        }

        public void BroadcastFarmPlacedSfx(Vector3 position)
        {
            if (!IsServer || !IsSpawned)
                return;

            PlayFarmPlacedClientRpc(position);
        }

        [ClientRpc]
        private void PlayFarmPlacedClientRpc(Vector3 position)
        {
            GameSoundManager.EnsureInstance().PlayDefenseAtPoint(DefenseSfxType.FarmPlace, position);
        }

        [ClientRpc]
        private void NotifyPlaceFarmResultClientRpc(
            bool success,
            FixedString32Bytes reason,
            ClientRpcParams clientRpcParams = default)
        {
            if (success)
            {
                FarmManager.Instance?.CleanupStaleFarmEntries();
                Debug.Log("[NetworkFarmManagerBridge] 밭 설치 성공");
                return;
            }

            string msg = ResolveFailureMessage(reason.ToString());
            Debug.LogWarning($"[NetworkFarmManagerBridge] 밭 설치 실패: {msg}");
            NotificationBanner.Instance?.Show(msg, FailureBannerDuration);
        }

        private void NotifyPlaceFarmResult(ulong clientId, bool success, string reason)
        {
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientId }
                }
            };

            NotifyPlaceFarmResultClientRpc(success, reason, clientRpcParams);
        }

        private static string ResolveFailureMessage(string reasonCode)
        {
            var manager = FarmManager.Instance;
            return reasonCode switch
            {
                "max_farms" => manager != null
                    ? manager.GetPlacementBlockMessage()
                    : "밭은 최대 4개까지 설치할 수 있습니다.",
                "wrong_phase" => "정비 시간에만 밭을 설치할 수 있습니다.",
                "consume_failed" => "밭 설치 키트가 없습니다.",
                "place_failed" => manager != null
                    ? manager.GetPlacementBlockMessage()
                    : "밭을 설치할 수 없습니다.",
                "no_manager" => "밭 시스템을 사용할 수 없습니다.",
                "player_missing" => "플레이어 정보를 찾을 수 없습니다.",
                _ => manager != null
                    ? manager.GetPlacementBlockMessage()
                    : "밭을 설치할 수 없습니다."
            };
        }

        private static GameObject ResolvePlayer(ulong clientId)
        {
            if (NetworkManager.Singleton == null)
                return null;

            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                return null;

            return client.PlayerObject != null ? client.PlayerObject.gameObject : null;
        }
    }
}
