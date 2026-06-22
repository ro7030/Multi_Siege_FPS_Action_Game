using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using ProjectM.Economy;
using ProjectM.Player;

namespace ProjectM.Network
{
    /// <summary>
    /// 클라이언트 밭 설치 요청을 서버 FarmManager 로 전달한다.
    /// </summary>
    public class NetworkFarmManagerBridge : NetworkBehaviour
    {
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

        [ClientRpc]
        private void NotifyPlaceFarmResultClientRpc(
            bool success,
            FixedString32Bytes reason,
            ClientRpcParams clientRpcParams = default)
        {
            if (success)
                Debug.Log("[NetworkFarmManagerBridge] 밭 설치 성공");
            else
                Debug.LogWarning($"[NetworkFarmManagerBridge] 밭 설치 실패: {reason}");
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
