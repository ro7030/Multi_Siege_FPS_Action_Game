using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ProjectM.Core;
using ProjectM.Economy;

namespace ProjectM.UI
{
    /// <summary>
    /// 상점 UI 패널. H키로 토글. 정비 시간(Preparation)에만 열 수 있다.
    /// 웨이브가 시작되면 자동으로 닫힌다. 열려 있는 동안 커서 자유, 닫으면 다시 잠금.
    /// 카탈로그 항목을 버튼으로 나열, 클릭 시 ShopController.TryPurchase 호출.
    /// </summary>
    public class ShopView : MonoBehaviour
    {
        [SerializeField] private ShopController shop;
        [SerializeField] private CurrencyWallet wallet;
        [SerializeField] private GameSessionManager session;
        [SerializeField] private Key toggleKey = Key.H;

        private GameObject panelGo;
        private RectTransform listParent;
        private Text headerText;
        private bool isOpen;

        private readonly List<Button> itemButtons = new();
        private readonly List<Text> itemLabels = new();
        private readonly List<ItemData> currentItems = new();

        private void Awake()
        {
            if (shop == null) shop = FindAnyObjectByType<ShopController>();
            if (wallet == null) wallet = FindAnyObjectByType<CurrencyWallet>();
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
        }

        private void Start()
        {
            if (UIRoot.Instance == null) { enabled = false; return; }
            BuildPanel();
            Hide();

            if (shop != null)
            {
                shop.OnPurchased += _ => Refresh();
                shop.OnPurchaseFailed += (_, __) => Refresh();
            }
            if (wallet != null) wallet.OnChanged += _ => Refresh();
            // 웨이브가 시작되면 상점 강제 종료
            if (session != null) session.OnWaveStarted += HandleWaveStarted;
        }

        private void OnDisable()
        {
            if (session != null) session.OnWaveStarted -= HandleWaveStarted;
        }

        private void HandleWaveStarted(int _)
        {
            if (isOpen) Hide();
        }

        /// <summary>상점을 열 수 있는 시점인지 (정비 시간만).</summary>
        private bool CanOpenShop()
        {
            if (session == null) return true; // 세션 없으면 제한 없음 (테스트용)
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
            if (panelGo == null) return;
            if (!CanOpenShop())
            {
                Debug.Log("[Shop] 정비 시간에만 상점을 열 수 있습니다.");
                return;
            }
            panelGo.SetActive(true);
            isOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Refresh();
        }

        public void Hide()
        {
            if (panelGo == null) return;
            panelGo.SetActive(false);
            isOpen = false;
            // 다른 UI(결과창 등)가 떠 있지 않으면 잠금 복귀
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void BuildPanel()
        {
            var root = UIRoot.Instance.RootTransform;
            var bg = UIRoot.CreatePanel("ShopPanel", root, new Color(0.05f, 0.07f, 0.12f, 0.92f));
            panelGo = bg.gameObject;
            var rt = bg.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(540, 640);
            rt.anchoredPosition = Vector2.zero;

            // Header
            headerText = UIRoot.CreateText("Header", rt, 28, TextAnchor.UpperCenter);
            headerText.fontStyle = FontStyle.Bold;
            headerText.color = new Color(1, 0.9f, 0.4f);
            var hrt = headerText.rectTransform;
            hrt.anchorMin = new Vector2(0, 1); hrt.anchorMax = new Vector2(1, 1);
            hrt.offsetMin = new Vector2(20, -70); hrt.offsetMax = new Vector2(-20, -10);

            // List container
            var listGo = UIRoot.CreateChild("List", rt);
            listParent = listGo.GetComponent<RectTransform>();
            listParent.anchorMin = new Vector2(0, 0); listParent.anchorMax = new Vector2(1, 1);
            listParent.offsetMin = new Vector2(20, 70); listParent.offsetMax = new Vector2(-20, -80);

            var vlg = listGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            // Close button
            var closeBtn = UIRoot.CreateButton("CloseBtn", rt, "Close (H)");
            var crt = closeBtn.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0); crt.anchorMax = new Vector2(0.5f, 0);
            crt.pivot = new Vector2(0.5f, 0); crt.sizeDelta = new Vector2(160, 44);
            crt.anchoredPosition = new Vector2(0, 16);
            closeBtn.onClick.AddListener(Hide);
        }

        private void Refresh()
        {
            if (panelGo == null || !isOpen) return;
            int wave = shop != null ? shop.CurrentWave : 0;
            int bal = wallet != null ? wallet.Balance : 0;
            headerText.text = $"SHOP    (Wave {wave}   ₩ {bal})";

            // 기존 버튼 제거
            foreach (var b in itemButtons) if (b != null) Destroy(b.gameObject);
            itemButtons.Clear();
            itemLabels.Clear();
            currentItems.Clear();

            if (shop == null || shop.Catalog == null) return;

            foreach (var item in shop.Catalog.items)
            {
                if (item == null) continue;
                currentItems.Add(item);

                var btn = UIRoot.CreateButton($"Btn_{item.id}", listParent, "");
                var rt = btn.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(0, 48);

                var lbl = btn.GetComponentInChildren<Text>();
                bool unlocked = shop.IsUnlocked(item);
                bool affordable = wallet != null && wallet.Balance >= item.price;
                lbl.text = $"{item.displayName}    ₩{item.price}    {(unlocked ? "" : $"[Wave {item.unlockWave}+]")}";
                lbl.color = unlocked ? (affordable ? Color.white : new Color(1f, 0.5f, 0.5f)) : new Color(0.6f, 0.6f, 0.6f);

                var capturedId = item.id;
                btn.interactable = unlocked && affordable;
                btn.onClick.AddListener(() => { shop.TryPurchase(capturedId); });

                itemButtons.Add(btn);
                itemLabels.Add(lbl);
            }
        }
    }
}
