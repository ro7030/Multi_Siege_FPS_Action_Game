using Unity.Netcode;
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
    /// Retry는 전원 확인 후 orchestrated rematch, Home은 rematch 그룹에서 제외.
    /// </summary>
    public class ResultView : MonoBehaviour
    {
        private const string DefaultFontResourcePath = "Fonts/Jalnan2/Jalnan2TTF SDF";

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
        [SerializeField] private TMP_Text rematchStatusText;
        [SerializeField] private TMP_FontAsset rematchStatusFont;

        [Header("표시 라벨")]
        [SerializeField] private string victoryLabel = "victory";
        [SerializeField] private string defeatLabel  = "game over";
        [SerializeField] private Color  victoryColor = new Color(0.5f, 1f, 0.7f);
        [SerializeField] private Color  defeatColor  = new Color(1f, 0.4f, 0.4f);
        [SerializeField] private string playerLabelFormat = "player {0}";
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
        [SerializeField] private int resultCanvasSortOrder = 100;

        private bool localRetryRegistered;
        private bool rematchUiLocked;
        private bool isResultOpen;

        private void Awake()
        {
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
            if (wallet  == null) wallet  = LocalPlayerUtility.FindLocalCurrencyWallet();
            if (reward  == null) reward  = FindAnyObjectByType<RewardCalculator>();
            if (stats   == null) stats   = FindAnyObjectByType<PlayerStatsTracker>();

            EnsureRematchStatusText();

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

            SubscribeEvents();
        }

        private void Start()
        {
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
            UnsubscribeEvents();
            SubscribeEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void SubscribeEvents()
        {
            if (session != null) session.OnMatchEnded += HandleMatchEnded;
            NetworkMatchDirector.RematchStatusUpdated += HandleRematchStatusUpdated;
            MatchRematchCoordinator.RematchTransitionStarted += HandleRematchTransitionStarted;
            MatchRematchCoordinator.HostReturnedHome += HandleHostReturnedHome;
            MatchRematchCoordinator.RematchOrchestrationFailed += HandleRematchOrchestrationFailed;
        }

        private void UnsubscribeEvents()
        {
            if (session != null) session.OnMatchEnded -= HandleMatchEnded;
            NetworkMatchDirector.RematchStatusUpdated -= HandleRematchStatusUpdated;
            MatchRematchCoordinator.RematchTransitionStarted -= HandleRematchTransitionStarted;
            MatchRematchCoordinator.HostReturnedHome -= HandleHostReturnedHome;
            MatchRematchCoordinator.RematchOrchestrationFailed -= HandleRematchOrchestrationFailed;
        }

        private void OnDisable()
        {
            ReleaseModalIfOpen();
        }

        private void HandleMatchEnded(bool cleared) => Show(cleared);

        public void Show(bool cleared)
        {
            if (panelRoot == null) return;

            if (isResultOpen)
            {
                RefreshRematchStatusUi();
                RefreshActionButtons();
                return;
            }

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            EnsureVisible();

            localRetryRegistered = false;
            rematchUiLocked = false;
            if (!NetworkSessionHelper.IsMultiplayerSession)
                MatchPartyContext.ResetRematchSession();

            panelRoot.SetActive(true);
            isResultOpen = true;
            UIInputModal.Push();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log($"[ResultView] Show cleared={cleared} mp={NetworkSessionHelper.IsMultiplayerSession}");

            // UIInputModal로 gameplay 입력 차단. Time.timeScale=0은 MP 게스트 UI 클릭을 막을 수 있어 사용하지 않음.

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
                waveText.text = $"wave {session.State.CurrentWave} / {session.State.MaxWave}";

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

            RefreshRematchStatusUi();
            RefreshActionButtons();
        }

        public void Hide()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            ReleaseModalIfOpen();
            gameObject.SetActive(false);
        }

        private void ReleaseModalIfOpen()
        {
            if (!isResultOpen) return;

            isResultOpen = false;
            UIInputModal.Pop();
        }

        private void HandleRematchStatusUpdated(RematchStatusPayload payload)
        {
            if (panelRoot == null || !panelRoot.activeSelf) return;
            SyncLocalRetryRegisteredFromPayload(payload);
            RefreshRematchStatusUi();
            RefreshActionButtons();
        }

        private void SyncLocalRetryRegisteredFromPayload(RematchStatusPayload payload)
        {
            if (!NetworkSessionHelper.IsMultiplayerSession || NetworkManager.Singleton == null)
                return;

            ulong localClientId = NetworkManager.Singleton.LocalClientId;
            localRetryRegistered = false;

            for (int i = 0; i < payload.PlayerCount; i++)
            {
                var entry = payload.GetPlayer(i);
                if (entry.OwnerClientId != localClientId) continue;
                localRetryRegistered = entry.PlayerState == RematchPlayerState.RetryReady;
                break;
            }
        }

        private void HandleRematchTransitionStarted()
        {
            rematchUiLocked = true;
            RefreshRematchStatusUi("Rematch 준비 중...");
            RefreshActionButtons();
        }

        private void HandleHostReturnedHome()
        {
            if (panelRoot == null || !panelRoot.activeSelf) return;
            RefreshRematchStatusUi();
            RefreshActionButtons();
        }

        private void HandleRematchOrchestrationFailed()
        {
            rematchUiLocked = false;
            localRetryRegistered = false;
            RefreshRematchStatusUi();
            RefreshActionButtons();
        }

        private void RefreshRematchStatusUi(string overrideText = null)
        {
            EnsureRematchStatusText();
            if (rematchStatusText == null) return;

            if (!string.IsNullOrEmpty(overrideText))
            {
                rematchStatusText.text = overrideText;
                return;
            }

            if (IsGuestBlockedByHostLeftHome())
            {
                rematchStatusText.text = MatchPartyContext.FormatStatusText()
                    + "\n\n호스트가 나갔습니다. 홈으로 이동하세요.";
                return;
            }

            rematchStatusText.text = MatchPartyContext.FormatStatusText();
        }

        private void RefreshActionButtons()
        {
            bool locked = rematchUiLocked || MatchPartyContext.RematchOrchestrationStarted;
            bool hostLeftBlocked = IsGuestBlockedByHostLeftHome();

            if (retryButton != null)
                retryButton.interactable = !locked && !localRetryRegistered && !hostLeftBlocked;

            if (homeButton != null)
                homeButton.interactable = !locked;
        }

        private static bool IsGuestBlockedByHostLeftHome()
        {
            if (!NetworkSessionHelper.IsMultiplayerSession) return false;
            if (NetworkSessionHelper.IsServer) return false;
            return MatchPartyContext.HostLeftViaHome;
        }

        private void EnsureRematchStatusText()
        {
            if (panelRoot == null) return;

            if (rematchStatusText == null)
            {
                var existing = panelRoot.transform.Find("RematchStatusText");
                if (existing != null)
                    rematchStatusText = existing.GetComponent<TMP_Text>();
            }

            if (rematchStatusText == null)
            {
                var go = new GameObject("RematchStatusText", typeof(RectTransform));
                go.transform.SetParent(panelRoot.transform, false);

                rematchStatusText = go.AddComponent<TextMeshProUGUI>();
                rematchStatusText.fontSize = 18f;
                rematchStatusText.alignment = TextAlignmentOptions.TopLeft;
                rematchStatusText.color = new Color(0.85f, 0.9f, 1f);
            }

            ApplyRematchStatusTextStyle();
            PlaceRematchStatusTextInPanel();
        }

        private void PlaceRematchStatusTextInPanel()
        {
            if (rematchStatusText == null || panelRoot == null) return;

            var rect = rematchStatusText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -120f);
            rect.sizeDelta = new Vector2(480f, 160f);

            int targetIndex = retryButton != null
                ? retryButton.transform.GetSiblingIndex()
                : panelRoot.transform.childCount;
            rematchStatusText.transform.SetSiblingIndex(targetIndex);
        }

        private void ApplyRematchStatusTextStyle()
        {
            if (rematchStatusText == null) return;

            rematchStatusText.raycastTarget = false;

            var font = ResolveRematchStatusFont();
            if (font != null)
                rematchStatusText.font = font;
        }

        private TMP_FontAsset ResolveRematchStatusFont()
        {
            if (rematchStatusFont != null)
                return rematchStatusFont;

            rematchStatusFont = Resources.Load<TMP_FontAsset>(DefaultFontResourcePath);
            if (rematchStatusFont != null)
                return rematchStatusFont;

            if (playerText != null && playerText.font != null)
                return playerText.font;

            if (waveText != null && waveText.font != null)
                return waveText.font;

            if (killsText != null && killsText.font != null)
                return killsText.font;

            return null;
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
            if (rematchUiLocked || localRetryRegistered) return;
            if (IsGuestBlockedByHostLeftHome()) return;

            if (!NetworkSessionHelper.IsMultiplayerSession)
            {
                Hide();
                MatchExitHelper.ExitToCharacterSelect();
                return;
            }

            localRetryRegistered = true;
            RefreshActionButtons();

            var director = FindAnyObjectByType<NetworkMatchDirector>();
            if (director != null && director.IsSpawned)
            {
                director.RegisterRematchIntent();
                RefreshRematchStatusUi();
                return;
            }

            Debug.LogWarning("[ResultView] NetworkMatchDirector not found — cannot register rematch intent.");
            localRetryRegistered = false;
            RefreshActionButtons();
        }

        private void OnHomeClicked()
        {
            if (rematchUiLocked) return;
            Hide();
            MatchExitHelper.ExitToMainMenu();
        }

        private void EnsureVisible()
        {
            Transform node = transform;
            while (node != null)
            {
                if (!node.gameObject.activeSelf)
                    node.gameObject.SetActive(true);
                node = node.parent;
            }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, resultCanvasSortOrder);

            if (panelRoot != null && panelRoot.TryGetComponent(out Image panelImage))
            {
                var c = panelImage.color;
                if (c.a < 0.85f)
                    panelImage.color = new Color(c.r, c.g, c.b, 0.85f);
            }
        }
    }
}
