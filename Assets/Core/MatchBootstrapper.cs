using System.Collections;
using UnityEngine;

namespace ProjectM.Core
{
    /// <summary>
    /// Gameplay 씬 진입 시 매치 자동 진행. 디버그 UI 없이 게임이 흘러가도록 한다.
    /// 흐름: Lobby → (자동) StartMatch → Preparation N초 → StartWave → (적 모두 처치) → Preparation → ...
    /// </summary>
    public class MatchBootstrapper : MonoBehaviour
    {
        [SerializeField] private GameSessionManager session;

        [Header("자동 진행")]
        [SerializeField] private bool autoStartMatch = true;
        [SerializeField] private float startDelay = 2f;          // 씬 진입 후 매치 시작까지 대기
        [SerializeField] private float preparationDuration = 15f; // 다음 웨이브 시작까지 준비 시간
        [SerializeField] private float firstWaveDelay = 5f;      // 첫 웨이브까지 대기 (짧게)

        [Header("상태 (읽기 전용)")]
        [SerializeField] private float preparationRemaining;

        public float PreparationRemaining => preparationRemaining;

        private void Awake()
        {
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
        }

        private IEnumerator Start()
        {
            if (!autoStartMatch || session == null) yield break;

            yield return new WaitForSeconds(startDelay);
            session.StartMatch();
            Debug.Log("[Bootstrap] 매치 자동 시작");

            // 첫 웨이브
            yield return RunPreparation(firstWaveDelay);
            session.StartWave();

            session.OnWaveEnded += HandleWaveEnded;
            session.OnMatchEnded += HandleMatchEnded;
        }

        private void HandleWaveEnded(int wave)
        {
            // EndWave 시 GameSessionManager 가 Preparation 페이즈로 전환 (마지막 웨이브가 아니면)
            if (session.State.CurrentPhase == GamePhase.Preparation)
            {
                StartCoroutine(PrepareNextWave());
            }
        }

        private void HandleMatchEnded(bool _)
        {
            preparationRemaining = 0f;
            session.OnWaveEnded -= HandleWaveEnded;
            session.OnMatchEnded -= HandleMatchEnded;
        }

        private IEnumerator PrepareNextWave()
        {
            yield return RunPreparation(preparationDuration);
            if (session.State.CurrentPhase == GamePhase.Preparation)
                session.StartWave();
        }

        private IEnumerator RunPreparation(float duration)
        {
            preparationRemaining = duration;
            while (preparationRemaining > 0f)
            {
                preparationRemaining -= Time.deltaTime;
                yield return null;
            }
            preparationRemaining = 0f;
        }
    }
}
