#if UNITY_EDITOR
using ProjectM.Enemy;
using UnityEditor;
using UnityEngine;

namespace ProjectM.EditorTools
{
    public static class EnemyAnimatorSetup
    {
        // Enemy_Normal(고블린)은 GoblinAnimatorBuilder.cs 가 전담한다 (Tools/ProjectM/Build Goblin Animator).
        // 이 목록에 다시 포함시키면 MediumController(Rabbit 리그)로 덮어써져 고블린 걸음 애니가 깨진다.
        private static readonly (string prefabPath, string controllerPath)[] EnemyPrefabs =
        {
            ("Assets/Prefab/Enemy/Enemy_Runner.prefab", "Assets/StylizedCharacterPack/Animations/Controllers/MediumController.controller"),
            ("Assets/Prefab/Enemy/Enemy_Tank.prefab", "Assets/StylizedCharacterPack/Animations/Controllers/MediumController.controller"),
            ("Assets/Prefab/Enemy/Enemy_DPS.prefab", "Assets/StylizedCharacterPack/Animations/Controllers/SmallController.controller"),
            ("Assets/Prefab/Enemy/Enemy_Boss.prefab", "Assets/StylizedCharacterPack/Animations/Controllers/BigController.controller"),
        };

        [MenuItem("Tools/ProjectM/Setup Enemy Animators")]
        public static void Setup()
        {
            foreach (var (prefabPath, controllerPath) in EnemyPrefabs)
            {
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
                if (controller == null)
                {
                    Debug.LogWarning($"[EnemyAnimatorSetup] Controller missing: {controllerPath}");
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                var animator = root.GetComponentInChildren<Animator>(true);
                if (animator != null)
                {
                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;
                }

                if (root.GetComponent<EnemyAnimator>() == null)
                    root.AddComponent<EnemyAnimator>();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[EnemyAnimatorSetup] Enemy animator components configured.");
        }
    }
}
#endif
