using UnityEngine;

namespace ProjectM.Player
{
    /// <summary>
    /// [디버그] 소생 시스템 테스트용. 컴포넌트 우클릭 메뉴 또는 키로 즉시 다운시킨다.
    /// 더미 팀원에 붙여서 다운 → F키 소생 흐름을 검증한다.
    /// </summary>
    [RequireComponent(typeof(HealthSystem))]
    public class ReviveDebugTester : MonoBehaviour
    {
        [SerializeField] private HealthSystem health;
        [SerializeField] private KeyCode downKey = KeyCode.None; // 키로도 다운 가능 (선택)

        private void Awake()
        {
            if (health == null) health = GetComponent<HealthSystem>();
        }

        private void Update()
        {
            if (downKey != KeyCode.None && Input.GetKeyDown(downKey)) ForceDown();
        }

        [ContextMenu("Down (즉시 다운)")]
        public void ForceDown()
        {
            if (health != null) health.TakeDamage(999999f, null); // HP 0 → ReviveSystem 이 다운 상태로 전환
            Debug.Log($"[ReviveTest] {name} 강제 다운");
        }

        [ContextMenu("Heal (체력 회복)")]
        public void ForceHeal()
        {
            if (health != null) health.ResetHp();
        }
    }
}
