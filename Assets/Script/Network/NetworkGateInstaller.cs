using Unity.Netcode;
using UnityEngine;
using ProjectM.Defense;
using ProjectM.Player;

namespace ProjectM.Network
{
    /// <summary>
    /// 게이트 설치 상태를 NGO로 동기화한다.
    /// </summary>
    [RequireComponent(typeof(GateInstaller))]
    public class NetworkGateInstaller : NetworkBehaviour
    {
        private readonly NetworkVariable<bool> netInstalled = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private GateInstaller installer;

        private void Awake() => installer = GetComponent<GateInstaller>();

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                netInstalled.Value = installer != null && installer.IsInstalled;

            netInstalled.OnValueChanged += HandleInstalledChanged;
            ApplyInstalled(netInstalled.Value);
        }

        public override void OnNetworkDespawn()
        {
            netInstalled.OnValueChanged -= HandleInstalledChanged;
        }

        public void RequestInstall(GameObject interactor)
        {
            if (!NetworkSessionHelper.IsMultiplayerSession || !IsSpawned)
            {
                installer?.TryInstallFromInteractor(interactor);
                return;
            }

            if (IsServer)
            {
                if (installer != null && installer.TryInstallFromInteractor(interactor))
                    ServerSetInstalled(true);
                return;
            }

            RequestInstallServerRpc();
        }

        public void ServerSetInstalled(bool installed)
        {
            if (!IsServer)
                return;

            netInstalled.Value = installed;
            ApplyInstalled(installed);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestInstallServerRpc(ServerRpcParams rpcParams = default)
        {
            var player = ResolvePlayer(rpcParams.Receive.SenderClientId);
            if (installer == null || player == null)
                return;

            if (!installer.TryInstallFromInteractor(player))
                return;

            ServerSetInstalled(true);
        }

        private void HandleInstalledChanged(bool _, bool installed) => ApplyInstalled(installed);

        private void ApplyInstalled(bool installed)
        {
            installer?.ApplyNetworkInstalled(installed);
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
