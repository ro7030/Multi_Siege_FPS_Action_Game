using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectM.UI
{
    /// <summary>
    /// Gameplay 씬 진입 직후 카운트다운 오버레이를 표시한다.
    /// countdownDuration 이 끝나면 패널을 숨기고 OnCountdownFinished 이벤트를 발생시킨다.
    /// MatchBootstrapper 가 이 이벤트를 받아 매치를 시작한다.
    ///
    /// [Inspector 설정]
    ///  - countdownDuration  : 카운트다운 시간 (초) — 에디터에서 자유롭게 조절
    ///  - countdownPanel     : 전체화면 패널 (없으면 자동 생성)
    ///  - backgroundImage    : 이미지 슬롯 — 원하는 Sprite 를 여기에 연결
    ///  - countdownText      : 숫자 텍스트 (없으면 자동 생성)
    /// </summary>
    public class CountdownPresenter : MonoBehaviour
    {
        [Header("카운트다운 설정")]
        [SerializeField] private float countdownDuration = 10f;

        [Header("UI 연결 (비워두면 자동 생성)")]
        [SerializeField] private GameObject countdownPanel;
        [SerializeField] private Image backgroundImage;   // 이미지를 넣을 슬롯
        [SerializeField] private Text countdownText;

        /// <summary>카운트다운 완료 시 발생. MatchBootstrapper 가 구독한다.</summary>
        public event Action OnCountdownFinished;

        /// <summary>남은 시간 (읽기 전용). HUD 등 외부에서 참조 가능.</summary>
        public float Remaining { get; private set; }

        // ─────────────────────────────────────────────────────────────
        private void Start()
        {
            EnsureUI();
            StartCoroutine(RunCountdown());
        }

        // ─────────────────────────────────────────────────────────────
        private IEnumerator RunCountdown()
        {
            Remaining = countdownDuration;

            while (Remaining > 0f)
            {
                if (countdownText != null)
                    countdownText.text = Mathf.CeilToInt(Remaining).ToString();

                Remaining -= Time.deltaTime;
                yield return null;
            }

            Remaining = 0f;

            if (countdownText != null)
                countdownText.text = "0";

            // 패널 숨기기
            if (countdownPanel != null)
                countdownPanel.SetActive(false);

            OnCountdownFinished?.Invoke();
        }

        // ─────────────────────────────────────────────────────────────
        /// <summary>Inspector 에서 연결하지 않은 요소를 자동으로 생성한다.</summary>
        private void EnsureUI()
        {
            // 패널 없으면 생성
            if (countdownPanel == null)
            {
                countdownPanel = new GameObject("CountdownPanel");

                // UIRoot Canvas 아래에 붙이거나, 없으면 독립 Canvas 생성
                Canvas rootCanvas = FindAnyObjectByType<Canvas>();
                if (rootCanvas != null)
                    countdownPanel.transform.SetParent(rootCanvas.transform, false);

                var rect = countdownPanel.AddComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                // 반투명 어두운 배경
                var bg = countdownPanel.AddComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0.75f);
            }

            // 이미지 슬롯 없으면 패널 위에 생성 (연결 시 Sprite 교체 가능)
            if (backgroundImage == null)
            {
                var imgObj = new GameObject("CountdownBackground");
                imgObj.transform.SetParent(countdownPanel.transform, false);

                var rect = imgObj.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot     = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(400f, 400f);
                rect.anchoredPosition = new Vector2(0f, 60f);   // 숫자 위쪽

                backgroundImage = imgObj.AddComponent<Image>();
                backgroundImage.color = new Color(1f, 1f, 1f, 0f); // 기본 투명 — Sprite 연결 시 보임
            }

            // 숫자 텍스트 없으면 생성
            if (countdownText == null)
            {
                var textObj = new GameObject("CountdownText");
                textObj.transform.SetParent(countdownPanel.transform, false);

                var rect = textObj.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot     = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(300f, 150f);
                rect.anchoredPosition = new Vector2(0f, -80f);  // 이미지 아래

                countdownText = textObj.AddComponent<Text>();
                countdownText.alignment  = TextAnchor.MiddleCenter;
                countdownText.fontSize   = 100;
                countdownText.fontStyle  = FontStyle.Bold;
                countdownText.color      = Color.white;
                countdownText.font       = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            countdownPanel.SetActive(true);
        }
    }
}
