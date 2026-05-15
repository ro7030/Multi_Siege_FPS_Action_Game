using UnityEngine;
using ProjectM.Defense;

namespace ProjectM.Economy
{
    /// <summary>
    /// 씬 안의 모든 FarmPlot의 OnHarvested 이벤트를 구독하여 CurrencyWallet에 입금한다.
    /// FarmPlot 자체는 수확 메커니즘만 알고, 경제 연결은 이 클래스가 담당한다.
    /// </summary>
    public class HarvestSystem : MonoBehaviour
    {
        [SerializeField] private CurrencyWallet wallet;
        [SerializeField] private float refreshInterval = 2f;

        private float refreshTimer;
        private readonly System.Collections.Generic.HashSet<FarmPlot> subscribed = new();

        private void Awake()
        {
            if (wallet == null) wallet = FindAnyObjectByType<CurrencyWallet>();
            RescanFarms();
        }

        private void Update()
        {
            refreshTimer += Time.deltaTime;
            if (refreshTimer >= refreshInterval)
            {
                refreshTimer = 0f;
                RescanFarms();
            }
        }

        private void RescanFarms()
        {
            var farms = FindObjectsByType<FarmPlot>(FindObjectsSortMode.None);
            foreach (var f in farms)
            {
                if (f == null || subscribed.Contains(f)) continue;
                f.OnHarvested += HandleHarvested;
                subscribed.Add(f);
            }
            // 파괴된 FarmPlot은 GC가 처리. 안전을 위해 null 정리:
            subscribed.RemoveWhere(f => f == null);
        }

        private void HandleHarvested(FarmPlot plot, int yieldAmount)
        {
            if (wallet == null) return;
            wallet.Add(yieldAmount);
            Debug.Log($"[Harvest] {plot.name} +{yieldAmount} (잔액 {wallet.Balance})");
        }

        private void OnDestroy()
        {
            foreach (var f in subscribed)
                if (f != null) f.OnHarvested -= HandleHarvested;
        }
    }
}
