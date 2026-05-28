using System;
using UnityEngine;
using ProjectM.Player;

namespace ProjectM.UI
{
    [Serializable]
    public struct KitSlotIcon
    {
        public KitType type;
        public Sprite normal;
        public Sprite selected;
    }

    [Serializable]
    public struct ThrowableSlotIcon
    {
        public ThrowableType type;
        public Sprite normal;
        public Sprite selected;
    }

    /// <summary>
    /// 하단 무기/키트 슬롯바. 활성 슬롯은 <see cref="WeaponKitSlot"/> 이 normal/selected 스프라이트로 교체한다.
    ///
    ///   primarySlot   : 주 무기(총) — ActiveSlot == Primary 일 때 강조 + 탄약
    ///   secondarySlot : 보조(근접) — ActiveSlot == Secondary 일 때 강조
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

        [Header("슬롯 3 (키트) — 장착 종류별 아이콘")]
        [Tooltip("아무 키트도 장착되지 않았을 때 기본 이미지")]
        [SerializeField] private Sprite kitEmptyNormal;
        [SerializeField] private Sprite kitEmptySelected;
        [Tooltip("HealKit / RepairKit / FarmKit 각각의 normal·selected 스프라이트를 직접 넣어주세요.")]
        [SerializeField] private KitSlotIcon[] kitIcons = new KitSlotIcon[]
        {
            new KitSlotIcon { type = KitType.HealKit },
            new KitSlotIcon { type = KitType.RepairKit },
            new KitSlotIcon { type = KitType.FarmKit },
        };

        [Header("슬롯 4 (투척무기) — 장착 종류별 아이콘")]
        [Tooltip("아무 투척무기도 장착되지 않았을 때 기본 이미지")]
        [SerializeField] private Sprite throwableEmptyNormal;
        [SerializeField] private Sprite throwableEmptySelected;
        [Tooltip("Grenade / Molotov / Flash 각각의 normal·selected 스프라이트를 직접 넣어주세요.")]
        [SerializeField] private ThrowableSlotIcon[] throwableIcons = new ThrowableSlotIcon[]
        {
            new ThrowableSlotIcon { type = ThrowableType.Grenade },
            new ThrowableSlotIcon { type = ThrowableType.Molotov },
            new ThrowableSlotIcon { type = ThrowableType.Flash },
        };

        [Tooltip("투척 슬롯이 미장착일 때도 마지막 선택(LastSelected) 종류의 아이콘을 보여줄지")]
        [SerializeField] private bool throwableShowLastSelectedWhenIdle = true;

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
            // 4개 슬롯 중 한 번에 하나만 강조되도록, 키트/투척 장착 여부를 먼저 계산.
            //   - 키트 장착 중      → 슬롯 3 강조 (1·2·4 는 평소)
            //   - 투척 장착 중      → 슬롯 4 강조 (1·2·3 는 평소)
            //   - 둘 다 미장착      → ActiveSlot(1 또는 2) 강조
            // 3·4 를 눌러도 보유 물품이 없으면 EquippedKit/EquippedThrowable 이 그대로 None 이라
            // 자동으로 강조가 옮겨가지 않고 슬롯 1·2 가 원래대로 강조된 상태를 유지함.
            bool kitEquipped     = kitEquipper        != null && kitEquipper.EquippedKit != KitType.None;
            bool throwEquipped   = throwableEquipper  != null && throwableEquipper.IsThrowableEquipped;
            bool weaponActive    = !kitEquipped && !throwEquipped;

            // 슬롯 1: 주 무기 (활성 강조 + 탄약) — 키트/투척 들고 있으면 강조 해제
            if (primarySlot != null)
            {
                bool active = weaponActive && arsenal != null && arsenal.ActiveSlot == WeaponSlot.Primary;
                primarySlot.SetHighlight(active);
                primarySlot.SetInfo(rangedWeapon != null
                    ? $"{rangedWeapon.CurrentMagazine}/{rangedWeapon.ReserveAmmo}" : "");
            }

            // 슬롯 2: 보조 무기 (활성 강조) — 키트/투척 들고 있으면 강조 해제
            if (secondarySlot != null)
            {
                bool active = weaponActive && arsenal != null && arsenal.ActiveSlot == WeaponSlot.Secondary;
                secondarySlot.SetHighlight(active);
            }

            // 슬롯 3: 키트 (키트 장착 시 강조 + 개수 + 종류별 아이콘 교체)
            if (kitSlot != null)
            {
                KitType eq = kitEquipper != null ? kitEquipper.EquippedKit : KitType.None;
                int count = (kitInventory != null && eq != KitType.None) ? kitInventory.GetCount(eq) : 0;
                ApplyKitSlotIcon(eq);
                kitSlot.SetHighlight(kitEquipped);
                kitSlot.SetInfo(eq != KitType.None ? $"{count}" : "");
            }

            // 슬롯 4: 투척무기 — 장착(선택) 시 강조 + 현재 선택 종류의 개수 + 종류별 아이콘 교체
            if (throwableSlot != null)
            {
                ThrowableType iconType = ThrowableType.None;
                if (throwableEquipper != null)
                {
                    if (throwEquipped) iconType = throwableEquipper.EquippedThrowable;
                    else if (throwableShowLastSelectedWhenIdle) iconType = throwableEquipper.LastSelected;
                }
                ApplyThrowableSlotIcon(iconType);
                throwableSlot.SetHighlight(throwEquipped);

                if (throwableInventory != null && throwableEquipper != null)
                {
                    var t = throwableEquipper.IsThrowableEquipped
                        ? throwableEquipper.EquippedThrowable
                        : throwableEquipper.LastSelected;
                    throwableSlot.SetInfo($"{throwableInventory.GetCount(t)}");
                }
            }
        }

        private void ApplyKitSlotIcon(KitType type)
        {
            if (kitSlot == null) return;

            if (type == KitType.None)
            {
                kitSlot.SetSpritePair(kitEmptyNormal, kitEmptySelected);
                return;
            }

            if (kitIcons != null)
            {
                for (int i = 0; i < kitIcons.Length; i++)
                {
                    if (kitIcons[i].type != type) continue;
                    kitSlot.SetSpritePair(kitIcons[i].normal, kitIcons[i].selected);
                    return;
                }
            }
            // 매핑이 없으면 빈 아이콘으로 폴백
            kitSlot.SetSpritePair(kitEmptyNormal, kitEmptySelected);
        }

        private void ApplyThrowableSlotIcon(ThrowableType type)
        {
            if (throwableSlot == null) return;

            if (type == ThrowableType.None)
            {
                throwableSlot.SetSpritePair(throwableEmptyNormal, throwableEmptySelected);
                return;
            }

            if (throwableIcons != null)
            {
                for (int i = 0; i < throwableIcons.Length; i++)
                {
                    if (throwableIcons[i].type != type) continue;
                    throwableSlot.SetSpritePair(throwableIcons[i].normal, throwableIcons[i].selected);
                    return;
                }
            }
            throwableSlot.SetSpritePair(throwableEmptyNormal, throwableEmptySelected);
        }
    }
}
