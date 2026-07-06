using System.Collections.Generic;
using UnityEngine;
using ProjectM.Player;

namespace ProjectM.Audio
{
    // 로컬 플레이어 투척 시 던지기 SFX를 재생한다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ThrowableEquipper))]
    public class PlayerThrowAudio : MonoBehaviour
    {
        private ThrowableEquipper throwableEquipper;
        private AudioSource throwSource;
        private readonly Dictionary<string, AudioClip> clipCache = new();
        private readonly HashSet<string> warnedMissingPaths = new();

        private void Awake()
        {
            throwableEquipper = GetComponent<ThrowableEquipper>();

            throwSource = gameObject.AddComponent<AudioSource>();
            throwSource.playOnAwake = false;
            throwSource.spatialBlend = 0f;
        }

        private void OnEnable()
        {
            if (throwableEquipper != null)
                throwableEquipper.OnThrown += HandleThrown;
        }

        private void OnDisable()
        {
            if (throwableEquipper != null)
                throwableEquipper.OnThrown -= HandleThrown;
        }

        private void HandleThrown()
        {
            if (throwableEquipper == null || !throwableEquipper.IsLocalPlayer)
                return;

            var type = throwableEquipper.LastThrownType;
            if (type == ThrowableType.None)
                return;

            var def = throwableEquipper.GetDefinition(type);
            if (def == null || string.IsNullOrEmpty(def.throwSoundResourcePath))
                return;

            var clip = GetClip(def.throwSoundResourcePath);
            if (clip == null)
                return;

            throwSource.pitch = def.throwSoundPitch;
            throwSource.PlayOneShot(clip, def.throwSoundVolume);
        }

        private AudioClip GetClip(string resourcePath)
        {
            if (clipCache.TryGetValue(resourcePath, out var cached))
                return cached;

            var clip = Resources.Load<AudioClip>(resourcePath);
            if (clip != null)
                clip.LoadAudioData();

            clipCache[resourcePath] = clip;

            if (clip == null && warnedMissingPaths.Add(resourcePath))
                Debug.LogWarning($"[PlayerThrowAudio] Clip not found: Resources/{resourcePath}");

            return clip;
        }
    }
}
