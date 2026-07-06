using System.Collections.Generic;
using UnityEngine;
using ProjectM.Player;

namespace ProjectM.Audio
{
    // 로컬 플레이어 주무기 발사 시 1인칭 단발 SFX를 재생한다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponController))]
    public class PlayerGunAudio : MonoBehaviour
    {
        private const float PitchMin = 0.97f;
        private const float PitchMax = 1.03f;

        private WeaponController weaponController;
        private ThrowableEquipper throwableEquipper;
        private PlayerCombatInputGate combatInputGate;
        private AudioSource gunSource;
        private readonly Dictionary<string, AudioClip> clipCache = new();
        private readonly HashSet<string> warnedMissingPaths = new();

        private void Awake()
        {
            weaponController = GetComponent<WeaponController>();
            throwableEquipper = GetComponent<ThrowableEquipper>();
            combatInputGate = GetComponent<PlayerCombatInputGate>();

            gunSource = gameObject.AddComponent<AudioSource>();
            gunSource.playOnAwake = false;
            gunSource.spatialBlend = 0f;
        }

        private void OnEnable()
        {
            if (weaponController != null)
            {
                weaponController.OnFired += HandleFired;
                weaponController.OnReloadStart += HandleReloadStart;
            }
        }

        private void OnDisable()
        {
            if (weaponController != null)
            {
                weaponController.OnFired -= HandleFired;
                weaponController.OnReloadStart -= HandleReloadStart;
            }
        }

        private void HandleFired()
        {
            if (weaponController == null || !weaponController.IsLocalPlayer)
                return;

            if (combatInputGate != null && combatInputGate.IsSuppressed)
                return;

            if (throwableEquipper != null && throwableEquipper.SuppressesWeaponFire)
                return;

            var def = weaponController.CurrentDefinition;
            if (def == null || string.IsNullOrEmpty(def.fireSoundResourcePath))
                return;

            var clip = GetClip(def.fireSoundResourcePath);
            if (clip == null)
                return;

            gunSource.pitch = Random.Range(PitchMin, PitchMax);
            gunSource.PlayOneShot(clip, def.fireSoundVolume);
        }

        private void HandleReloadStart()
        {
            if (weaponController == null || !weaponController.IsLocalPlayer)
                return;

            if (combatInputGate != null && combatInputGate.IsSuppressed)
                return;

            var def = weaponController.CurrentDefinition;
            if (def == null || string.IsNullOrEmpty(def.reloadSoundResourcePath))
                return;

            var clip = GetClip(def.reloadSoundResourcePath);
            if (clip == null)
                return;

            gunSource.pitch = def.reloadSoundPitch;
            gunSource.PlayOneShot(clip, def.reloadSoundVolume);
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
                Debug.LogWarning($"[PlayerGunAudio] Clip not found: Resources/{resourcePath}");

            return clip;
        }
    }
}
