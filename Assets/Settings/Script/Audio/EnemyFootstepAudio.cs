using UnityEngine;
using UnityEngine.AI;
using ProjectM.Enemy;
using ProjectM.Network;

namespace ProjectM.Audio
{
    /// <summary>
    /// 적 지상 이동 시 3D 발자국 루프를 재생한다. 멀티 클라이언트는 Transform 차분으로 속도를 추정한다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public class EnemyFootstepAudio : MonoBehaviour
    {
        [SerializeField] private string walkLoopResourcePath = "Sound/Enemy/footstep_normal";
        [SerializeField] private float volume = 0.45f;
        [SerializeField] private float pitch = 1f;
        [SerializeField] private float moveThreshold = 0.05f;
        [SerializeField] private float minDistance = 1.5f;
        [SerializeField] private float maxDistance = 22f;
        [SerializeField] private float maxAudibleDistance = 25f;

        private EnemyAIController enemyAi;
        private NavMeshAgent agent;
        private AudioSource walkSource;
        private Transform listenerTransform;
        private Vector3 lastPosition;
        private bool hasLastPosition;

        private void Awake()
        {
            enemyAi = GetComponent<EnemyAIController>();
            agent = GetComponent<NavMeshAgent>();

            walkSource = gameObject.AddComponent<AudioSource>();
            walkSource.loop = true;
            walkSource.playOnAwake = false;
            walkSource.spatialBlend = 1f;
            walkSource.rolloffMode = AudioRolloffMode.Linear;
            walkSource.dopplerLevel = 0f;
            walkSource.minDistance = minDistance;
            walkSource.maxDistance = maxDistance;
            walkSource.volume = volume;
            walkSource.pitch = pitch;

            var clip = Resources.Load<AudioClip>(walkLoopResourcePath);
            if (clip == null)
            {
                Debug.LogWarning($"[EnemyFootstepAudio] Clip not found: Resources/{walkLoopResourcePath} ({name})");
            }
            else
            {
                clip.LoadAudioData();
                walkSource.clip = clip;
            }

            lastPosition = transform.position;
            hasLastPosition = true;
        }

        private void OnEnable()
        {
            if (enemyAi != null)
                enemyAi.OnDeath += HandleDeath;

            listenerTransform = null;
            lastPosition = transform.position;
            hasLastPosition = true;
        }

        private void OnDisable()
        {
            if (enemyAi != null)
                enemyAi.OnDeath -= HandleDeath;

            StopWalkLoop();
        }

        private void HandleDeath(EnemyAIController _) => StopWalkLoop();

        private void LateUpdate()
        {
            if (!ShouldPlayWalkLoop())
            {
                StopWalkLoop();
                lastPosition = transform.position;
                hasLastPosition = true;
                return;
            }

            walkSource.volume = volume;
            walkSource.pitch = pitch;

            if (!walkSource.isPlaying && walkSource.clip != null)
                walkSource.Play();

            lastPosition = transform.position;
            hasLastPosition = true;
        }

        private bool ShouldPlayWalkLoop()
        {
            if (walkSource == null || walkSource.clip == null)
                return false;

            if (enemyAi != null && (!enemyAi.IsAlive || enemyAi.IsStunned))
                return false;

            if (!IsWithinAudibleRange())
                return false;

            return GetHorizontalSpeed() > moveThreshold;
        }

        private bool IsWithinAudibleRange()
        {
            if (listenerTransform == null)
            {
                var listener = FindAnyObjectByType<AudioListener>();
                listenerTransform = listener != null ? listener.transform : null;
            }

            if (listenerTransform == null)
                return true;

            return Vector3.Distance(listenerTransform.position, transform.position) <= maxAudibleDistance;
        }

        private float GetHorizontalSpeed()
        {
            if (ShouldUseAgentVelocity())
            {
                var velocity = agent.velocity;
                velocity.y = 0f;
                if (velocity.sqrMagnitude > 0.0001f)
                    return velocity.magnitude;
            }

            if (!hasLastPosition || Time.deltaTime <= 0f)
                return 0f;

            var delta = transform.position - lastPosition;
            delta.y = 0f;
            return delta.magnitude / Time.deltaTime;
        }

        private bool ShouldUseAgentVelocity()
        {
            return agent != null && agent.enabled && agent.isOnNavMesh
                && (!NetworkSessionHelper.IsMultiplayerSession || NetworkSessionHelper.IsServer);
        }

        private void StopWalkLoop()
        {
            if (walkSource != null && walkSource.isPlaying)
                walkSource.Stop();
        }
    }
}
