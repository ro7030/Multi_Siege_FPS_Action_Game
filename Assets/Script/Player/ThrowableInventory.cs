using System;
using UnityEngine;

namespace ProjectM.Player
{
    /// <summary>
    /// 투척무기 보유량. 상점 구매 시 증가, 던질 때 1개 소모.
    /// 기본 지급: 수류탄 1, 섬광탄 1 (기획서 10-5)
    /// </summary>
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

        public int GrenadeCount => grenadeCount;
        public int MolotovCount => molotovCount;
        public int FlashCount => flashCount;

        /// <summary>(타입, 새 보유량)</summary>
        public event Action<ThrowableType, int> OnCountChanged;

        private void Awake()
        {
            grenadeCount = Mathf.Max(0, startingGrenade);
            molotovCount = Mathf.Max(0, startingMolotov);
            flashCount   = Mathf.Max(0, startingFlash);
        }

        public int GetCount(ThrowableType type) => type switch
        {
            ThrowableType.Grenade => grenadeCount,
            ThrowableType.Molotov => molotovCount,
            ThrowableType.Flash   => flashCount,
            _ => 0
        };

        public bool Has(ThrowableType type) => GetCount(type) > 0;

        public void Add(ThrowableType type, int count = 1)
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

        public bool TryConsume(ThrowableType type)
        {
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
