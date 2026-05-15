using System;

namespace ProjectM.Economy
{
    public enum ItemType
    {
        None,
        Heal,           // 체력 회복
        AmmoRefill,     // 예비 탄약 지급
        WeaponUpgrade,  // 무기 강화 (데미지 등)
        Consumable,     // 기타 소모품
    }

    [Serializable]
    public class ItemData
    {
        public string id = "item_id";
        public string displayName = "Item";
        public ItemType type = ItemType.Heal;

        public int price = 50;
        public int unlockWave = 1; // 이 웨이브부터 구매 가능

        public float value = 50f;  // 효과 수치 (회복량, 탄약 수, 데미지 증가량 등)

        public string description = "";
    }
}
