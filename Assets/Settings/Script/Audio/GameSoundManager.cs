using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ProjectM.Network;
using ProjectM.Player;

namespace ProjectM.Audio
{
    // 게임 SFX 중앙 재생·선로드·디버그 로그.
    // 씬에 없으면 최초 접근 시 자동 생성된다.
    public class GameSoundManager : MonoBehaviour
    {
        public static GameSoundManager Instance { get; private set; }

        private const float DefaultVolume = 1f;
        private const string UiClickPath = "Sound/UI/ui_click";
        private const string UiPurchasePath = "Sound/UI/ui_purchase";

        private static readonly Dictionary<ThrowableType, string> ThrowableResourcePaths = new()
        {
            { ThrowableType.Grenade, "Sound/grenade" },
            { ThrowableType.Flash, "Sound/flash" },
            { ThrowableType.Molotov, "Sound/molotov" },
        };

        private static readonly Dictionary<ThrowableType, float> ThrowableEffectVolumes = new()
        {
            { ThrowableType.Grenade, 1.5f },
            { ThrowableType.Flash, 1f },
            { ThrowableType.Molotov, 1f },
        };

        private static readonly Dictionary<DefenseSfxType, string> DefenseResourcePaths = new()
        {
            { DefenseSfxType.FarmPlace, "Sound/Defense/farm_place" },
            { DefenseSfxType.GateInstall, "Sound/Defense/gate_install" },
            { DefenseSfxType.FarmHarvest, "Sound/Defense/farm_harvest" },
        };

        private static readonly Dictionary<DefenseSfxType, float> DefenseEffectVolumes = new()
        {
            { DefenseSfxType.FarmPlace, 1.2f },
            { DefenseSfxType.GateInstall, 1.25f },
            { DefenseSfxType.FarmHarvest, 0.8f },
        };

        [SerializeField] private bool debugLogging = true;
        [SerializeField] private float uiClickVolume = 0.8f;
        [SerializeField] private float uiPurchaseVolume = 1f;
        [SerializeField] private AudioSource uiAudioSource;
        [SerializeField] private string[] preloadResourcePaths =
        {
            "Sound/flash",
            "Sound/molotov",
            "Sound/grenade",
            "Sound/UI/ui_click",
            "Sound/UI/ui_purchase",
            "Sound/Gun/gun_rust_carbine",
            "Sound/Gun/gun_break_rifle",
            "Sound/Gun/gun_pump_striker",
            "Sound/Gun/gun_core_yellow",
            "Sound/Throw/throw_grenade",
            "Sound/Throw/throw_flash",
            "Sound/Throw/throw_molotov",
            "Sound/Melee/melee_field_knife",
            "Sound/Melee/melee_reaper_blade",
            "Sound/Melee/melee_blood_fang",
            "Sound/Melee/melee_zero_edge",
            "Sound/Gun/gun_reload_carbine",
            "Sound/Gun/gun_reload_break_rifle",
            "Sound/Gun/gun_reload_pump_striker",
            "Sound/Gun/gun_reload_core_yellow",
            "Sound/Defense/farm_place",
            "Sound/Defense/gate_install",
            "Sound/Defense/farm_harvest",
        };

        private readonly Dictionary<string, AudioClip> clipCache = new();
        private readonly HashSet<string> warnedMissingPaths = new();
        private bool warnedNoListener;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            if (!IsDedicatedHost())
            {
                Destroy(this);
                EnsureInstance();
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureUiAudioSource();
            UISoundBinder.EnsureOn(this);
            PreloadClips();
        }

        private bool IsDedicatedHost() =>
            GetComponent<NetworkPlayerSpawner>() == null
            && GetComponent<NetworkObject>() == null;

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static GameSoundManager EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            var existing = FindAnyObjectByType<GameSoundManager>();
            if (existing != null)
                return existing;

            var go = new GameObject("GameSoundManager");
            return go.AddComponent<GameSoundManager>();
        }

        public void PlayUIClick() => PlayUI(UiClickPath, uiClickVolume);

        public void PlayUIPurchase() => PlayUI(UiPurchasePath, uiPurchaseVolume);

        public void PlayThrowableEffect(ThrowableType type, Vector3 worldPosition)
        {
            if (type == ThrowableType.None)
                return;

            if (!ThrowableResourcePaths.TryGetValue(type, out var path))
                return;

            var volume = ThrowableEffectVolumes.TryGetValue(type, out var vol) ? vol : DefaultVolume;
            PlayAtPoint(path, worldPosition, volume);
        }

        public void PlayDefenseAtPoint(DefenseSfxType type, Vector3 worldPosition)
        {
            if (!DefenseResourcePaths.TryGetValue(type, out var path))
                return;

            var volume = DefenseEffectVolumes.TryGetValue(type, out var vol) ? vol : DefaultVolume;
            PlayAtPoint(path, worldPosition, volume);
        }

        public void PlayAtPoint(string resourcePath, Vector3 worldPosition, float volume = DefaultVolume)
        {
            if (string.IsNullOrEmpty(resourcePath))
                return;

            var clip = GetClip(resourcePath);
            if (clip == null)
                return;

            if (!HasActiveAudioListener())
                return;

            AudioSource.PlayClipAtPoint(clip, worldPosition, volume);

            if (debugLogging)
                Debug.Log($"[GameSoundManager] Play {resourcePath} at {worldPosition}");
        }

        private void PlayUI(string resourcePath, float volume)
        {
            if (string.IsNullOrEmpty(resourcePath))
                return;

            var clip = GetClip(resourcePath);
            if (clip == null)
                return;

            if (!HasActiveAudioListener())
                return;

            EnsureUiAudioSource();
            uiAudioSource.PlayOneShot(clip, volume);

            if (debugLogging)
                Debug.Log($"[GameSoundManager] Play UI {resourcePath}");
        }

        private void EnsureUiAudioSource()
        {
            if (uiAudioSource != null)
                return;

            uiAudioSource = GetComponent<AudioSource>();
            if (uiAudioSource == null)
                uiAudioSource = gameObject.AddComponent<AudioSource>();

            uiAudioSource.playOnAwake = false;
            uiAudioSource.spatialBlend = 0f;
        }

        private void PreloadClips()
        {
            if (preloadResourcePaths == null)
                return;

            foreach (var path in preloadResourcePaths)
            {
                var clip = GetClip(path);
                if (clip != null)
                    Debug.Log($"[GameSoundManager] loaded: {path} OK");
                else
                    Debug.LogWarning($"[GameSoundManager] failed to load: Resources/{path}");
            }
        }

        private AudioClip GetClip(string resourcePath)
        {
            if (clipCache.TryGetValue(resourcePath, out var cached))
                return cached;

            var clip = Resources.Load<AudioClip>(resourcePath);
            clipCache[resourcePath] = clip;

            if (clip == null && warnedMissingPaths.Add(resourcePath))
                Debug.LogWarning($"[GameSoundManager] AudioClip not found: Resources/{resourcePath}");

            return clip;
        }

        private bool HasActiveAudioListener()
        {
            if (FindAnyObjectByType<AudioListener>() != null)
                return true;

            if (!warnedNoListener)
            {
                warnedNoListener = true;
                Debug.LogWarning("[GameSoundManager] AudioListener not found — SFX will not play.");
            }

            return false;
        }
    }
}
