using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectM.Core;
using ProjectM.Economy;
using ProjectM.Player;
using ProjectM.Wave;
using ProjectM.Enemy;

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
        [SerializeField] private KitInventory kitInventory;
        [SerializeField] private KitEquipper kitEquipper;
        [SerializeField] private WaveManager waveManager;
        [SerializeField] private EnemySpawner enemySpawner;

        [Header("UI 요소 — 비워두면 자동 생성")]
        [SerializeField] private TMP_Text waveText;
        [SerializeField] private TMP_Text enemyCountText;   // 좌상단 적 수 (현재/최대)
        [SerializeField] private Image hpFill;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text ammoText;
        [SerializeField] private TMP_Text reloadText;
        [SerializeField] private TMP_Text kitText;

        [Header("자동 생성 옵션")]
        [SerializeField] private bool autoBuildMissing = true;

        private MatchBootstrapper bootstrap;

        private void Awake()
        {
            if (playerHealth == null) playerHealth = FindAnyObjectByType<HealthSystem>();
            if (playerWeapon == null) playerWeapon = FindAnyObjectByType<WeaponController>();
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
            if (kitInventory == null) kitInventory = FindAnyObjectByType<KitInventory>();
            if (kitEquipper == null) kitEquipper = FindAnyObjectByType<KitEquipper>();
            if (waveManager == null) waveManager = FindAnyObjectByType<WaveManager>();
            if (enemySpawner == null) enemySpawner = FindAnyObjectByType<EnemySpawner>();
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

            if (enemyCountText == null)
            {
                // 좌상단, 웨이브 텍스트 아래 — 적 수 (현재/최대)
                enemyCountText = UIRoot.CreateText("EnemyCountText", root, 32, TextAnchor.UpperLeft);
                Anchor(enemyCountText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -64), new Vector2(360, -110));
                enemyCountText.color = new Color(1f, 0.5f, 0.5f);
                enemyCountText.fontStyle = FontStyles.Bold;
            }

            // 재화는 인게임 HUD 에 표시하지 않음 (상점 팝업에서만 표시)

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

            if (kitText == null)
            {
                // 하단 중앙: 키트 보유량 표시
                var kitBg = UIRoot.CreatePanel("KitBg", root, new Color(0, 0, 0, 0.55f));
                Anchor(kitBg.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-260, 20), new Vector2(260, 56));
                kitText = UIRoot.CreateText("KitText", kitBg.rectTransform, 20, TextAnchor.MiddleCenter);
                Anchor(kitText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                kitText.color = Color.white;
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
            if (kitInventory != null) kitInventory.OnCountChanged += HandleKitChanged;
            if (kitEquipper != null) kitEquipper.OnEquippedChanged += HandleKitEquippedChanged;
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
            if (kitInventory != null) kitInventory.OnCountChanged -= HandleKitChanged;
            if (kitEquipper != null) kitEquipper.OnEquippedChanged -= HandleKitEquippedChanged;
        }

        // ── 핸들러 ────────────────────────────────────────────────
        private void HandleHpChanged(float cur, float max) => RefreshHp();
        private void HandleWaveStarted(int wave) => RefreshWave();
        private void HandleKitChanged(KitType type, int count) => RefreshKit();
        private void HandleKitEquippedChanged(KitType type) => RefreshKit();

        private void Update()
        {
            if (playerWeapon != null && reloadText != null)
            {
                if (playerWeapon.IsReloading) reloadText.text = "RELOADING...";
                else if (reloadText.text != "") reloadText.text = "";
            }
            if (session != null && session.State.CurrentPhase == GamePhase.Preparation)
                RefreshWave();

            RefreshEnemyCount(); // 적 수는 실시간으로 변하므로 매 프레임 갱신
        }

        private void RefreshAll()
        {
            RefreshHp();
            RefreshAmmo();
            RefreshWave();
            RefreshReload();
            RefreshKit();
            RefreshEnemyCount();
        }

        private void RefreshEnemyCount()
        {
            if (enemyCountText == null || waveManager == null) return;

            int total = waveManager.TotalToSpawn;          // 이번 웨이브 총 적 수
            int spawned = waveManager.SpawnedCount;         // 지금까지 스폰된 수
            int alive = enemySpawner != null ? enemySpawner.AliveCount : 0;

            // 남은 적 = 아직 안 나온 적 + 살아있는 적
            int remaining = Mathf.Max(0, (total - spawned) + alive);

            enemyCountText.text = $"{remaining} / {total}";
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
            waveText.text = $"Wave {session.State.CurrentWave}";
        }

        private void RefreshReload()
        {
            if (reloadText == null) return;
            reloadText.text = (playerWeapon != null && playerWeapon.IsReloading) ? "RELOADING..." : "";
        }

        private void RefreshKit()
        {
            if (kitInventory == null || kitText == null) return;

            KitType equipped = kitEquipper != null ? kitEquipper.EquippedKit : KitType.None;

            // 장착 중인 키트는 [대괄호]로 강조
            string heal   = Format("회복", kitInventory.HealKitCount,   equipped == KitType.HealKit);
            string repair = Format("수리", kitInventory.RepairKitCount, equipped == KitType.RepairKit);
            string farm   = Format("밭",   kitInventory.FarmKitCount,   equipped == KitType.FarmKit);

            string hint = equipped == KitType.None ? "  (3번키로 키트 장착)" : "  (좌클릭 사용)";
            kitText.text = $"{heal}   {repair}   {farm}{hint}";
        }

        private static string Format(string label, int count, bool equipped)
        {
            string body = $"{label}:{count}";
            return equipped ? $"▶[{body}]" : body;
        }
    }
}
