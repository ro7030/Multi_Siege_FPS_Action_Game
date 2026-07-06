using Unity.Netcode;
using UnityEngine;
using ProjectM.Audio;
using ProjectM.Defense;
using ProjectM.Economy;

namespace ProjectM.Network
{
    /// <summary>
    /// 밭 누적 수확량·파괴 상태를 NGO로 복제한다.
    /// </summary>
    [RequireComponent(typeof(FarmPlot))]
    public class NetworkFarmBridge : NetworkBehaviour
    {
        private const byte StateActive = 0;
        private const byte StateDestroyed = 1;

        private readonly NetworkVariable<int> netAccumulatedYield = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<byte> netState = new(
            StateActive,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private FarmPlot plot;

        private void Awake() => plot = GetComponent<FarmPlot>();

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                ServerSyncFromPlot();
            }
            else
            {
                netAccumulatedYield.OnValueChanged += HandleYieldChanged;
                netState.OnValueChanged += HandleStateChanged;
                ApplyClientMirror();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer)
            {
                netAccumulatedYield.OnValueChanged -= HandleYieldChanged;
                netState.OnValueChanged -= HandleStateChanged;

                // destroyOnDeath Despawn이 netState=Destroyed보다 먼저 올 수 있어 State 무관 unregister.
                if (plot != null)
                {
                    plot.ApplyDestroyedPresentation();
                    FarmManager.Instance?.NotifyFarmDestroyedFromMirror(plot);
                }
            }
        }

        public void RequestHarvest()
        {
            if (!NetworkSessionHelper.IsMultiplayerSession || !IsSpawned)
            {
                FarmManager.Instance?.HarvestFarm(plot);
                return;
            }

            if (IsServer)
            {
                ulong clientId = NetworkManager.Singleton != null
                    ? NetworkManager.Singleton.LocalClientId
                    : ulong.MaxValue;
                FarmManager.Instance?.HarvestFarm(plot, clientId);
                return;
            }

            RequestHarvestServerRpc();
        }

        public void ServerSyncFromPlot()
        {
            if (!IsServer || plot == null)
                return;

            netAccumulatedYield.Value = plot.AccumulatedYield;
            netState.Value = plot.State == FarmPlot.FarmState.Destroyed ? StateDestroyed : StateActive;
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestHarvestServerRpc(ServerRpcParams rpcParams = default)
        {
            FarmManager.Instance?.HarvestFarm(plot, rpcParams.Receive.SenderClientId);
        }

        public void BroadcastHarvestSfx(Vector3 position)
        {
            if (!IsServer || !IsSpawned)
                return;

            PlayFarmHarvestedClientRpc(position);
        }

        [ClientRpc]
        private void PlayFarmHarvestedClientRpc(Vector3 position)
        {
            GameSoundManager.EnsureInstance().PlayDefenseAtPoint(DefenseSfxType.FarmHarvest, position);
        }

        private void HandleYieldChanged(int _, int __) => ApplyClientMirror();
        private void HandleStateChanged(byte _, byte __) => ApplyClientMirror();

        private void ApplyClientMirror()
        {
            if (IsServer || plot == null)
                return;

            var state = netState.Value == StateDestroyed
                ? FarmPlot.FarmState.Destroyed
                : FarmPlot.FarmState.Active;
            plot.ApplyNetworkMirror(netAccumulatedYield.Value, state);
        }
    }
}
