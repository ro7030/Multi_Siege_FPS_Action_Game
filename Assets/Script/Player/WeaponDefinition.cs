using UnityEngine;

namespace ProjectM.Player
{
    public enum WeaponSlot { Primary, Secondary }   // 주 무기 / 보조 무기
    public enum WeaponKind { Ranged, Melee }        // 원거리(총) / 근접(칼)

    /// <summary>
    /// 무기 1종(=1단계)의 데이터. ScriptableObject 라서 Unity 에서 자유롭게 추가/삭제 가능.
    /// 기획서 9-2(주무기), 9-3(보조무기) 기준.
    ///
    /// 같은 무기의 단계들은 WeaponProgression 의 리스트 순서로 정의된다.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectM/Weapon/WeaponDefinition", fileName = "WeaponDef")]
    public class WeaponDefinition : ScriptableObject
    {
        [Header("공통")]
        public string displayName = "Weapon";
        public WeaponSlot slot = WeaponSlot.Primary;
        public WeaponKind kind = WeaponKind.Ranged;
        [Tooltip("상점 업그레이드 가격. 0이면 기본 지급(1단계).")]
        public int price = 0;
        public float damage = 14f;
        public Sprite icon;   // UI 표시용 (선택)

        [Header("원거리 (Ranged 전용)")]
        public float fireRate = 5f;       // 초당 발사
        public int magazineSize = 30;
        public float reloadDuration = 2.5f;
        public float range = 200f;
        public bool isAutomatic = true;

        [Header("근접 (Melee 전용)")]
        [Tooltip("1회 공격 사이 간격(초). 기획서 '공격 속도'.")]
        public float attackInterval = 2.5f;
        public float meleeRange = 2.5f;
        [Tooltip("정면 기준 부채꼴 각도(도). 이 안에 있는 대상만 타격.")]
        public float meleeAngle = 100f;
    }
}
