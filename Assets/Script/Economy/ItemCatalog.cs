using System.Collections.Generic;
using UnityEngine;

namespace ProjectM.Economy
{
    /// <summary>
    /// 상점에서 판매할 아이템 카탈로그. 데이터 전용 SO.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectM/Economy/ItemCatalog", fileName = "ItemCatalog")]
    public class ItemCatalog : ScriptableObject
    {
        public List<ItemData> items = new();

        public ItemData GetById(string id)
        {
            foreach (var it in items)
                if (it != null && it.id == id) return it;
            return null;
        }

        public IEnumerable<ItemData> GetUnlocked(int currentWave)
        {
            foreach (var it in items)
                if (it != null && it.unlockWave <= currentWave) yield return it;
        }
    }
}
