using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectM.Player
{
    /// <summary>
    /// 투척무기 장착/투척 (배그식 탭 사이클 + 빠른 투척).
    ///   - 4번키 "한 번 누름": cycleOrder 순서대로 보유 투척무기를 순환 장착
    ///       · 미장착 상태에서 누르면 첫 번째 보유 종류를 장착
    ///       · 이미 장착된 상태에서 누르면 다음 보유 종류로 교체 (한 종류만 있으면 그대로)
    ///   - 좌클릭(투척 장착 중): 선택한 투척무기를 던짐
    ///   - G키: 어느 상태든 마지막 선택 투척무기를 즉시 던짐 (빠른 투척)
    /// 키트와 상호 배타 (하나 들면 다른 건 내려놓음).
    /// </summary>
    [RequireComponent(typeof(ThrowableInventory))]
    public class ThrowableEquipper : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private ThrowableInventory inventory;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private KitEquipper kitEquipper;

        [Header("투척무기 정의 (Inspector 연결)")]
        [SerializeField] private ThrowableDefinition grenadeDef;
        [SerializeField] private ThrowableDefinition molotovDef;
        [SerializeField] private ThrowableDefinition flashDef;

        [Header("던지기")]
        [SerializeField] private float throwForce = 14f;
        [SerializeField] private float throwUpward = 3f;
        [SerializeField] private float spawnForward = 0.8f;

        [Header("입력")]
        [SerializeField] private bool isLocalPlayer = true;
        [SerializeField] private Key cycleKey = Key.Digit4;
        [SerializeField] private Key quickThrowKey = Key.G;

        [Header("탭 사이클")]
        [Tooltip("4번키를 누를 때마다 이 순서대로 보유 투척무기를 순환 장착합니다. (Inspector 에서 자유롭게 재정렬)")]
        [SerializeField] private ThrowableType[] cycleOrder = new ThrowableType[]
        {
            ThrowableType.Grenade,
            ThrowableType.Flash,
            ThrowableType.Molotov,
        };

        // 상태
        public ThrowableType EquippedThrowable { get; private set; } = ThrowableType.None;
        public bool IsThrowableEquipped => EquippedThrowable != ThrowableType.None;
        public ThrowableType LastSelected { get; private set; } = ThrowableType.Grenade;

        // ── (구) 휠 호환용 — 항상 비활성. 기존 ThrowableWheelView 가 컴파일/실행은 되지만 표시되지 않음. ──
        public bool IsSelecting => false;
        public ThrowableType HighlightedType => ThrowableType.None;
        public Vector2 SelectionDirection => Vector2.zero;
        public float SelectionDeadzone => 0f;
        public float SelectionMaxRadius => 1f;

        public event Action<ThrowableType> OnEquippedChanged;

        private void Awake()
        {
            if (inventory == null) inventory = GetComponent<ThrowableInventory>();
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
            if (kitEquipper == null) kitEquipper = GetComponent<KitEquipper>();
        }

        private void OnEnable()
        {
            if (inventory != null) inventory.OnCountChanged += HandleInventoryChanged;
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.OnCountChanged -= HandleInventoryChanged;
        }

        private void HandleInventoryChanged(ThrowableType type, int newCount)
        {
            if (EquippedThrowable == type && newCount <= 0) SetEquipped(ThrowableType.None);
        }

        // ─────────────────────────────────────────────────────────────
        private void Update()
        {
            if (!isLocalPlayer) return;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return;

            // 4번 키 한 번 누름: 보유 투척무기 사이클
            if (kb[cycleKey].wasPressedThisFrame) CycleEquipped();

            // G 빠른 투척 (어느 상태든)
            if (kb[quickThrowKey].wasPressedThisFrame)
                Throw(LastSelected);

            // 좌클릭 투척 (투척 장착 중 + 커서 잠금)
            if (IsThrowableEquipped && mouse != null
                && Cursor.lockState == CursorLockMode.Locked
                && mouse.leftButton.wasPressedThisFrame)
            {
                Throw(EquippedThrowable);
            }
        }

        /// <summary>
        /// cycleOrder 에 정의된 순서대로, 현재 장착 종류 다음 칸부터 한 바퀴 돌며
        /// 보유 중(인벤토리 ≥ 1)인 첫 투척무기를 장착한다.
        /// 보유 투척무기가 하나도 없으면 변경 없음.
        /// </summary>
        public void CycleEquipped()
        {
            if (inventory == null || cycleOrder == null || cycleOrder.Length == 0)
            {
                Debug.Log("[Throw] 사이클 순서가 비어 있습니다.");
                return;
            }

            int startIdx = -1;
            for (int i = 0; i < cycleOrder.Length; i++)
                if (cycleOrder[i] == EquippedThrowable) { startIdx = i; break; }

            for (int step = 1; step <= cycleOrder.Length; step++)
            {
                int idx = ((startIdx + step) % cycleOrder.Length + cycleOrder.Length) % cycleOrder.Length;
                var next = cycleOrder[idx];
                if (next == ThrowableType.None) continue;
                if (inventory.Has(next))
                {
                    LastSelected = next;
                    SetEquipped(next);
                    return;
                }
            }

            Debug.Log("[Throw] 보유한 투척무기가 없습니다.");
        }

        private void SetEquipped(ThrowableType type)
        {
            if (EquippedThrowable == type) return;
            EquippedThrowable = type;
            if (type != ThrowableType.None) kitEquipper?.Holster(); // 키트와 배타
            OnEquippedChanged?.Invoke(type);
        }

        public void Holster() => SetEquipped(ThrowableType.None);

        // ── 투척 ──
        private void Throw(ThrowableType type)
        {
            if (type == ThrowableType.None || inventory == null) return;
            if (!inventory.Has(type)) { Debug.Log($"[Throw] {type} 없음"); return; }

            var def = GetDef(type);
            if (def == null) { Debug.LogWarning($"[Throw] {type} 정의 미설정"); return; }
            if (viewCamera == null) return;

            if (!inventory.TryConsume(type)) return;

            Vector3 origin = viewCamera.transform.position + viewCamera.transform.forward * spawnForward;
            Vector3 velocity = viewCamera.transform.forward * throwForce + Vector3.up * throwUpward;

            GameObject go = def.projectilePrefab != null
                ? Instantiate(def.projectilePrefab, origin, Quaternion.identity)
                : CreateDefaultProjectile(origin);

            var proj = go.GetComponent<ThrowableProjectile>();
            if (proj == null) proj = go.AddComponent<ThrowableProjectile>();
            proj.Launch(def, gameObject, velocity);

            Debug.Log($"[Throw] {def.displayName} 투척");
        }

        private GameObject CreateDefaultProjectile(Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.3f;
            if (go.GetComponent<Rigidbody>() == null) go.AddComponent<Rigidbody>();
            return go;
        }

        private ThrowableDefinition GetDef(ThrowableType type) => type switch
        {
            ThrowableType.Grenade => grenadeDef,
            ThrowableType.Molotov => molotovDef,
            ThrowableType.Flash   => flashDef,
            _ => null
        };
    }
}
