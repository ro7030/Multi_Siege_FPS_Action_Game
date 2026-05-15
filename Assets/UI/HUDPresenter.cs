using UnityEngine;
using UnityEngine.UI;
using ProjectM.Core;
using ProjectM.Economy;
using ProjectM.Player;

namespace ProjectM.UI
{
    /// <summary>
    /// 화면에 항상 표시되는 HUD. HP/탄약/웨이브/잔액을 게임 상태에 구독하여 표시한다.
    ///
    /// 두 가지 사용법:
    ///   1) Inspector 필드를 모두 비워두면 코드로 자동 UI 생성 (MVP 기본값)
    ///   2) 직접 Canvas 안에 UI 요소를 만들고 Inspector에 드래그하면 그것을 사용
    ///   3) 일부만 연결해도 됨 — null인 요소만 자동 생성됨
    /// </summary>
    public class HUDPresenter : MonoBehaviour
    {
        [Header("게임 상태 참조 (자동 탐색)")]
        [SerializeField] private HealthSystem playerHealth;
        [SerializeField] private WeaponController playerWeapon;
        [SerializeField] private GameSessionManager session;
        [SerializeField] private CurrencyWallet wallet;

        [Header("UI 요소 — 비워두면 자동 생성")]
        [SerializeField] private Text waveText;
        [SerializeField] private Text currencyText;
        [SerializeField] private Image hpFill;
        [SerializeField] private Text hpText;
        [SerializeField] private Text ammoText;
        [SerializeField] private Text reloadText;

        [Header("자동 생성 옵션")]
        [SerializeField] private bool autoBuildMissing = true;

        private MatchBootstrapper bootstrap;

        private void Awake()
        {
            if (playerHealth == null) playerHealth = FindAnyObjectByType<HealthSystem>();
            if (playerWeapon == null) playerWeapon = FindAnyObjectByType<WeaponController>();
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
            if (wallet == null) wallet = FindAnyObjectByType<CurrencyWallet>();
            bootstrap = FindAnyObjectByType<MatchBootstrapper>();
        }

        private void Start()
        {
            if (autoBuildMissing) BuildMissingElements();
            BindEvents();
            RefreshAll();
        }

        // ── 누락된 UI 요소만 자동 생성 ──────────────────────────────
        private void BuildMissingElements()
        {
            if (UIRoot.Instance == null)
            {
                Debug.LogWarning("[HUD] UIRoot.Instance == null — 직접 연결한 UI만 사용");
                return;
            }
            var root = UIRoot.Instance.RootTransform;

            if (waveText == null)
            {
                waveText = UIRoot.CreateText("WaveText", root, 28, TextAnchor.UpperLeft);
                Anchor(waveText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -20), new Vector2(360, -60));
                waveText.color = new Color(1, 0.9f, 0.4f);
            }

            if (currencyText == null)
            {
                currencyText = UIRoot.CreateText("CurrencyText", root, 28, TextAnchor.UpperRight);
                Anchor(currencyText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-360, -20), new Vector2(-20, -60));
                currencyText.color = new Color(0.6f, 1f, 0.6f);
            }

            if (hpFill == null || hpText == null)
            {
                var hpBg = UIRoot.CreatePanel("HpBg", root, new Color(0, 0, 0, 0.55f));
                Anchor(hpBg.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 20), new Vector2(420, 60));

                if (hpFill == null)
                {
                    hpFill = UIRoot.CreatePanel("HpFill", hpBg.rectTransform, new Color(0.85f, 0.25f, 0.25f, 0.95f));
                    hpFill.type = Image.Type.Filled;
                    hpFill.fillMethod = Image.FillMethod.Horizontal;
                    hpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                    Anchor(hpFill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                }
                if (hpText == null)
                {
                    hpText = UIRoot.CreateText("HpText", hpBg.rectTransform, 22);
                    Anchor(hpText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                }
            }

            if (ammoText == null)
            {
                var ammoBg = UIRoot.CreatePanel("AmmoBg", root, new Color(0, 0, 0, 0.55f));
                Anchor(ammoBg.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-260, 20), new Vector2(-20, 80));
                ammoText = UIRoot.CreateText("AmmoText", ammoBg.rectTransform, 28);
                Anchor(ammoText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }

            if (reloadText == null)
            {
                reloadText = UIRoot.CreateText("ReloadText", root, 22, TextAnchor.MiddleCenter);
                Anchor(reloadText.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-260, 80), new Vector2(-20, 110));
                reloadText.color = new Color(1f, 0.85f, 0.4f);
                reloadText.text = "";
            }
        }

        private static void Anchor(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = offMin; rt.offsetMax = offMax;
        }

        // ── 이벤트 바인딩 ─────────────────────────────────────────
        private void BindEvents()
        {
            if (playerHealth != null) playerHealth.OnHpChanged += HandleHpChanged;
            if (playerWeapon != null)
            {
                playerWeapon.OnFired += RefreshAmmo;
                playerWeapon.OnReloadStart += RefreshReload;
                playerWeapon.OnReloadEnd += RefreshReload;
            }
            if (session != null) session.OnWaveStarted += HandleWaveStarted;
            if (wallet != null) wallet.OnChanged += HandleCurrencyChanged;
        }

        private void OnDisable()
        {
            if (playerHealth != null) playerHealth.OnHpChanged -= HandleHpChanged;
            if (playerWeapon != null)
            {
                playerWeapon.OnFired -= RefreshAmmo;
                playerWeapon.OnReloadStart -= RefreshReload;
                playerWeapon.OnReloadEnd -= RefreshReload;
            }
            if (session != null) session.OnWaveStarted -= HandleWaveStarted;
            if (wallet != null) wallet.OnChanged -= HandleCurrencyChanged;
        }

        // ── 핸들러 ────────────────────────────────────────────────
        private void HandleHpChanged(float cur, float max) => RefreshHp();
        private void HandleWaveStarted(int wave) => RefreshWave();
        private void HandleCurrencyChanged(int balance) => RefreshCurrency();

        private void Update()
        {
            if (playerWeapon != null && reloadText != null)
            {
                if (playerWeapon.IsReloading) reloadText.text = "RELOADING...";
                else if (reloadText.text != "") reloadText.text = "";
            }
            if (session != null && session.State.CurrentPhase == GamePhase.Preparation)
                RefreshWave();
        }

        private void RefreshAll()
        {
            RefreshHp();
            RefreshAmmo();
            RefreshWave();
            RefreshCurrency();
            RefreshReload();
        }

        private void RefreshHp()
        {
            if (playerHealth == null) return;
            if (hpText != null) hpText.text = $"HP  {playerHealth.CurrentHp:F0} / {playerHealth.MaxHp:F0}";
            if (hpFill != null) hpFill.fillAmount = playerHealth.HpRatio;
        }

        private void RefreshAmmo()
        {
            if (playerWeapon == null || ammoText == null) return;
            ammoText.text = $"{playerWeapon.CurrentMagazine}  /  {playerWeapon.ReserveAmmo}";
        }

        private void RefreshWave()
        {
            if (session == null || waveText == null) return;
            string phaseLabel = session.State.CurrentPhase.ToString();
            if (bootstrap != null && session.State.CurrentPhase == GamePhase.Preparation && bootstrap.PreparationRemaining > 0f)
                phaseLabel = $"Preparation  {bootstrap.PreparationRemaining:F0}s";
            waveText.text = $"Wave {session.State.CurrentWave} / {session.State.MaxWave}\n[{phaseLabel}]";
        }

        private void RefreshCurrency()
        {
            if (wallet == null || currencyText == null) return;
            currencyText.text = $"₩ {wallet.Balance}";
        }

        private void RefreshReload()
        {
            if (reloadText == null) return;
            reloadText.text = (playerWeapon != null && playerWeapon.IsReloading) ? "RELOADING..." : "";
        }
    }
}
