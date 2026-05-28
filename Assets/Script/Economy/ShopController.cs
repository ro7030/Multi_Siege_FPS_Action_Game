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
        [SerializeField] private KitInventory playerKitInventory;
        [SerializeField] private ThrowableInventory playerThrowableInventory;
        [SerializeField] private PlayerArsenal playerArsenal;

        public ItemCatalog Catalog => catalog;
        public int CurrentWave => session != null ? session.State.CurrentWave : 1;

        public event Action<ItemData> OnPurchased;
        public event Action<ItemData, string> OnPurchaseFailed; // 사유

        private void Awake()
        {
            if (wallet == null) wallet = LocalPlayerUtility.FindLocalCurrencyWallet();
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
            if (playerHealth == null) playerHealth = LocalPlayerUtility.FindLocalHealthSystem();
            if (playerWeapon == null) playerWeapon = LocalPlayerUtility.FindLocalWeaponController();
            if (playerKitInventory == null) playerKitInventory = FindAnyObjectByType<KitInventory>();
            if (playerThrowableInventory == null) playerThrowableInventory = FindAnyObjectByType<ThrowableInventory>();
            if (playerArsenal == null) playerArsenal = FindAnyObjectByType<PlayerArsenal>();
        }

        // ── 무기 업그레이드 ────────────────────────────────────────
        public bool CanUpgradeWeapon(WeaponSlot slot) => playerArsenal != null && playerArsenal.CanUpgrade(slot);

        public int WeaponUpgradePrice(WeaponSlot slot) => playerArsenal != null ? playerArsenal.NextUpgradePrice(slot) : 0;

        public string WeaponUpgradeName(WeaponSlot slot)
        {
            var d = playerArsenal != null ? playerArsenal.NextTier(slot) : null;
            return d != null ? d.displayName : "MAX";
        }

        public bool TryUpgradeWeapon(WeaponSlot slot)
        {
            if (playerArsenal == null) { Fail(null, "무기고 없음"); return false; }
            int next = playerArsenal.CurrentTierIndex(slot) + 1;
            return TryPurchaseWeaponTier(slot, next);
        }

        /// <summary>원하는 무기 티어를 바로 구매(순서 무관, 미보유 티어만).</summary>
        public bool TryPurchaseWeaponTier(WeaponSlot slot, int tierIndex)
        {
            if (playerArsenal == null) { Fail(null, "무기고 없음"); return false; }
            if (!playerArsenal.CanPurchaseTier(slot, tierIndex)) { Fail(null, "구매 불가"); return false; }

            int price = playerArsenal.GetTierPrice(slot, tierIndex);
            if (wallet == null) { Fail(null, "지갑 없음"); return false; }
            if (price > 0 && !wallet.TrySpend(price)) { Fail(null, "잔액 부족"); return false; }

            if (!playerArsenal.TrySetTier(slot, tierIndex))
            {
                if (price > 0) wallet.Add(price);
                Fail(null, "적용 실패");
                return false;
            }

            OnPurchased?.Invoke(null);
            Debug.Log($"[Shop] {slot} 티어 {tierIndex} 구매 (-{price}, 잔액 {wallet.Balance})");
            return true;
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
                    // legacy: 즉시 회복
                    if (playerHealth != null) playerHealth.Heal(item.value);
                    break;
                case ItemType.AmmoRefill:
                    if (playerWeapon != null) playerWeapon.AddReserveAmmo(Mathf.RoundToInt(item.value));
                    break;
                case ItemType.WeaponUpgrade:
                    Debug.Log($"[Shop] WeaponUpgrade 효과 적용 보류: {item.displayName} (+{item.value})");
                    break;
                case ItemType.Consumable:
                    Debug.Log($"[Shop] Consumable {item.displayName} 사용");
                    break;

                // 키트류: 인벤토리에 추가 (실제 사용은 KitEquipper 가 좌클릭 시 처리)
                case ItemType.HealKit:
                    AddKit(KitType.HealKit, item);
                    break;
                case ItemType.RepairKit:
                    AddKit(KitType.RepairKit, item);
                    break;
                case ItemType.FarmKit:
                    AddKit(KitType.FarmKit, item);
                    break;

                // 투척무기: ThrowableInventory 에 추가
                case ItemType.Grenade:
                    AddThrowable(ThrowableType.Grenade, item);
                    break;
                case ItemType.Molotov:
                    AddThrowable(ThrowableType.Molotov, item);
                    break;
                case ItemType.Flash:
                    AddThrowable(ThrowableType.Flash, item);
                    break;
            }
        }

        private void AddThrowable(ThrowableType type, ItemData item)
        {
            if (playerThrowableInventory == null)
            {
                Debug.LogWarning($"[Shop] {type} 구매했지만 ThrowableInventory 가 없음 — 효과 미적용");
                return;
            }
            int count = Mathf.Max(1, Mathf.RoundToInt(item.value));
            playerThrowableInventory.Add(type, count);
            Debug.Log($"[Shop] {type} +{count} (총 {playerThrowableInventory.GetCount(type)})");
        }

        private void AddKit(KitType type, ItemData item)
        {
            if (playerKitInventory == null)
            {
                Debug.LogWarning($"[Shop] {type} 구매했지만 KitInventory 가 없음 — 효과 미적용");
                return;
            }
            int count = Mathf.Max(1, Mathf.RoundToInt(item.value)); // value 를 묶음 단위로 사용
            playerKitInventory.Add(type, count);
            Debug.Log($"[Shop] {type} +{count} 인벤토리 추가 (총 {playerKitInventory.GetCount(type)})");
        }
    }
}
