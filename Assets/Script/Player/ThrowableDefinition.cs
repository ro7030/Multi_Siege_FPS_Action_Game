using UnityEngine;

namespace ProjectM.Player
{
    public enum ThrowableType { None, Grenade, Molotov, Flash }
    public enum ThrowableEffect { Damage, Fire, Stun }

    /// <summary>
    /// 투척무기 1종 데이터. 기획서 9-4 기준. Unity 에서 자유롭게 추가/삭제 가능.
    /// </summary>
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
        [Tooltip("화염/스턴 지속 시간(초). Damage 타입은 무시.")]
        public float effectDuration = 0f;

        [Header("프리팹/표시")]
        [Tooltip("던질 프리팹 (ThrowableProjectile + Rigidbody + Collider). 비우면 기본 구체 생성.")]
        public GameObject projectilePrefab;
        public Sprite icon;
        public int price = 35;
    }
}
