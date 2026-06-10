using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using ProjectM.Core;
using ProjectM.Data;
using ProjectM.Economy;
using ProjectM.Player;

namespace ProjectM.UI
{
    /// <summary>
    /// 매치 종료 시 자동으로 표시되는 결과 화면.
    /// 인스펙터에 미리 만든 패널/텍스트/버튼을 켜고 꺼서 표시한다.
    /// (이전: 코드에서 UI 를 빌드 → 현재: 프리팹/씬에 디자인된 패널을 토글)
    /// GameSessionManager.OnMatchEnded 구독.
    /// </summary>
    public class ResultView : MonoBehaviour
    {
        [Header("데이터 소스 (비우면 자동 탐색)")]
        [SerializeField] private GameSessionManager session;
        [SerializeField] private CurrencyWallet wallet;
        [SerializeField] private RewardCalculator reward;
        [SerializeField] private PlayerStatsTracker stats;

        [Header("패널 루트")]
        [Tooltip("Game Over 패널 전체. 평소엔 비활성, 결과 시 활성.")]
        [SerializeField] private GameObject panelRoot;

        [Header("텍스트 (필요한 것만 연결)")]
        [SerializeField] private TMP_Text titleText;        // 예: \"Game Over\" / \"Victory\"
        [SerializeField] private TMP_Text playerText;       // 예: \"Player 1\"
        [SerializeField] private TMP_Text waveText;         // 예: \"Wave 2 / 4\"
        [SerializeField] private TMP_Text killsText;        // 예: \"쓰러트린 적 수: 1,000\" (카운터 연결 전엔 비워둠)
        [SerializeField] private TMP_Text playTimeText;     // 예: \"플레이 시간: 123.4초\"
        [SerializeField] private TMP_Text balanceText;      // 예: \"₩ 1,000\"
        [SerializeField] private TMP_Text rewardText;       // 예: \"+250\"

        [Header("표시 라벨")]
        [SerializeField] private string victoryLabel = "Victory";
        [SerializeField] private string defeatLabel  = "Game Over";
        [SerializeField] private Color  victoryColor = new Color(0.5f, 1f, 0.7f);
        [SerializeField] private Color  defeatColor  = new Color(1f, 0.4f, 0.4f);
        [SerializeField] private string playerLabelFormat = "Player {0}";
        [SerializeField] private int    localPlayerIndex  = 1;

        [Header("승패 이미지 (선택)")]
        [Tooltip("승리/패배에 따라 sprite 를 갈아끼울 이미지 (예: 헤더 장식). 비워두면 미사용.")]
        [SerializeField] private Image  resultImage;
        [SerializeField] private Sprite victorySprite;
        [SerializeField] private Sprite defeatSprite;

        [Header("버튼")]
        [SerializeField] private Button retryButton;   // 다시하기
        [SerializeField] private Button homeButton;    // 홈으로

        [Header("동작")]
        [Tooltip("결과창이 뜰 때 Time.timeScale 을 0 으로 멈출지")]
        [SerializeField] private bool pauseGameOnShow = true;
        [Tooltip("다시하기 시 로드할 씬 이름. 비우면 현재 씬 재로드.")]
        [SerializeField] private string retrySceneName = "";

        private float storedTimeScale = 1f;

        private void Awake()
        {
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
            if (wallet  == null) wallet  = LocalPlayerUtility.FindLocalCurrencyWallet();
            if (reward  == null) reward  = FindAnyObjectByType<RewardCalculator>();
            if (stats   == null) stats   = FindAnyObjectByType<PlayerStatsTracker>();

            // 평소엔 숨김
            if (panelRoot != null) panelRoot.SetActive(false);

            // 버튼 와이어링
            if (retryButton != null)
            {
                retryButton.onClick.RemoveAllListeners();
                retryButton.onClick.AddListener(OnRetryClicked);
            }
            if (homeButton != null)
            {
                homeButton.onClick.RemoveAllListeners();
                homeButton.onClick.AddListener(OnHomeClicked);
            }
        }

        private void OnEnable()
        {
            if (session != null) session.OnMatchEnded += HandleMatchEnded;
        }

        private void OnDisable()
        {
            if (session != null) session.OnMatchEnded -= HandleMatchEnded;
        }

        private void HandleMatchEnded(bool cleared) => Show(cleared);

        public void Show(bool cleared)
        {
            if (panelRoot == null) return;
            panelRoot.SetActive(true);

            // 커서 풀기 (씬은 안 바꾸므로 직접 해줘야 함)
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // 게임 일시정지
            if (pauseGameOnShow)
            {
                storedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }

            // 텍스트 채우기 (연결된 것만)
            if (titleText != null)
            {
                titleText.text  = cleared ? victoryLabel : defeatLabel;
                titleText.color = cleared ? victoryColor : defeatColor;
            }

            if (resultImage != null)
            {
                var sprite = cleared ? victorySprite : defeatSprite;
                if (sprite != null) resultImage.sprite = sprite;
            }

            if (playerText != null)
                playerText.text = string.Format(playerLabelFormat, localPlayerIndex);

            if (waveText != null && session != null)
                waveText.text = $"Wave {session.State.CurrentWave} / {session.State.MaxWave}";

            if (playTimeText != null && session != null)
            {
                float sec = Time.unscaledTime - session.State.MatchStartTime;
                playTimeText.text = $"플레이 시간: {sec:F1}초";
            }

            if (balanceText != null && wallet != null)
                balanceText.text = $"₩ {wallet.Balance:N0}";

            if (rewardText != null && reward != null)
                rewardText.text = $"+{reward.LastReward:N0}";

            if (killsText != null && stats != null)
                killsText.text = $"{stats.Kills:N0}";
        }

        public void Hide()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            if (pauseGameOnShow) Time.timeScale = storedTimeScale <= 0f ? 1f : storedTimeScale;
        }

        /// <summary>외부에서 처치 수를 주입할 때 사용.</summary>
        public void SetKills(int kills)
        {
            if (killsText != null) killsText.text = $"쓰러트린 적 수: {kills:N0}";
        }

        // ─── 버튼 핸들러 ────────────────────────────────────────────────
        private void OnRetryClicked()
        {
            // 타임스케일 복구 후 씬 재로드
            Time.timeScale = 1f;
            var sceneName = string.IsNullOrEmpty(retrySceneName)
                ? SceneManager.GetActiveScene().name
                : retrySceneName;
            SceneManager.LoadScene(sceneName);
        }

        private void OnHomeClicked()
        {
            Time.timeScale = 1f;
            if (session != null) session.ReturnToLobby();
        }
    }
}
