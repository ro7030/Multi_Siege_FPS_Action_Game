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

        public EnemyAIController Spawn(GameObject prefab)
        {
            if (prefab == null || spawnPoints == null || spawnPoints.Length == 0) return null;
            var sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
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
