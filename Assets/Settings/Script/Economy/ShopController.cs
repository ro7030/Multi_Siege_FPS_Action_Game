using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using ProjectM.Core;
using ProjectM.Network;
using ProjectM.Player;

namespace ProjectM.Economy
{
    // 상점. 카탈로그에서 아이템 구매 요청을 검증하고, 화폐를 차감한 뒤 효과를 적용한다.
    // 효과 적용은 ItemType에 따라 분기. UI는 ShopView가 이 컴포넌트의 API만 사용한다.
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

        private PlayerArsenal ResolveLocalArsenal()
        {
            var resolved = LocalPlayerUtility.FindLocalComponent<PlayerArsenal>();
            if (resolved != null)
                playerArsenal = resolved;
            return resolved ?? playerArsenal;
        }

        // ── 무기 업그레이드 ────────────────────────────────────────
        public bool CanUpgradeWeapon(WeaponSlot slot)
        {
            var arsenal = ResolveLocalArsenal();
            return arsenal != null && arsenal.CanUpgrade(slot);
        }

        public int WeaponUpgradePrice(WeaponSlot slot)
        {
            var arsenal = ResolveLocalArsenal();
            return arsenal != null ? arsenal.NextUpgradePrice(slot) : 0;
        }

        public string WeaponUpgradeName(WeaponSlot slot)
        {
            var arsenal = ResolveLocalArsenal();
            var d = arsenal != null ? arsenal.NextTier(slot) : null;
            return d != null ? d.displayName : "MAX";
        }

        public bool TryUpgradeWeapon(WeaponSlot slot)
        {
            if (NetworkSessionHelper.IsMultiplayerSession)
            {
                var local = NetworkPlayerRegistry.LocalPlayer;
                if (local == null) { Fail(null, "플레이어 없음"); return false; }
                local.RequestWeaponUpgradeServerRpc((int)slot);
                return false;
            }

            var arsenal = ResolveLocalArsenal();
            if (arsenal == null) { Fail(null, "무기고 없음"); return false; }

            int next = arsenal.CurrentTierIndex(slot) + 1;
            return TryPurchaseWeaponTier(slot, next);
        }

        // 원하는 무기 티어를 바로 구매(순서 무관, 미보유 티어만).
        public bool TryPurchaseWeaponTier(WeaponSlot slot, int tierIndex)
        {
            if (NetworkSessionHelper.IsMultiplayerSession)
            {
                var local = NetworkPlayerRegistry.LocalPlayer;
                if (local == null) { Fail(null, "플레이어 없음"); return false; }
                local.RequestWeaponTierPurchaseServerRpc((int)slot, tierIndex);
                return false;
            }

            return TryPurchaseWeaponTierForPlayer(LocalPlayerUtility.FindLocalPlayerObject(), slot, tierIndex);
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

            if (NetworkSessionHelper.IsMultiplayerSession)
            {
                var local = NetworkPlayerRegistry.LocalPlayer;
                if (local == null) { Fail(item, "플레이어 없음"); return false; }
                local.RequestShopPurchaseServerRpc(new FixedString64Bytes(item.id));
                return false;
            }

            return TryPurchaseForPlayer(LocalPlayerUtility.FindLocalPlayerObject(), item);
        }

        public bool TryPurchaseForPlayer(GameObject buyer, string itemId)
        {
            if (catalog == null) return false;
            var item = catalog.GetById(itemId);
            return TryPurchaseForPlayer(buyer, item);
        }

        public bool TryPurchaseForPlayer(GameObject buyer, ItemData item)
        {
            if (item == null) { Fail(null, "아이템 없음"); return false; }
            if (!IsUnlocked(item)) { Fail(item, "아직 잠금"); return false; }

            var buyerWallet = buyer != null ? buyer.GetComponent<CurrencyWallet>() : null;
            if (buyerWallet == null) { Fail(item, "지갑 없음"); return false; }
            if (!buyerWallet.TrySpend(item.price)) { Fail(item, "잔액 부족"); return false; }

            ApplyEffectForBuyer(buyer, item);
            OnPurchased?.Invoke(item);

            ulong clientId = 0;
            if (buyer != null && buyer.TryGetComponent(out NetworkPlayer netPlayer))
                clientId = netPlayer.OwnerClientId;

            Debug.Log(
                $"[Shop] 구매 성공: {item.displayName} (-{item.price}, 잔액 {buyerWallet.Balance}) " +
                $"buyer={buyer?.name} clientId={clientId}");
            return true;
        }

        // 서버 구매 후 Owner 클라이언트에만 적용 — NGO 미동기화 즉시 효과(탄약 등).
        public void ApplyPurchasedEffectOnOwner(GameObject buyer, string itemId)
        {
            if (catalog == null || buyer == null || string.IsNullOrEmpty(itemId)) return;

            var item = catalog.GetById(itemId);
            if (item == null || !RequiresOwnerLocalApply(item.type)) return;

            ApplyOwnerLocalEffectForBuyer(buyer, item);
        }

        public bool RequiresOwnerLocalApply(ItemType type) => type == ItemType.AmmoRefill;

        public bool RequiresOwnerLocalApplyForItem(string itemId)
        {
            if (catalog == null || string.IsNullOrEmpty(itemId)) return false;
            var item = catalog.GetById(itemId);
            return item != null && RequiresOwnerLocalApply(item.type);
        }

        private void ApplyOwnerLocalEffectForBuyer(GameObject buyer, ItemData item)
        {
            if (buyer == null || item == null) return;

            switch (item.type)
            {
                case ItemType.AmmoRefill:
                    var buyerWeapon = ResolveBuyerWeaponController(buyer);
                    if (buyerWeapon == null)
                    {
                        ulong clientId = buyer.TryGetComponent(out NetworkPlayer np) ? np.OwnerClientId : 0;
                        Debug.LogWarning(
                            $"[Shop][Owner] WeaponController not found — buyer={buyer.name} clientId={clientId} item={item.id}");
                        break;
                    }

                    int amount = Mathf.RoundToInt(item.value);
                    int before = buyerWeapon.ReserveAmmo;
                    buyerWeapon.AddReserveAmmo(amount);
                    Debug.Log(
                        $"[Shop][Owner] AmmoRefill +{amount} reserve {before}→{buyerWeapon.ReserveAmmo} " +
                        $"buyer={buyer.name} item={item.id}");
                    break;
            }
        }

        private static WeaponController ResolveBuyerWeaponController(GameObject buyer)
        {
            if (buyer == null) return null;

            var weapon = buyer.GetComponentInChildren<WeaponController>();
            if (weapon != null) return weapon;

            if (buyer.TryGetComponent(out NetworkPlayer netPlayer) && netPlayer.IsOwner)
                return LocalPlayerUtility.FindLocalWeaponController();

            return null;
        }

        public bool TryPurchaseWeaponTierForPlayer(GameObject buyer, WeaponSlot slot, int tierIndex)
        {
            var arsenal = buyer != null ? buyer.GetComponent<PlayerArsenal>() : null;
            var buyerWallet = buyer != null ? buyer.GetComponent<CurrencyWallet>() : null;

            if (arsenal == null) { Fail(null, "무기고 없음"); return false; }
            if (!arsenal.CanPurchaseTier(slot, tierIndex)) { Fail(null, "구매 불가"); return false; }

            int price = arsenal.GetTierPrice(slot, tierIndex);
            if (buyerWallet == null) { Fail(null, "지갑 없음"); return false; }
            if (price > 0 && !buyerWallet.TrySpend(price)) { Fail(null, "잔액 부족"); return false; }

            if (!arsenal.TrySetTier(slot, tierIndex))
            {
                if (price > 0) buyerWallet.Add(price);
                Fail(null, "적용 실패");
                return false;
            }

            OnPurchased?.Invoke(null);
            Debug.Log($"[Shop] {slot} 티어 {tierIndex} 구매 (-{price}, 잔액 {buyerWallet.Balance})");
            return true;
        }

        private void Fail(ItemData item, string reason)
        {
            OnPurchaseFailed?.Invoke(item, reason);
            Debug.LogWarning($"[Shop] 구매 실패: {(item != null ? item.displayName : "?")} ({reason})");
        }

        private void ApplyEffect(ItemData item)
        {
            ApplyEffectForBuyer(LocalPlayerUtility.FindLocalPlayerObject(), item);
        }

        private void ApplyEffectForBuyer(GameObject buyer, ItemData item)
        {
            if (buyer == null) return;

            var buyerHealth = buyer.GetComponentInChildren<HealthSystem>();
            var buyerWeapon = buyer.GetComponentInChildren<WeaponController>();
            var buyerKitInventory = buyer.GetComponent<KitInventory>();
            var buyerThrowableInventory = buyer.GetComponent<ThrowableInventory>();

            switch (item.type)
            {
                case ItemType.Heal:
                    if (buyerHealth != null) buyerHealth.Heal(item.value);
                    break;
                case ItemType.AmmoRefill:
                    // MP: Owner ClientRpc가 ReserveAmmo를 적용한다. 서버 쪽 WeaponController는 HUD에 반영되지 않는다.
                    if (NetworkSessionHelper.IsMultiplayerSession) break;
                    if (buyerWeapon != null) buyerWeapon.AddReserveAmmo(Mathf.RoundToInt(item.value));
                    break;
                case ItemType.WeaponUpgrade:
                    Debug.Log($"[Shop] WeaponUpgrade 효과 적용 보류: {item.displayName} (+{item.value})");
                    break;
                case ItemType.Consumable:
                    Debug.Log($"[Shop] Consumable {item.displayName} 사용");
                    break;

                case ItemType.HealKit:
                    AddKit(buyerKitInventory, KitType.HealKit, item);
                    break;
                case ItemType.RepairKit:
                    AddKit(buyerKitInventory, KitType.RepairKit, item);
                    break;
                case ItemType.FarmKit:
                    AddKit(buyerKitInventory, KitType.FarmKit, item);
                    break;

                case ItemType.Grenade:
                    AddThrowable(buyerThrowableInventory, ThrowableType.Grenade, item);
                    break;
                case ItemType.Molotov:
                    AddThrowable(buyerThrowableInventory, ThrowableType.Molotov, item);
                    break;
                case ItemType.Flash:
                    AddThrowable(buyerThrowableInventory, ThrowableType.Flash, item);
                    break;
            }
        }

        private void AddThrowable(ThrowableInventory inventory, ThrowableType type, ItemData item)
        {
            if (inventory == null)
            {
                Debug.LogWarning($"[Shop] {type} 구매했지만 ThrowableInventory 가 없음 — 효과 미적용");
                return;
            }
            int count = Mathf.Max(1, Mathf.RoundToInt(item.value));
            inventory.Add(type, count);
            Debug.Log($"[Shop] {type} +{count} (총 {inventory.GetCount(type)})");
        }

        private void AddKit(KitInventory inventory, KitType type, ItemData item)
        {
            if (inventory == null)
            {
                Debug.LogWarning($"[Shop] {type} 구매했지만 KitInventory 가 없음 — 효과 미적용");
                return;
            }
            int count = Mathf.Max(1, Mathf.RoundToInt(item.value));
            inventory.Add(type, count);
            Debug.Log($"[Shop] {type} +{count} 인벤토리 추가 (총 {inventory.GetCount(type)})");
        }
    }
}
