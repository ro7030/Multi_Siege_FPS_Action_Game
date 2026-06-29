using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProjectM.UI
{
    /// <summary>
    /// ShopView 용 화면 UI를 런타임에 구성한다. (씬 ShopPanel 자식이 비어 있을 때)
    /// </summary>
    public static class ShopUiBuilder
    {
        public struct BuiltRefs
        {
            public GameObject panelRoot;
            public Button closeButton;
            public TMP_Text balanceText;
            public Button[] topTabButtons;
            public Button[] subTabButtons;
            public TMP_Text[] subTabLabels;
            public Button[] itemRowButtons;
            public TMP_Text[] itemRowLabels;
            public TMP_Text detailNameText;
            public TMP_Text detailPriceText;
            public TMP_Text detailDescriptionText;
            public Image detailIconImage;
            public Button buyButton;
            public TMP_Text buyButtonLabel;
        }

        public static BuiltRefs Build(RectTransform panelRt)
        {
            var refs = new BuiltRefs { panelRoot = panelRt.gameObject };

            SetupPanelFrame(panelRt);

            var header = CreateRect(panelRt, "Header");
            Anchor(header, 0, 1, 1, 1, new Vector2(16, -16), new Vector2(-16, -56));

            refs.balanceText = UIRoot.CreateText("BalanceText", header, 26, TextAnchor.MiddleLeft);
            Anchor(refs.balanceText.rectTransform, 0, 0, 0.5f, 1, new Vector2(8, 4), new Vector2(-8, -4));

            refs.closeButton = UIRoot.CreateButton("Close", header, "닫기 (H)");
            Anchor(refs.closeButton.GetComponent<RectTransform>(), 1, 0, 1, 1, new Vector2(-148, 4), new Vector2(-8, -4));

            var topTabs = CreateRect(panelRt, "TopIconTabs");
            Anchor(topTabs, 0, 1, 1, 1, new Vector2(16, -64), new Vector2(-16, -112));
            refs.topTabButtons = CreateTabRow(topTabs, new[] { "무기", "킷", "성" }, "TopTab", 3);

            var subTabs = CreateRect(panelRt, "CategoryTabs");
            Anchor(subTabs, 0, 0, 1, 0, new Vector2(16, 16), new Vector2(-16, 64));
            refs.subTabButtons = CreateTabRow(subTabs, new[] { "1", "2", "3", "4" }, "SubTab", 4);
            refs.subTabLabels = LabelsFromButtons(refs.subTabButtons);

            var body = CreateRect(panelRt, "Body");
            Anchor(body, 0, 0, 1, 1, new Vector2(16, 72), new Vector2(-16, -120));

            var left = CreateRect(body, "ItemList");
            Anchor(left, 0, 0, 0.38f, 1, Vector2.zero, Vector2.zero);
            (refs.itemRowButtons, refs.itemRowLabels) = CreateItemRows(left, 4);

            var right = CreateRect(body, "DetailPanel");
            Anchor(right, 0.4f, 0, 1, 1, new Vector2(8, 0), Vector2.zero);

            refs.detailNameText = UIRoot.CreateText("DetailName", right, 28, TextAnchor.UpperLeft);
            Anchor(refs.detailNameText.rectTransform, 0, 1, 1, 1, new Vector2(12, -48), new Vector2(-12, -8));
            refs.detailNameText.fontStyle = FontStyles.Bold;

            refs.detailPriceText = UIRoot.CreateText("DetailPrice", right, 22, TextAnchor.UpperLeft);
            Anchor(refs.detailPriceText.rectTransform, 0, 1, 1, 1, new Vector2(12, -88), new Vector2(-12, -52));
            refs.detailPriceText.color = new Color(1f, 0.9f, 0.45f);

            var iconBg = UIRoot.CreatePanel("DetailIcon", right, new Color(0.12f, 0.14f, 0.2f, 0.9f));
            refs.detailIconImage = iconBg;
            Anchor(iconBg.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, new Vector2(-100, -20), new Vector2(100, 180));
            iconBg.preserveAspect = true;

            refs.detailDescriptionText = UIRoot.CreateText("DetailDesc", right, 18, TextAnchor.UpperLeft);
            Anchor(refs.detailDescriptionText.rectTransform, 0, 0, 1, 0.45f, new Vector2(12, 12), new Vector2(-12, -8));

            refs.buyButton = UIRoot.CreateButton("BuyButton", right, "구매");
            Anchor(refs.buyButton.GetComponent<RectTransform>(), 0.5f, 0, 0.5f, 0, new Vector2(-90, 20), new Vector2(90, 68));
            refs.buyButtonLabel = refs.buyButton.GetComponentInChildren<TMP_Text>(true);

            return refs;
        }

        private static void SetupPanelFrame(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1100, 680);
            rt.anchoredPosition = Vector2.zero;

            var bg = rt.GetComponent<Image>();
            if (bg == null) bg = rt.gameObject.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.07f, 0.12f, 0.94f);
            bg.raycastTarget = true;
        }

        private static RectTransform CreateRect(RectTransform parent, string name)
        {
            var go = UIRoot.CreateChild(name, parent);
            return go.GetComponent<RectTransform>();
        }

        private static Button[] CreateTabRow(RectTransform parent, string[] labels, string prefix, int count)
        {
            var buttons = new Button[count];
            float w = 1f / count;
            for (int i = 0; i < count; i++)
            {
                string label = i < labels.Length ? labels[i] : (i + 1).ToString();
                var btn = UIRoot.CreateButton($"{prefix}_{i}", parent, label);
                var brt = btn.GetComponent<RectTransform>();
                Anchor(brt, i * w, 0, (i + 1) * w, 1, new Vector2(4, 0), new Vector2(-4, 0));
                buttons[i] = btn;
            }
            return buttons;
        }

        private static (Button[], TMP_Text[]) CreateItemRows(RectTransform parent, int count)
        {
            var buttons = new Button[count];
            var labels = new TMP_Text[count];
            float h = 1f / count;

            for (int i = 0; i < count; i++)
            {
                var row = CreateRect(parent, $"ItemRow_{i}");
                Anchor(row, 0, 1f - (i + 1) * h, 1, 1f - i * h, new Vector2(0, 3), new Vector2(0, -3));

                var btn = UIRoot.CreateButton("Button", row, "");
                buttons[i] = btn;
                Anchor(btn.GetComponent<RectTransform>(), 0, 0, 1, 1, Vector2.zero, Vector2.zero);
                labels[i] = btn.GetComponentInChildren<TMP_Text>(true);
            }

            return (buttons, labels);
        }

        private static TMP_Text[] LabelsFromButtons(Button[] buttons)
        {
            var labels = new TMP_Text[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
                labels[i] = buttons[i] != null ? buttons[i].GetComponentInChildren<TMP_Text>(true) : null;
            return labels;
        }

        private static void Anchor(RectTransform rt, float minX, float minY, float maxX, float maxY, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
        }
    }
}
