using System;
using UnityEngine;
using ProjectM.Network;

namespace ProjectM.Player
{
    public enum KitType { None, HealKit, RepairKit, FarmKit }

    // 플레이어 키트 인벤토리. 상점에서 구매하면 카운트가 증가하고,
    // KitEquipper 가 좌클릭으로 사용할 때 1개 소모.
    // 기본 지급: 밭 설치 키트 1개 (기획서 10-5)
    [DefaultExecutionOrder(-100)]
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

        private bool startingCountsApplied;

        public int HealKitCount   => healKitCount;
        public int RepairKitCount => repairKitCount;
        public int FarmKitCount   => farmKitCount;

        // (KitType, 새 보유량)
        public event Action<KitType, int> OnCountChanged;

        private void Awake() => ApplyStartingCounts();

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

        // 스폰 시점에 starting 값이 반드시 적용되도록 보장한다.
        internal void ApplyStartingCounts()
        {
            if (startingCountsApplied)
                return;

            healKitCount   = Mathf.Max(0, startingHealKit);
            repairKitCount = Mathf.Max(0, startingRepairKit);
            farmKitCount   = Mathf.Max(0, startingFarmKit);
            startingCountsApplied = true;
        }

        public void Add(KitType type, int count = 1)
        {
            if (type == KitType.None || count <= 0) return;

            if (TryGetComponent<NetworkKitInventory>(out var netInventory)
                && NetworkSessionHelper.IsMultiplayerSession
                && netInventory.IsSpawned)
            {
                if (NetworkSessionHelper.IsServer)
                    netInventory.ServerAdd(type, count);
                return;
            }

            AddLocal(type, count);
        }

        public bool TryConsume(KitType type)
        {
            if (TryGetComponent<NetworkKitInventory>(out var netInventory)
                && NetworkSessionHelper.IsMultiplayerSession
                && netInventory.IsSpawned)
            {
                if (NetworkSessionHelper.IsServer)
                    return netInventory.ServerTryConsume(type);

                return false;
            }

            return TryConsumeLocal(type);
        }

        // 서버/네트워크 동기화용. 카운트 갱신 + 이벤트 발행.
        public void NotifyCount(KitType type, int value)
        {
            value = Mathf.Max(0, value);
            switch (type)
            {
                case KitType.HealKit:   healKitCount = value; break;
                case KitType.RepairKit: repairKitCount = value; break;
                case KitType.FarmKit:   farmKitCount = value; break;
                default: return;
            }

            OnCountChanged?.Invoke(type, value);
        }

        // 스폰 직후 클라이언트 스냅샷 일괄 적용.
        public void NotifyAllCounts(int heal, int repair, int farm)
        {
            NotifyCount(KitType.HealKit, heal);
            NotifyCount(KitType.RepairKit, repair);
            NotifyCount(KitType.FarmKit, farm);
        }

        internal void AddLocal(KitType type, int count = 1)
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

        internal bool TryConsumeLocal(KitType type)
        {
            ApplyStartingCounts();
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
