using UnityEngine;

namespace ProjectM.Combat
{
    // 기절 등 CC 효과를 받을 수 있는 오브젝트(적 등)가 구현한다.
    public interface IStunnable
    {
        bool IsStunned { get; }
        void ApplyStun(float duration, GameObject source);
    }
}
