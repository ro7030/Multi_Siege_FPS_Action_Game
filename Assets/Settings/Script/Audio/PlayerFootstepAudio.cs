using UnityEngine;
using ProjectM.Player;
using ProjectM.UI;

namespace ProjectM.Audio
{
    /// <summary>
    /// 로컬 플레이어 지상 이동 시 발자국 루프를 재생한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerFootstepAudio : MonoBehaviour
    {
        [SerializeField] private string walkLoopResourcePath = "Sound/footstep_rocky_walk_loop";
        [SerializeField] private float walkVolume = 0.55f;
        [SerializeField] private float moveThreshold = 0.1f;
        [SerializeField] private float sprintSpeedThreshold = 6.5f;
        [SerializeField] private float sprintPitch = 1.12f;

        private CharacterController characterController;
        private PlayerController playerController;
        private AudioSource walkSource;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            playerController = GetComponent<PlayerController>();

            walkSource = gameObject.AddComponent<AudioSource>();
            walkSource.loop = true;
            walkSource.playOnAwake = false;
            walkSource.spatialBlend = 0f;
            walkSource.volume = walkVolume;

            var clip = Resources.Load<AudioClip>(walkLoopResourcePath);
            if (clip == null)
                Debug.LogWarning($"[PlayerFootstepAudio] Clip not found: Resources/{walkLoopResourcePath}");
            else
                walkSource.clip = clip;
        }

        private void OnDisable()
        {
            StopWalkLoop();
        }

        private void Update()
        {
            if (!ShouldPlayWalkLoop())
            {
                StopWalkLoop();
                return;
            }

            walkSource.volume = walkVolume;
            walkSource.pitch = GetHorizontalSpeed() >= sprintSpeedThreshold ? sprintPitch : 1f;

            if (!walkSource.isPlaying)
                walkSource.Play();
        }

        private bool ShouldPlayWalkLoop()
        {
            if (playerController != null && !playerController.IsLocalPlayer)
                return false;

            if (playerController != null && !playerController.CanControl)
                return false;

            if (UIInputModal.IsBlockingGameplayInput)
                return false;

            if (characterController == null || !characterController.isGrounded)
                return false;

            return GetHorizontalSpeed() > moveThreshold;
        }

        private float GetHorizontalSpeed()
        {
            if (characterController == null)
                return 0f;

            var velocity = characterController.velocity;
            velocity.y = 0f;
            return velocity.magnitude;
        }

        private void StopWalkLoop()
        {
            if (walkSource != null && walkSource.isPlaying)
                walkSource.Stop();
        }
    }
}
