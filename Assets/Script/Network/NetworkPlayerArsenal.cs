using Unity.Netcode;
using UnityEngine;
using ProjectM.Player;

namespace ProjectM.Network
{
    /// <summary>
    /// 플레이어 무기 티어를 서버 권한으로 NGO 동기화한다.
    /// </summary>
    [RequireComponent(typeof(PlayerArsenal))]
    public class NetworkPlayerArsenal : NetworkBehaviour
    {
        private readonly NetworkVariable<int> netPrimaryTier = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> netSecondaryTier = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private PlayerArsenal arsenal;

        private void Awake() => arsenal = GetComponent<PlayerArsenal>();

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                PushServerSnapshot();
            else
            {
                netPrimaryTier.OnValueChanged += HandlePrimaryChanged;
                netSecondaryTier.OnValueChanged += HandleSecondaryChanged;
                arsenal.ApplyNetworkTier(WeaponSlot.Primary, netPrimaryTier.Value);
                arsenal.ApplyNetworkTier(WeaponSlot.Secondary, netSecondaryTier.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer)
            {
                netPrimaryTier.OnValueChanged -= HandlePrimaryChanged;
                netSecondaryTier.OnValueChanged -= HandleSecondaryChanged;
            }
        }

        public bool ServerSetTier(WeaponSlot slot, int tierIndex)
        {
            if (!IsServer || arsenal == null)
                return false;

            if (arsenal.Progression == null || arsenal.Progression.GetTier(slot, tierIndex) == null)
                return false;

            if (!IsSpawned)
                return arsenal.ApplyNetworkTier(slot, tierIndex);

            switch (slot)
            {
                case WeaponSlot.Primary:
                    netPrimaryTier.Value = tierIndex;
                    break;
                case WeaponSlot.Secondary:
                    netSecondaryTier.Value = tierIndex;
                    break;
                default:
                    return false;
            }

            arsenal.ApplyNetworkTier(slot, tierIndex);
            return true;
        }

        private void PushServerSnapshot()
        {
            netPrimaryTier.Value = arsenal.PrimaryTierIndex;
            netSecondaryTier.Value = arsenal.SecondaryTierIndex;
            arsenal.ApplyNetworkTier(WeaponSlot.Primary, netPrimaryTier.Value);
            arsenal.ApplyNetworkTier(WeaponSlot.Secondary, netSecondaryTier.Value);
        }

        private void HandlePrimaryChanged(int _, int value) =>
            arsenal.ApplyNetworkTier(WeaponSlot.Primary, value);

        private void HandleSecondaryChanged(int _, int value) =>
            arsenal.ApplyNetworkTier(WeaponSlot.Secondary, value);
    }
}
