using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using ProjectM.Auth;
using ProjectM.CharacterSelect;
using ProjectM.Economy;
using ProjectM.Player;
using ProjectM.UI;

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

        private NetworkVariable<FixedString64Bytes> networkAuthPlayerId = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private NetworkVariable<int> networkCharacterIndex = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private CharacterVisualBinder cachedBinder;

        public string DisplayName => networkNickname.Value.ToString();
        public string AuthPlayerId => networkAuthPlayerId.Value.ToString();
        public int CharacterIndex => networkCharacterIndex.Value;

        public override void OnNetworkSpawn()
        {
            NetworkPlayerRegistry.Register(this);

            cachedBinder = GetComponentInChildren<CharacterVisualBinder>(true);
            if (cachedBinder != null)
                cachedBinder.OnVisualApplied += HandleVisualApplied;

            if (IsServer)
            {
                networkNickname.Value = ResolveNickname(OwnerClientId);
                networkCharacterIndex.Value = ResolveCharacterIndex(OwnerClientId);
            }

            networkCharacterIndex.OnValueChanged += HandleCharacterIndexChanged;
            ApplyCharacterVisual(networkCharacterIndex.Value);

            if (IsOwner)
            {
                SubmitNicknameServerRpc(new FixedString64Bytes(AuthSessionManager.ResolveNickname("Player")));
                string authId = AuthSessionManager.Instance != null
                    ? AuthSessionManager.Instance.PlayerId
                    : string.Empty;
                if (!string.IsNullOrEmpty(authId))
                    SubmitAuthPlayerIdServerRpc(new FixedString64Bytes(authId));
            }

            ConfigureOwnership();
        }

        public override void OnNetworkDespawn()
        {
            networkCharacterIndex.OnValueChanged -= HandleCharacterIndexChanged;
            if (cachedBinder != null)
                cachedBinder.OnVisualApplied -= HandleVisualApplied;
            NetworkPlayerRegistry.Unregister(this);
        }

        private void HandleCharacterIndexChanged(int previous, int current)
        {
            ApplyCharacterVisual(current);
        }

        private void ApplyCharacterVisual(int index)
        {
            if (cachedBinder == null) return;
            cachedBinder.ApplyCharacter(index);
        }

        private void HandleVisualApplied(GameObject visual, Transform eyeAnchor)
        {
            if (eyeAnchor == null) return;
            foreach (var pc in GetComponentsInChildren<PlayerController>(true))
                pc.AlignCameraPivotTo(eyeAnchor);
        }

        private static int ResolveCharacterIndex(ulong clientId)
        {
            if (CharacterLobbyNetwork.Instance != null)
                return CharacterLobbyNetwork.Instance.GetCharacterIndexForClient(clientId);
            return 0;
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
        private void SubmitAuthPlayerIdServerRpc(FixedString64Bytes authPlayerId)
        {
            if (!authPlayerId.IsEmpty)
                networkAuthPlayerId.Value = authPlayerId;
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
            string id = itemId.ToString();
            var shop = Object.FindAnyObjectByType<ShopController>();
            bool success = shop != null && shop.TryPurchaseForPlayer(gameObject, id);

            int balanceAfter = TryGetComponent(out CurrencyWallet wallet) ? wallet.Balance : -1;
            Debug.Log(
                $"[Shop][Server] purchase client={OwnerClientId} buyer={DisplayName} item={id} " +
                $"success={success} balanceAfter={balanceAfter}");

            if (success && shop != null && shop.RequiresOwnerLocalApplyForItem(id))
                ApplyShopEffectOwnerClientRpc(itemId);

            NotifyShopResultClientRpc(success, itemId);
        }

        [ClientRpc]
        private void ApplyShopEffectOwnerClientRpc(FixedString64Bytes itemId)
        {
            if (!IsOwner) return;

            var shop = Object.FindAnyObjectByType<ShopController>();
            if (shop == null)
            {
                Debug.LogWarning($"[Shop][Owner] ShopController missing — item={itemId}");
                return;
            }

            shop.ApplyPurchasedEffectOnOwner(gameObject, itemId.ToString());
        }

        [ClientRpc]
        private void NotifyShopResultClientRpc(bool success, FixedString64Bytes itemId)
        {
            if (!IsOwner) return;

            var shopView = Object.FindAnyObjectByType<ShopView>();
            shopView?.RefreshFromNetwork();

            if (!success)
                Debug.LogWarning($"[Shop] 구매 실패: {itemId}");
        }

        [ServerRpc]
        public void RequestWeaponUpgradeServerRpc(int slot)
        {
            var shop = Object.FindAnyObjectByType<ShopController>();
            if (shop == null)
            {
                NotifyWeaponTierResultClientRpc(false, slot, -1);
                return;
            }

            var arsenal = GetComponent<PlayerArsenal>();
            if (arsenal == null)
            {
                NotifyWeaponTierResultClientRpc(false, slot, -1);
                return;
            }

            int next = arsenal.CurrentTierIndex((WeaponSlot)slot) + 1;
            bool success = shop.TryPurchaseWeaponTierForPlayer(gameObject, (WeaponSlot)slot, next);
            NotifyWeaponTierResultClientRpc(success, slot, next);
        }

        [ServerRpc]
        public void RequestWeaponTierPurchaseServerRpc(int slot, int tierIndex)
        {
            var shop = Object.FindAnyObjectByType<ShopController>();
            bool success = shop != null
                && shop.TryPurchaseWeaponTierForPlayer(gameObject, (WeaponSlot)slot, tierIndex);
            NotifyWeaponTierResultClientRpc(success, slot, tierIndex);
        }

        [ClientRpc]
        private void NotifyWeaponTierResultClientRpc(bool success, int slot, int tierIndex)
        {
            if (!IsOwner) return;

            var shopView = Object.FindAnyObjectByType<ShopView>();
            shopView?.RefreshFromNetwork();

            if (!success)
            {
                Debug.LogWarning($"[Shop] 무기 티어 구매 실패: slot={slot}, tier={tierIndex}");
                return;
            }

            if (!TryGetComponent<PlayerArsenal>(out var arsenal))
                return;

            var weaponSlot = (WeaponSlot)slot;
            arsenal.ApplyNetworkTier(weaponSlot, tierIndex);
            arsenal.SetActiveSlot(weaponSlot);
        }
    }
}
