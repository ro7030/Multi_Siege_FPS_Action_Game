using System;
using UnityEngine;

namespace ProjectM.Player
{
    /// <summary>
    /// 체력 관리. 플레이어/적/방어 오브젝트가 공용으로 사용한다.
    /// 실제 데미지 판정은 Host 권한이지만 MVP 검증 단계에서는 로컬에서 직접 호출 가능.
    /// </summary>
    public class HealthSystem : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHp = 100f;
        [SerializeField] private float currentHp = 100f;
        [SerializeField] private bool destroyOnDeath = false;

        public float MaxHp => maxHp;
        public float CurrentHp => currentHp;
        public bool IsAlive => currentHp > 0f;
        public float HpRatio => maxHp <= 0 ? 0 : Mathf.Clamp01(currentHp / maxHp);

        public event Action<float, float> OnHpChanged; // (current, max)
        public event Action<float, GameObject> OnDamaged; // (amount, attacker)
        public event Action<GameObject> OnDied; // (attacker)
        public event Action<float> OnHealed;

        private void Awake()
        {
            currentHp = Mathf.Clamp(currentHp <= 0 ? maxHp : currentHp, 0, maxHp);
        }

        public void TakeDamage(float amount, GameObject attacker)
        {
            if (!IsAlive || amount <= 0f) return;
            currentHp = Mathf.Max(0f, currentHp - amount);
            OnDamaged?.Invoke(amount, attacker);
            OnHpChanged?.Invoke(currentHp, maxHp);

            if (currentHp <= 0f)
            {
                OnDied?.Invoke(attacker);
                if (destroyOnDeath) Destroy(gameObject);
            }
        }

        public void Heal(float amount)
        {
            if (amount <= 0f || !IsAlive) return;
            currentHp = Mathf.Min(maxHp, currentHp + amount);
            OnHealed?.Invoke(amount);
            OnHpChanged?.Invoke(currentHp, maxHp);
        }

        public void ResetHp()
        {
            currentHp = maxHp;
            OnHpChanged?.Invoke(currentHp, maxHp);
        }

        public void SetMaxHp(float value, bool refill = true)
        {
            maxHp = Mathf.Max(1f, value);
            if (refill) currentHp = maxHp;
            else currentHp = Mathf.Min(currentHp, maxHp);
            OnHpChanged?.Invoke(currentHp, maxHp);
        }
    }
}
