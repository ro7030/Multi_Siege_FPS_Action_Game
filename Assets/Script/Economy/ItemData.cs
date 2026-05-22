using System;

namespace ProjectM.Economy
{
    public enum ItemType
    {
        None,
        Heal,           // (legacy) 즉시 회복
        AmmoRefill,     // 예비 탄약 지급 (즉시)
        WeaponUpgrade,  // 무기 강화 (즉시)
        Consumable,     // 기타 소모품
        HealKit,        // 인벤토리 추가 → 3번키 후 좌클릭으로 자가 회복
        RepairKit,      // 인벤토리 추가 → 3번키 후 좌클릭으로 방어물 수리
        FarmKit,        // 인벤토리 추가 → 3번키 후 좌클릭으로 밭 설치
        Grenade,        // 투척: 수류탄
        Molotov,        // 투척: 화염병
        Flash,          // 투척: 섬광탄
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
