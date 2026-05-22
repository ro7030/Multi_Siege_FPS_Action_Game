using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectM.Player;

namespace ProjectM.UI
{
    /// <summary>
    /// 투척무기 선택 라디얼 휠. ThrowableEquipper 의 선택 상태를 폴링하여 표시한다.
    /// 4번키를 누르고 있는 동안만 표시. 위=수류탄 / 좌하=화염병 / 우하=섬광탄.
    /// 자동 생성형. (KitWheelView 와 동일 패턴)
    /// </summary>
    public class ThrowableWheelView : MonoBehaviour
    {
        [SerializeField] private ThrowableEquipper equipper;
        [SerializeField] private ThrowableInventory inventory;

        [Header("휠 외형")]
        [SerializeField] private float wheelRadius = 150f;
        [SerializeField] private float pointerScale = 0.8f;

        private GameObject panelGo;
        private RectTransform pointerRt;
        private Image grenadeSlot, molotovSlot, flashSlot;
        private TMP_Text grenadeLabel, molotovLabel, flashLabel;

        private static readonly Color Normal = new(0.15f, 0.18f, 0.25f, 0.85f);
        private static readonly Color Highlight = new(1f, 0.6f, 0.3f, 0.95f);
        private static readonly Color Disabled = new(0.12f, 0.12f, 0.12f, 0.7f);

        private void Awake()
        {
            if (equipper == null) equipper = FindAnyObjectByType<ThrowableEquipper>();
            if (inventory == null) inventory = FindAnyObjectByType<ThrowableInventory>();
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

        private void BuildWheel()
        {
            var root = UIRoot.Instance.RootTransform;
            var bg = UIRoot.CreatePanel("ThrowableWheel", root, new Color(0, 0, 0, 0.35f));
            panelGo = bg.gameObject;
            var rt = bg.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            bg.raycastTarget = false;

            var center = UIRoot.CreateChild("WheelCenter", rt).GetComponent<RectTransform>();
            center.anchorMin = center.anchorMax = new Vector2(0.5f, 0.5f);
            center.pivot = new Vector2(0.5f, 0.5f);
            center.anchoredPosition = Vector2.zero;
            center.sizeDelta = new Vector2(wheelRadius * 2.4f, wheelRadius * 2.4f);

            (grenadeSlot, grenadeLabel) = CreateSlot(center, "수류탄", 90f);
            (molotovSlot, molotovLabel) = CreateSlot(center, "화염병", 210f);
            (flashSlot,   flashLabel)   = CreateSlot(center, "섬광탄", 330f);

            var ptr = UIRoot.CreatePanel("Pointer", center, new Color(1, 1, 1, 0.9f));
            pointerRt = ptr.rectTransform;
            pointerRt.anchorMin = pointerRt.anchorMax = new Vector2(0.5f, 0.5f);
            pointerRt.pivot = new Vector2(0.5f, 0.5f);
            pointerRt.sizeDelta = new Vector2(24, 24);
            pointerRt.anchoredPosition = Vector2.zero;
        }

        private (Image, TMP_Text) CreateSlot(RectTransform center, string name, float angleDeg)
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

        private void RefreshSlots()
        {
            ThrowableType hi = equipper.HighlightedType;
            UpdateSlot(grenadeSlot, grenadeLabel, "수류탄", ThrowableType.Grenade, hi);
            UpdateSlot(molotovSlot, molotovLabel, "화염병", ThrowableType.Molotov, hi);
            UpdateSlot(flashSlot,   flashLabel,   "섬광탄", ThrowableType.Flash,   hi);
        }

        private void UpdateSlot(Image slot, TMP_Text label, string name, ThrowableType type, ThrowableType highlighted)
        {
            int count = inventory != null ? inventory.GetCount(type) : 0;
            bool owned = count > 0;
            label.text = $"{name}\n×{count}";
            if (!owned) slot.color = Disabled;
            else if (type == highlighted) slot.color = Highlight;
            else slot.color = Normal;
            label.color = owned ? Color.white : new Color(0.5f, 0.5f, 0.5f);
        }

        private void RefreshPointer()
        {
            if (pointerRt == null) return;
            Vector2 dir = equipper.SelectionDirection;
            float max = Mathf.Max(1f, equipper.SelectionMaxRadius);
            pointerRt.anchoredPosition = (dir / max) * (wheelRadius * pointerScale);
        }
    }
}
