#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectM.Network;
using ProjectM.Player;

namespace ProjectM.Network.Editor
{
    // Phase 3: NetworkPlayer에 NetworkDamageBridge(HP 동기화) + 부활 상호작용 콜라이더 추가.
    // 메뉴: ProjectM/Setup Phase 3 Network Player Health
    public static class Phase3NetworkPlayerHealthSetup
    {
        private const string NetworkPlayerPrefabPath = "Assets/Prefab/Network/NetworkPlayer.prefab";

        [MenuItem("ProjectM/Setup Phase 3 Network Player Health")]
        public static void Setup()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(NetworkPlayerPrefabPath);

            if (prefabRoot.GetComponent<NetworkDamageBridge>() == null)
                prefabRoot.AddComponent<NetworkDamageBridge>();

            if (prefabRoot.GetComponent<ReviveInteractable>() == null)
                prefabRoot.AddComponent<ReviveInteractable>();

            var interactCollider = prefabRoot.GetComponent<CapsuleCollider>();
            if (interactCollider == null)
                interactCollider = prefabRoot.AddComponent<CapsuleCollider>();

            interactCollider.isTrigger = true;
            interactCollider.height = 1.8f;
            interactCollider.radius = 0.5f;
            interactCollider.center = new Vector3(0f, 0.9f, 0f);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, NetworkPlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            AssetDatabase.SaveAssets();
            Debug.Log("[Phase3] NetworkPlayer — NetworkDamageBridge + ReviveInteractable + CapsuleCollider 적용 완료");
        }
    }
}
#endif
