using UnityEngine;

namespace ProjectM.Economy
{
    /// <summary>
    /// [Deprecated] 구버전 수확 시스템. 새 구조에서는 FarmManager 가 수확/분배를 담당한다.
    ///
    /// 변경 이유:
    ///   - 시간 기반 → 웨이브 기반 성장으로 변경
    ///   - 단일 지갑 입금 → 팀 전체 균등 분배
    ///   - 수동 호출 → 웨이브 종료 시 자동 정산
    ///
    /// 이 컴포넌트가 씬에 남아있어도 동작하지 않도록 빈 클래스로 유지.
    /// 추후 안전하게 삭제 가능.
    /// </summary>
    [System.Obsolete("FarmManager 로 대체되었습니다. 씬에서 제거하세요.")]
    public class HarvestSystem : MonoBehaviour
    {
        private void Awake()
        {
            Debug.LogWarning("[HarvestSystem] Deprecated — FarmManager 로 대체되었습니다. 이 컴포넌트는 비활성화 상태입니다.");
            enabled = false;
        }
    }
}
