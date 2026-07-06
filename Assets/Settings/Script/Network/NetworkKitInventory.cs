using Unity.Netcode;
using UnityEngine;
using ProjectM.Player;

namespace ProjectM.Network
{
    // 플레이어 키트 보유량을 서버 권한으로 NGO 동기화한다.
    [RequireComponent(typeof(KitInventory))]
    public class NetworkKitInventory : NetworkBehaviour
    {
        private readonly NetworkVariable<int> netHealKit = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> netRepairKit = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> netFarmKit = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> netHealTier = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private KitInventory inventory;
        private KitEquipper kitEquipper;

        private void Awake()
        {
            inventory = GetComponent<KitInventory>();
            kitEquipper = GetComponent<KitEquipper>();
        }

        public override void OnNetworkSpawn()
        {
            inventory.ApplyStartingCounts();

            if (IsServer)
                PushServerSnapshot();
            else
            {
                netHealKit.OnValueChanged += HandleHealChanged;
                netRepairKit.OnValueChanged += HandleRepairChanged;
                netFarmKit.OnValueChanged += HandleFarmChanged;
                netHealTier.OnValueChanged += HandleHealTierChanged;
                inventory.NotifyAllCounts(
                    netHealKit.Value,
                    netRepairKit.Value,
                    netFarmKit.Value);
                kitEquipper?.ApplyNetworkHealTier(netHealTier.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer)
            {
                netHealKit.OnValueChanged -= HandleHealChanged;
                netRepairKit.OnValueChanged -= HandleRepairChanged;
                netFarmKit.OnValueChanged -= HandleFarmChanged;
                netHealTier.OnValueChanged -= HandleHealTierChanged;
            }
        }

        public void ServerAdd(KitType type, int count = 1)
        {
            if (!IsServer || type == KitType.None || count <= 0)
                return;

            if (!IsSpawned)
            {
                inventory.AddLocal(type, count);
                return;
            }

            switch (type)
            {
                case KitType.HealKit:
                    netHealKit.Value += count;
                    inventory.NotifyCount(KitType.HealKit, netHealKit.Value);
                    break;
                case KitType.RepairKit:
                    netRepairKit.Value += count;
                    inventory.NotifyCount(KitType.RepairKit, netRepairKit.Value);
                    break;
                case KitType.FarmKit:
                    netFarmKit.Value += count;
                    inventory.NotifyCount(KitType.FarmKit, netFarmKit.Value);
                    break;
            }
        }

        public bool ServerTryConsume(KitType type)
        {
            if (!IsServer || type == KitType.None)
                return false;

            inventory.ApplyStartingCounts();

            if (!IsSpawned)
                return inventory.TryConsumeLocal(type);

            ReconcileNetFromLocal(type);

            int current = GetNetCount(type);
            if (current <= 0)
            {
                Debug.LogWarning(
                    $"[NetworkKitInventory] 소모 실패 {type}: net={current}, local={inventory.GetCount(type)}");
                return false;
            }

            switch (type)
            {
                case KitType.HealKit:
                    netHealKit.Value--;
                    inventory.NotifyCount(KitType.HealKit, netHealKit.Value);
                    return true;
                case KitType.RepairKit:
                    netRepairKit.Value--;
                    inventory.NotifyCount(KitType.RepairKit, netRepairKit.Value);
                    return true;
                case KitType.FarmKit:
                    netFarmKit.Value--;
                    inventory.NotifyCount(KitType.FarmKit, netFarmKit.Value);
                    return true;
                default:
                    return false;
            }
        }

        public bool ServerSetHealTier(int tierIndex)
        {
            if (!IsServer || kitEquipper == null)
                return false;

            if (kitEquipper.HealProgression == null
                || kitEquipper.HealProgression.GetTier(tierIndex) == null)
                return false;

            if (!IsSpawned)
                return kitEquipper.ApplyNetworkHealTier(tierIndex);

            netHealTier.Value = tierIndex;
            kitEquipper.ApplyNetworkHealTier(tierIndex);
            return true;
        }

        private void PushServerSnapshot()
        {
            netHealKit.Value = inventory.HealKitCount;
            netRepairKit.Value = inventory.RepairKitCount;
            netFarmKit.Value = inventory.FarmKitCount;
            inventory.NotifyAllCounts(
                netHealKit.Value,
                netRepairKit.Value,
                netFarmKit.Value);

            int tier = kitEquipper != null ? kitEquipper.CurrentHealTier : 0;
            netHealTier.Value = tier;
            kitEquipper?.ApplyNetworkHealTier(netHealTier.Value);
        }

        // 서버 로컬 인벤이 net보다 앞서 있으면 net을 맞춘다 (스폰 직후 desync 방지).
        private void ReconcileNetFromLocal(KitType type)
        {
            int local = inventory.GetCount(type);
            int net = GetNetCount(type);
            if (local <= net)
                return;

            switch (type)
            {
                case KitType.HealKit:   netHealKit.Value = local; break;
                case KitType.RepairKit: netRepairKit.Value = local; break;
                case KitType.FarmKit:   netFarmKit.Value = local; break;
            }

            inventory.NotifyCount(type, local);
        }

        private int GetNetCount(KitType type)
        {
            return type switch
            {
                KitType.HealKit => netHealKit.Value,
                KitType.RepairKit => netRepairKit.Value,
                KitType.FarmKit => netFarmKit.Value,
                _ => 0
            };
        }

        private float ResolveServerHealAmount()
        {
            if (kitEquipper == null)
                return 50f;

            var def = kitEquipper.HealProgression != null
                ? kitEquipper.HealProgression.GetTier(netHealTier.Value)
                : null;

            return def != null ? def.healAmount : 50f;
        }

        private void HandleHealChanged(int _, int value) =>
            inventory.NotifyCount(KitType.HealKit, value);

        private void HandleRepairChanged(int _, int value) =>
            inventory.NotifyCount(KitType.RepairKit, value);

        private void HandleFarmChanged(int _, int value) =>
            inventory.NotifyCount(KitType.FarmKit, value);

        private void HandleHealTierChanged(int _, int value) =>
            kitEquipper?.ApplyNetworkHealTier(value);

        // Owner 클라이언트 힐킷 사용 요청.
        public void RequestUseHealKitFromOwner(float amount)
        {
            if (!IsOwner)
                return;

            RequestUseHealKitServerRpc(amount);
        }

        [ServerRpc]
        private void RequestUseHealKitServerRpc(float amount)
        {
            if (!ServerTryConsume(KitType.HealKit))
                return;

            var health = GetComponent<HealthSystem>();
            if (health == null || !health.IsAlive)
            {
                ServerAdd(KitType.HealKit, 1);
                return;
            }

            float healAmount = ResolveServerHealAmount();
            health.ApplyHealLocal(healAmount);
        }
    }
}
