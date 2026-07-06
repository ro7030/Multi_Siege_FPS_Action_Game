using System;
using UnityEngine;
using ProjectM.Player;

namespace ProjectM.Defense
{
    // 방어 구조물 공통 베이스. HealthSystem 위에서 수리/파괴 이벤트를 제공한다.
    // "DefenseObject" 태그가 자동으로 적용되어 EnemyAI의 우선 타깃이 된다.
    [RequireComponent(typeof(HealthSystem))]
    public class DefenseObject : MonoBehaviour
    {
        [Header("표시")]
        [SerializeField] private string displayName = "Defense Object";
        [SerializeField] private bool autoApplyTag = true;

        public string DisplayName => displayName;
        public bool IsDestroyed => health != null && !health.IsAlive;
        public HealthSystem Health => health;

        public event Action<DefenseObject, float> OnDamaged;
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

        // 디버그 UI 전용 일괄 회복.
        public void RepairInstant(float amount)
        {
            if (IsDestroyed || amount <= 0f) return;
            health.Heal(amount);
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
