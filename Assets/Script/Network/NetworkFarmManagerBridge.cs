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
            var manager = FarmManager.Instance;
            if (manager == null)
                return;

            var player = ResolvePlayer(rpcParams.Receive.SenderClientId);
            if (player == null)
                return;

            var inventory = player.GetComponent<KitInventory>();
            if (inventory == null || !inventory.TryConsume(KitType.FarmKit))
                return;

            var rotation = Quaternion.Euler(0f, rotationY, 0f);
            manager.PlaceFarmInternal(position, rotation, out _);
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
