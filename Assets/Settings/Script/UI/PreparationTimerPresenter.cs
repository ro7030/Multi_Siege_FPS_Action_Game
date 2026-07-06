using UnityEngine;
using TMPro;
using ProjectM.Core;
using ProjectM.Network;

namespace ProjectM.UI
{
    // 정비(Preparation) 시간 카운트다운. MatchBootstrapper 의 남은 시간을 표시한다.
    // 웨이브 종료 후 준비 시간(예: 60 → 59 → 58 …) 동안만 보인다.
    public class PreparationTimerPresenter : MonoBehaviour
    {
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private MatchBootstrapper bootstrap;
        [SerializeField] private GameSessionManager session;
        [SerializeField] private NetworkMatchDirector director;

        [Header("표시")]
        [Tooltip("비우면 숫자만 (예: 60). {0} = 남은 초")]
        [SerializeField] private string displayFormat = "{0}";
        [SerializeField] private bool hideOutsidePreparation = true;
        [SerializeField] private bool hideWhenZero = true;

        private void Awake()
        {
            if (bootstrap == null) bootstrap = FindAnyObjectByType<MatchBootstrapper>();
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
            if (director == null) director = FindAnyObjectByType<NetworkMatchDirector>();

            if (timerText == null)
                timerText = GetComponent<TMP_Text>();

            if (timerText == null)
            {
                var found = GameObject.Find("TimerText");
                if (found != null) timerText = found.GetComponent<TMP_Text>();
            }
        }

        private void Update()
        {
            if (timerText == null) return;

            bool inPrepPhase = session != null && session.State.CurrentPhase == GamePhase.Preparation;
            bool useNet = NetworkSessionHelper.IsMultiplayerSession && !NetworkSessionHelper.IsServer;
            float remaining = useNet && director != null
                ? director.SyncedPrepRemaining
                : bootstrap != null ? bootstrap.PreparationRemaining : 0f;
            bool counting = inPrepPhase && remaining > 0f;

            if (hideOutsidePreparation && (!inPrepPhase || !counting))
            {
                SetVisible(false);
                return;
            }

            int seconds = Mathf.Max(0, Mathf.CeilToInt(remaining));

            if (hideWhenZero && seconds <= 0)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            timerText.text = string.IsNullOrEmpty(displayFormat)
                ? seconds.ToString()
                : string.Format(displayFormat, seconds);
        }

        private void SetVisible(bool on)
        {
            if (timerText.gameObject.activeSelf != on)
                timerText.gameObject.SetActive(on);
        }
    }
}
