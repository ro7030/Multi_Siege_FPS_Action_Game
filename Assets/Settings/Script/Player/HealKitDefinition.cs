using UnityEngine;

namespace ProjectM.Player
{
    /// <summary>
    /// 힐킷 1단계의 데이터. ScriptableObject 라서 Unity 에서 자유롭게 추가/삭제 가능.
    /// 단계는 HealKitProgression 의 리스트 순서로 정의된다.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectM/Player/HealKitDefinition", fileName = "HealKitDef")]
    public class HealKitDefinition : ScriptableObject
    {
        public string displayName = "HealKit";
        [Tooltip("상점 업그레이드 가격. 0이면 기본 지급(1단계).")]
        public int price = 0;
        public float healAmount = 50f;
        [Tooltip("들고 있을 때 1인칭 카메라 자식 소켓에 인스턴스화되는 모델 프리팹.")]
        public GameObject heldViewModelPrefab;
        public Sprite icon;
    }
}
