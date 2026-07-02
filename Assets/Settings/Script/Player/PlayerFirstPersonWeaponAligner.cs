using Unity.Netcode;
using UnityEngine;
using ProjectM.Network;

namespace ProjectM.Player
{
    /// <summary>
    /// Owner + useAttachedWeapons 일 때 활성 무기를 카메라 소켓 포즈에 스냅해 1인칭 느낌을 유지한다.
    /// 원격 클라이언트는 비활성 — 손 본 애니메이션을 따른다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerFirstPersonWeaponAligner : MonoBehaviour
    {
        [SerializeField] private PlayerAttachedWeaponVisual attachedVisual;
        [SerializeField] private PlayerArsenal arsenal;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private Transform primarySocket;
        [SerializeField] private Transform meleeSocket;
        [SerializeField] private Transform kitSocket;
        [SerializeField] private Transform throwableSocket;
        [SerializeField] private ThrowableEquipper throwableEquipper;

        [Tooltip("검 1인칭 스냅 기본 오프셋(소켓 로컬).")]
        [SerializeField] private Vector3 meleeFirstPersonLocalOffset = Vector3.zero;

        [Tooltip("투척무기 1인칭 스냅 기본 오프셋(소켓 로컬). 하단 UI 가림 방지.")]
        [SerializeField] private Vector3 throwableFirstPersonLocalOffset = new(0f, 0.18f, 0.03f);

        [Header("검 걸음 bob (선택)")]
        [SerializeField] private bool enableMeleeWalkBob;
        [SerializeField] private float meleeWalkBobAmplitude = 0.025f;
        [SerializeField] private float meleeWalkBobFrequency = 9f;

        private NetworkObject networkObject;

        private void Awake()
        {
            if (attachedVisual == null)
                attachedVisual = GetComponent<PlayerAttachedWeaponVisual>();
            if (arsenal == null)
                arsenal = GetComponent<PlayerArsenal>();
            if (characterController == null)
                characterController = GetComponent<CharacterController>();
            if (throwableEquipper == null)
                throwableEquipper = GetComponent<ThrowableEquipper>();
            networkObject = GetComponent<NetworkObject>();
        }

        private void LateUpdate()
        {
            if (attachedVisual == null || !attachedVisual.UseAttachedWeapons)
                return;

            if (!IsLocalOwner())
                return;

            var kind = attachedVisual.ActiveDisplay;
            var instance = attachedVisual.ActiveDisplayedInstance;
            if (instance == null)
                return;

            if (!ShouldSnapToCameraSocket(kind))
                return;

            Transform target = ResolveAlignTarget(kind);
            if (target == null)
                return;

            Vector3 position = target.position;
            Quaternion rotation = target.rotation;

            if (kind == AttachedWeaponDisplayKind.Secondary)
            {
                Vector3 localOffset = meleeFirstPersonLocalOffset;
                if (arsenal != null)
                {
                    var def = arsenal.CurrentDefinition(WeaponSlot.Secondary);
                    if (def != null)
                        localOffset += def.attachedFpAlignOffset;
                }

                position = target.TransformPoint(localOffset);
                position += ComputeMeleeWalkBob(target);
            }
            else if (kind == AttachedWeaponDisplayKind.Throwable)
            {
                Vector3 localOffset = throwableFirstPersonLocalOffset;
                if (throwableEquipper != null)
                {
                    var def = throwableEquipper.GetDefinition(attachedVisual.ActiveThrowableType);
                    if (def != null)
                        localOffset += def.attachedFpAlignOffset;
                }

                position = target.TransformPoint(localOffset);
            }

            instance.transform.SetPositionAndRotation(position, rotation);
        }

        private Vector3 ComputeMeleeWalkBob(Transform target)
        {
            if (!enableMeleeWalkBob || target == null)
                return Vector3.zero;

            float speed = 0f;
            if (characterController != null && characterController.enabled)
            {
                Vector3 velocity = characterController.velocity;
                velocity.y = 0f;
                speed = velocity.magnitude;
            }

            if (speed < 0.05f)
                return Vector3.zero;

            float t = Time.time * meleeWalkBobFrequency;
            float bobY = Mathf.Sin(t * Mathf.PI * 2f) * meleeWalkBobAmplitude * speed;
            float bobX = Mathf.Sin(t * Mathf.PI) * meleeWalkBobAmplitude * 0.5f * speed;
            return target.TransformVector(new Vector3(bobX, bobY, 0f));
        }

        private static bool ShouldSnapToCameraSocket(AttachedWeaponDisplayKind kind)
        {
            return kind switch
            {
                AttachedWeaponDisplayKind.Primary => true,
                AttachedWeaponDisplayKind.Secondary => true,
                AttachedWeaponDisplayKind.Kit => true,
                AttachedWeaponDisplayKind.Throwable => true,
                _ => false
            };
        }

        private Transform ResolveAlignTarget(AttachedWeaponDisplayKind kind)
        {
            return kind switch
            {
                AttachedWeaponDisplayKind.Primary => primarySocket,
                AttachedWeaponDisplayKind.Secondary => meleeSocket,
                AttachedWeaponDisplayKind.Kit => kitSocket != null ? kitSocket : primarySocket,
                AttachedWeaponDisplayKind.Throwable => throwableSocket != null ? throwableSocket : meleeSocket,
                _ => null
            };
        }

        private bool IsLocalOwner()
        {
            if (networkObject != null && networkObject.IsSpawned)
                return networkObject.IsOwner;

            return true;
        }
    }
}
