using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectM.Core;
using ProjectM.Economy;
using ProjectM.Player;

namespace ProjectM.UI
{
    /// <summary>
    /// 매치 종료 시 자동으로 표시되는 결과 화면.
    /// GameSessionManager.OnMatchEnded 구독, ResultUploader(Phase 8)와 연동 예정.
    /// </summary>
    public class ResultView : MonoBehaviour
    {
        [SerializeField] private GameSessionManager session;
        [SerializeField] private CurrencyWallet wallet;
        [SerializeField] private RewardCalculator reward;

        private GameObject panelGo;
        private TMP_Text titleText;
        private TMP_Text statsText;

        private void Awake()
        {
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
            if (wallet == null) wallet = LocalPlayerUtility.FindLocalCurrencyWallet();
            if (reward == null) reward = FindAnyObjectByType<RewardCalculator>();
        }

        private void Start()
        {
            if (UIRoot.Instance == null) { enabled = false; return; }
            BuildPanel();
            panelGo.SetActive(false);

            if (session != null) session.OnMatchEnded += HandleMatchEnded;
        }

        private void OnDisable()
        {
            if (session != null) session.OnMatchEnded -= HandleMatchEnded;
        }

        private void HandleMatchEnded(bool cleared)
        {
            Show(cleared);
        }

        public void Show(bool cleared)
        {
            if (panelGo == null) return;
            panelGo.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            titleText.text = cleared ? "VICTORY" : "DEFEAT";
            titleText.color = cleared ? new Color(0.5f, 1f, 0.7f) : new Color(1f, 0.4f, 0.4f);

            var sb = new System.Text.StringBuilder();
            if (session != null)
            {
                sb.AppendLine($"도달 웨이브: {session.State.CurrentWave} / {session.State.MaxWave}");
                sb.AppendLine($"플레이 시간: {Time.time - session.State.MatchStartTime:F1}초");
            }
            if (wallet != null) sb.AppendLine($"최종 잔액: ₩ {wallet.Balance}");
            if (reward != null) sb.AppendLine($"마지막 보상: +{reward.LastReward}");
            sb.AppendLine();
            sb.AppendLine("(Phase 8에서 DB로 전송됩니다)");
            statsText.text = sb.ToString();
        }

        public void Hide()
        {
            panelGo.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void BuildPanel()
        {
            var root = UIRoot.Instance.RootTransform;
            var bg = UIRoot.CreatePanel("ResultPanel", root, new Color(0.04f, 0.06f, 0.1f, 0.94f));
            panelGo = bg.gameObject;
            var rt = bg.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(640, 480);

            titleText = UIRoot.CreateText("Title", rt, 56, TextAnchor.UpperCenter);
            titleText.fontStyle = FontStyles.Bold;
            var trt = titleText.rectTransform;
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
            trt.offsetMin = new Vector2(0, -120); trt.offsetMax = new Vector2(0, -20);

            statsText = UIRoot.CreateText("Stats", rt, 22, TextAnchor.UpperCenter);
            var srt = statsText.rectTransform;
            srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 1);
            srt.offsetMin = new Vector2(40, 80); srt.offsetMax = new Vector2(-40, -140);

            var lobbyBtn = UIRoot.CreateButton("LobbyBtn", rt, "Return to Lobby");
            var lrt = lobbyBtn.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.5f, 0); lrt.anchorMax = new Vector2(0.5f, 0);
            lrt.pivot = new Vector2(0.5f, 0); lrt.sizeDelta = new Vector2(220, 48);
            lrt.anchoredPosition = new Vector2(0, 24);
            lobbyBtn.onClick.AddListener(() =>
            {
                Hide();
                if (session != null) session.ReturnToLobby();
            });
        }
    }
}
