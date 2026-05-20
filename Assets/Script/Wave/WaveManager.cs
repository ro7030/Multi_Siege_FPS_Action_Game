using System;
using System.Collections;
using UnityEngine;
using ProjectM.Core;
using ProjectM.Enemy;

namespace ProjectM.Wave
{
    /// <summary>
    /// 웨이브 순서대로 EnemySpawner에게 스폰 지시를 보낸다.
    /// Host 권한. GameSessionManager의 OnWaveStarted/OnWaveEnded 이벤트와 연동.
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        [SerializeField] private GameSessionManager session;
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private WaveConfig[] waves;
        [SerializeField] private bool autoStartFromSession = true;

        public bool IsSpawning { get; private set; }
        public bool IsWaveCleared { get; private set; }
        public WaveConfig CurrentConfig { get; private set; }
        public int SpawnedCount { get; private set; }
        public int TotalToSpawn { get; private set; }

        public event Action<WaveConfig> OnWaveSpawnStarted;
        public event Action<WaveConfig> OnWaveSpawnFinished; // 스폰 종료 (적 잔존 가능)
        public event Action<WaveConfig> OnWaveCleared;       // 모든 적 사망

        private Coroutine spawnRoutine;

        private void Awake()
        {
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
            if (spawner == null) spawner = FindAnyObjectByType<EnemySpawner>();
        }

        private void OnEnable()
        {
            if (autoStartFromSession && session != null)
                session.OnWaveStarted += HandleSessionWaveStarted;
        }

        private void OnDisable()
        {
            if (session != null) session.OnWaveStarted -= HandleSessionWaveStarted;
        }

        private void HandleSessionWaveStarted(int waveNumber)
        {
            StartWave(waveNumber);
        }

        public void StartWave(int waveNumber)
        {
            if (IsSpawning) { Debug.LogWarning("[Wave] 이미 스폰 중"); return; }
            int idx = Mathf.Clamp(waveNumber - 1, 0, (waves?.Length ?? 1) - 1);
            if (waves == null || waves.Length == 0)
            {
                Debug.LogWarning("[Wave] WaveConfig 미설정");
                return;
            }
            CurrentConfig = waves[idx];
            IsWaveCleared = false;
            spawnRoutine = StartCoroutine(SpawnRoutine(CurrentConfig));
        }

        private IEnumerator SpawnRoutine(WaveConfig config)
        {
            IsSpawning = true;
            SpawnedCount = 0;
            TotalToSpawn = 0;
            if (config.groups != null)
                foreach (var g in config.groups) TotalToSpawn += Mathf.Max(0, g.count);

            OnWaveSpawnStarted?.Invoke(config);
            Debug.Log($"[Wave] {config.label} 스폰 시작 (총 {TotalToSpawn}마리)");

            if (config.groups != null)
            {
                foreach (var group in config.groups)
                {
                    if (group.startDelay > 0f) yield return new WaitForSeconds(group.startDelay);
                    for (int i = 0; i < group.count; i++)
                    {
                        spawner?.Spawn(group.enemyPrefab, group.spawnPointIndex);
                        SpawnedCount++;
                        if (group.spawnInterval > 0f) yield return new WaitForSeconds(group.spawnInterval);
                    }
                }
            }

            IsSpawning = false;
            OnWaveSpawnFinished?.Invoke(config);
            Debug.Log($"[Wave] {config.label} 스폰 완료. 잔존 적 처치 대기.");

            // 모든 적이 죽을 때까지 대기
            while (spawner != null && spawner.AliveCount > 0)
                yield return new WaitForSeconds(0.5f);

            IsWaveCleared = true;
            OnWaveCleared?.Invoke(config);
            Debug.Log($"[Wave] {config.label} 클리어!");

            // GameSession에 종료 통보
            if (session != null && session.State.CurrentPhase == GamePhase.Wave)
                session.EndWave();
        }

        public void AbortWave()
        {
            if (spawnRoutine != null) StopCoroutine(spawnRoutine);
            IsSpawning = false;
            spawner?.KillAll();
        }
    }
}
