using System;
using UnityEngine;

namespace ProjectM.Enemy
{
    public enum EnemyTier { Normal, Special, Boss }

    [Obsolete("EnemyAIController가 고정 티어(플레이어>게이트>밭>베이스)를 사용합니다.")]
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

        [Tooltip("공격 감지 범위. 범위 안: 플레이어·방어 모두. 범위 밖 fallback: 게이트·밭·베이스만(플레이어 제외).")]
        public float detectRange = 25f;

        [Header("레거시 (미사용)")]
        [Obsolete("EnemyAIController가 detectRange와 고정 티어 규칙을 사용합니다.")]
        public TargetPriority targetPriority = TargetPriority.DefenseFirst;
        [Obsolete("EnemyAIController가 detectRange를 사용합니다.")]
        public float playerAggroRange = 10f;

        [Header("보상")]
        public int currencyReward = 5;
        public int scoreReward = 10;
    }
}
