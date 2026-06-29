using System.Collections.Generic;
using UnityEngine;

namespace ProjectM.Player
{
    /// <summary>
    /// 무기 단계 진행표. 주무기/보조무기 각각의 단계 목록을 리스트 순서로 정의한다.
    /// Unity 에서 WeaponDefinition 에셋을 리스트에 추가/제거하면 단계가 늘거나 준다.
    ///   primaryTiers[0]   = 기본 지급 주무기 (러스트 카빈)
    ///   primaryTiers[1..] = 업그레이드 단계
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectM/Weapon/WeaponProgression", fileName = "WeaponProgression")]
    public class WeaponProgression : ScriptableObject
    {
        [Tooltip("주 무기 단계 (순서 = 단계). [0]은 기본 지급.")]
        public List<WeaponDefinition> primaryTiers = new();

        [Tooltip("보조 무기 단계 (순서 = 단계). [0]은 기본 지급.")]
        public List<WeaponDefinition> secondaryTiers = new();

        public List<WeaponDefinition> GetTiers(WeaponSlot slot)
            => slot == WeaponSlot.Primary ? primaryTiers : secondaryTiers;

        public int TierCount(WeaponSlot slot)
        {
            var list = GetTiers(slot);
            return list != null ? list.Count : 0;
        }

        public WeaponDefinition GetTier(WeaponSlot slot, int index)
        {
            var list = GetTiers(slot);
            if (list == null || index < 0 || index >= list.Count) return null;
            return list[index];
        }
    }
}
