using System;
using ProjectM.Player;
using UnityEngine;

namespace ProjectM.Economy
{
    public enum ShopTopTab
    {
        Weapon = 0,
        Kit = 1,
        Base = 2,
    }

    public enum ShopSubTab
    {
        Gun = 0,
        Melee = 1,
        Throw = 2,
        Ammo = 3,

        Heal = 10,

        Defense = 20,
        Currency = 21,
    }

    public enum ShopEntryKind
    {
        CatalogItem,
        WeaponTier,
    }

    [Serializable]
    public class ShopEntry
    {
        public string entryId;
        public ShopEntryKind kind;
        public string displayName;
        public int price;
        public string description;
        public bool unlocked = true;
        public bool affordable;
        public bool canPurchase;
        public bool isOwned;

        public ItemData catalogItem;
        public WeaponSlot weaponSlot;
        public int weaponTierIndex;

        /// <summary>상세 패널 Image 스프라이트.</summary>
        public Sprite icon;
    }
}
