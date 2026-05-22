using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectM.Player
{
    /// <summary>
    /// 투척무기 장착/투척 (GTA식 휠 + 빠른 투척).
    ///   - 4번키 홀드: 라디얼 휠 (위=수류탄 / 좌하=화염병 / 우하=섬광탄), 떼기/클릭으로 선택
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
        [SerializeField] private Key selectKey = Key.Digit4;
        [SerializeField] private Key quickThrowKey = Key.G;

        [Header("휠")]
        [SerializeField] private float selectionDeadzone = 40f;
        [SerializeField] private float selectionMaxRadius = 200f;
        [SerializeField] private float mouseSensitivity = 1f;

        // 상태
        public ThrowableType EquippedThrowable { get; private set; } = ThrowableType.None;
        public bool IsThrowableEquipped => EquippedThrowable != ThrowableType.None;
        public bool IsSelecting { get; private set; }
        public ThrowableType HighlightedType { get; private set; } = ThrowableType.None;
        public Vector2 SelectionDirection { get; private set; }
        public float SelectionDeadzone => selectionDeadzone;
        public float SelectionMaxRadius => selectionMaxRadius;
        public ThrowableType LastSelected { get; private set; } = ThrowableType.Grenade;

        public event Action<ThrowableType> OnEquippedChanged;
        public event Action<bool> OnSelectionStateChanged;

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

            // 휠 열기
            if (kb[selectKey].wasPressedThisFrame) BeginSelection();

            if (IsSelecting)
            {
                UpdateSelection(mouse);
                bool confirm = (mouse != null && mouse.leftButton.wasPressedThisFrame)
                               || kb[selectKey].wasReleasedThisFrame;
                if (confirm) EndSelection();
                return;
            }

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

        // ── 휠 선택 ──
        private void BeginSelection()
        {
            IsSelecting = true;
            SelectionDirection = Vector2.zero;
            HighlightedType = ThrowableType.None;
            OnSelectionStateChanged?.Invoke(true);
        }

        private void UpdateSelection(Mouse mouse)
        {
            if (mouse != null)
            {
                Vector2 v = SelectionDirection + mouse.delta.ReadValue() * mouseSensitivity;
                if (v.magnitude > selectionMaxRadius) v = v.normalized * selectionMaxRadius;
                SelectionDirection = v;
            }
            HighlightedType = SelectionDirection.magnitude >= selectionDeadzone
                ? DirectionToThrowable(SelectionDirection) : ThrowableType.None;
        }

        private void EndSelection()
        {
            IsSelecting = false;
            OnSelectionStateChanged?.Invoke(false);

            if (HighlightedType != ThrowableType.None && inventory != null && inventory.Has(HighlightedType))
            {
                LastSelected = HighlightedType;
                SetEquipped(HighlightedType);
            }
            else SetEquipped(ThrowableType.None);
        }

        /// <summary>위=수류탄, 좌하=화염병, 우하=섬광탄.</summary>
        public static ThrowableType DirectionToThrowable(Vector2 dir)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (ang < 0) ang += 360f;
            if (ang >= 30f && ang < 150f) return ThrowableType.Grenade;
            if (ang >= 150f && ang < 270f) return ThrowableType.Molotov;
            return ThrowableType.Flash;
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
