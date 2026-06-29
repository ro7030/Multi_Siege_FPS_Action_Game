using UnityEngine;

namespace ProjectM.Player
{
    /// <summary>
    /// 피격 가능한 모든 오브젝트(플레이어, 적, 방어 구조물)가 구현하는 공통 인터페이스.
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(float amount, GameObject attacker);
    }
}
