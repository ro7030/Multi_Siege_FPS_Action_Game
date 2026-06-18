using Unity.Netcode;
using UnityEngine;
using ProjectM.Player;

namespace ProjectM.Network
{
    /// <summary>
    /// NGO 적 데미지를 서버에서만 적용한다. 클라이언트 TakeDamage 요청은 ServerRpc로 전달.
    /// </summary>
    [RequireComponent(typeof(HealthSystem))]
    public class NetworkDamageBridge : NetworkBehaviour
    {
        /// <summary>
        /// 클라이언트면 ServerRpc로 위임하고 true. 서버/오프라인이면 false (로컬 적용).
        /// </summary>
        public bool TryRequestServerDamage(float amount, GameObject attacker)
        {
            if (!NetworkSessionHelper.IsMultiplayerSession || !IsSpawned)
                return false;

            if (IsServer)
                return false;

            RequestDamageServerRpc(amount);
            return true;
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestDamageServerRpc(float amount, ServerRpcParams rpcParams = default)
        {
            var health = GetComponent<HealthSystem>();
            if (health == null || !health.IsAlive || amount <= 0f)
                return;

            GameObject attacker = ResolveAttacker(rpcParams.Receive.SenderClientId);
            health.ApplyDamageLocal(amount, attacker);
        }

        private static GameObject ResolveAttacker(ulong clientId)
        {
            if (NetworkManager.Singleton == null)
                return null;

            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                return null;

            return client.PlayerObject != null ? client.PlayerObject.gameObject : null;
        }
    }
}
