using UnityEngine;
using ProjectM.Player;

namespace ProjectM.UI
{
    /// <summary>
    /// 하단 무기/키트 슬롯바. 슬롯 아이콘은 Canvas 에서 직접 넣는 고정 이미지이며 코드가 sprite 를 바꾸지 않는다.
    ///
    ///   슬롯 1 : 주 무기 (총)   — 활성 시 강조, 탄약 표시
    ///   슬롯 2 : 보조 무기 (칼) — 활성 시 강조
    ///   슬롯 3 : 키트 (3번 홀드로 휠 선택) — 키트 장착 시 강조 + 개수
    ///   슬롯 4 : 힐 키트 아이콘 — 힐 키트를 보유 중일 때만 표시
    ///
    /// 사용법
    ///   1) Canvas 에 슬롯 이미지를 직접 디자인 (아이콘은 고정 이미지)
    ///   2) 각 슬롯에 WeaponKitSlot 붙이고 infoText/highlight/tintTarget 연결
    ///   3) 이 컴포넌트의 슬롯들에 드래그
    /// </summary>
    public class WeaponKitHUD : MonoBehaviour
    {
        [Header("참조 (자동 탐색)")]
        [SerializeField] private PlayerArsenal arsenal;
        [SerializeField] private WeaponController rangedWeapon;
        [SerializeField] private KitInventory kitInventory;
        [SerializeField] private KitEquipper kitEquipper;
        [SerializeField] private ThrowableInventory throwableInventory;
        [SerializeField] private ThrowableEquipper throwableEquipper;

        [Header("직접 배치한 슬롯 (위치/이미지 자유)")]
        [SerializeField] private WeaponKitSlot primarySlot;   // 1 보조(근접) — Secondary 에 연결
        [SerializeField] private WeaponKitSlot secondarySlot; // 2 주무기(라이플) — Primary 에 연결
        [SerializeField] private WeaponKitSlot kitSlot;       // 3 키트 (휠)
        [SerializeField] private WeaponKitSlot throwableSlot; // 4 투척무기 (휠)

        private void Awake()
        {
            if (arsenal == null) arsenal = FindAnyObjectByType<PlayerArsenal>();
            if (rangedWeapon == null) rangedWeapon = FindAnyObjectByType<WeaponController>();
            if (kitInventory == null) kitInventory = FindAnyObjectByType<KitInventory>();
            if (kitEquipper == null) kitEquipper = FindAnyObjectByType<KitEquipper>();
            if (throwableInventory == null) throwableInventory = FindAnyObjectByType<ThrowableInventory>();
            if (throwableEquipper == null) throwableEquipper = FindAnyObjectByType<ThrowableEquipper>();
        }

        private void Update() => Refresh();

        // ─────────────────────────────────────────────────────────────
        private void Refresh()
        {
            // 슬롯 1: 주 무기 (활성 강조 + 탄약)
            if (primarySlot != null)
            {
                bool active = arsenal != null && arsenal.ActiveSlot == WeaponSlot.Primary;
                primarySlot.SetHighlight(active);
                primarySlot.SetInfo(rangedWeapon != null
                    ? $"{rangedWeapon.CurrentMagazine}/{rangedWeapon.ReserveAmmo}" : "");
            }

            // 슬롯 2: 보조 무기 (활성 강조)
            if (secondarySlot != null)
            {
                bool active = arsenal != null && arsenal.ActiveSlot == WeaponSlot.Secondary;
                secondarySlot.SetHighlight(active);
            }

            // 슬롯 3: 키트 (키트 장착 시 강조 + 개수)
            if (kitSlot != null)
            {
                KitType eq = kitEquipper != null ? kitEquipper.EquippedKit : KitType.None;
                int count = (kitInventory != null && eq != KitType.None) ? kitInventory.GetCount(eq) : 0;
                kitSlot.SetHighlight(eq != KitType.None);
                kitSlot.SetInfo(eq != KitType.None ? $"{count}" : "");
            }

            // 슬롯 4: 투척무기 — 장착(선택) 시 강조 + 현재 선택 종류의 개수
            if (throwableSlot != null)
            {
                bool equipped = throwableEquipper != null && throwableEquipper.IsThrowableEquipped;
                throwableSlot.SetHighlight(equipped);

                if (throwableInventory != null && throwableEquipper != null)
                {
                    var t = throwableEquipper.IsThrowableEquipped
                        ? throwableEquipper.EquippedThrowable
                        : throwableEquipper.LastSelected;
                    throwableSlot.SetInfo($"{throwableInventory.GetCount(t)}");
                }
            }
        }
    }
}
