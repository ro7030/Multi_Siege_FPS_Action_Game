using Unity.Netcode;
using UnityEngine;
using ProjectM.Audio;
using ProjectM.Player;

namespace ProjectM.Network
{
    // NGO 세션에서 서버가 투척 투사체를 Launch/Despawn 한다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(ThrowableProjectile))]
    public class NetworkThrowableProjectile : NetworkBehaviour
    {
        private ThrowableProjectile projectile;
        private ThrowableDefinition pendingDef;
        private GameObject pendingThrower;
        private Vector3 pendingVelocity;
        private bool hasPendingLaunch;
        private int velocityReapplyFrames;

        private void Awake()
        {
            projectile = GetComponent<ThrowableProjectile>();
        }

        private void OnEnable()
        {
            if (projectile != null)
                projectile.OnExploded += HandleExploded;
        }

        private void OnDisable()
        {
            if (projectile != null)
                projectile.OnExploded -= HandleExploded;
        }

        // 서버(피해 판정 권한)에서 실제 폭발이 일어난 순간, 게스트 클라이언트에도 폭발 VFX를 재생시킨다.
        // 서버 자신은 Explode() 안에서 이미 로컬로 VFX를 재생했으므로 ClientRpc 수신 시 중복 재생하지 않는다.
        private void HandleExploded()
        {
            // 싱글플레이(비스폰 상태)에서는 RPC를 보낼 필요/자격이 없다 — 로컬에서 이미 VFX·SFX 재생 완료.
            if (!IsSpawned || !NetworkSessionHelper.IsServer)
                return;

            var position = transform.position;
            var type = projectile != null ? projectile.ThrowableType : ThrowableType.None;

            PlayExplosionVfxClientRpc();
            PlayExplosionSfxClientRpc((int)type, position);
        }

        [ClientRpc]
        private void PlayExplosionVfxClientRpc()
        {
            if (IsServer)
                return;

            projectile?.PlayExplosionVfxOnly();
        }

        [ClientRpc]
        private void PlayExplosionSfxClientRpc(int typeInt, Vector3 position)
        {
            if (IsServer)
                return;

            GameSoundManager.EnsureInstance().PlayThrowableEffect((ThrowableType)typeInt, position);
        }

        public void PrepareServerLaunch(ThrowableDefinition def, GameObject thrower, Vector3 velocity)
        {
            pendingDef = def;
            pendingThrower = thrower;
            pendingVelocity = velocity;
            hasPendingLaunch = true;
        }

        public override void OnNetworkSpawn()
        {
            ConfigureRigidbodyForRole();

            if (IsServer && hasPendingLaunch)
                ApplyServerLaunch();
        }

        private void ApplyServerLaunch()
        {
            hasPendingLaunch = false;
            if (projectile == null || pendingDef == null)
                return;

            projectile.Launch(pendingDef, pendingThrower, pendingVelocity, serverOnlyExplosion: true);
            velocityReapplyFrames = 3;
        }

        private void FixedUpdate()
        {
            if (!IsServer || velocityReapplyFrames <= 0)
                return;

            velocityReapplyFrames--;
            var rb = GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
                rb.linearVelocity = pendingVelocity;
        }

        private void ConfigureRigidbodyForRole()
        {
            var rb = GetComponent<Rigidbody>();
            if (rb == null)
                return;

            // 서버만 물리 시뮬, 클라이언트는 NetworkTransform 위치를 따른다.
            rb.isKinematic = !IsServer;
            if (IsServer)
            {
                rb.WakeUp();
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
}
