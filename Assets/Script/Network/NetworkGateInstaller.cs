using Unity.Collections;
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

        public bool IsGateInstalled => IsSpawned && netInstalled.Value;

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
            if (!NetworkSessionHelper.IsMultiplayerSession)
            {
                installer?.TryInstallFromInteractor(interactor);
                return;
            }

            if (!IsSpawned)
            {
                Debug.LogWarning("[NetworkGateInstaller] NGO 미스폰 — 멀티 설치 요청 무시");
                return;
            }

            if (IsServer)
            {
                installer?.TryInstallFromInteractor(interactor);
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
            ulong clientId = rpcParams.Receive.SenderClientId;

            if (installer == null)
            {
                NotifyInstallResult(clientId, false, "installer_missing");
                return;
            }

            if (IsGateInstalled || (installer != null && installer.IsInstalled))
            {
                NotifyInstallResult(clientId, false, "already_installed");
                return;
            }

            var player = ResolvePlayer(clientId);
            if (player == null)
            {
                NotifyInstallResult(clientId, false, "player_missing");
                return;
            }

            if (!installer.TryInstallFromServer(player, requireEquippedKit: false, out var failReason))
            {
                NotifyInstallResult(clientId, false, failReason);
                return;
            }

            NotifyInstallResult(clientId, true, "ok");
        }

        [ClientRpc]
        private void NotifyInstallResultClientRpc(
            bool success,
            FixedString32Bytes reason,
            ClientRpcParams clientRpcParams = default)
        {
            if (success)
                Debug.Log("[NetworkGateInstaller] 문 설치 성공");
            else
                Debug.LogWarning($"[NetworkGateInstaller] 문 설치 실패: {reason}");
        }

        private void NotifyInstallResult(ulong clientId, bool success, string reason)
        {
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientId }
                }
            };

            NotifyInstallResultClientRpc(success, reason, clientRpcParams);
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
