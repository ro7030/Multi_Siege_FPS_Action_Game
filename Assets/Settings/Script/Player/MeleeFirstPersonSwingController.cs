using Unity.Netcode;
using UnityEngine;

namespace ProjectM.Player
{
    /// <summary>
    /// Owner 1인칭 검 공격 시 매 타 좌→우 수평 베기를 적용한다.
    /// PlayerFirstPersonWeaponAligner(0) 이후 LateUpdate에서 idle 포즈 위에 델타를 더한다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public class MeleeFirstPersonSwingController : MonoBehaviour
    {
        [SerializeField] private MeleeWeapon meleeWeapon;
        [SerializeField] private PlayerAttachedWeaponVisual attachedVisual;
        [SerializeField] private Transform meleeSocket;
        [SerializeField] private NetworkObject networkObject;

        [Header("스윙 타이밍")]
        [SerializeField] private float swingDuration = 0.24f;

        [Header("수평 베기 (좌→우)")]
        [SerializeField] private float arcHorizontal = 0.24f;
        [SerializeField] private float verticalDip = -0.03f;
        [SerializeField] private float forwardPush = 0.09f;

        [Header("회전 델타")]
        [SerializeField] private float yawDegrees = 56f;
        [SerializeField] private float rollDegrees = -36f;
        [SerializeField] private float pitchDegrees = 10f;

        [Header("레거시 FP (useAttachedWeapons=false)")]
        [SerializeField] private Vector3 legacyIdleLocalOffset = new(0.10f, -0.07f, 0.04f);
        [SerializeField] private Vector3 legacyIdleLocalEuler = new(5f, -30f, 12f);

        private float swingStartTime = float.NegativeInfinity;

        private void Awake()
        {
            if (meleeWeapon == null)
                meleeWeapon = GetComponent<MeleeWeapon>();
            if (attachedVisual == null)
                attachedVisual = GetComponent<PlayerAttachedWeaponVisual>();
            if (networkObject == null)
                networkObject = GetComponent<NetworkObject>();
        }

        private void OnEnable()
        {
            if (meleeWeapon != null)
                meleeWeapon.OnAttack += HandleAttack;
        }

        private void OnDisable()
        {
            if (meleeWeapon != null)
                meleeWeapon.OnAttack -= HandleAttack;
        }

        private void HandleAttack()
        {
            if (!IsLocalOwner() || !IsMeleeDisplayActive())
                return;

            swingStartTime = Time.time;
        }

        private void LateUpdate()
        {
            if (!IsLocalOwner() || !IsSwingActive())
                return;

            Transform weapon = ResolveWeaponTransform();
            if (weapon == null || meleeSocket == null)
                return;

            float duration = Mathf.Max(0.01f, swingDuration);
            float t = (Time.time - swingStartTime) / duration;
            EvaluateLeftToRightSlash(Mathf.Clamp01(t), out Vector3 swingPos, out Quaternion swingRot);

            if (attachedVisual != null && attachedVisual.UseAttachedWeapons)
            {
                weapon.SetPositionAndRotation(
                    weapon.position + meleeSocket.TransformVector(swingPos),
                    weapon.rotation * swingRot);
            }
            else
            {
                weapon.localPosition = legacyIdleLocalOffset + swingPos;
                weapon.localRotation = Quaternion.Euler(legacyIdleLocalEuler) * swingRot;
            }
        }

        private void EvaluateLeftToRightSlash(float t, out Vector3 swingPos, out Quaternion swingRot)
        {
            const int sign = 1; // 좌→우 (-X → +X)

            float progress = EaseOutCubic(t);
            float wave = Mathf.Sin(t * Mathf.PI);

            float x = sign * arcHorizontal * Mathf.Lerp(-1f, 1f, progress) * wave;
            swingPos = new Vector3(x, verticalDip * wave, forwardPush * wave);

            float yaw = sign * yawDegrees * progress;
            float roll = sign * rollDegrees * wave;
            float pitch = pitchDegrees * wave;
            swingRot = Quaternion.Euler(pitch, -yaw, roll);
        }

        private static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        private bool IsSwingActive()
        {
            if (swingStartTime <= float.NegativeInfinity + 1f)
                return false;

            return Time.time - swingStartTime < swingDuration;
        }

        private bool IsMeleeDisplayActive()
        {
            if (attachedVisual != null && attachedVisual.UseAttachedWeapons)
                return attachedVisual.ActiveDisplay == AttachedWeaponDisplayKind.Secondary;

            return meleeWeapon != null && meleeWeapon.IsActive;
        }

        private Transform ResolveWeaponTransform()
        {
            if (attachedVisual != null && attachedVisual.UseAttachedWeapons)
            {
                if (attachedVisual.ActiveDisplay != AttachedWeaponDisplayKind.Secondary)
                    return null;

                var instance = attachedVisual.ActiveDisplayedInstance;
                return instance != null ? instance.transform : null;
            }

            return meleeWeapon != null ? meleeWeapon.FirstPersonViewTransform : null;
        }

        private bool IsLocalOwner()
        {
            if (networkObject != null && networkObject.IsSpawned)
                return networkObject.IsOwner;

            return true;
        }
    }
}
