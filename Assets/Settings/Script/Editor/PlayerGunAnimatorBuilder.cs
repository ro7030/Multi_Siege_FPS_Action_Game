#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using ProjectM.Network;
using ProjectM.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectM.EditorTools
{
    public static class PlayerGunAnimatorBuilder
    {
        private const string ChibiAvatarPath = "Assets/Suriyun/Pspsps/FBX/Characters/Chibi_Monkey.fbx";
        private const string ControllerPath = "Assets/Animations/Player/PlayerGunAnimator.controller";

        /// <summary>Idle/Walk ↔ Run 전환 기준 속도(m/s). walkSpeed 5 / sprintSpeed 8의 중간값.</summary>
        private const float SwordSprintSpeedThreshold = 6.5f;

        private static readonly (string assetPath, string clipName, bool loop)[] ClipSources =
        {
            ("Assets/Resources/Monkey Gun Ani/X Bot@Rifle Idle.fbx", "Idle", true),
            ("Assets/Resources/Monkey Gun Ani/X Bot@Rifle Run.fbx", "Run", true),
            ("Assets/Resources/Monkey Gun Ani/X Bot@Rifle Jump.fbx", "Jump", false),
            ("Assets/Resources/Monkey Gun Ani/X Bot@Gunplay.fbx", "GunPlay", false),
            ("Assets/Resources/Monkey Gun Ani/X Bot@Reloading.fbx", "Reloading", false),
            ("Assets/Resources/Monkey Gun Ani/X Bot@Throw.fbx", "Throw", false),
            ("Assets/Resources/Monkey Sword Ani/X Bot@Great Sword Idle.fbx", "SwordIdle", true),
            ("Assets/Resources/Monkey Sword Ani/X Bot@Sword And Shield Walk.fbx", "SwordWalk", true),
            ("Assets/Resources/Monkey Sword Ani/X Bot@Sword And Shield Run.fbx", "SwordRun", true),
            ("Assets/Resources/Monkey Sword Ani/X Bot@Great Sword Jump.fbx", "SwordJump", false),
            ("Assets/Resources/Monkey Sword Ani/X Bot@Great Sword Slash.fbx", "SwordAttack", false),
        };

        private static readonly string[] ChibiPrefabPaths =
        {
            "Assets/Prefab/Character/Chibi_Monkey_00.prefab",
            "Assets/Prefab/Character/Chibi_Monkey_01.prefab",
            "Assets/Prefab/Character/Chibi_Monkey_02.prefab",
        };

        [MenuItem("Tools/ProjectM/Build Player Gun Animator")]
        public static void Build()
        {
            EnsureFolder("Assets/Animations/Player");

            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(ChibiAvatarPath);
            if (avatar == null)
            {
                Debug.LogWarning("[PlayerGunAnimatorBuilder] Chibi_Monkey Avatar not found; Mixamo clips will use their own Humanoid rigs.");
            }

            var clips = new Dictionary<string, AnimationClip>();
            foreach (var (assetPath, clipName, loop) in ClipSources)
            {
                if (!ConfigureClipImport(assetPath, clipName, loop))
                    return;

                var clip = LoadAnimationClip(assetPath, clipName);
                if (clip == null)
                {
                    Debug.LogError($"[PlayerGunAnimatorBuilder] Clip missing: {clipName} ({assetPath})");
                    return;
                }

                clips[clipName] = clip;
            }

            BuildController(clips);
            AssignControllerToChibiPrefabs();
            EnsureNetworkPlayerAnimationDriver();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PlayerGunAnimatorBuilder] Player gun animator build complete.");
        }

        private static bool ConfigureClipImport(string assetPath, string clipName, bool loop)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[PlayerGunAnimatorBuilder] Missing importer: {assetPath}");
                return false;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.importAnimation = true;
            importer.importConstraints = false;

            var clipAnimations = importer.clipAnimations;
            if (clipAnimations == null || clipAnimations.Length == 0)
            {
                clipAnimations = importer.defaultClipAnimations;
            }

            if (clipAnimations == null || clipAnimations.Length == 0)
            {
                clipAnimations = new[]
                {
                    new ModelImporterClipAnimation { name = clipName }
                };
            }

            foreach (var clip in clipAnimations)
            {
                clip.name = clipName;
                clip.loopTime = loop;
                clip.loopPose = loop;
            }

            importer.clipAnimations = clipAnimations;
            importer.SaveAndReimport();
            return true;
        }

        private static AnimationClip LoadAnimationClip(string assetPath, string clipName)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview"))
                {
                    if (clip.name == clipName || clip.name.Contains("mixamo") || clip.name == "Take 001")
                        return clip;
                }
            }

            return AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        }

        private static void BuildController(Dictionary<string, AnimationClip> clips)
        {
            if (File.Exists(ControllerPath))
                AssetDatabase.DeleteAsset(ControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AddParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            AddParameter(controller, "Grounded", AnimatorControllerParameterType.Bool);
            SetBoolParameterDefault(controller, "Grounded", true);
            AddParameter(controller, "VerticalSpeed", AnimatorControllerParameterType.Float);
            AddParameter(controller, "IsAiming", AnimatorControllerParameterType.Bool);
            AddParameter(controller, "Reload", AnimatorControllerParameterType.Trigger);
            AddParameter(controller, "IsReloading", AnimatorControllerParameterType.Bool);
            AddParameter(controller, "Throw", AnimatorControllerParameterType.Trigger);
            AddParameter(controller, "IsMelee", AnimatorControllerParameterType.Bool);
            AddParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);

            var root = controller.layers[0].stateMachine;
            var idle = AddMotionState(root, "Idle", clips["Idle"], new Vector3(300, 0, 0));
            var run = AddMotionState(root, "Run", clips["Run"], new Vector3(300, 120, 0));
            var jump = AddMotionState(root, "Jump", clips["Jump"], new Vector3(550, 60, 0));
            var gunPlay = AddMotionState(root, "GunPlay", clips["GunPlay"], new Vector3(550, 180, 0));
            var reloading = AddMotionState(root, "Reloading", clips["Reloading"], new Vector3(550, 300, 0));
            var throwing = AddMotionState(root, "Throw", clips["Throw"], new Vector3(300, 420, 0));

            var swordIdle = AddMotionState(root, "SwordIdle", clips["SwordIdle"], new Vector3(900, 0, 0));
            var swordWalk = AddMotionState(root, "SwordWalk", clips["SwordWalk"], new Vector3(900, 120, 0));
            var swordRun = AddMotionState(root, "SwordRun", clips["SwordRun"], new Vector3(900, 240, 0));
            var swordJump = AddMotionState(root, "SwordJump", clips["SwordJump"], new Vector3(1150, 60, 0));
            var swordAttack = AddMotionState(root, "SwordAttack", clips["SwordAttack"], new Vector3(1150, 180, 0));

            root.defaultState = idle;

            AddTransition(idle, run, AnimatorConditionMode.Greater, "Speed", 0.1f);
            AddTransition(run, idle, AnimatorConditionMode.Less, "Speed", 0.1f);

            AddBoolTransition(idle, jump, "Grounded", false);
            AddBoolTransition(run, jump, "Grounded", false);
            AddBoolTransition(jump, idle, "Grounded", true);

            AddBoolTransition(idle, gunPlay, "IsAiming", true);
            AddBoolTransition(run, gunPlay, "IsAiming", true);
            AddBoolTransition(gunPlay, idle, "IsAiming", false);

            AddTriggerTransition(idle, reloading, "Reload");
            AddTriggerTransition(run, reloading, "Reload");
            AddTriggerTransition(gunPlay, reloading, "Reload");
            AddBoolTransition(gunPlay, reloading, "IsReloading", true);
            AddExitTransition(reloading, idle, 0.95f);

            AddTriggerTransition(idle, throwing, "Throw");
            AddTriggerTransition(run, throwing, "Throw");
            AddTriggerTransition(gunPlay, throwing, "Throw");
            AddTriggerTransition(swordIdle, throwing, "Throw");
            AddTriggerTransition(swordWalk, throwing, "Throw");
            AddTriggerTransition(swordRun, throwing, "Throw");
            AddExitTransitionWithBoolCondition(throwing, idle, 0.9f, "IsMelee", false);
            AddExitTransitionWithBoolCondition(throwing, swordIdle, 0.9f, "IsMelee", true);

            // ── 총 ↔ 검 브랜치 전환 (IsMelee 게이팅) ──
            AddBoolTransition(idle, swordIdle, "IsMelee", true);
            AddBoolTransition(swordIdle, idle, "IsMelee", false);

            AddMultiConditionTransition(run, swordWalk,
                (AnimatorConditionMode.If, 0f, "IsMelee"),
                (AnimatorConditionMode.Less, SwordSprintSpeedThreshold, "Speed"));
            AddMultiConditionTransition(run, swordRun,
                (AnimatorConditionMode.If, 0f, "IsMelee"),
                (AnimatorConditionMode.Greater, SwordSprintSpeedThreshold - 0.001f, "Speed"));
            AddBoolTransition(swordWalk, run, "IsMelee", false);
            AddBoolTransition(swordRun, run, "IsMelee", false);

            // ── 검 자체 로코모션 (Idle → Walk → Run 3단) ──
            AddTransition(swordIdle, swordWalk, AnimatorConditionMode.Greater, "Speed", 0.1f);
            AddTransition(swordWalk, swordIdle, AnimatorConditionMode.Less, "Speed", 0.1f);
            AddTransition(swordWalk, swordRun, AnimatorConditionMode.Greater, "Speed", SwordSprintSpeedThreshold);
            AddTransition(swordRun, swordWalk, AnimatorConditionMode.Less, "Speed", SwordSprintSpeedThreshold);

            AddBoolTransition(swordIdle, swordJump, "Grounded", false);
            AddBoolTransition(swordWalk, swordJump, "Grounded", false);
            AddBoolTransition(swordRun, swordJump, "Grounded", false);
            AddBoolTransition(swordJump, swordIdle, "Grounded", true);

            AddTriggerTransition(swordIdle, swordAttack, "Attack");
            AddTriggerTransition(swordWalk, swordAttack, "Attack");
            AddTriggerTransition(swordRun, swordAttack, "Attack");
            AddExitTransition(swordAttack, swordIdle, 0.85f);
        }

        private static void AssignControllerToChibiPrefabs()
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (controller == null) return;

            foreach (var path in ChibiPrefabPaths)
            {
                var prefabRoot = PrefabUtility.LoadPrefabContents(path);
                var animator = prefabRoot.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    Debug.LogWarning($"[PlayerGunAnimatorBuilder] Animator missing: {path}");
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                    continue;
                }

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void EnsureNetworkPlayerAnimationDriver()
        {
            const string networkPlayerPath = "Assets/Prefab/Network/NetworkPlayer.prefab";
            var prefabRoot = PrefabUtility.LoadPrefabContents(networkPlayerPath);
            if (prefabRoot.GetComponent<PlayerAnimationController>() == null)
                prefabRoot.AddComponent<PlayerAnimationController>();
            if (prefabRoot.GetComponent<NetworkPlayerAnimationBridge>() == null)
                prefabRoot.AddComponent<NetworkPlayerAnimationBridge>();

            var pac = prefabRoot.GetComponent<PlayerAnimationController>();
            var fallbackController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (pac != null && fallbackController != null)
            {
                var pacSo = new SerializedObject(pac);
                pacSo.FindProperty("fallbackAnimatorController").objectReferenceValue = fallbackController;
                pacSo.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, networkPlayerPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        private static AnimatorState AddMotionState(
            AnimatorStateMachine machine,
            string stateName,
            Motion motion,
            Vector3 position)
        {
            var state = machine.AddState(stateName, position);
            state.motion = motion;
            return state;
        }

        private static void AddParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type)
        {
            controller.AddParameter(name, type);
        }

        private static void SetBoolParameterDefault(AnimatorController controller, string name, bool value)
        {
            var parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name != name
                    || parameters[i].type != AnimatorControllerParameterType.Bool)
                    continue;

                parameters[i].defaultBool = value;
                controller.parameters = parameters;
                EditorUtility.SetDirty(controller);
                return;
            }
        }

        private static void AddTransition(
            AnimatorState from,
            AnimatorState to,
            AnimatorConditionMode mode,
            string param,
            float threshold)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.1f;
            transition.AddCondition(mode, threshold, param);
        }

        private static void AddBoolTransition(AnimatorState from, AnimatorState to, string param, bool value)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.1f;
            transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
        }

        private static void AddTriggerTransition(AnimatorState from, AnimatorState to, string trigger)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.05f;
            transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        }

        private static void AddExitTransition(AnimatorState from, AnimatorState to, float exitTime)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.duration = 0.1f;
        }

        private static void AddExitTransitionWithBoolCondition(
            AnimatorState from,
            AnimatorState to,
            float exitTime,
            string param,
            bool value)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.duration = 0.1f;
            transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
        }

        /// <summary>여러 조건을 모두 만족해야(AND) 전이되는 전환을 추가한다.</summary>
        private static void AddMultiConditionTransition(
            AnimatorState from,
            AnimatorState to,
            params (AnimatorConditionMode mode, float threshold, string param)[] conditions)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.1f;
            foreach (var (mode, threshold, param) in conditions)
                transition.AddCondition(mode, threshold, param);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
