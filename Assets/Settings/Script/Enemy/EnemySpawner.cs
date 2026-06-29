using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using ProjectM.Network;

namespace ProjectM.Enemy
{
    /// <summary>
    /// 적 프리팹을 지정된 스폰 포인트에서 생성한다. WaveManager의 지시를 받는다.
    /// 멀티에서는 서버만 스폰하고 NGO로 복제한다.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("스폰 포인트")]
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private float spawnRadius = 1.5f;

        private readonly List<EnemyAIController> alive = new();

        public int AliveCount
        {
            get
            {
                if (NetworkSessionHelper.IsMultiplayerSession && !NetworkSessionHelper.IsServer)
                    return CountSceneEnemies();

                alive.RemoveAll(e => e == null);
                return alive.Count;
            }
        }

        public EnemyAIController Spawn(GameObject prefab)
        {
            return Spawn(prefab, -1);
        }

        public EnemyAIController Spawn(GameObject prefab, int spawnPointIndex)
        {
            if (!NetworkSessionHelper.IsGameplayAuthority) return null;
            if (prefab == null || spawnPoints == null || spawnPoints.Length == 0) return null;

            Transform sp = ResolveSpawnPoint(spawnPointIndex);
            if (sp == null) return null;

            Vector3 offset = Random.insideUnitSphere * spawnRadius;
            offset.y = 0;
            Vector3 pos = sp.position + offset;

            if (NavMesh.SamplePosition(pos, out var hit, 4f, NavMesh.AllAreas))
                pos = hit.position;

            var go = Instantiate(prefab, pos, sp.rotation);
            var ai = go.GetComponent<EnemyAIController>();
            if (ai == null) return null;

            if (NetworkSessionHelper.IsMultiplayerSession)
            {
                var netObj = go.GetComponent<NetworkObject>();
                if (netObj == null)
                {
                    Debug.LogError($"[Spawner] {prefab.name} 에 NetworkObject 없음 — Phase 2 Setup 실행 필요");
                    Destroy(go);
                    return null;
                }

                netObj.Spawn();
            }

            alive.Add(ai);
            ai.OnDeath += HandleEnemyDied;
            return ai;
        }

        private static int CountSceneEnemies()
        {
            var enemies = Object.FindObjectsByType<EnemyAIController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int count = 0;
            foreach (var enemy in enemies)
            {
                if (enemy != null && enemy.IsAlive)
                    count++;
            }

            return count;
        }

        private Transform ResolveSpawnPoint(int index)
        {
            if (index >= 0 && index < spawnPoints.Length)
            {
                if (spawnPoints[index] != null) return spawnPoints[index];
                Debug.LogWarning($"[Spawner] spawnPoints[{index}] 가 비어있음 — 랜덤으로 폴백");
            }
            else if (index >= 0)
            {
                Debug.LogWarning($"[Spawner] spawnPointIndex {index} 가 배열 범위(0~{spawnPoints.Length - 1}) 밖 — 랜덤으로 폴백");
            }

            return spawnPoints[Random.Range(0, spawnPoints.Length)];
        }

        private void HandleEnemyDied(EnemyAIController ai)
        {
            ai.OnDeath -= HandleEnemyDied;
            alive.Remove(ai);
        }

        public void KillAll()
        {
            if (!NetworkSessionHelper.IsGameplayAuthority) return;

            foreach (var e in alive)
            {
                if (e == null) continue;

                if (NetworkSessionHelper.IsMultiplayerSession
                    && e.TryGetComponent<NetworkObject>(out var netObj)
                    && netObj.IsSpawned)
                {
                    netObj.Despawn(true);
                    continue;
                }

                Destroy(e.gameObject);
            }

            alive.Clear();
        }
    }
}
