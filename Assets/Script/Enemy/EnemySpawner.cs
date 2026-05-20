using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectM.Enemy
{
    /// <summary>
    /// 적 프리팹을 지정된 스폰 포인트에서 생성한다. WaveManager의 지시를 받는다.
    /// MVP는 풀 없이 즉시 Instantiate/Destroy 방식.
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
                alive.RemoveAll(e => e == null);
                return alive.Count;
            }
        }

        /// <summary>랜덤 스폰포인트에서 생성 (기존 동작, 폴백용).</summary>
        public EnemyAIController Spawn(GameObject prefab)
        {
            return Spawn(prefab, -1);
        }

        /// <summary>
        /// 지정한 인덱스의 스폰포인트에서 생성.
        /// spawnPointIndex 가 -1 이거나 범위를 벗어나면 랜덤 스폰포인트로 폴백.
        /// </summary>
        public EnemyAIController Spawn(GameObject prefab, int spawnPointIndex)
        {
            if (prefab == null || spawnPoints == null || spawnPoints.Length == 0) return null;

            Transform sp = ResolveSpawnPoint(spawnPointIndex);
            if (sp == null) return null;

            Vector3 offset = Random.insideUnitSphere * spawnRadius; offset.y = 0;
            Vector3 pos = sp.position + offset;

            // NavMesh 위로 보정
            if (NavMesh.SamplePosition(pos, out var hit, 4f, NavMesh.AllAreas)) pos = hit.position;

            var go = Instantiate(prefab, pos, sp.rotation);
            var ai = go.GetComponent<EnemyAIController>();
            if (ai != null)
            {
                alive.Add(ai);
                ai.OnDeath += HandleEnemyDied;
            }
            return ai;
        }

        /// <summary>인덱스로 스폰포인트를 찾고, 유효하지 않으면 랜덤으로 폴백.</summary>
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
            foreach (var e in alive)
            {
                if (e == null) continue;
                Destroy(e.gameObject);
            }
            alive.Clear();
        }
    }
}
