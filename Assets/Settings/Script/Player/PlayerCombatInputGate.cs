using UnityEngine;

namespace ProjectM.Player
{
    // 키트 사용·무기 전환·커서 재잠금 직후 좌클릭이 총/근접 공격으로 새는 것을 방지한다.
    public class PlayerCombatInputGate : MonoBehaviour
    {
        private const float DefaultSuppressDuration = 0.2f;

        private float suppressUntil;

        public bool IsSuppressed => Time.time < suppressUntil;

        public void Suppress(float duration = DefaultSuppressDuration) =>
            suppressUntil = Mathf.Max(suppressUntil, Time.time + duration);
    }

}