using System;
using System.Collections.Generic;
using ProjectM.Core;
using ProjectM.Enemy;
using ProjectM.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ProjectM.Network
{
    public struct MatchStatEntry : INetworkSerializable, IEquatable<MatchStatEntry>
    {
        public ulong ClientId;
        public FixedString64Bytes Nickname;
        public int Kills;
        public int HarvestCount;
        public int ReviveCount;
        public float DamageDealt;

        public int Score => Kills * 100 + HarvestCount * 10 + ReviveCount * 200;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Nickname);
            serializer.SerializeValue(ref Kills);
            serializer.SerializeValue(ref HarvestCount);
            serializer.SerializeValue(ref ReviveCount);
            serializer.SerializeValue(ref DamageDealt);
        }

        public bool Equals(MatchStatEntry other)
        {
            return ClientId == other.ClientId
                   && Nickname.Equals(other.Nickname)
                   && Kills == other.Kills
                   && HarvestCount == other.HarvestCount
                   && ReviveCount == other.ReviveCount
                   && Mathf.Approximately(DamageDealt, other.DamageDealt);
        }
    }

    /// <summary>
    /// 서버 권한 매치 통계. 킬·데미지·수확·부활을 집계하고 NetworkList로 전원에 복제한다.
    /// </summary>
    public class NetworkMatchStats : NetworkBehaviour
    {
        public static NetworkMatchStats Instance { get; private set; }

        [SerializeField] private GameSessionManager session;

        private NetworkList<MatchStatEntry> statsList;
        private readonly Dictionary<ulong, MatchStatEntry> serverStats = new();
        private readonly Dictionary<ulong, int> serverIndexByClient = new();

        private readonly NetworkVariable<int> netLastReward = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private float scanTimer;
        private readonly HashSet<HealthSystem> trackedEnemyHealth = new();

        public int LastReward => netLastReward.Value;

        private void Awake()
        {
            statsList = new NetworkList<MatchStatEntry>(
                default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

            if (session == null)
                session = FindAnyObjectByType<GameSessionManager>();
        }

        public override void OnNetworkSpawn()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning("[NetworkMatchStats] Duplicate instance detected.");
            else
                Instance = this;

            if (IsServer)
            {
                if (session != null)
                    session.OnMatchStarted += ResetAll;

                NetworkManager.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
                EnsureAllConnectedClientsRegistered();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                if (session != null)
                    session.OnMatchStarted -= ResetAll;

                NetworkManager.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
                UnsubscribeAllEnemies();
            }

            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (!IsServer) return;

            scanTimer += Time.deltaTime;
            if (scanTimer >= 1.5f)
            {
                scanTimer = 0f;
                RescanEnemySubscriptions();
            }
        }

        public void ResetAll()
        {
            if (!IsServer) return;

            serverStats.Clear();
            serverIndexByClient.Clear();
            statsList.Clear();
            netLastReward.Value = 0;
            UnsubscribeAllEnemies();
            EnsureAllConnectedClientsRegistered();
            Debug.Log("[NetworkMatchStats] 통계 리셋");
        }

        public void ServerSetLastReward(int amount)
        {
            if (!IsServer) return;
            netLastReward.Value = amount;
        }

        public bool TryGetStat(ulong clientId, out MatchStatEntry entry)
        {
            foreach (var stat in statsList)
            {
                if (stat.ClientId == clientId)
                {
                    entry = stat;
                    return true;
                }
            }

            entry = default;
            return false;
        }

        public MatchStatEntry GetLocalSnapshot()
        {
            ulong localId = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.LocalClientId
                : 0;

            return TryGetStat(localId, out var entry) ? entry : default;
        }

        public int Count => statsList.Count;

        public MatchStatEntry GetEntryAt(int index) => statsList[index];

        public void RecordKill(ulong clientId)
        {
            if (!IsServer || clientId == ulong.MaxValue) return;
            ModifyStat(clientId, stat =>
            {
                stat.Kills++;
                return stat;
            });
        }

        public void RecordDamage(ulong clientId, float amount)
        {
            if (!IsServer || clientId == ulong.MaxValue || amount <= 0f) return;
            ModifyStat(clientId, stat =>
            {
                stat.DamageDealt += amount;
                return stat;
            });
        }

        public void RecordHarvest(ulong clientId)
        {
            if (!IsServer || clientId == ulong.MaxValue) return;
            ModifyStat(clientId, stat =>
            {
                stat.HarvestCount++;
                return stat;
            });
        }

        public void RecordRevive(ulong clientId)
        {
            if (!IsServer || clientId == ulong.MaxValue) return;
            ModifyStat(clientId, stat =>
            {
                stat.ReviveCount++;
                return stat;
            });
        }

        private void ModifyStat(ulong clientId, Func<MatchStatEntry, MatchStatEntry> mutator)
        {
            EnsureEntry(clientId);
            var updated = mutator(serverStats[clientId]);
            updated.Nickname = ResolveNickname(clientId);
            serverStats[clientId] = updated;
            PushEntry(clientId, updated);
        }

        private void EnsureAllConnectedClientsRegistered()
        {
            if (!IsServer || NetworkManager.Singleton == null) return;

            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
                EnsureEntry(clientId);
        }

        private void HandleClientConnected(ulong clientId) => EnsureEntry(clientId);

        private void HandleClientDisconnected(ulong clientId)
        {
            if (!serverIndexByClient.TryGetValue(clientId, out int index))
                return;

            if (index >= 0 && index < statsList.Count)
                statsList.RemoveAt(index);

            serverStats.Remove(clientId);
            RebuildIndexMap();
        }

        private void EnsureEntry(ulong clientId)
        {
            if (serverStats.ContainsKey(clientId))
                return;

            var entry = new MatchStatEntry
            {
                ClientId = clientId,
                Nickname = ResolveNickname(clientId),
            };
            serverStats[clientId] = entry;
            serverIndexByClient[clientId] = statsList.Count;
            statsList.Add(entry);
        }

        private void PushEntry(ulong clientId, MatchStatEntry entry)
        {
            if (!serverIndexByClient.TryGetValue(clientId, out int index)
                || index < 0
                || index >= statsList.Count)
            {
                serverIndexByClient[clientId] = statsList.Count;
                statsList.Add(entry);
                return;
            }

            statsList[index] = entry;
        }

        private void RebuildIndexMap()
        {
            serverIndexByClient.Clear();
            for (int i = 0; i < statsList.Count; i++)
                serverIndexByClient[statsList[i].ClientId] = i;
        }

        private static FixedString64Bytes ResolveNickname(ulong clientId)
        {
            foreach (var player in NetworkPlayerRegistry.All)
            {
                if (player != null && player.OwnerClientId == clientId)
                    return new FixedString64Bytes(player.DisplayName);
            }

            return new FixedString64Bytes($"Player{clientId}");
        }

        private void RescanEnemySubscriptions()
        {
            foreach (var ai in FindObjectsByType<EnemyAIController>(FindObjectsSortMode.None))
            {
                var hp = ai.GetComponent<HealthSystem>();
                if (hp == null || !trackedEnemyHealth.Add(hp))
                    continue;

                hp.OnDamaged += HandleEnemyDamaged;
                hp.OnDied += HandleEnemyDied;
            }

            trackedEnemyHealth.RemoveWhere(h => h == null);
        }

        private void UnsubscribeAllEnemies()
        {
            foreach (var hp in trackedEnemyHealth)
            {
                if (hp == null) continue;
                hp.OnDamaged -= HandleEnemyDamaged;
                hp.OnDied -= HandleEnemyDied;
            }

            trackedEnemyHealth.Clear();
        }

        private void HandleEnemyDamaged(float amount, GameObject attacker)
        {
            RecordDamage(ResolveAttackerClientId(attacker), amount);
        }

        private void HandleEnemyDied(GameObject attacker)
        {
            RecordKill(ResolveAttackerClientId(attacker));
        }

        private static ulong ResolveAttackerClientId(GameObject attacker)
        {
            if (attacker == null) return ulong.MaxValue;

            var netObj = attacker.GetComponentInParent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
                return netObj.OwnerClientId;

            return ulong.MaxValue;
        }
    }
}
