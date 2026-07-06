using Unity.Netcode;
using UnityEngine;
using ProjectM.Defense;
using ProjectM.Player;

namespace ProjectM.Network
{
    // Gate Body NGO: 서버 사망 시 슬롯 설치 해제, 재설치 시 HP·NetworkObject·NavMesh 복구.
    [RequireComponent(typeof(HealthSystem))]
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkGateBodyBridge : NetworkBehaviour
    {
        private HealthSystem health;
        private NetworkGateInstaller slotInstaller;
        private GateController gateController;

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
            gateController = GetComponent<GateController>();
            slotInstaller = GetComponentInParent<NetworkGateInstaller>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && health != null)
                health.OnDied += HandleServerDied;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && health != null)
                health.OnDied -= HandleServerDied;
        }

        // 서버: 재설치 전 Body HP·NetworkObject·NavMesh를 복구한다.
        public void ServerPrepareForInstall()
        {
            if (!NetworkSessionHelper.IsServer || health == null)
                return;

            health.ResetHp();

            var netObj = NetworkObject;
            if (netObj != null && !netObj.IsSpawned)
                netObj.Spawn();

            gateController?.RestoreAliveState();

            if (TryGetComponent<NetworkDamageBridge>(out var bridge))
                bridge.ServerSyncHealthNow();
        }

        private void HandleServerDied(GameObject _)
        {
            slotInstaller?.ServerSetInstalled(false);
        }
    }
}
