using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectM.Core;
using ProjectM.Player;

namespace ProjectM.Economy
{
    /// <summary>
    /// 상점. 카탈로그에서 아이템 구매 요청을 검증하고, 화폐를 차감한 뒤 효과를 적용한다.
    /// 효과 적용은 ItemType에 따라 분기. UI는 ShopView가 이 컴포넌트의 API만 사용한다.
    /// </summary>
    public class ShopController : MonoBehaviour
    {
        [SerializeField] private ItemCatalog catalog;
        [SerializeField] private CurrencyWallet wallet;
        [SerializeField] private GameSessionManager session;

        [Header("효과 적용 대상 (로컬 플레이어)")]
        [SerializeField] private HealthSystem playerHealth;
        [SerializeField] private WeaponController playerWeapon;

        public ItemCatalog Catalog => catalog;
        public int CurrentWave => session != null ? session.State.CurrentWave : 1;

        public event Action<ItemData> OnPurchased;
        public event Action<ItemData, string> OnPurchaseFailed; // 사유

        private void Awake()
        {
            if (wallet == null) wallet = FindAnyObjectByType<CurrencyWallet>();
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
            if (playerHealth == null) playerHealth = FindAnyObjectByType<HealthSystem>();
            if (playerWeapon == null) playerWeapon = FindAnyObjectByType<WeaponController>();
        }

        public IEnumerable<ItemData> GetUnlockedItems()
        {
            if (catalog == null) yield break;
            int wave = Mathf.Max(1, CurrentWave);
            foreach (var it in catalog.GetUnlocked(wave)) yield return it;
        }

        public bool IsUnlocked(ItemData item)
        {
            return item != null && item.unlockWave <= Mathf.Max(1, CurrentWave);
        }

        public bool TryPurchase(string itemId)
        {
            if (catalog == null) { Fail(null, "카탈로그 없음"); return false; }
            var item = catalog.GetById(itemId);
            return TryPurchase(item);
        }

        public bool TryPurchase(ItemData item)
        {
            if (item == null) { Fail(null, "아이템 없음"); return false; }
            if (!IsUnlocked(item)) { Fail(item, "아직 잠금"); return false; }
            if (wallet == null) { Fail(item, "지갑 없음"); return false; }
            if (!wallet.TrySpend(item.price)) { Fail(item, "잔액 부족"); return false; }

            ApplyEffect(item);
            OnPurchased?.Invoke(item);
            Debug.Log($"[Shop] 구매 성공: {item.displayName} (-{item.price}, 잔액 {wallet.Balance})");
            return true;
        }

        private void Fail(ItemData item, string reason)
        {
            OnPurchaseFailed?.Invoke(item, reason);
            Debug.LogWarning($"[Shop] 구매 실패: {(item != null ? item.displayName : "?")} ({reason})");
        }

        private void ApplyEffect(ItemData item)
        {
            switch (item.type)
            {
                case ItemType.Heal:
                    if (playerHealth != null) playerHealth.Heal(item.value);
                    break;
                case ItemType.AmmoRefill:
                    if (playerWeapon != null) playerWeapon.AddReserveAmmo(Mathf.RoundToInt(item.value));
                    break;
                case ItemType.WeaponUpgrade:
                    // MVP: 데미지 강화는 WeaponController가 별도 API를 노출할 때 연결. 일단 로그만.
                    Debug.Log($"[Shop] WeaponUpgrade 효과 적용 보류: {item.displayName} (+{item.value})");
                    break;
                case ItemType.Consumable:
                    Debug.Log($"[Shop] Consumable {item.displayName} 사용");
                    break;
            }
        }
    }
}
