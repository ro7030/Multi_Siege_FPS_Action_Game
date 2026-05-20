using UnityEngine;
using UnityEngine.UI;
using ProjectM.Player;

namespace ProjectM.UI
{
    /// <summary>
    /// GTA식 키트 선택 라디얼 휠. KitEquipper 의 선택 상태를 폴링하여 표시한다.
    /// - 3번키를 누르고 있는 동안만 표시
    /// - 위=회복 / 좌하=수리 / 우하=밭, 마우스 방향으로 가리킨 칸이 강조됨
    /// - 중앙 포인터가 현재 선택 방향을 가리킴
    ///
    /// 자동 생성형 UI. 직접 Canvas 로 만들고 싶으면 이 컴포넌트를 빼고 별도 구현하면 된다.
    /// </summary>
    public class KitWheelView : MonoBehaviour
    {
        [SerializeField] private KitEquipper equipper;
        [SerializeField] private KitInventory inventory;

        [Header("휠 외형")]
        [SerializeField] private float wheelRadius = 150f;     // 칸 라벨 배치 반경
        [SerializeField] private float pointerScale = 0.8f;    // 포인터 이동 반경 = 휠반경 * 이 값

        private GameObject panelGo;
        private RectTransform panelRt;
        private RectTransform pointerRt;

        // 칸: Heal(위), Repair(좌하), Farm(우하)
        private Text healLabel, repairLabel, farmLabel;
        private Image healSlot, repairSlot, farmSlot;

        private static readonly Color Normal = new(0.15f, 0.18f, 0.25f, 0.85f);
        private static readonly Color Highlight = new(1f, 0.85f, 0.3f, 0.95f);
        private static readonly Color Disabled = new(0.12f, 0.12f, 0.12f, 0.7f);

        private void Awake()
        {
            if (equipper == null) equipper = FindAnyObjectByType<KitEquipper>();
            if (inventory == null) inventory = FindAnyObjectByType<KitInventory>();
        }

        private void Start()
        {
            if (UIRoot.Instance == null) { enabled = false; return; }
            BuildWheel();
            panelGo.SetActive(false);
        }

        private void Update()
        {
            if (equipper == null) return;

            bool selecting = equipper.IsSelecting;
            if (panelGo.activeSelf != selecting) panelGo.SetActive(selecting);
            if (!selecting) return;

            RefreshSlots();
            RefreshPointer();
        }

        // ─────────────────────────────────────────────────────────────
        private void BuildWheel()
        {
            var root = UIRoot.Instance.RootTransform;

            // 반투명 풀스크린 배경
            var bg = UIRoot.CreatePanel("KitWheel", root, new Color(0, 0, 0, 0.35f));
            panelGo = bg.gameObject;
            panelRt = bg.rectTransform;
            panelRt.anchorMin = Vector2.zero; panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero; panelRt.offsetMax = Vector2.zero;
            bg.raycastTarget = false;

            // 중앙 컨테이너
            var center = UIRoot.CreateChild("WheelCenter", panelRt).GetComponent<RectTransform>();
            center.anchorMin = center.anchorMax = new Vector2(0.5f, 0.5f);
            center.pivot = new Vector2(0.5f, 0.5f);
            center.anchoredPosition = Vector2.zero;
            center.sizeDelta = new Vector2(wheelRadius * 2.4f, wheelRadius * 2.4f);

            // 각 방향 칸 생성 (Heal 위 90°, Repair 좌하 210°, Farm 우하 330°)
            (healSlot,   healLabel)   = CreateSlot(center, "회복", 90f);
            (repairSlot, repairLabel) = CreateSlot(center, "수리", 210f);
            (farmSlot,   farmLabel)   = CreateSlot(center, "밭",   330f);

            // 중앙 포인터
            var ptr = UIRoot.CreatePanel("Pointer", center, new Color(1f, 1f, 1f, 0.9f));
            pointerRt = ptr.rectTransform;
            pointerRt.anchorMin = pointerRt.anchorMax = new Vector2(0.5f, 0.5f);
            pointerRt.pivot = new Vector2(0.5f, 0.5f);
            pointerRt.sizeDelta = new Vector2(24, 24);
            pointerRt.anchoredPosition = Vector2.zero;

            // 안내 텍스트
            var hint = UIRoot.CreateText("Hint", center, 18, TextAnchor.MiddleCenter);
            hint.text = "방향 선택 후 클릭 / 떼기";
            hint.color = new Color(1, 1, 1, 0.6f);
            var hrt = hint.rectTransform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0.5f);
            hrt.pivot = new Vector2(0.5f, 0.5f);
            hrt.sizeDelta = new Vector2(300, 30);
            hrt.anchoredPosition = new Vector2(0, -wheelRadius - 40f);
        }

        private (Image slot, Text label) CreateSlot(RectTransform center, string name, float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector2 pos = new(Mathf.Cos(rad) * wheelRadius, Mathf.Sin(rad) * wheelRadius);

            var slot = UIRoot.CreatePanel($"Slot_{name}", center, Normal);
            var srt = slot.rectTransform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(130, 80);
            srt.anchoredPosition = pos;
            slot.raycastTarget = false;

            var label = UIRoot.CreateText($"Label_{name}", srt, 22, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.text = name;

            return (slot, label);
        }

        // ─────────────────────────────────────────────────────────────
        private void RefreshSlots()
        {
            KitType hi = equipper.HighlightedKit;
            UpdateSlot(healSlot,   healLabel,   "회복", KitType.HealKit,   hi);
            UpdateSlot(repairSlot, repairLabel, "수리", KitType.RepairKit, hi);
            UpdateSlot(farmSlot,   farmLabel,   "밭",   KitType.FarmKit,   hi);
        }

        private void UpdateSlot(Image slot, Text label, string name, KitType type, KitType highlighted)
        {
            int count = inventory != null ? inventory.GetCount(type) : 0;
            bool owned = count > 0;

            label.text = $"{name}\n×{count}";

            if (!owned)               slot.color = Disabled;
            else if (type == highlighted) slot.color = Highlight;
            else                      slot.color = Normal;

            label.color = owned ? Color.white : new Color(0.5f, 0.5f, 0.5f);
        }

        private void RefreshPointer()
        {
            if (pointerRt == null) return;
            Vector2 dir = equipper.SelectionDirection;
            float max = Mathf.Max(1f, equipper.SelectionMaxRadius);
            // 0~1 비율로 환산 후 휠 반경 안에서 이동
            Vector2 norm = dir / max;
            pointerRt.anchoredPosition = norm * (wheelRadius * pointerScale);
        }
    }
}
