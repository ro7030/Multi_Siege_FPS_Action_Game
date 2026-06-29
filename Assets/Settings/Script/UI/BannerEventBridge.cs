using UnityEngine;
using ProjectM.Core;
using ProjectM.Economy;
using ProjectM.Defense;

namespace ProjectM.UI
{
    /// <summary>
    /// 게임 이벤트를 NotificationBanner 로 연결한다.
    /// 표시 문구는 Inspector 에서 편집 가능. {wave} 는 웨이브 번호로 치환된다.
    /// </summary>
    public class BannerEventBridge : MonoBehaviour
    {
        [Header("참조 (자동 탐색)")]
        [SerializeField] private GameSessionManager session;
        [SerializeField] private FarmManager farmManager;

        [Header("메시지 문구 (편집 가능, {wave} 치환)")]
        [SerializeField] private string waveStartedFormat = "Wave {wave} 시작!";
        [SerializeField] private string waveClearedFormat = "Wave {wave} 클리어!";
        [SerializeField] private string preparationFormat = "정비 시간 — 다음 웨이브를 준비하세요";
        [SerializeField] private string matchClearedText = "방어 성공! 모든 웨이브 클리어!";
        [SerializeField] private string matchFailedText  = "베이스가 파괴되었습니다…";
        [SerializeField] private string farmDestroyedText = "밭이 파괴되었습니다!";

        [Header("표시 시간")]
        [SerializeField] private float waveDuration = 2.5f;
        [SerializeField] private float resultDuration = 4f;

        private void Awake()
        {
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
            if (farmManager == null) farmManager = FindAnyObjectByType<FarmManager>();
        }

        private void OnEnable()
        {
            if (session != null)
            {
                session.OnWaveStarted += HandleWaveStarted;
                session.OnWaveEnded += HandleWaveEnded;
                session.OnMatchEnded += HandleMatchEnded;
            }
            if (farmManager != null) farmManager.OnFarmDestroyed += HandleFarmDestroyed;
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.OnWaveStarted -= HandleWaveStarted;
                session.OnWaveEnded -= HandleWaveEnded;
                session.OnMatchEnded -= HandleMatchEnded;
            }
            if (farmManager != null) farmManager.OnFarmDestroyed -= HandleFarmDestroyed;
        }

        // ── 핸들러 ──
        private void HandleWaveStarted(int wave) => Show(Format(waveStartedFormat, wave), waveDuration);

        private void HandleWaveEnded(int wave)
        {
            Show(Format(waveClearedFormat, wave), waveDuration);
            // 다음이 정비 페이즈면 안내 추가
            if (session != null && session.State.CurrentPhase == GamePhase.Preparation && !string.IsNullOrEmpty(preparationFormat))
                Show(preparationFormat, waveDuration);
        }

        private void HandleMatchEnded(bool cleared)
            => Show(cleared ? matchClearedText : matchFailedText, resultDuration);

        private void HandleFarmDestroyed(FarmPlot _)
            => Show(farmDestroyedText, waveDuration);

        // ── 헬퍼 ──
        private string Format(string template, int wave)
            => string.IsNullOrEmpty(template) ? "" : template.Replace("{wave}", wave.ToString());

        private void Show(string msg, float dur)
        {
            if (string.IsNullOrEmpty(msg)) return;
            if (NotificationBanner.Instance != null) NotificationBanner.Instance.Show(msg, dur);
        }
    }
}
