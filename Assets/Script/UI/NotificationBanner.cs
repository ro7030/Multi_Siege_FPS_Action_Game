using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProjectM.UI
{
    /// <summary>
    /// 상단 중앙 공지/안내 배너. 메시지를 큐에 넣으면 순서대로 표시 후 자동으로 사라진다.
    /// 페이드 인/아웃 지원. 웨이브 시작, 거점 파괴 경고, 시스템 안내 등에 사용.
    ///
    /// 사용:
    ///   NotificationBanner.Instance.Show("Wave 3 시작!");
    ///   NotificationBanner.Instance.Show("성문이 파괴되었습니다!", 4f);
    ///
    /// 두 가지 사용법
    ///   1) UI 슬롯 비움 → 코드가 자동 생성 (위치/색/폰트 Inspector 조절)
    ///   2) 직접 Canvas 로 배너 만들고 슬롯 연결 → 그 디자인 사용
    /// </summary>
    public class NotificationBanner : MonoBehaviour
    {
        public static NotificationBanner Instance { get; private set; }

        [Header("UI 요소 — 비워두면 자동 생성")]
        [SerializeField] private GameObject bannerRoot;   // 배경 + 텍스트 묶음
        [SerializeField] private Image background;        // 배너 배경 (이미지 교체 가능)
        [SerializeField] private TMP_Text messageText;        // 메시지

        [Header("타이밍")]
        [SerializeField] private float defaultDuration = 2.5f; // 표시 유지 시간
        [SerializeField] private float fadeIn = 0.25f;
        [SerializeField] private float fadeOut = 0.4f;

        [Header("자동 생성 레이아웃")]
        [SerializeField] private Vector2 anchorOffset = new(0, -60); // 상단 중앙 기준
        [SerializeField] private Vector2 size = new(900, 64);
        [SerializeField] private int fontSize = 30;
        [SerializeField] private Color bgColor = new(0, 0, 0, 0.65f);
        [SerializeField] private Color textColor = Color.white;

        private CanvasGroup group;
        private readonly Queue<(string msg, float dur)> queue = new();

        private enum State { Idle, FadingIn, Holding, FadingOut }
        private State state = State.Idle;
        private float timer;
        private float currentDuration;

        // ─────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void Start()
        {
            if (UIRoot.Instance == null) { enabled = false; return; }
            BuildMissing();
            SetAlpha(0f);
            if (bannerRoot != null) bannerRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── 공개 API ─────────────────────────────────────────────
        /// <summary>배너 메시지 표시 (현재 표시 중이면 큐에 대기).</summary>
        public void Show(string message, float duration = -1f)
        {
            if (duration <= 0f) duration = defaultDuration;
            queue.Enqueue((message, duration));
        }

        /// <summary>즉시 모든 큐 비우고 현재 배너 숨김.</summary>
        public void ClearAll()
        {
            queue.Clear();
            state = State.Idle;
            SetAlpha(0f);
            if (bannerRoot != null) bannerRoot.SetActive(false);
        }

        // ─────────────────────────────────────────────────────────────
        private void Update()
        {
            switch (state)
            {
                case State.Idle:
                    if (queue.Count > 0) BeginNext();
                    break;

                case State.FadingIn:
                    timer += Time.deltaTime;
                    SetAlpha(fadeIn <= 0 ? 1f : Mathf.Clamp01(timer / fadeIn));
                    if (timer >= fadeIn) { state = State.Holding; timer = 0f; }
                    break;

                case State.Holding:
                    timer += Time.deltaTime;
                    if (timer >= currentDuration) { state = State.FadingOut; timer = 0f; }
                    break;

                case State.FadingOut:
                    timer += Time.deltaTime;
                    SetAlpha(fadeOut <= 0 ? 0f : 1f - Mathf.Clamp01(timer / fadeOut));
                    if (timer >= fadeOut)
                    {
                        state = State.Idle;
                        timer = 0f;
                        if (queue.Count == 0 && bannerRoot != null) bannerRoot.SetActive(false);
                    }
                    break;
            }
        }

        private void BeginNext()
        {
            var (msg, dur) = queue.Dequeue();
            if (messageText != null) messageText.text = msg;
            currentDuration = dur;
            if (bannerRoot != null) bannerRoot.SetActive(true);
            state = State.FadingIn;
            timer = 0f;
        }

        private void SetAlpha(float a)
        {
            if (group != null) group.alpha = a;
        }

        // ─────────────────────────────────────────────────────────────
        private void BuildMissing()
        {
            var root = UIRoot.Instance.RootTransform;

            if (bannerRoot == null)
            {
                var go = UIRoot.CreateChild("NotificationBanner", root);
                bannerRoot = go;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = size;
                rt.anchoredPosition = anchorOffset;

                if (background == null)
                {
                    background = go.AddComponent<Image>();
                    background.color = bgColor;
                }

                if (messageText == null)
                {
                    messageText = UIRoot.CreateText("Message", rt, fontSize, TextAnchor.MiddleCenter);
                    messageText.color = textColor;
                    messageText.fontStyle = FontStyles.Bold;
                    var mrt = messageText.rectTransform;
                    mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one;
                    mrt.offsetMin = new Vector2(20, 0); mrt.offsetMax = new Vector2(-20, 0);
                }
            }

            group = bannerRoot.GetComponent<CanvasGroup>();
            if (group == null) group = bannerRoot.AddComponent<CanvasGroup>();
        }
    }
}
