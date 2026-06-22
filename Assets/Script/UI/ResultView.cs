using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectM.Auth;
using ProjectM.Core;
using ProjectM.Data;
using ProjectM.Economy;
using ProjectM.Network;
using ProjectM.Player;

namespace ProjectM.UI
{
    /// <summary>
    /// 매치 종료 시 자동으로 표시되는 결과 화면.
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
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text playerText;
        [SerializeField] private TMP_Text waveText;
        [SerializeField] private TMP_Text killsText;
        [SerializeField] private TMP_Text playTimeText;
        [SerializeField] private TMP_Text balanceText;
        [SerializeField] private TMP_Text rewardText;

        [Header("표시 라벨")]
        [SerializeField] private string victoryLabel = "Victory";
        [SerializeField] private string defeatLabel  = "Game Over";
        [SerializeField] private Color  victoryColor = new Color(0.5f, 1f, 0.7f);
        [SerializeField] private Color  defeatColor  = new Color(1f, 0.4f, 0.4f);
        [SerializeField] private string playerLabelFormat = "Player {0}";
        [SerializeField] private int    localPlayerIndex  = 1;

        [Header("승패 이미지 (선택)")]
        [SerializeField] private Image  resultImage;
        [SerializeField] private Sprite victorySprite;
        [SerializeField] private Sprite defeatSprite;

        [Header("버튼")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button homeButton;

        [Header("동작")]
        [SerializeField] private bool pauseGameOnShow = true;

        private float storedTimeScale = 1f;

        private void Awake()
        {
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
            if (wallet  == null) wallet  = LocalPlayerUtility.FindLocalCurrencyWallet();
            if (reward  == null) reward  = FindAnyObjectByType<RewardCalculator>();
            if (stats   == null) stats   = FindAnyObjectByType<PlayerStatsTracker>();

            if (panelRoot != null) panelRoot.SetActive(false);

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

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (pauseGameOnShow)
            {
                storedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }

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
                playerText.text = ResolveLocalDisplayName();

            if (waveText != null && session != null)
                waveText.text = $"Wave {session.State.CurrentWave} / {session.State.MaxWave}";

            if (playTimeText != null && session != null)
            {
                float sec = Time.time - session.State.MatchStartTime;
                playTimeText.text = $"플레이 시간: {sec:F1}초";
            }

            if (balanceText != null && wallet != null)
                balanceText.text = $"₩ {wallet.Balance:N0}";

            if (rewardText != null)
                rewardText.text = $"+{ResolveLastReward():N0}";

            if (killsText != null && stats != null)
                killsText.text = $"쓰러트린 적 수: {stats.Kills:N0}";

            if (retryButton != null)
            {
                bool canRetry = !NetworkSessionHelper.IsMultiplayerSession || NetworkSessionHelper.IsServer;
                retryButton.interactable = canRetry;
            }
        }

        public void Hide()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            if (pauseGameOnShow) Time.timeScale = storedTimeScale <= 0f ? 1f : storedTimeScale;
        }

        private string ResolveLocalDisplayName()
        {
            var netLocal = NetworkPlayerRegistry.LocalPlayer;
            if (netLocal != null && !string.IsNullOrEmpty(netLocal.DisplayName))
                return netLocal.DisplayName;

            if (stats != null && !string.IsNullOrEmpty(stats.LocalNickname))
                return stats.LocalNickname;

            return string.Format(playerLabelFormat, localPlayerIndex);
        }

        private int ResolveLastReward()
        {
            if (NetworkSessionHelper.IsMultiplayerSession && NetworkMatchStats.Instance != null)
                return NetworkMatchStats.Instance.LastReward;

            return reward != null ? reward.LastReward : 0;
        }

        private void OnRetryClicked()
        {
            if (NetworkSessionHelper.IsMultiplayerSession && !NetworkSessionHelper.IsServer)
                return;

            Hide();
            MatchExitHelper.ExitToCharacterSelect();
        }

        private void OnHomeClicked()
        {
            Hide();
            MatchExitHelper.ExitToCharacterSelect();
        }
    }
}
