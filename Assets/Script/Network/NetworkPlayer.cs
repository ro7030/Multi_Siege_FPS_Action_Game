using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using ProjectM.Auth;
using ProjectM.CharacterSelect;
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

            if (TryGetComponent<CharacterController>(out var cc))
                cc.enabled = local;

            if (local)
                gameObject.tag = "Player";
        }

        private static FixedString64Bytes ResolveNickname(ulong clientId)
        {
            if (CharacterLobbyNetwork.Instance != null)
            {
                string name = CharacterLobbyNetwork.Instance.GetNicknameForClient(clientId);
                if (!string.IsNullOrEmpty(name))
                    return new FixedString64Bytes(name);
            }

            if (NetworkManager.Singleton != null
                && clientId == NetworkManager.Singleton.LocalClientId)
            {
                return AuthSessionManager.ResolveNickname("Player");
            }

            return new FixedString64Bytes($"Player{clientId}");
        }
    }
}
