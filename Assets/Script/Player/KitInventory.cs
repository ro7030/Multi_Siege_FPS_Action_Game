using System;
using UnityEngine;

namespace ProjectM.Player
{
    public enum KitType { None, HealKit, RepairKit, FarmKit }

    /// <summary>
    /// 플레이어 키트 인벤토리. 상점에서 구매하면 카운트가 증가하고,
    /// KitEquipper 가 좌클릭으로 사용할 때 1개 소모.
    /// 기본 지급: 밭 설치 키트 1개 (기획서 10-5)
    /// </summary>
    public class KitInventory : MonoBehaviour
    {
        [Header("초기 지급 (기획서 10-5)")]
        [SerializeField] private int startingHealKit = 0;
        [SerializeField] private int startingRepairKit = 0;
        [SerializeField] private int startingFarmKit = 1;

        [Header("상태 (읽기 전용)")]
        [SerializeField] private int healKitCount;
        [SerializeField] private int repairKitCount;
        [SerializeField] private int farmKitCount;

        public int HealKitCount   => healKitCount;
        public int RepairKitCount => repairKitCount;
        public int FarmKitCount   => farmKitCount;

        /// <summary>(KitType, 새 보유량)</summary>
        public event Action<KitType, int> OnCountChanged;

        private void Awake()
        {
            healKitCount   = Mathf.Max(0, startingHealKit);
            repairKitCount = Mathf.Max(0, startingRepairKit);
            farmKitCount   = Mathf.Max(0, startingFarmKit);
        }

        public int GetCount(KitType type)
        {
            return type switch
            {
                KitType.HealKit   => healKitCount,
                KitType.RepairKit => repairKitCount,
                KitType.FarmKit   => farmKitCount,
                _ => 0
            };
        }

        public bool Has(KitType type) => GetCount(type) > 0;

        public void Add(KitType type, int count = 1)
        {
            if (type == KitType.None || count <= 0) return;
            switch (type)
            {
                case KitType.HealKit:   healKitCount   += count; break;
                case KitType.RepairKit: repairKitCount += count; break;
                case KitType.FarmKit:   farmKitCount   += count; break;
            }
            OnCountChanged?.Invoke(type, GetCount(type));
        }

        public bool TryConsume(KitType type)
        {
            if (!Has(type)) return false;
            switch (type)
            {
                case KitType.HealKit:   healKitCount--;   break;
                case KitType.RepairKit: repairKitCount--; break;
                case KitType.FarmKit:   farmKitCount--;   break;
                default: return false;
            }
            OnCountChanged?.Invoke(type, GetCount(type));
            return true;
        }
    }
}
