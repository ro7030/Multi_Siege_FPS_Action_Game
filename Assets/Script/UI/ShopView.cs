using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using ProjectM.Core;
using ProjectM.Economy;
using ProjectM.Player;

namespace ProjectM.UI
{
    /// <summary>
    /// 상점 UI. H키로 토글, 정비 시간(Preparation)에만 열림, 웨이브 시작 시 자동 종료.
    ///
    /// 사용법
    ///   1) 슬롯을 비우면 코드가 자동 생성 (이때 Font 필드로 폰트 지정 가능)
    ///   2) Panel Root 등을 직접 연결하면 그 디자인을 사용
    /// </summary>
    public class ShopView : MonoBehaviour
    {
        [Header("참조 (자동 탐색)")]
        [SerializeField] private ShopController shop;
        [SerializeField] private CurrencyWallet wallet;
        [SerializeField] private GameSessionManager session;
        [SerializeField] private Key toggleKey = Key.H;

        [Header("폰트 (자동 생성 시 적용. 비우면 TMP 기본)")]
        [SerializeField] private TMP_FontAsset font;

        [Header("직접 만든 UI (비우면 자동 생성)")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private RectTransform listParent;
        [Tooltip("아이템 버튼 프리팹 (Button + 자식 TMP_Text). 비우면 자동 생성.")]
        [SerializeField] private Button itemButtonPrefab;
        [SerializeField] private Button closeButton;

        [Header("헤더 문구")]
        [SerializeField] private string headerFormat = "상점   ₩ {balance}";

        private bool isOpen;
        private readonly List<Button> spawnedButtons = new();

        private void Awake()
        {
            if (shop == null) shop = FindAnyObjectByType<ShopController>();
            if (wallet == null) wallet = FindAnyObjectByType<CurrencyWallet>();
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
        }

        private void Start()
        {
            BuildMissing();
            if (panelRoot == null) { enabled = false; return; }

            if (closeButton != null) closeButton.onClick.AddListener(Hide);

            if (shop != null)
            {
                shop.OnPurchased += _ => Refresh();
                shop.OnPurchaseFailed += (_, __) => Refresh();
            }
            if (wallet != null) wallet.OnChanged += _ => Refresh();
            if (session != null) session.OnWaveStarted += HandleWaveStarted;

            Hide();
        }

        private void OnDisable()
        {
            if (session != null) session.OnWaveStarted -= HandleWaveStarted;
        }

        private void HandleWaveStarted(int _)
        {
            if (isOpen) Hide();
        }

        private bool CanOpenShop()
        {
            if (session == null) return true;
            return session.State.CurrentPhase == GamePhase.Preparation;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb[toggleKey].wasPressedThisFrame) Toggle();
            if (isOpen && kb.escapeKey.wasPressedThisFrame) Hide();
        }

        public void Toggle()
        {
            if (isOpen) Hide();
            else Show();
        }

        public void Show()
        {
            if (panelRoot == null) return;
            if (!CanOpenShop())
            {
                Debug.Log("[Shop] 정비 시간에만 상점을 열 수 있습니다.");
                return;
            }
            panelRoot.SetActive(true);
            isOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Refresh();
        }

        public void Hide()
        {
            if (panelRoot == null) return;
            panelRoot.SetActive(false);
            isOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // ── 자동 생성 (슬롯 비어있을 때만) ────────────────────────────
        private void BuildMissing()
        {
            if (panelRoot != null) return; // 직접 연결됨 → 자동 생성 안 함
            if (UIRoot.Instance == null)
            {
                Debug.LogWarning("[Shop] UIRoot.Instance 없음 — 직접 UI 연결 필요");
                return;
            }

            var root = UIRoot.Instance.RootTransform;
            var bg = UIRoot.CreatePanel("ShopPanel", root, new Color(0.05f, 0.07f, 0.12f, 0.92f));
            panelRoot = bg.gameObject;
            var rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(540, 640);
            rt.anchoredPosition = Vector2.zero;

            // 헤더
            headerText = UIRoot.CreateText("Header", rt, 28, TextAnchor.UpperCenter);
            ApplyFont(headerText);
            headerText.fontStyle = FontStyles.Bold;
            headerText.color = new Color(1, 0.9f, 0.4f);
            var hrt = headerText.rectTransform;
            hrt.anchorMin = new Vector2(0, 1); hrt.anchorMax = new Vector2(1, 1);
            hrt.offsetMin = new Vector2(20, -70); hrt.offsetMax = new Vector2(-20, -10);

            // 리스트 컨테이너
            var listGo = UIRoot.CreateChild("List", rt);
            listParent = listGo.GetComponent<RectTransform>();
            listParent.anchorMin = new Vector2(0, 0); listParent.anchorMax = new Vector2(1, 1);
            listParent.offsetMin = new Vector2(20, 70); listParent.offsetMax = new Vector2(-20, -80);
            var vlg = listGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlHeight = false; vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = true;

            // 닫기 버튼
            closeButton = UIRoot.CreateButton("CloseBtn", rt, "닫기 (H)");
            ApplyFont(closeButton.GetComponentInChildren<TMP_Text>());
            var crt = closeButton.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0); crt.anchorMax = new Vector2(0.5f, 0);
            crt.pivot = new Vector2(0.5f, 0); crt.sizeDelta = new Vector2(160, 44);
            crt.anchoredPosition = new Vector2(0, 16);
        }

        // ── 갱신 ──────────────────────────────────────────────────
        private void Refresh()
        {
            if (!isOpen) return;

            if (headerText != null)
            {
                ApplyFont(headerText);
                int bal = wallet != null ? wallet.Balance : 0;
                int wave = shop != null ? shop.CurrentWave : 0;
                headerText.text = headerFormat.Replace("{balance}", bal.ToString()).Replace("{wave}", wave.ToString());
            }

            foreach (var b in spawnedButtons) if (b != null) Destroy(b.gameObject);
            spawnedButtons.Clear();

            if (shop == null) return;

            AddWeaponUpgradeButton(WeaponSlot.Primary, "주무기");
            AddWeaponUpgradeButton(WeaponSlot.Secondary, "보조무기");

            if (shop.Catalog == null) return;
            foreach (var item in shop.Catalog.items)
            {
                if (item == null) continue;
                var (btn, lbl) = SpawnButton();
                bool unlocked = shop.IsUnlocked(item);
                bool affordable = wallet != null && wallet.Balance >= item.price;

                if (lbl != null)
                {
                    lbl.text = $"{item.displayName}    ₩{item.price}    {(unlocked ? "" : $"[Wave {item.unlockWave}+]")}";
                    lbl.color = unlocked ? (affordable ? Color.white : new Color(1f, 0.5f, 0.5f)) : new Color(0.6f, 0.6f, 0.6f);
                }

                var capturedId = item.id;
                btn.interactable = unlocked && affordable;
                btn.onClick.AddListener(() => shop.TryPurchase(capturedId));
            }
        }

        private void AddWeaponUpgradeButton(WeaponSlot slot, string label)
        {
            var (btn, lbl) = SpawnButton();

            if (shop.CanUpgradeWeapon(slot))
            {
                int price = shop.WeaponUpgradePrice(slot);
                string nm = shop.WeaponUpgradeName(slot);
                bool affordable = wallet != null && wallet.Balance >= price;

                if (lbl != null)
                {
                    lbl.text = $"{label} ▶ {nm}    ₩{price}";
                    lbl.color = affordable ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 0.5f, 0.5f);
                }
                btn.interactable = affordable;
                var capturedSlot = slot;
                btn.onClick.AddListener(() => shop.TryUpgradeWeapon(capturedSlot));
            }
            else
            {
                if (lbl != null) { lbl.text = $"{label}   [MAX]"; lbl.color = new Color(0.6f, 0.6f, 0.6f); }
                btn.interactable = false;
            }
        }

        /// <summary>버튼 생성: 프리팹 있으면 복제, 없으면 자동 생성. 폰트 적용.</summary>
        private (Button, TMP_Text) SpawnButton()
        {
            Button btn;
            if (itemButtonPrefab != null)
            {
                btn = Instantiate(itemButtonPrefab, listParent);
            }
            else
            {
                btn = UIRoot.CreateButton("ShopBtn", listParent, "");
                var rt = btn.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(0, 48);
            }
            btn.gameObject.SetActive(true);
            var lbl = btn.GetComponentInChildren<TMP_Text>();
            ApplyFont(lbl);
            spawnedButtons.Add(btn);
            return (btn, lbl);
        }

        /// <summary>Font 가 지정돼 있으면 적용. 비어있으면 기존 폰트 유지.</summary>
        private void ApplyFont(TMP_Text text)
        {
            if (text != null && font != null) text.font = font;
        }
    }
}
