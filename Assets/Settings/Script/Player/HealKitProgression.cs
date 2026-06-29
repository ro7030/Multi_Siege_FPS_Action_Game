using System.Collections.Generic;
using UnityEngine;

namespace ProjectM.Player
{
    /// <summary>
    /// 힐킷 단계 진행표. 리스트 순서가 곧 단계.
    ///   tiers[0]   = 기본 지급 힐킷
    ///   tiers[1..] = 업그레이드 단계
    /// Unity 에서 HealKitDefinition 을 리스트에 추가/제거하면 단계가 늘거나 준다.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectM/Player/HealKitProgression", fileName = "HealKitProgression")]
    public class HealKitProgression : ScriptableObject
    {
        [Tooltip("힐킷 단계 (순서 = 단계). [0]은 기본 지급.")]
        public List<HealKitDefinition> tiers = new();

        public int TierCount => tiers != null ? tiers.Count : 0;

        public HealKitDefinition GetTier(int index)
        {
            if (tiers == null || index < 0 || index >= tiers.Count) return null;
            return tiers[index];
        }
    }
}
