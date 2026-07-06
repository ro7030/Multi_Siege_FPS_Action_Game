using System;
using Unity.Netcode;
using UnityEngine;

namespace ProjectM.Network
{
    // Owner 클라이언트의 Animator 파라미터를 NetworkVariable로 복제한다.
    [DisallowMultipleComponent]
    public class NetworkPlayerAnimationBridge : NetworkBehaviour
    {
        private const float FloatChangeThreshold = 0.05f;

        private readonly NetworkVariable<float> netSpeed = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<bool> netGrounded = new(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<float> netVerticalSpeed = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<bool> netIsAiming = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<bool> netIsReloading = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> netThrowToken = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<bool> netIsMelee = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> netAttackToken = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private float lastSentSpeed;
        private float lastSentVerticalSpeed;

        public float SyncedSpeed => netSpeed.Value;
        public bool SyncedGrounded => netGrounded.Value;
        public float SyncedVerticalSpeed => netVerticalSpeed.Value;
        public bool SyncedIsAiming => netIsAiming.Value;
        public bool SyncedIsReloading => netIsReloading.Value;
        public bool SyncedIsMelee => netIsMelee.Value;

        public event Action OnSyncedStateChanged;
        // Owner가 투척을 실행한 순간마다 정확히 1회 발생 (Remote 애니메이션 트리거용).
        public event Action OnThrowRequested;
        // Owner가 근접 공격을 실행한 순간마다 정확히 1회 발생 (Remote 애니메이션 트리거용).
        public event Action OnAttackRequested;

        public void Publish(float speed, bool grounded, float verticalSpeed, bool isAiming, bool isReloading, bool isMelee)
        {
            if (!IsSpawned || !IsOwner) return;

            if (Mathf.Abs(speed - lastSentSpeed) > FloatChangeThreshold)
            {
                netSpeed.Value = speed;
                lastSentSpeed = speed;
            }

            if (Mathf.Abs(verticalSpeed - lastSentVerticalSpeed) > FloatChangeThreshold)
            {
                netVerticalSpeed.Value = verticalSpeed;
                lastSentVerticalSpeed = verticalSpeed;
            }

            if (netGrounded.Value != grounded)
                netGrounded.Value = grounded;

            if (netIsAiming.Value != isAiming)
                netIsAiming.Value = isAiming;

            if (netIsReloading.Value != isReloading)
                netIsReloading.Value = isReloading;

            if (netIsMelee.Value != isMelee)
                netIsMelee.Value = isMelee;
        }

        public void ForcePublish(float speed, bool grounded, float verticalSpeed, bool isAiming, bool isReloading, bool isMelee)
        {
            if (!IsSpawned || !IsOwner) return;

            netSpeed.Value = speed;
            netGrounded.Value = grounded;
            netVerticalSpeed.Value = verticalSpeed;
            netIsAiming.Value = isAiming;
            netIsReloading.Value = isReloading;
            netIsMelee.Value = isMelee;

            lastSentSpeed = speed;
            lastSentVerticalSpeed = verticalSpeed;
        }

        // 투척 1회 이벤트를 Remote에 전파한다. bool과 달리 값이 매번 바뀌어 edge가 유실되지 않는다.
        public void PublishThrow()
        {
            if (!IsSpawned || !IsOwner) return;

            unchecked
            {
                netThrowToken.Value = netThrowToken.Value + 1;
            }
        }

        // 근접 공격 1회 이벤트를 Remote에 전파한다 (netThrowToken과 동일한 edge-보존 방식).
        public void PublishAttack()
        {
            if (!IsSpawned || !IsOwner) return;

            unchecked
            {
                netAttackToken.Value = netAttackToken.Value + 1;
            }
        }

        public override void OnNetworkSpawn()
        {
            netSpeed.OnValueChanged += HandleAnyChanged;
            netGrounded.OnValueChanged += HandleBoolChanged;
            netVerticalSpeed.OnValueChanged += HandleAnyChanged;
            netIsAiming.OnValueChanged += HandleBoolChanged;
            netIsReloading.OnValueChanged += HandleBoolChanged;
            netThrowToken.OnValueChanged += HandleThrowTokenChanged;
            netIsMelee.OnValueChanged += HandleBoolChanged;
            netAttackToken.OnValueChanged += HandleAttackTokenChanged;

            OnSyncedStateChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            netSpeed.OnValueChanged -= HandleAnyChanged;
            netGrounded.OnValueChanged -= HandleBoolChanged;
            netVerticalSpeed.OnValueChanged -= HandleAnyChanged;
            netIsAiming.OnValueChanged -= HandleBoolChanged;
            netIsReloading.OnValueChanged -= HandleBoolChanged;
            netThrowToken.OnValueChanged -= HandleThrowTokenChanged;
            netIsMelee.OnValueChanged -= HandleBoolChanged;
            netAttackToken.OnValueChanged -= HandleAttackTokenChanged;
        }

        private void HandleAnyChanged(float _, float __) => OnSyncedStateChanged?.Invoke();
        private void HandleBoolChanged(bool _, bool __) => OnSyncedStateChanged?.Invoke();
        private void HandleThrowTokenChanged(int _, int __) => OnThrowRequested?.Invoke();
        private void HandleAttackTokenChanged(int _, int __) => OnAttackRequested?.Invoke();
    }
}
