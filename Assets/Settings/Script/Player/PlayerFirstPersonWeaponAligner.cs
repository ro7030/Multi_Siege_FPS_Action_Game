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
        [SerializeField] private Transform primarySocket;
        [SerializeField] private Transform meleeSocket;
        [SerializeField] private Transform kitSocket;
        [SerializeField] private Transform throwableSocket;
        [SerializeField] private ThrowableEquipper throwableEquipper;
        [Tooltip("투척무기 1인칭 스냅 기본 오프셋(소켓 로컬). 하단 UI 가림 방지.")]
        [SerializeField] private Vector3 throwableFirstPersonLocalOffset = new(0f, 0.18f, 0.03f);

        private NetworkObject networkObject;

        private void Awake()
        {
            if (attachedVisual == null)
                attachedVisual = GetComponent<PlayerAttachedWeaponVisual>();
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

            // 근접(검)은 Hand_r_equipment 본·SwordWalk 등 손 애니를 그대로 따른다.
            // 카메라 스냅을 쓰면 걸을 때 손만 움직이고 칼이 공중에 떠 보인다.
            if (!ShouldSnapToCameraSocket(kind))
            {
                attachedVisual.RestoreActiveHandOffset();
                return;
            }

            Transform target = ResolveAlignTarget(kind);
            if (target == null)
                return;

            Vector3 position = target.position;
            Quaternion rotation = target.rotation;
            if (kind == AttachedWeaponDisplayKind.Throwable)
            {
                Vector3 localOffset = throwableFirstPersonLocalOffset;
                if (attachedVisual != null && throwableEquipper != null)
                {
                    var def = throwableEquipper.GetDefinition(attachedVisual.ActiveThrowableType);
                    if (def != null)
                        localOffset += def.attachedFpAlignOffset;
                }

                position = target.TransformPoint(localOffset);
            }

            instance.transform.SetPositionAndRotation(position, rotation);
        }

        private static bool ShouldSnapToCameraSocket(AttachedWeaponDisplayKind kind)
        {
            return kind switch
            {
                AttachedWeaponDisplayKind.Primary => true,
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
