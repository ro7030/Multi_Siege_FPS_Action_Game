using UnityEngine;
using TMPro;
using ProjectM.Core;

namespace ProjectM.UI
{
    /// <summary>
    /// 정비(Preparation) 시간 카운트다운. MatchBootstrapper 의 남은 시간을 표시한다.
    /// 웨이브 종료 후 준비 시간(예: 60 → 59 → 58 …) 동안만 보인다.
    /// </summary>
    public class PreparationTimerPresenter : MonoBehaviour
    {
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private MatchBootstrapper bootstrap;
        [SerializeField] private GameSessionManager session;

        [Header("표시")]
        [Tooltip("비우면 숫자만 (예: 60). {0} = 남은 초")]
        [SerializeField] private string displayFormat = "{0}";
        [SerializeField] private bool hideOutsidePreparation = true;
        [SerializeField] private bool hideWhenZero = true;

        private void Awake()
        {
            if (bootstrap == null) bootstrap = FindAnyObjectByType<MatchBootstrapper>();
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();

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
            bool counting = bootstrap != null && bootstrap.IsPreparationCounting;

            if (hideOutsidePreparation && (!inPrepPhase || !counting))
            {
                SetVisible(false);
                return;
            }

            int seconds = bootstrap != null
                ? Mathf.Max(0, Mathf.CeilToInt(bootstrap.PreparationRemaining))
                : 0;

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
