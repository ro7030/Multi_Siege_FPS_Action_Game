#if UNITY_EDITOR
using System.IO;
using ProjectM.Enemy;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectM.EditorTools
{
    /// <summary>
    /// 고블린(Enemy_Normal) 전용 Animator Controller를 빌드한다.
    /// Mixamo Walking.fbx 하나에서 Idle(정지 포즈)과 Walk 두 클립을 분리 추출해
    /// 고블린 자체 Avatar 기준으로 재생되도록 한다 (StylizedCharacterPack Rabbit 리그와의 불일치 해소).
    /// </summary>
    public static class GoblinAnimatorBuilder
    {
        private const string WalkingFbxPath = "Assets/Resources/Gobin Ani/X Bot@Walking.fbx";
        private const string ControllerPath = "Assets/Animations/Enemy/GoblinAnimator.controller";
        private const string GoblinPrefabPath = "Assets/Prefab/Enemy/Enemy_Normal.prefab";

        // Enemy_Normal.prefab은 PFB_Basic_Goblin.prefab을 중첩 인스턴스로 포함하고, Animator 컴포넌트는
        // PFB_Basic_Goblin.prefab 레벨에서 "추가된 컴포넌트"로 존재한다. Enemy_Normal에서 그 프로퍼티를
        // 오버라이드하면 다단계 중첩 프리팹 한계로 조용히 무시되므로, 컴포넌트가 실제로 추가된
        // PFB_Basic_Goblin.prefab을 직접 수정해야 런타임에 정상 적용된다.
        private const string GoblinVisualPrefabPath = "Assets/Standout7/LOWPO_Goblin_Pack/Prefabs/PFB_Basic_Goblin.prefab";

        private const string IdleClipName = "Idle";
        private const string WalkClipName = "Walk";

        [MenuItem("Tools/ProjectM/Build Goblin Animator")]
        public static void Build()
        {
            EnsureFolder("Assets/Animations/Enemy");

            if (!ConfigureClipImport())
                return;

            var idleClip = LoadAnimationClip(IdleClipName);
            var walkClip = LoadAnimationClip(WalkClipName);
            if (idleClip == null || walkClip == null)
            {
                Debug.LogError("[GoblinAnimatorBuilder] Idle/Walk 클립을 찾을 수 없습니다.");
                return;
            }

            BuildController(idleClip, walkClip);
            AssignControllerToGoblinPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GoblinAnimatorBuilder] Goblin animator build complete.");
        }

        /// <summary>
        /// Walking.fbx 하나를 Humanoid로 재설정하고, 원본 프레임 범위를 기준으로
        /// 전체 구간(Walk)과 첫 프레임 정지 포즈(Idle) 두 클립으로 분리한다.
        /// </summary>
        private static bool ConfigureClipImport()
        {
            var importer = AssetImporter.GetAtPath(WalkingFbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[GoblinAnimatorBuilder] Missing importer: {WalkingFbxPath}");
                return false;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.importAnimation = true;
            importer.importConstraints = false;

            var defaultClips = importer.defaultClipAnimations;
            if (defaultClips == null || defaultClips.Length == 0)
            {
                Debug.LogError($"[GoblinAnimatorBuilder] 기본 클립 정보를 읽을 수 없습니다: {WalkingFbxPath}");
                return false;
            }

            float firstFrame = defaultClips[0].firstFrame;
            float lastFrame = defaultClips[0].lastFrame;
            float idleLastFrame = Mathf.Min(firstFrame + 1f, lastFrame);

            var clipAnimations = new[]
            {
                new ModelImporterClipAnimation
                {
                    name = WalkClipName,
                    firstFrame = firstFrame,
                    lastFrame = lastFrame,
                    loopTime = true,
                    loopPose = true,
                    // X-Bot(Mixamo) 원본 비율과 고블린 아바타의 다리 길이가 달라 "Center of Mass" 기준(기본값)으로는
                    // 발이 지면에 살짝 파묻히거나 뜬다. Bake Into Pose + Based Upon: Feet로 매 프레임 발 접지 기준
                    // 높이를 재계산해 아바타 체형 차이와 무관하게 발이 지면에 붙도록 보정한다.
                    lockRootHeightY = true,
                    heightFromFeet = true,
                },
                new ModelImporterClipAnimation
                {
                    name = IdleClipName,
                    firstFrame = firstFrame,
                    lastFrame = idleLastFrame,
                    loopTime = true,
                    loopPose = true,
                    lockRootHeightY = true,
                    heightFromFeet = true,
                },
            };

            importer.clipAnimations = clipAnimations;
            importer.SaveAndReimport();
            return true;
        }

        private static AnimationClip LoadAnimationClip(string clipName)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(WalkingFbxPath))
            {
                if (obj is AnimationClip clip && clip.name == clipName)
                    return clip;
            }

            return null;
        }

        private static void BuildController(AnimationClip idleClip, AnimationClip walkClip)
        {
            if (File.Exists(ControllerPath))
                AssetDatabase.DeleteAsset(ControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            SetBoolParameterDefault(controller, "Grounded", true);
            controller.AddParameter("Sprint", AnimatorControllerParameterType.Bool);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

            var root = controller.layers[0].stateMachine;
            var idle = root.AddState("Idle", new Vector3(300, 0, 0));
            idle.motion = idleClip;

            var walk = root.AddState("Walk", new Vector3(300, 120, 0));
            walk.motion = walkClip;
            walk.speedParameterActive = true;
            walk.speedParameter = "Speed";

            root.defaultState = idle;

            AddTransition(idle, walk, AnimatorConditionMode.Greater, "Speed", 0.1f);
            AddTransition(walk, idle, AnimatorConditionMode.Less, "Speed", 0.1f);
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
            transition.duration = 0.15f;
            transition.AddCondition(mode, threshold, param);
        }

        private static void SetBoolParameterDefault(AnimatorController controller, string name, bool value)
        {
            var parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name != name || parameters[i].type != AnimatorControllerParameterType.Bool)
                    continue;

                parameters[i].defaultBool = value;
                controller.parameters = parameters;
                EditorUtility.SetDirty(controller);
                return;
            }
        }

        private static void AssignControllerToGoblinPrefab()
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (controller == null) return;

            // Animator가 실제로 "추가"된 PFB_Basic_Goblin.prefab 레벨에서 직접 설정해야
            // Enemy_Normal.prefab의 중첩 오버라이드로는 조용히 무시되는 문제를 피할 수 있다.
            var visualRoot = PrefabUtility.LoadPrefabContents(GoblinVisualPrefabPath);
            var animator = visualRoot.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogWarning($"[GoblinAnimatorBuilder] Animator missing: {GoblinVisualPrefabPath}");
                PrefabUtility.UnloadPrefabContents(visualRoot);
                return;
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            PrefabUtility.SaveAsPrefabAsset(visualRoot, GoblinVisualPrefabPath);
            PrefabUtility.UnloadPrefabContents(visualRoot);

            var root = PrefabUtility.LoadPrefabContents(GoblinPrefabPath);
            if (root.GetComponent<EnemyAnimator>() == null)
                root.AddComponent<EnemyAnimator>();

            PrefabUtility.SaveAsPrefabAsset(root, GoblinPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
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
