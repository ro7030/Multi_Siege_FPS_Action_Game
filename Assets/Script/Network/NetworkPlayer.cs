using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using ProjectM.Auth;
using ProjectM.CharacterSelect;
using ProjectM.Economy;
using ProjectM.Player;

namespace ProjectM.Network
{
    /// <summary>
    /// NGO 플레이어 루트. Owner만 입력·카메라·CharacterController 사용.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkPlayer : NetworkBehaviour
    {
        private NetworkVariable<FixedString64Bytes> networkNickname = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public string DisplayName => networkNickname.Value.ToString();

        public override void OnNetworkSpawn()
        {
            NetworkPlayerRegistry.Register(this);

            if (IsServer)
                networkNickname.Value = ResolveNickname(OwnerClientId);

            if (IsOwner)
                SubmitNicknameServerRpc(new FixedString64Bytes(AuthSessionManager.ResolveNickname("Player")));

            ConfigureOwnership();
        }

        public override void OnNetworkDespawn()
        {
            NetworkPlayerRegistry.Unregister(this);
        }

        private void ConfigureOwnership()
        {
            bool local = IsOwner;

            foreach (var pc in GetComponentsInChildren<PlayerController>(true))
                pc.IsLocalPlayer = local;

            foreach (var wc in GetComponentsInChildren<WeaponController>(true))
                wc.IsLocalPlayer = local;

            foreach (var mw in GetComponentsInChildren<MeleeWeapon>(true))
                mw.IsLocalPlayer = local;

            foreach (var ke in GetComponentsInChildren<KitEquipper>(true))
                ke.IsLocalPlayer = local;

            foreach (var te in GetComponentsInChildren<ThrowableEquipper>(true))
                te.IsLocalPlayer = local;

            foreach (var pa in GetComponentsInChildren<PlayerArsenal>(true))
                pa.IsLocalPlayer = local;

            foreach (var pi in GetComponentsInChildren<PlayerInteractor>(true))
                pi.IsLocalPlayer = local;

            foreach (var cam in GetComponentsInChildren<Camera>(true))
                cam.enabled = local;

            foreach (var listener in GetComponentsInChildren<AudioListener>(true))
                listener.enabled = local;

            if (TryGetComponent<CharacterController>(out var cc))
                cc.enabled = local;

            if (local)
                gameObject.tag = "Player";
        }

        [ServerRpc]
        private void SubmitNicknameServerRpc(FixedString64Bytes nickname)
        {
            if (!nickname.IsEmpty)
                networkNickname.Value = nickname;
            else if (networkNickname.Value.IsEmpty)
                networkNickname.Value = ResolveNickname(OwnerClientId);
        }

        private static FixedString64Bytes ResolveNickname(ulong clientId)
        {
            if (CharacterLobbyNetwork.Instance != null)
            {
                string lobbyName = CharacterLobbyNetwork.Instance.GetNicknameForClient(clientId);
                if (!string.IsNullOrWhiteSpace(lobbyName))
                    return new FixedString64Bytes(lobbyName.Trim());
            }

            return new FixedString64Bytes($"Player{clientId}");
        }

        [ServerRpc]
        public void RequestShopPurchaseServerRpc(FixedString64Bytes itemId)
        {
            var shop = Object.FindAnyObjectByType<ShopController>();
            shop?.TryPurchaseForPlayer(gameObject, itemId.ToString());
        }

        [ServerRpc]
        public void RequestWeaponUpgradeServerRpc(int slot)
        {
            var shop = Object.FindAnyObjectByType<ShopController>();
            if (shop == null) return;

            var arsenal = GetComponent<PlayerArsenal>();
            if (arsenal == null) return;

            int next = arsenal.CurrentTierIndex((WeaponSlot)slot) + 1;
            shop.TryPurchaseWeaponTierForPlayer(gameObject, (WeaponSlot)slot, next);
        }

        [ServerRpc]
        public void RequestWeaponTierPurchaseServerRpc(int slot, int tierIndex)
        {
            var shop = Object.FindAnyObjectByType<ShopController>();
            shop?.TryPurchaseWeaponTierForPlayer(gameObject, (WeaponSlot)slot, tierIndex);
        }
    }
}
