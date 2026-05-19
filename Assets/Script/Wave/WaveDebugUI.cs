using UnityEngine;
using ProjectM.Enemy;

namespace ProjectM.Wave
{
    /// <summary>
    /// Phase 4 임시 디버그 UI. 웨이브 상태/스폰 진행/잔존 적 수를 표시한다.
    /// </summary>
    public class WaveDebugUI : MonoBehaviour
    {
        [SerializeField] private WaveManager waveManager;
        [SerializeField] private EnemySpawner spawner;

        private void Awake()
        {
            if (waveManager == null) waveManager = FindAnyObjectByType<WaveManager>();
            if (spawner == null) spawner = FindAnyObjectByType<EnemySpawner>();
        }

        private void OnGUI()
        {
            GUI.skin.label.fontSize = 14;
            GUI.skin.button.fontSize = 13;

            GUILayout.BeginArea(new Rect(Screen.width - 280, 320, 270, 220), GUI.skin.box);
            GUILayout.Label("=== Phase 4 Wave Debug ===");

            if (waveManager != null)
            {
                var cfg = waveManager.CurrentConfig;
                GUILayout.Label($"Wave   : {(cfg != null ? cfg.label : "-")}");
                GUILayout.Label($"Spawn  : {waveManager.SpawnedCount} / {waveManager.TotalToSpawn}");
                GUILayout.Label($"Spawning: {waveManager.IsSpawning}  Cleared: {waveManager.IsWaveCleared}");
            }
            if (spawner != null) GUILayout.Label($"Alive  : {spawner.AliveCount}");

            GUILayout.Space(6);
            GUILayout.Label("Manual Start (테스트용)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Wave 1")) waveManager?.StartWave(1);
            if (GUILayout.Button("Wave 2")) waveManager?.StartWave(2);
            if (GUILayout.Button("Wave 3")) waveManager?.StartWave(3);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Abort Wave")) waveManager?.AbortWave();

            GUILayout.EndArea();
        }
    }
}
