using UnityEngine;

namespace ProjectM.Player
{
    public enum ThrowableType { None, Grenade, Molotov, Flash }
    public enum ThrowableEffect { Damage, Fire, Stun }

    // 투척무기 1종 데이터. 기획서 9-4 기준. Unity 에서 자유롭게 추가/삭제 가능.
    [CreateAssetMenu(menuName = "ProjectM/Weapon/ThrowableDefinition", fileName = "ThrowableDef")]
    public class ThrowableDefinition : ScriptableObject
    {
        public string displayName = "Throwable";
        public ThrowableType type = ThrowableType.Grenade;
        public ThrowableEffect effect = ThrowableEffect.Damage;

        [Header("효과")]
        public float damage = 120f;
        public float radius = 4f;
        [Tooltip("던진 후 폭발까지 시간(초). 0이면 충돌 시 폭발.")]
        public float fuseTime = 1.5f;
        [Tooltip("Fire: 장판 지속(초) / Stun: 기절(초). Damage 타입은 무시.")]
        public float effectDuration = 0f;
        [Tooltip("Fire: 장판 1틱당 데미지. Stun/Damage 타입은 무시.")]
        public float fireTickDamage;
        [Tooltip("Fire: 장판 데미지 틱 간격(초).")]
        public float fireTickInterval = 0.5f;

        [Header("프리팹/표시")]
        [Tooltip("던질 프리팹 (ThrowableProjectile + Rigidbody + Collider). 비우면 기본 구체 생성.")]
        public GameObject projectilePrefab;
        [Tooltip("들고 있을 때 1인칭 카메라 자식 소켓에 인스턴스화되는 모델 프리팹.")]
        public GameObject heldViewModelPrefab;
        [Tooltip("부착 무기 모드에서 Owner 1인칭 카메라 스냅 시 추가 로컬 오프셋(하단 UI 가림 보정).")]
        public Vector3 attachedFpAlignOffset;
        public Sprite icon;
        public int price = 35;

        [Header("사운드")]
        [Tooltip("Resources 경로. 예: Sound/Throw/throw_grenade")]
        public string throwSoundResourcePath;
        [Range(0f, 1f)] public float throwSoundVolume = 0.85f;
        [Range(0.5f, 1.5f)] public float throwSoundPitch = 1f;
    }
}
