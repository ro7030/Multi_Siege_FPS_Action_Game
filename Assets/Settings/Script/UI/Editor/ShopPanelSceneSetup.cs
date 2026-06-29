#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectM.UI.Editor
{
    public static class ShopPanelSceneSetup
    {
        [MenuItem("ProjectM/UI/ShopPanel — 카테고리·탭 UI 다시 만들기")]
        public static void RebuildShopPanelUi()
        {
            var shopView = Object.FindAnyObjectByType<ShopView>(FindObjectsInactive.Include);
            if (shopView == null)
            {
                Debug.LogError("[Shop] 씬에 ShopView 가 없습니다. Canvas → ShopPanel 에 ShopView 를 붙여 주세요.");
                return;
            }

            shopView.EditorRebuildPanelUi();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Shop] 완료. Hierarchy 와 ShopView 인스펙터를 확인한 뒤 Ctrl+S 로 씬을 저장하세요.");
        }
    }
}
#endif
