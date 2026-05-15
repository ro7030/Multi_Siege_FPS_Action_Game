using UnityEngine;

namespace ProjectM.Economy
{
    /// <summary>
    /// Phase 6 임시 디버그 UI. 잔액 표시, 잠금 해제된 아이템 목록, 구매 버튼.
    /// </summary>
    public class EconomyDebugUI : MonoBehaviour
    {
        [SerializeField] private CurrencyWallet wallet;
        [SerializeField] private ShopController shop;

        private Vector2 scroll;

        private void Awake()
        {
            if (wallet == null) wallet = FindAnyObjectByType<CurrencyWallet>();
            if (shop == null) shop = FindAnyObjectByType<ShopController>();
        }

        private void OnGUI()
        {
            GUI.skin.label.fontSize = 13;
            GUI.skin.button.fontSize = 12;

            GUILayout.BeginArea(new Rect(10, Screen.height - 320, 360, 310), GUI.skin.box);
            GUILayout.Label("=== Phase 6 Economy Debug ===");

            if (wallet != null)
            {
                GUILayout.Label($"Balance : {wallet.Balance}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("+ 50")) wallet.Add(50);
                if (GUILayout.Button("- 50")) wallet.TrySpend(50);
                if (GUILayout.Button("Reset 0")) wallet.SetBalance(0);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);
            GUILayout.Label($"-- Shop (Wave {(shop != null ? shop.CurrentWave : 0)}) --");

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
            if (shop != null && shop.Catalog != null)
            {
                foreach (var item in shop.Catalog.items)
                {
                    if (item == null) continue;
                    bool unlocked = shop.IsUnlocked(item);
                    GUI.enabled = unlocked && wallet != null && wallet.Balance >= item.price;
                    string label = $"{item.displayName} ({item.price}) {(unlocked ? "" : $"[W{item.unlockWave}+]")}";
                    if (GUILayout.Button(label)) shop.TryPurchase(item);
                    GUI.enabled = true;
                }
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }
    }
}
