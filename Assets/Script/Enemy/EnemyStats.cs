using UnityEngine;

namespace ProjectM.Enemy
{
    public enum EnemyTier { Normal, Special, Boss }

    /// <summary>
    /// 적의 1순위 타겟 설정.
    /// DefenseFirst : 방어물 우선. 단, 플레이어가 detectRange 안에 들어오면 어그로(플레이어 공격).
    /// PlayerFirst  : 플레이어 우선 (사거리 무제한 추적). 플레이어가 한 명도 없으면 방어물 공격.
    /// </summary>
    public enum TargetPriority { DefenseFirst, PlayerFirst }

    [CreateAssetMenu(menuName = "ProjectM/Enemy/EnemyStats", fileName = "EnemyStats")]
    public class EnemyStats : ScriptableObject
    {
        public string displayName = "Enemy";
        public EnemyTier tier = EnemyTier.Normal;

        [Header("스탯")]
        public float maxHp = 50f;
        public float moveSpeed = 3.5f;
        public float attackDamage = 10f;
        public float attackRange = 1.8f;
        public float attackInterval = 1.2f;
        public float detectRange = 25f;

        [Header("타겟 우선순위")]
        [Tooltip("DefenseFirst: 방어물 우선 (단, detectRange 안 플레이어에게 어그로) / PlayerFirst: 플레이어 우선")]
        public TargetPriority targetPriority = TargetPriority.DefenseFirst;

        [Header("보상")]
        public int currencyReward = 5;
        public int scoreReward = 10;
    }
}
