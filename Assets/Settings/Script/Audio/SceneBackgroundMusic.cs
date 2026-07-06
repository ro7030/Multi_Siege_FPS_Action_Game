using UnityEngine;

namespace ProjectM.Audio
{
    // 씬에 배치된 배경음악. 씬 이탈 시 오브젝트가 파괴되며 자동 정지된다.
    [DisallowMultipleComponent]
    public class SceneBackgroundMusic : MonoBehaviour
    {
        [SerializeField] private string resourcePath = "Music/Login";
        [SerializeField] [Range(0f, 1f)] private float volume = 0.35f;
        [SerializeField] private bool playOnStart = true;

        private AudioSource audioSource;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 0f;
            audioSource.volume = volume;
        }

        private void Start()
        {
            if (!playOnStart)
                return;

            var clip = Resources.Load<AudioClip>(resourcePath);
            if (clip == null)
            {
                Debug.LogWarning($"[SceneBackgroundMusic] AudioClip not found: Resources/{resourcePath}");
                return;
            }

            audioSource.clip = clip;
            audioSource.Play();
        }

        private void OnDestroy()
        {
            if (audioSource != null && audioSource.isPlaying)
                audioSource.Stop();
        }
    }
}
