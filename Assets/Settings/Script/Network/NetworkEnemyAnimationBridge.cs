using System;
using Unity.Netcode;
using UnityEngine;

namespace ProjectM.Network
{
    // 서버(Host)가 계산한 적 Animator 파라미터를 NetworkVariable로 복제한다.
    // 적은 Owner가 아닌 서버가 시뮬레이션 권한을 가지므로 Write 권한은 Server.
    [DisallowMultipleComponent]
    public class NetworkEnemyAnimationBridge : NetworkBehaviour
    {
        private const float FloatChangeThreshold = 0.05f;

        private readonly NetworkVariable<float> netSpeed = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> netGrounded = new(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> netSprint = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> netVerticalSpeed = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> netAttackToken = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private float lastSentSpeed;
        private float lastSentVerticalSpeed;

        public float SyncedSpeed => netSpeed.Value;
        public bool SyncedGrounded => netGrounded.Value;
        public bool SyncedSprint => netSprint.Value;
        public float SyncedVerticalSpeed => netVerticalSpeed.Value;

        public event Action OnSyncedStateChanged;
        // 서버가 Attack 상태에 진입한 순간마다 정확히 1회 발생 (클라이언트 애니메이션 트리거용).
        public event Action OnAttackRequested;

        public void Publish(float speed, bool grounded, bool sprint, float verticalSpeed)
        {
            if (!IsSpawned || !IsServer) return;

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

            if (netSprint.Value != sprint)
                netSprint.Value = sprint;
        }

        // 공격 1회 이벤트를 클라이언트에 전파한다. bool과 달리 값이 매번 바뀌어 edge가 유실되지 않는다.
        public void PublishAttack()
        {
            if (!IsSpawned || !IsServer) return;

            unchecked
            {
                netAttackToken.Value = netAttackToken.Value + 1;
            }
        }

        public override void OnNetworkSpawn()
        {
            netSpeed.OnValueChanged += HandleAnyChanged;
            netGrounded.OnValueChanged += HandleBoolChanged;
            netSprint.OnValueChanged += HandleBoolChanged;
            netVerticalSpeed.OnValueChanged += HandleAnyChanged;
            netAttackToken.OnValueChanged += HandleAttackTokenChanged;

            OnSyncedStateChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            netSpeed.OnValueChanged -= HandleAnyChanged;
            netGrounded.OnValueChanged -= HandleBoolChanged;
            netSprint.OnValueChanged -= HandleBoolChanged;
            netVerticalSpeed.OnValueChanged -= HandleAnyChanged;
            netAttackToken.OnValueChanged -= HandleAttackTokenChanged;
        }

        private void HandleAnyChanged(float _, float __) => OnSyncedStateChanged?.Invoke();
        private void HandleBoolChanged(bool _, bool __) => OnSyncedStateChanged?.Invoke();
        private void HandleAttackTokenChanged(int _, int __) => OnAttackRequested?.Invoke();
    }
}
