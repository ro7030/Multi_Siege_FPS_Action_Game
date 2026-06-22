using Unity.Netcode;
using UnityEngine;
using ProjectM.Player;

namespace ProjectM.Network
{
    /// <summary>
    /// 플레이어 투척무기 보유량을 서버 권한으로 NGO 동기화한다.
    /// </summary>
    [RequireComponent(typeof(ThrowableInventory))]
    public class NetworkThrowableInventory : NetworkBehaviour
    {
        private const float MaxOriginOffset = 2.5f;
        private const float MaxThrowSpeed = 40f;

        private readonly NetworkVariable<int> netGrenade = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> netMolotov = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> netFlash = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private ThrowableInventory inventory;
        private ThrowableEquipper equipper;

        private void Awake()
        {
            inventory = GetComponent<ThrowableInventory>();
            equipper = GetComponent<ThrowableEquipper>();
        }

        public override void OnNetworkSpawn()
        {
            inventory.ApplyStartingCounts();

            if (IsServer)
                PushServerSnapshot();
            else
            {
                netGrenade.OnValueChanged += HandleGrenadeChanged;
                netMolotov.OnValueChanged += HandleMolotovChanged;
                netFlash.OnValueChanged += HandleFlashChanged;
                inventory.NotifyAllCounts(
                    netGrenade.Value,
                    netMolotov.Value,
                    netFlash.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer)
            {
                netGrenade.OnValueChanged -= HandleGrenadeChanged;
                netMolotov.OnValueChanged -= HandleMolotovChanged;
                netFlash.OnValueChanged -= HandleFlashChanged;
            }
        }

        public void ServerAdd(ThrowableType type, int count = 1)
        {
            if (!IsServer || type == ThrowableType.None || count <= 0)
                return;

            if (!IsSpawned)
            {
                inventory.AddLocal(type, count);
                return;
            }

            switch (type)
            {
                case ThrowableType.Grenade:
                    netGrenade.Value += count;
                    inventory.NotifyCount(ThrowableType.Grenade, netGrenade.Value);
                    break;
                case ThrowableType.Molotov:
                    netMolotov.Value += count;
                    inventory.NotifyCount(ThrowableType.Molotov, netMolotov.Value);
                    break;
                case ThrowableType.Flash:
                    netFlash.Value += count;
                    inventory.NotifyCount(ThrowableType.Flash, netFlash.Value);
                    break;
            }
        }

        public bool ServerTryConsume(ThrowableType type)
        {
            if (!IsServer || type == ThrowableType.None)
                return false;

            inventory.ApplyStartingCounts();

            if (!IsSpawned)
                return inventory.TryConsumeLocal(type);

            ReconcileNetFromLocal(type);

            int current = GetNetCount(type);
            if (current <= 0)
            {
                Debug.LogWarning(
                    $"[NetworkThrowableInventory] 소모 실패 {type}: net={current}, local={inventory.GetCount(type)}");
                return false;
            }

            switch (type)
            {
                case ThrowableType.Grenade:
                    netGrenade.Value--;
                    inventory.NotifyCount(ThrowableType.Grenade, netGrenade.Value);
                    return true;
                case ThrowableType.Molotov:
                    netMolotov.Value--;
                    inventory.NotifyCount(ThrowableType.Molotov, netMolotov.Value);
                    return true;
                case ThrowableType.Flash:
                    netFlash.Value--;
                    inventory.NotifyCount(ThrowableType.Flash, netFlash.Value);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Owner 클라이언트 투척 요청 (Guest).</summary>
        public void RequestThrowFromOwner(ThrowableType type, Vector3 origin, Vector3 velocity)
        {
            if (!IsOwner || type == ThrowableType.None)
                return;

            RequestThrowServerRpc((int)type, origin, velocity);
        }

        [ServerRpc]
        private void RequestThrowServerRpc(int typeInt, Vector3 origin, Vector3 velocity)
        {
            var type = (ThrowableType)typeInt;
            if (type == ThrowableType.None || equipper == null)
                return;

            if (!ServerTryConsume(type))
                return;

            var def = equipper.GetDefinition(type);
            if (def == null)
            {
                ServerAdd(type, 1);
                return;
            }

            if (!TryValidateThrow(origin, velocity, out var safeOrigin, out var safeVelocity))
            {
                ServerAdd(type, 1);
                return;
            }

            ThrowableSpawner.SpawnProjectile(def, gameObject, safeOrigin, safeVelocity);
            Debug.Log($"[NetworkThrowableInventory] {def.displayName} 투척 (서버)");
        }

        private bool TryValidateThrow(
            Vector3 origin,
            Vector3 velocity,
            out Vector3 safeOrigin,
            out Vector3 safeVelocity)
        {
            safeOrigin = origin;
            safeVelocity = velocity;

            var cam = equipper != null ? equipper.ViewCamera : null;
            if (cam == null)
                cam = GetComponentInChildren<Camera>();

            if (cam != null)
            {
                Vector3 expected = cam.transform.position + cam.transform.forward * equipper.SpawnForward;
                if (Vector3.Distance(origin, expected) > MaxOriginOffset)
                    safeOrigin = expected;
            }
            else if (Vector3.Distance(origin, transform.position) > MaxOriginOffset * 2f)
            {
                return false;
            }

            if (safeVelocity.sqrMagnitude > MaxThrowSpeed * MaxThrowSpeed)
                safeVelocity = safeVelocity.normalized * MaxThrowSpeed;

            return true;
        }

        private void PushServerSnapshot()
        {
            netGrenade.Value = inventory.GrenadeCount;
            netMolotov.Value = inventory.MolotovCount;
            netFlash.Value = inventory.FlashCount;
            inventory.NotifyAllCounts(
                netGrenade.Value,
                netMolotov.Value,
                netFlash.Value);
        }

        private void ReconcileNetFromLocal(ThrowableType type)
        {
            int local = inventory.GetCount(type);
            int net = GetNetCount(type);
            if (local <= net)
                return;

            switch (type)
            {
                case ThrowableType.Grenade: netGrenade.Value = local; break;
                case ThrowableType.Molotov: netMolotov.Value = local; break;
                case ThrowableType.Flash:   netFlash.Value = local; break;
            }

            inventory.NotifyCount(type, local);
        }

        private int GetNetCount(ThrowableType type)
        {
            return type switch
            {
                ThrowableType.Grenade => netGrenade.Value,
                ThrowableType.Molotov => netMolotov.Value,
                ThrowableType.Flash   => netFlash.Value,
                _ => 0
            };
        }

        private void HandleGrenadeChanged(int _, int value) =>
            inventory.NotifyCount(ThrowableType.Grenade, value);

        private void HandleMolotovChanged(int _, int value) =>
            inventory.NotifyCount(ThrowableType.Molotov, value);

        private void HandleFlashChanged(int _, int value) =>
            inventory.NotifyCount(ThrowableType.Flash, value);
    }
}
