using UnityEngine;

namespace ProjectM.Defense
{
    /// <summary>
    /// [Deprecated] 슬롯 기반 밭 설치 컴포넌트.
    /// 새 구조에서는 KitEquipper(3번키 + 좌클릭) 로 밭을 설치한다.
    /// 추후 안전하게 삭제 가능.
    /// </summary>
    [System.Obsolete("KitEquipper.UseFarmKit() 로 대체되었습니다. 씬에서 제거하세요.")]
    public class FarmPlaceInteractable : MonoBehaviour
    {
        private void Awake()
        {
            Debug.LogWarning("[FarmPlaceInteractable] Deprecated — KitEquipper(3번키) 로 대체되었습니다.");
            enabled = false;
        }
    }
}
