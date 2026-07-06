using System.Collections.Generic;
using UnityEngine;
using ProjectM.Player;

namespace ProjectM.Audio
{
    /// <summary>
    /// 로컬 플레이어 근접 공격 시 1인칭 검격 SFX를 재생한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeleeWeapon))]
    public class PlayerMeleeAudio : MonoBehaviour
    {
        private const float PitchJitterMin = 0.97f;
        private const float PitchJitterMax = 1.03f;

        private MeleeWeapon meleeWeapon;
        private AudioSource meleeSource;
        private readonly Dictionary<string, AudioClip> clipCache = new();
        private readonly HashSet<string> warnedMissingPaths = new();

        private void Awake()
        {
            meleeWeapon = GetComponent<MeleeWeapon>();

            meleeSource = gameObject.AddComponent<AudioSource>();
            meleeSource.playOnAwake = false;
            meleeSource.spatialBlend = 0f;
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
            if (meleeWeapon == null || !meleeWeapon.IsLocalPlayer)
                return;

            var def = meleeWeapon.CurrentDefinition;
            if (def == null || string.IsNullOrEmpty(def.attackSoundResourcePath))
                return;

            var clip = GetClip(def.attackSoundResourcePath);
            if (clip == null)
                return;

            meleeSource.pitch = def.attackSoundPitch * Random.Range(PitchJitterMin, PitchJitterMax);
            meleeSource.PlayOneShot(clip, def.attackSoundVolume);
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
                Debug.LogWarning($"[PlayerMeleeAudio] Clip not found: Resources/{resourcePath}");

            return clip;
        }
    }

}