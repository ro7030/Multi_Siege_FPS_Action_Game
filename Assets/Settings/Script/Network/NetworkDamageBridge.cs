using System;
using Unity.Netcode;
using UnityEngine;
using ProjectM.Player;

namespace ProjectM.Network
{
    // NGO 체력 권한: 클라이언트 데미지/회복 요청은 ServerRpc, HP·다운 상태는 NetworkVariable로 복제.
    [RequireComponent(typeof(HealthSystem))]
    public class NetworkDamageBridge : NetworkBehaviour
    {
        private const byte LifeAlive = 0;
        private const byte LifeDown = 1;
        private const byte LifeDead = 2;

        private readonly NetworkVariable<float> netCurrentHp = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> netMaxHp = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<byte> netLifeState = new(
            LifeAlive,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> netReviveProgress = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // ReviveSystem이 없는 엔티티(적)가 게스트 클라이언트에서 사망 상태로 전환될 때 1회 발생.
        // EnemyAIController.OnDeath는 서버/싱글플레이에서만 실행되므로, 순수 게스트 클라이언트의
        // 시각 효과(쓰러짐 이펙트 등)는 이 이벤트로 트리거한다.
        public event Action OnClientVisualDeath;

        private HealthSystem health;
        private ReviveSystem revive;
        private ulong lastReviverClientId = ulong.MaxValue;

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
            revive = GetComponent<ReviveSystem>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                PushHealthSnapshot();
                health.OnHpChanged += HandleServerHpChanged;
                BindReviveEvents();
            }
            else
            {
                netCurrentHp.OnValueChanged += HandleClientHpChanged;
                netMaxHp.OnValueChanged += HandleClientHpChanged;
                netLifeState.OnValueChanged += HandleClientLifeStateChanged;
                netReviveProgress.OnValueChanged += HandleClientReviveProgressChanged;
                ApplyClientHealthSnapshot();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                if (health != null)
                    health.OnHpChanged -= HandleServerHpChanged;
                UnbindReviveEvents();
            }
            else
            {
                netCurrentHp.OnValueChanged -= HandleClientHpChanged;
                netMaxHp.OnValueChanged -= HandleClientHpChanged;
                netLifeState.OnValueChanged -= HandleClientLifeStateChanged;
                netReviveProgress.OnValueChanged -= HandleClientReviveProgressChanged;
            }
        }

        public bool TryRequestServerDamage(float amount, GameObject attacker)
        {
            if (!NetworkSessionHelper.IsMultiplayerSession || !IsSpawned)
                return false;

            if (IsServer)
                return false;

            RequestDamageServerRpc(amount);
            return true;
        }

        public bool TryRequestServerHeal(float amount)
        {
            if (!NetworkSessionHelper.IsMultiplayerSession || !IsSpawned)
                return false;

            if (IsServer)
                return false;

            if (!IsOwner)
                return false;

            RequestHealServerRpc(amount);
            return true;
        }

        public void RequestReviveHold(float deltaTime, GameObject interactor = null)
        {
            if (!NetworkSessionHelper.IsMultiplayerSession || !IsSpawned)
            {
                revive?.ProgressRevive(deltaTime);
                return;
            }

            if (IsServer)
            {
                TrackReviver(interactor);
                revive?.ProgressRevive(deltaTime);
                PushHealthSnapshot();
                return;
            }

            RequestReviveHoldServerRpc(deltaTime);
        }

        public void RequestReviveHoldCancel()
        {
            if (!NetworkSessionHelper.IsMultiplayerSession || !IsSpawned)
            {
                revive?.CancelRevive();
                return;
            }

            if (IsServer)
            {
                revive?.CancelRevive();
                return;
            }

            CancelReviveHoldServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestReviveHoldServerRpc(float deltaTime, ServerRpcParams rpcParams = default)
        {
            lastReviverClientId = rpcParams.Receive.SenderClientId;
            revive?.ProgressRevive(deltaTime);
            PushHealthSnapshot();
        }

        [ServerRpc(RequireOwnership = false)]
        private void CancelReviveHoldServerRpc()
        {
            revive?.CancelRevive();
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestDamageServerRpc(float amount, ServerRpcParams rpcParams = default)
        {
            if (health == null || !health.IsAlive || amount <= 0f)
                return;

            GameObject attacker = ResolveAttacker(rpcParams.Receive.SenderClientId);
            health.ApplyDamageLocal(amount, attacker);
            PushHealthSnapshot();
        }

        [ServerRpc(RequireOwnership = true)]
        private void RequestHealServerRpc(float amount)
        {
            if (health == null || amount <= 0f)
                return;

            health.ApplyHealLocal(amount);
            PushHealthSnapshot();
        }

        private void HandleServerHpChanged(float current, float max) => PushHealthSnapshot();

        private void PushHealthSnapshot()
        {
            if (!IsServer || health == null)
                return;

            netMaxHp.Value = health.MaxHp;
            netCurrentHp.Value = health.CurrentHp;
            netLifeState.Value = ResolveLifeState();
            netReviveProgress.Value = revive != null ? revive.ReviveProgress : 0f;
        }

        // 게이트 설치 등 비활성→활성 전환 후 서버 HP 스냅샷을 즉시 밀어넣는다.
        public void ServerSyncHealthNow()
        {
            if (!IsServer || !IsSpawned || health == null)
                return;

            PushHealthSnapshot();
        }

        private byte ResolveLifeState()
        {
            if (revive == null)
                return health.IsAlive ? LifeAlive : LifeDead;

            if (revive.IsDead) return LifeDead;
            if (revive.IsDown) return LifeDown;
            if (!health.IsAlive) return LifeDown;
            return LifeAlive;
        }

        private void BindReviveEvents()
        {
            if (revive == null) return;

            revive.OnDowned += HandleReviveStateChanged;
            revive.OnRevived += HandleReviveCompleted;
            revive.OnFullDeath += HandleReviveStateChanged;
        }

        private void UnbindReviveEvents()
        {
            if (revive == null) return;

            revive.OnDowned -= HandleReviveStateChanged;
            revive.OnRevived -= HandleReviveCompleted;
            revive.OnFullDeath -= HandleReviveStateChanged;
        }

        private void HandleReviveStateChanged() => PushHealthSnapshot();

        private void HandleReviveCompleted()
        {
            if (lastReviverClientId != ulong.MaxValue)
                NetworkMatchStats.Instance?.RecordRevive(lastReviverClientId);

            lastReviverClientId = ulong.MaxValue;
            PushHealthSnapshot();
        }

        private void TrackReviver(GameObject interactor)
        {
            lastReviverClientId = ResolveInteractorClientId(interactor);
        }

        private static ulong ResolveInteractorClientId(GameObject interactor)
        {
            if (interactor == null)
            {
                return NetworkManager.Singleton != null
                    ? NetworkManager.Singleton.LocalClientId
                    : ulong.MaxValue;
            }

            var netObj = interactor.GetComponentInParent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
                return netObj.OwnerClientId;

            return ulong.MaxValue;
        }

        private void HandleClientHpChanged(float _, float __) => ApplyClientHealthSnapshot();

        private void HandleClientLifeStateChanged(byte previous, byte current)
        {
            if (revive != null)
            {
                revive.ApplyNetworkLifeState(current);
                return;
            }

            // ReviveSystem이 없는 엔티티(적): Alive/Down -> Dead 전환 시점에만 게스트 시각 효과 트리거.
            if (current == LifeDead && previous != LifeDead)
                OnClientVisualDeath?.Invoke();
        }

        private void HandleClientReviveProgressChanged(float _, float __)
        {
            if (revive == null) return;
            revive.ApplyNetworkReviveProgress(netReviveProgress.Value);
        }

        private void ApplyClientHealthSnapshot()
        {
            if (IsServer || health == null) return;
            health.SetNetworkSnapshot(netCurrentHp.Value, netMaxHp.Value);

            if (revive != null)
            {
                revive.ApplyNetworkLifeState(netLifeState.Value);
                revive.ApplyNetworkReviveProgress(netReviveProgress.Value);
            }
        }

        private static GameObject ResolveAttacker(ulong clientId)
        {
            if (NetworkManager.Singleton == null)
                return null;

            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                return null;

            return client.PlayerObject != null ? client.PlayerObject.gameObject : null;
        }
    }
}
