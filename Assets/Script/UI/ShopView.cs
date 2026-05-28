using System;
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
    /// 상점 UI.
    /// · 상단/하단 탭: 카테고리 전환만
    /// · 왼쪽 목록: 이름만, 클릭 시 선택
    /// · 오른쪽 상세: 이름·가격·스프라이트(Image) + 구매 버튼으로만 구매
    /// </summary>
    public class ShopView : MonoBehaviour
    {
        [Header("참조 (비우면 자동 탐색)")]
        [SerializeField] private ShopController shop;
        [SerializeField] private CurrencyWallet wallet;
        [SerializeField] private GameSessionManager session;
        [SerializeField] private PlayerArsenal arsenal;
        [SerializeField] private WeaponProgression weaponProgression;
        [SerializeField] private Key toggleKey = Key.H;

        [Header("UI 루트")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text balanceText;

        [Header("상단 대분류 (순서: 무기/킷/성)")]
        [SerializeField] private Button[] topTabButtons = new Button[3];

        [Header("하단 소분류 (최대 4칸) — 카테고리 전환만")]
        [SerializeField] private Button[] subTabButtons = new Button[4];
        [SerializeField] private TMP_Text[] subTabLabels = new TMP_Text[4];

        [Header("왼쪽 목록 (최대 4칸) — 이름만")]
        [SerializeField] private Button[] itemRowButtons = new Button[4];
        [SerializeField] private TMP_Text[] itemRowLabels = new TMP_Text[4];

        [Header("오른쪽 상세 패널")]
        [SerializeField] private TMP_Text detailNameText;
        [SerializeField] private TMP_Text detailPriceText;
        [SerializeField] private TMP_Text detailDescriptionText;
        [Tooltip("상세 썸네일 — WeaponDefinition.icon / ItemData.listIcon")]
        [SerializeField] private Image detailIconImage;
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text buyButtonLabel;

        [Header("상세 문구")]
        [SerializeField] private string priceFormat = "{0}";
        [SerializeField] private string ownedLabel = "보유 중";
        [SerializeField] private string lockedLabel = "잠김";
        [SerializeField] private string insufficientLabel = "잔액 부족";

        [Header("선택 강조 (이미지 명도)")]
        [Tooltip("선택된 버튼 이미지의 명도 (1 = 100%, 0.85 = 85%)")]
        [Range(0f, 1f)]
        [SerializeField] private float selectedBrightness = 0.85f;
        [Tooltip("선택되지 않은 버튼 이미지의 명도 (보통 1)")]
        [Range(0f, 1f)]
        [SerializeField] private float unselectedBrightness = 1f;

        private ShopTopTab currentTop = ShopTopTab.Weapon;
        private ShopSubTab currentSub = ShopSubTab.Gun;
        private readonly List<ShopEntry> currentEntries = new();
        private int selectedIndex = -1;
        // 각 버튼 이미지의 원래(=명도 100%) 색을 처음 본 시점에 기억해두고
        // 명도 변환은 항상 이 기준 색에 selectedBrightness/unselectedBrightness 를 곱해서 적용.
        private readonly Dictionary<Image, Color> originalImageColors = new();
        private bool isOpen;

        private void Awake()
        {
            if (shop == null) shop = FindAnyObjectByType<ShopController>();
            if (wallet == null) wallet = LocalPlayerUtility.FindLocalCurrencyWallet();
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
            if (arsenal == null) arsenal = FindAnyObjectByType<PlayerArsenal>();
            if (weaponProgression == null && arsenal != null) weaponProgression = arsenal.Progression;

            if (panelRoot == null) panelRoot = gameObject;
            TryAutoWire();
            EnsureShopUiBuilt();
            BindTopTabs();
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (buyButton != null) buyButton.onClick.AddListener(OnBuyClicked);
        }

        private void Start()
        {
            if (shop != null)
            {
                shop.OnPurchased += _ => RefreshAll();
                shop.OnPurchaseFailed += (_, __) => RefreshAll();
            }
            if (wallet != null) wallet.OnChanged += _ => RefreshAll();
            if (session != null) session.OnWaveStarted += _ => { if (isOpen) Hide(); };

            Hide();
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
            if (session != null && session.State.CurrentPhase != GamePhase.Preparation)
            {
                Debug.Log("[Shop] 정비 시간에만 상점을 열 수 있습니다.");
                return;
            }

            panelRoot.SetActive(true);
            isOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SelectTopTab(ShopTopTab.Weapon);
        }

        public void Hide()
        {
            if (panelRoot == null) return;
            panelRoot.SetActive(false);
            isOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // ── 탭 (구매 없음, 목록만 갱신) ───────────────────────────

        private void SelectTopTab(ShopTopTab top)
        {
            currentTop = top;
            var subs = ShopCatalogBuilder.GetSubTabsForTop(top);
            ApplySubTabBar(subs);
            currentSub = subs.Count > 0 ? subs[0] : ShopSubTab.Gun;
            RefreshAll();
        }

        private void SelectSubTab(ShopSubTab sub)
        {
            currentSub = sub;
            RefreshAll();
        }

        private void ApplySubTabBar(IReadOnlyList<ShopSubTab> subs)
        {
            for (int i = 0; i < subTabButtons.Length; i++)
            {
                var btn = subTabButtons[i];
                if (btn == null) continue;

                bool active = i < subs.Count;
                btn.gameObject.SetActive(active);
                if (!active) continue;

                var sub = subs[i];
                if (i < subTabLabels.Length && subTabLabels[i] != null)
                    subTabLabels[i].text = ShopCatalogBuilder.GetSubTabLabel(sub);

                btn.onClick.RemoveAllListeners();
                var captured = sub;
                btn.onClick.AddListener(() => SelectSubTab(captured));
            }
        }

        // ── 목록·상세·구매 ────────────────────────────────────────

        private void RefreshAll()
        {
            if (!isOpen) return;

            if (balanceText != null && wallet != null)
                balanceText.text = wallet.Balance.ToString();

            currentEntries.Clear();
            currentEntries.AddRange(ShopCatalogBuilder.BuildEntries(
                currentTop, currentSub, shop, wallet, weaponProgression, arsenal));

            if (currentEntries.Count == 0)
            {
                selectedIndex = -1;
                RefreshItemList();
                ClearDetailPanel();
                RefreshSelectionBrightness();
                return;
            }

            if (selectedIndex < 0 || selectedIndex >= currentEntries.Count)
                selectedIndex = 0;

            RefreshItemList();
            RefreshDetailPanel();
            RefreshSelectionBrightness();
        }

        // ── 선택 강조 (이미지 명도) ───────────────────────────────
        // 카테고리 3종(top/sub/item) 독립 그룹.
        // 각 그룹 내에서 currentTop / currentSub / selectedIndex 와 일치하는 버튼만 어둡게 처리.
        private void RefreshSelectionBrightness()
        {
            // top tab: 인덱스 순서가 BindTopTabs 의 tops 배열과 동일 (Weapon, Kit, Base).
            var tops = new[] { ShopTopTab.Weapon, ShopTopTab.Kit, ShopTopTab.Base };
            for (int i = 0; i < topTabButtons.Length; i++)
            {
                bool selected = i < tops.Length && tops[i] == currentTop;
                ApplyButtonBrightness(topTabButtons[i], selected);
            }

            // sub tab: 현재 top 의 sub 목록을 그대로 인덱스 순서로 매핑.
            var subs = ShopCatalogBuilder.GetSubTabsForTop(currentTop);
            for (int i = 0; i < subTabButtons.Length; i++)
            {
                bool selected = i < subs.Count && subs[i] == currentSub;
                ApplyButtonBrightness(subTabButtons[i], selected);
            }

            // item row: 현재 표시 중인 항목 중 selectedIndex 만 강조.
            for (int i = 0; i < itemRowButtons.Length; i++)
            {
                bool selected = i < currentEntries.Count && i == selectedIndex;
                ApplyButtonBrightness(itemRowButtons[i], selected);
            }
        }

        private void ApplyButtonBrightness(Button btn, bool selected)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img == null) return;

            if (!originalImageColors.TryGetValue(img, out var baseColor))
            {
                baseColor = img.color;
                originalImageColors[img] = baseColor;
            }

            float k = selected ? selectedBrightness : unselectedBrightness;
            k = Mathf.Clamp01(k);
            img.color = new Color(baseColor.r * k, baseColor.g * k, baseColor.b * k, baseColor.a);
        }

        private void RefreshItemList()
        {
            for (int i = 0; i < itemRowButtons.Length; i++)
            {
                var btn = itemRowButtons[i];
                if (btn == null) continue;

                bool show = i < currentEntries.Count;
                btn.gameObject.SetActive(show);

                if (!show) continue;

                var entry = currentEntries[i];
                if (i < itemRowLabels.Length && itemRowLabels[i] != null)
                {
                    // 색은 건드리지 않고 인스펙터(TMP_Text 컴포넌트)에 설정된 값을 그대로 유지.
                    itemRowLabels[i].text = entry.displayName;
                }

                btn.interactable = true;
                btn.onClick.RemoveAllListeners();
                int capturedIndex = i;
                btn.onClick.AddListener(() => SelectItem(capturedIndex));
            }
        }

        private void SelectItem(int index)
        {
            if (index < 0 || index >= currentEntries.Count) return;
            selectedIndex = index;
            RefreshItemList();
            RefreshDetailPanel();
        }

        private void RefreshDetailPanel()
        {
            if (selectedIndex < 0 || selectedIndex >= currentEntries.Count)
            {
                ClearDetailPanel();
                return;
            }

            var entry = currentEntries[selectedIndex];

            if (detailNameText != null)
                detailNameText.text = entry.displayName;

            if (detailPriceText != null)
            {
                if (!entry.unlocked)
                    detailPriceText.text = lockedLabel;
                else
                    detailPriceText.text = string.Format(priceFormat, entry.price);
            }

            if (detailDescriptionText != null)
            {
                detailDescriptionText.text = string.IsNullOrEmpty(entry.description) ? "" : entry.description;
                detailDescriptionText.gameObject.SetActive(!string.IsNullOrEmpty(entry.description));
            }

            ApplyDetailVisual(entry);

            if (buyButton != null)
            {
                bool canBuy = entry.canPurchase && entry.affordable;
                buyButton.interactable = canBuy;

                if (buyButtonLabel != null)
                {
                    if (entry.isOwned)
                        buyButtonLabel.text = ownedLabel;
                    else if (!entry.unlocked)
                        buyButtonLabel.text = lockedLabel;
                    else if (entry.canPurchase && !entry.affordable)
                        buyButtonLabel.text = insufficientLabel;
                    else
                        buyButtonLabel.text = "구매";
                }
            }
        }

        private void ApplyDetailVisual(ShopEntry entry)
        {
            if (detailIconImage == null) return;

            bool hasIcon = entry.icon != null;
            detailIconImage.gameObject.SetActive(hasIcon);
            if (hasIcon)
            {
                detailIconImage.sprite = entry.icon;
                detailIconImage.preserveAspect = true;
            }
        }

        private void ClearDetailPanel()
        {
            if (detailNameText != null) detailNameText.text = "";
            if (detailPriceText != null) detailPriceText.text = "";
            if (detailDescriptionText != null) detailDescriptionText.text = "";
            if (detailIconImage != null)
            {
                detailIconImage.sprite = null;
                detailIconImage.gameObject.SetActive(false);
            }
            if (buyButton != null) buyButton.interactable = false;
            if (buyButtonLabel != null) buyButtonLabel.text = "구매";
        }

        private void OnBuyClicked()
        {
            TryPurchaseSelected();
            RefreshAll();
        }

        private void TryPurchaseSelected()
        {
            if (shop == null || selectedIndex < 0 || selectedIndex >= currentEntries.Count) return;

            var entry = currentEntries[selectedIndex];
            switch (entry.kind)
            {
                case ShopEntryKind.CatalogItem when entry.catalogItem != null:
                    shop.TryPurchase(entry.catalogItem.id);
                    break;
                case ShopEntryKind.WeaponTier:
                    shop.TryPurchaseWeaponTier(entry.weaponSlot, entry.weaponTierIndex);
                    break;
            }
        }

        // ── 바인딩 ───────────────────────────────────────────────

        private void BindTopTabs()
        {
            var tops = new[] { ShopTopTab.Weapon, ShopTopTab.Kit, ShopTopTab.Base };
            for (int i = 0; i < topTabButtons.Length && i < tops.Length; i++)
            {
                var btn = topTabButtons[i];
                if (btn == null) continue;
                var captured = tops[i];
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => SelectTopTab(captured));
            }
        }

        private void EnsureShopUiBuilt()
        {
            if (HasBuiltShopUi()) return;
            if (UIRoot.Instance == null)
            {
                Debug.LogWarning("[Shop] UIRoot 가 없어 상점 UI 를 만들 수 없습니다.");
                return;
            }

            var panelRt = panelRoot != null
                ? panelRoot.GetComponent<RectTransform>()
                : GetComponent<RectTransform>();
            if (panelRt == null) return;

            var built = ShopUiBuilder.Build(panelRt);
            ApplyBuiltRefs(built);
            Debug.Log("[Shop] 씬에 UI 가 없어 상점 패널을 런타임에 생성했습니다.");
        }

        private bool HasBuiltShopUi()
        {
            return closeButton != null
                   && buyButton != null
                   && !IsEmpty(topTabButtons)
                   && !IsEmpty(itemRowButtons);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 메뉴: ShopPanel 자식(탭·카테고리·목록·상세)을 만들고 인스펙터에 연결한다.
        /// </summary>
        public void EditorRebuildPanelUi()
        {
            panelRoot = gameObject;
            var panelRt = GetComponent<RectTransform>();
            if (panelRt == null) return;

            for (int i = panelRt.childCount - 1; i >= 0; i--)
                DestroyImmediate(panelRt.GetChild(i).gameObject);

            panelRt.sizeDelta = new Vector2(1100, 680);
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;

            var bg = GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.07f, 0.12f, 0.94f);

            ApplyBuiltRefs(ShopUiBuilder.Build(panelRt));
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private void ApplyBuiltRefs(ShopUiBuilder.BuiltRefs built)
        {
            panelRoot = built.panelRoot;
            closeButton = built.closeButton;
            balanceText = built.balanceText;
            topTabButtons = built.topTabButtons;
            subTabButtons = built.subTabButtons;
            subTabLabels = built.subTabLabels;
            itemRowButtons = built.itemRowButtons;
            itemRowLabels = built.itemRowLabels;
            detailNameText = built.detailNameText;
            detailPriceText = built.detailPriceText;
            detailDescriptionText = built.detailDescriptionText;
            detailIconImage = built.detailIconImage;
            buyButton = built.buyButton;
            buyButtonLabel = built.buyButtonLabel;
        }

        private void TryAutoWire()
        {
            if (panelRoot == null) panelRoot = gameObject;
            var root = panelRoot.transform;

            if (closeButton == null)
                closeButton = FindButton(root, "Close", "CloseButton", "Btn_Close");

            if (balanceText == null)
                balanceText = FindTmp(root, "Balance", "CoinText", "Currency", "BalanceText");

            if (buyButton == null)
                buyButton = FindButton(root, "Buy", "BuyButton", "Btn_Buy");

            if (buyButton != null && buyButtonLabel == null)
                buyButtonLabel = buyButton.GetComponentInChildren<TMP_Text>(true);

            if (detailNameText == null)
                detailNameText = FindTmp(root, "DetailTitle", "DetailName", "ItemName");

            if (detailPriceText == null)
                detailPriceText = FindTmp(root, "DetailPrice", "PriceText");

            if (detailDescriptionText == null)
                detailDescriptionText = FindTmp(root, "DetailDesc", "DetailDescription");

            if (detailIconImage == null)
                detailIconImage = FindImage(root, "DetailIcon", "ItemIcon", "ItemPreview", "WeaponPreview");

            if (IsEmpty(topTabButtons))
                topTabButtons = FindButtonsInOrder(root,
                    new[] { "TopTab_Weapon", "TopTab_Kit", "TopTab_Base" },
                    3, new[] { "TopIconTabs", "TopTabs" });

            if (IsEmpty(subTabButtons))
                subTabButtons = FindButtonsInOrder(root,
                    new[] { "SubTab_0", "SubTab_1", "SubTab_2", "SubTab_3" },
                    4, new[] { "CategoryTabs", "SubTabs", "SubCategoryTabs" });

            if (IsEmpty(itemRowButtons))
                itemRowButtons = FindButtonsInOrder(root,
                    new[] { "ItemRow_0", "ItemRow_1", "ItemRow_2", "ItemRow_3" },
                    4, new[] { "ItemList", "ItemRows", "LeftColumn" });

            FillLabelsFromButtons(itemRowButtons, ref itemRowLabels);
            FillLabelsFromButtons(subTabButtons, ref subTabLabels);
        }

        private static bool IsEmpty(Button[] arr)
        {
            if (arr == null || arr.Length == 0) return true;
            foreach (var b in arr) if (b != null) return false;
            return true;
        }

        private static void FillLabelsFromButtons(Button[] buttons, ref TMP_Text[] labels)
        {
            if (buttons == null) return;
            if (labels != null && labels.Length == buttons.Length && HasAny(labels)) return;

            labels = new TMP_Text[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
                labels[i] = buttons[i] != null ? buttons[i].GetComponentInChildren<TMP_Text>(true) : null;
        }

        private static bool HasAny<T>(T[] arr) where T : UnityEngine.Object
        {
            if (arr == null) return false;
            foreach (var x in arr) if (x != null) return true;
            return false;
        }

        private static Button[] FindButtonsInOrder(Transform root, string[] directNames, int count, string[] parentNames)
        {
            var list = new List<Button>(count);
            foreach (var n in directNames)
            {
                if (list.Count >= count) break;
                var t = FindDeepChild(root, n);
                if (t != null)
                {
                    var b = t.GetComponent<Button>();
                    if (b != null && !list.Contains(b)) list.Add(b);
                }
            }

            if (list.Count < count)
            {
                foreach (var parentName in parentNames)
                {
                    var parent = FindDeepChild(root, parentName);
                    if (parent == null) continue;
                    foreach (var b in parent.GetComponentsInChildren<Button>(true))
                    {
                        if (list.Count >= count) break;
                        if (!list.Contains(b)) list.Add(b);
                    }
                    if (list.Count >= count) break;
                }
            }

            while (list.Count < count) list.Add(null);
            return list.ToArray();
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var found = FindDeepChild(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static Button FindButton(Transform root, params string[] names)
        {
            foreach (var n in names)
            {
                var t = FindDeepChild(root, n);
                if (t != null)
                {
                    var b = t.GetComponent<Button>();
                    if (b != null) return b;
                }
            }
            return null;
        }

        private static TMP_Text FindTmp(Transform root, params string[] names)
        {
            foreach (var n in names)
            {
                var t = FindDeepChild(root, n);
                if (t != null)
                {
                    var tmp = t.GetComponent<TMP_Text>();
                    if (tmp != null) return tmp;
                }
            }
            return null;
        }

        private static Image FindImage(Transform root, params string[] names)
        {
            foreach (var n in names)
            {
                var t = FindDeepChild(root, n);
                if (t != null)
                {
                    var img = t.GetComponent<Image>();
                    if (img != null) return img;
                }
            }
            return null;
        }
    }
}
