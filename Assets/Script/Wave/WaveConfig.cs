using System;
using UnityEngine;

namespace ProjectM.Wave
{
    [Serializable]
    public class EnemyGroup
    {
        public GameObject enemyPrefab;
        public int count = 5;
        [Tooltip("그룹 내 적 사이의 스폰 간격(초)")]
        public float spawnInterval = 0.4f;
        [Tooltip("이전 그룹과의 시작 지연(초)")]
        public float startDelay = 0f;
    }

    [CreateAssetMenu(menuName = "ProjectM/Wave/WaveConfig", fileName = "WaveConfig")]
    public class WaveConfig : ScriptableObject
    {
        public int waveNumber = 1;
        public string label = "Wave 1";

        [Tooltip("그룹 단위로 순차 스폰. 동시 스폰을 원하면 startDelay=0 으로 설정")]
        public EnemyGroup[] groups;

        [Tooltip("보스 여부")]
        public bool isBossWave = false;

        [Tooltip("웨이브 종료 후 다음 준비 페이즈까지의 대기 시간(초)")]
        public float postWaveBreak = 20f;
    }
}
