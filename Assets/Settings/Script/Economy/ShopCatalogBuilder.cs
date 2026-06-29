using System.Collections.Generic;
using ProjectM.Player;
using UnityEngine;

namespace ProjectM.Economy
{
    /// <summary>
    /// 상점 탭(대분류/소분류)에 맞는 목록 항목을 만든다.
    /// </summary>
    public static class ShopCatalogBuilder
    {
        public static IReadOnlyList<ShopSubTab> GetSubTabsForTop(ShopTopTab top)
        {
            switch (top)
            {
                case ShopTopTab.Weapon:
                    return new[] { ShopSubTab.Gun, ShopSubTab.Melee, ShopSubTab.Throw, ShopSubTab.Ammo };
                case ShopTopTab.Kit:
                    return new[] { ShopSubTab.Heal };
                case ShopTopTab.Base:
                    return new[] { ShopSubTab.Defense, ShopSubTab.Currency };
                default:
                    return System.Array.Empty<ShopSubTab>();
            }
        }

        public static string GetSubTabLabel(ShopSubTab sub)
        {
            switch (sub)
            {
                case ShopSubTab.Gun: return "총기";
                case ShopSubTab.Melee: return "근접";
                case ShopSubTab.Throw: return "투척";
                case ShopSubTab.Ammo: return "탄";
                case ShopSubTab.Heal: return "회복";
                case ShopSubTab.Defense: return "방어";
                case ShopSubTab.Currency: return "재화";
                default: return sub.ToString();
            }
        }

        public static List<ShopEntry> BuildEntries(
            ShopTopTab top,
            ShopSubTab sub,
            ShopController shop,
            CurrencyWallet wallet,
            WeaponProgression progression,
            PlayerArsenal arsenal)
        {
            var list = new List<ShopEntry>();
            if (shop == null) return list;

            switch (top)
            {
                case ShopTopTab.Weapon when sub == ShopSubTab.Gun:
                    AddWeaponTiers(list, WeaponSlot.Primary, shop, wallet, progression, arsenal);
                    break;
                case ShopTopTab.Weapon when sub == ShopSubTab.Melee:
                    AddWeaponTiers(list, WeaponSlot.Secondary, shop, wallet, progression, arsenal);
                    break;
                case ShopTopTab.Weapon when sub == ShopSubTab.Throw:
                    AddCatalogBySub(list, shop, wallet, ShopTopTab.Weapon, ShopSubTab.Throw);
                    break;
                case ShopTopTab.Weapon when sub == ShopSubTab.Ammo:
                    AddCatalogBySub(list, shop, wallet, ShopTopTab.Weapon, ShopSubTab.Ammo);
                    break;
                case ShopTopTab.Kit when sub == ShopSubTab.Heal:
                    AddCatalogBySub(list, shop, wallet, ShopTopTab.Kit, ShopSubTab.Heal);
                    break;
                case ShopTopTab.Base when sub == ShopSubTab.Defense:
                    AddCatalogBySub(list, shop, wallet, ShopTopTab.Base, ShopSubTab.Defense);
                    break;
                case ShopTopTab.Base when sub == ShopSubTab.Currency:
                    AddCatalogBySub(list, shop, wallet, ShopTopTab.Base, ShopSubTab.Currency);
                    break;
            }

            return list;
        }

        private static void AddWeaponTiers(
            List<ShopEntry> list,
            WeaponSlot slot,
            ShopController shop,
            CurrencyWallet wallet,
            WeaponProgression progression,
            PlayerArsenal arsenal)
        {
            if (progression == null) return;

            var tiers = progression.GetTiers(slot);
            if (tiers == null) return;

            int current = arsenal != null ? arsenal.CurrentTierIndex(slot) : 0;

            for (int i = 0; i < tiers.Count; i++)
            {
                var def = tiers[i];
                if (def == null) continue;

                bool owned = i <= current;
                bool canBuy = arsenal != null && arsenal.CanPurchaseTier(slot, i);
                int price = i > 0 ? def.price : 0;

                var entry = new ShopEntry
                {
                    entryId = $"weapon_{slot}_{i}",
                    kind = ShopEntryKind.WeaponTier,
                    weaponSlot = slot,
                    weaponTierIndex = i,
                    displayName = def.displayName,
                    price = price,
                    description = def.damage > 0 ? $"공격력 {def.damage}" : "",
                    isOwned = owned,
                    unlocked = true,
                    affordable = canBuy && wallet != null && wallet.Balance >= price,
                    canPurchase = canBuy && (price <= 0 || (wallet != null && wallet.Balance >= price)),
                    icon = def.icon,
                };
                list.Add(entry);
            }
        }

        private static void AddCatalogBySub(
            List<ShopEntry> list,
            ShopController shop,
            CurrencyWallet wallet,
            ShopTopTab top,
            ShopSubTab sub)
        {
            if (shop.Catalog == null) return;

            foreach (var item in shop.Catalog.items)
            {
                if (item == null) continue;
                if (item.shopTop != top || item.shopSub != sub) continue;

                bool unlocked = shop.IsUnlocked(item);
                bool affordable = wallet != null && wallet.Balance >= item.price;

                list.Add(new ShopEntry
                {
                    entryId = item.id,
                    kind = ShopEntryKind.CatalogItem,
                    catalogItem = item,
                    displayName = item.displayName,
                    price = item.price,
                    description = item.description,
                    unlocked = unlocked,
                    affordable = affordable,
                    canPurchase = unlocked && affordable,
                    icon = item.listIcon,
                });
            }
        }
    }
}
