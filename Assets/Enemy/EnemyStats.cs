using UnityEngine;

namespace ProjectM.Enemy
{
    public enum EnemyTier { Normal, Special, Boss }

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

        [Header("보상")]
        public int currencyReward = 5;
        public int scoreReward = 10;
    }
}
