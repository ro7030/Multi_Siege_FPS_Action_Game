using System;
using UnityEngine;
using ProjectM.Network;

namespace ProjectM.Player
{
    /// <summary>
    /// 투척무기 보유량. 상점 구매 시 증가, 던질 때 1개 소모.
    /// 기본 지급: 수류탄 1, 섬광탄 1 (기획서 10-5)
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class ThrowableInventory : MonoBehaviour
    {
        [Header("초기 지급")]
        [SerializeField] private int startingGrenade = 1;
        [SerializeField] private int startingMolotov = 0;
        [SerializeField] private int startingFlash = 1;

        [Header("상태 (읽기 전용)")]
        [SerializeField] private int grenadeCount;
        [SerializeField] private int molotovCount;
        [SerializeField] private int flashCount;

        private bool startingCountsApplied;

        public int GrenadeCount => grenadeCount;
        public int MolotovCount => molotovCount;
        public int FlashCount => flashCount;

        /// <summary>(타입, 새 보유량)</summary>
        public event Action<ThrowableType, int> OnCountChanged;

        private void Awake() => ApplyStartingCounts();

        public int GetCount(ThrowableType type) => type switch
        {
            ThrowableType.Grenade => grenadeCount,
            ThrowableType.Molotov => molotovCount,
            ThrowableType.Flash   => flashCount,
            _ => 0
        };

        public bool Has(ThrowableType type) => GetCount(type) > 0;

        public bool HasAny() =>
            grenadeCount > 0 || molotovCount > 0 || flashCount > 0;

        /// <summary>스폰 시점에 starting 값이 반드시 적용되도록 보장한다.</summary>
        internal void ApplyStartingCounts()
        {
            if (startingCountsApplied)
                return;

            grenadeCount = Mathf.Max(0, startingGrenade);
            molotovCount = Mathf.Max(0, startingMolotov);
            flashCount   = Mathf.Max(0, startingFlash);
            startingCountsApplied = true;
        }

        public void Add(ThrowableType type, int count = 1)
        {
            if (type == ThrowableType.None || count <= 0) return;

            if (TryGetComponent<NetworkThrowableInventory>(out var netInventory)
                && NetworkSessionHelper.IsMultiplayerSession
                && netInventory.IsSpawned)
            {
                if (NetworkSessionHelper.IsServer)
                    netInventory.ServerAdd(type, count);
                return;
            }

            AddLocal(type, count);
        }

        public bool TryConsume(ThrowableType type)
        {
            if (TryGetComponent<NetworkThrowableInventory>(out var netInventory)
                && NetworkSessionHelper.IsMultiplayerSession
                && netInventory.IsSpawned)
            {
                if (NetworkSessionHelper.IsServer)
                    return netInventory.ServerTryConsume(type);

                return false;
            }

            return TryConsumeLocal(type);
        }

        /// <summary>서버/네트워크 동기화용. 카운트 갱신 + 이벤트 발행.</summary>
        public void NotifyCount(ThrowableType type, int value)
        {
            value = Mathf.Max(0, value);
            switch (type)
            {
                case ThrowableType.Grenade: grenadeCount = value; break;
                case ThrowableType.Molotov: molotovCount = value; break;
                case ThrowableType.Flash:   flashCount = value; break;
                default: return;
            }

            OnCountChanged?.Invoke(type, value);
        }

        /// <summary>스폰 직후 클라이언트 스냅샷 일괄 적용.</summary>
        public void NotifyAllCounts(int grenade, int molotov, int flash)
        {
            NotifyCount(ThrowableType.Grenade, grenade);
            NotifyCount(ThrowableType.Molotov, molotov);
            NotifyCount(ThrowableType.Flash, flash);
        }

        internal void AddLocal(ThrowableType type, int count = 1)
        {
            if (type == ThrowableType.None || count <= 0) return;
            switch (type)
            {
                case ThrowableType.Grenade: grenadeCount += count; break;
                case ThrowableType.Molotov: molotovCount += count; break;
                case ThrowableType.Flash:   flashCount   += count; break;
            }

            OnCountChanged?.Invoke(type, GetCount(type));
        }

        internal bool TryConsumeLocal(ThrowableType type)
        {
            ApplyStartingCounts();
            if (!Has(type)) return false;
            switch (type)
            {
                case ThrowableType.Grenade: grenadeCount--; break;
                case ThrowableType.Molotov: molotovCount--; break;
                case ThrowableType.Flash:   flashCount--;   break;
                default: return false;
            }

            OnCountChanged?.Invoke(type, GetCount(type));
            return true;
        }
    }
}
