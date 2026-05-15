using System;
using UnityEngine;
using ProjectM.Player;

namespace ProjectM.Defense
{
    /// <summary>
    /// 방어 구조물 공통 베이스. HealthSystem 위에서 수리/파괴 이벤트를 제공한다.
    /// "DefenseObject" 태그가 자동으로 적용되어 EnemyAI의 우선 타깃이 된다.
    /// </summary>
    [RequireComponent(typeof(HealthSystem))]
    public class DefenseObject : MonoBehaviour
    {
        [Header("수리 설정")]
        [SerializeField] private float repairPerSecond = 10f;
        [SerializeField] private int repairCost = 0; // 0이면 무료, Phase 6에서 화폐 차감 연결
        [SerializeField] private bool canRepairWhileDestroyed = false;

        [Header("표시")]
        [SerializeField] private string displayName = "Defense Object";
        [SerializeField] private bool autoApplyTag = true;

        public string DisplayName => displayName;
        public float RepairPerSecond => repairPerSecond;
        public int RepairCost => repairCost;
        public bool IsDestroyed => health != null && !health.IsAlive;
        public HealthSystem Health => health;

        public event Action<DefenseObject, float> OnDamaged; // 받은 데미지
        public event Action<DefenseObject, float> OnRepaired; // 수리량
        public event Action<DefenseObject> OnDestroyed;

        private HealthSystem health;

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
            if (autoApplyTag) TryApplyDefenseTag();
        }

        private void OnEnable()
        {
            health.OnDamaged += HandleHealthDamaged;
            health.OnDied += HandleHealthDied;
        }

        private void OnDisable()
        {
            health.OnDamaged -= HandleHealthDamaged;
            health.OnDied -= HandleHealthDied;
        }

        private void HandleHealthDamaged(float amount, GameObject attacker)
        {
            OnDamaged?.Invoke(this, amount);
        }

        private void HandleHealthDied(GameObject _)
        {
            OnDestroyed?.Invoke(this);
            Debug.Log($"[Defense] {displayName} 파괴됨");
        }

        /// <summary>지속 수리 (delta는 초 단위). 외부 인터랙션이 매 프레임 호출.</summary>
        public void Repair(float deltaSeconds)
        {
            if (!canRepairWhileDestroyed && IsDestroyed) return;
            if (health.CurrentHp >= health.MaxHp) return;
            float amount = repairPerSecond * deltaSeconds;
            health.Heal(amount);
            OnRepaired?.Invoke(this, amount);
        }

        /// <summary>일괄 수리 (수치 직접 지정).</summary>
        public void RepairInstant(float amount)
        {
            if (!canRepairWhileDestroyed && IsDestroyed) return;
            health.Heal(amount);
            OnRepaired?.Invoke(this, amount);
        }

        private void TryApplyDefenseTag()
        {
            try { gameObject.tag = "DefenseObject"; }
            catch (UnityException)
            {
                Debug.LogWarning("[Defense] \"DefenseObject\" 태그가 등록되어 있지 않습니다. Project Settings > Tags and Layers 에 추가하세요.");
            }
        }
    }
}
