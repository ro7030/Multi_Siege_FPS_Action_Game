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
    /// 인게임 HUD. 로컬 플레이어 체력 1칸 + 팀원/더미 등 추가 체력 슬롯을 인스펙터에서 연결한다.
    /// </summary>
    public class HUDPresenter : MonoBehaviour
    {
        [Header("게임 상태 참조 (자동 탐색)")]
        [SerializeField] private WeaponController playerWeapon;
        [SerializeField] private GameSessionManager session;
        [SerializeField] private KitInventory kitInventory;
        [SerializeField] private KitEquipper kitEquipper;
        [SerializeField] private WaveManager waveManager;
        [SerializeField] private EnemySpawner enemySpawner;

        [Header("로컬 플레이어 체력 (화면 UI 1칸)")]
        [SerializeField] private HealthBarSlot playerHealthBar = new();

        [Header("팀원·더미 등 추가 체력 슬롯 (칸마다 Health + Fill 연결)")]
        [SerializeField] private HealthBarSlot[] teamHealthBars;

        [Header("UI 요소 — 비워두면 자동 생성")]
        [SerializeField] private TMP_Text waveText;
        [SerializeField] private TMP_Text enemyCountText;
        [SerializeField] private TMP_Text ammoText;
        [SerializeField] private TMP_Text reloadText;
        [SerializeField] private TMP_Text kitText;

        [Header("자동 생성 옵션")]
        [SerializeField] private bool autoBuildMissing = true;

        private void Awake()
        {
            if (playerHealthBar.health == null)
                playerHealthBar.health = LocalPlayerUtility.FindLocalHealthSystem();

            if (playerWeapon == null) playerWeapon = LocalPlayerUtility.FindLocalWeaponController();
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
            if (kitInventory == null) kitInventory = FindAnyObjectByType<KitInventory>();
            if (kitEquipper == null) kitEquipper = FindAnyObjectByType<KitEquipper>();
            if (waveManager == null) waveManager = FindAnyObjectByType<WaveManager>();
            if (enemySpawner == null) enemySpawner = FindAnyObjectByType<EnemySpawner>();

            ResolveTeamBarUiFromChildren();
        }

        private void Start()
        {
            if (autoBuildMissing) BuildMissingElements();
            BindEvents();
            RefreshAll();
        }

        /// <summary>팀 슬롯에 UI만 비어 있으면 HUD 자식 DummyHpBg 등을 0번 슬롯에 연결.</summary>
        private void ResolveTeamBarUiFromChildren()
        {
            if (teamHealthBars == null || teamHealthBars.Length == 0) return;

            var slot = teamHealthBars[0];
            if (slot == null || slot.fillImage != null) return;

            var barRoot = transform.Find("DummyHpBg") ?? transform.Find("HpBg");
            if (barRoot == null) return;

            var fillT = barRoot.Find("HpFill");
            if (fillT == null) return;

            slot.root = barRoot.gameObject;
            slot.fillImage = fillT.GetComponent<Image>();
            if (slot.labelText == null)
                slot.labelText = barRoot.GetComponentInChildren<TMP_Text>(true);
        }

        private void BuildMissingElements()
        {
            if (UIRoot.Instance == null)
            {
                Debug.LogWarning("[HUD] UIRoot.Instance == null — 직접 연결한 UI만 사용");
                return;
            }
            var root = UIRoot.Instance.RootTransform;
            if (root == null) return;

            if (waveText == null)
            {
                waveText = UIRoot.CreateText("WaveText", root, 28, TextAnchor.UpperLeft);
                Anchor(waveText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -20), new Vector2(360, -60));
                waveText.color = new Color(1, 0.9f, 0.4f);
            }

            if (enemyCountText == null)
            {
                enemyCountText = UIRoot.CreateText("EnemyCountText", root, 32, TextAnchor.UpperLeft);
                Anchor(enemyCountText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -64), new Vector2(360, -110));
                enemyCountText.color = new Color(1f, 0.5f, 0.5f);
                enemyCountText.fontStyle = FontStyles.Bold;
            }

            if (!playerHealthBar.HasFill || playerHealthBar.labelText == null)
                BuildPlayerHealthBarUi(root);

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
                var kitBg = UIRoot.CreatePanel("KitBg", root, new Color(0, 0, 0, 0.55f));
                Anchor(kitBg.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-260, 20), new Vector2(260, 56));
                kitText = UIRoot.CreateText("KitText", kitBg.rectTransform, 20, TextAnchor.MiddleCenter);
                Anchor(kitText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                kitText.color = Color.white;
            }
        }

        private void BuildPlayerHealthBarUi(RectTransform root)
        {
            var hpBg = UIRoot.CreatePanel("PlayerHpBg", root, new Color(0, 0, 0, 0.55f));
            Anchor(hpBg.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 20), new Vector2(420, 60));
            playerHealthBar.root = hpBg.gameObject;

            if (!playerHealthBar.HasFill)
            {
                playerHealthBar.fillImage = UIRoot.CreatePanel("HpFill", hpBg.rectTransform, new Color(0.85f, 0.25f, 0.25f, 0.95f));
                playerHealthBar.fillImage.type = Image.Type.Filled;
                playerHealthBar.fillImage.fillMethod = Image.FillMethod.Horizontal;
                playerHealthBar.fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                Anchor(playerHealthBar.fillImage.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }

            if (playerHealthBar.labelText == null)
            {
                playerHealthBar.labelText = UIRoot.CreateText("HpText", hpBg.rectTransform, 22);
                Anchor(playerHealthBar.labelText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }

            if (string.IsNullOrWhiteSpace(playerHealthBar.displayName))
                playerHealthBar.displayName = "HP";
        }

        private static void Anchor(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
        }

        private void BindEvents()
        {
            playerHealthBar.Bind(RefreshPlayerHp);
            BindTeamBars();

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
            playerHealthBar.Unbind();
            UnbindTeamBars();

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

        private void BindTeamBars()
        {
            if (teamHealthBars == null) return;
            foreach (var slot in teamHealthBars)
            {
                if (slot == null) continue;
                slot.Bind(RefreshTeamBars);
            }
        }

        private void UnbindTeamBars()
        {
            if (teamHealthBars == null) return;
            foreach (var slot in teamHealthBars)
                slot?.Unbind();
        }

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

            RefreshEnemyCount();
        }

        private void RefreshAll()
        {
            RefreshPlayerHp();
            RefreshTeamBars();
            RefreshAmmo();
            RefreshWave();
            RefreshReload();
            RefreshKit();
            RefreshEnemyCount();
        }

        private void RefreshPlayerHp() => playerHealthBar.Refresh();

        private void RefreshTeamBars()
        {
            if (teamHealthBars == null) return;
            foreach (var slot in teamHealthBars)
                slot?.Refresh();
        }

        private void RefreshEnemyCount()
        {
            if (enemyCountText == null || waveManager == null) return;

            int total = waveManager.TotalToSpawn;
            int spawned = waveManager.SpawnedCount;
            int alive = enemySpawner != null ? enemySpawner.AliveCount : 0;
            int remaining = Mathf.Max(0, (total - spawned) + alive);

            enemyCountText.text = $"{remaining} / {total}";
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

            string heal = Format("회복", kitInventory.HealKitCount, equipped == KitType.HealKit);
            string repair = Format("수리", kitInventory.RepairKitCount, equipped == KitType.RepairKit);
            string farm = Format("밭", kitInventory.FarmKitCount, equipped == KitType.FarmKit);

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
