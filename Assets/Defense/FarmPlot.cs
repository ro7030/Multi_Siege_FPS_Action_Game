using System;
using UnityEngine;

namespace ProjectM.Defense
{
    /// <summary>
    /// 농작물 플롯. 시간이 지나면서 성장 단계(GrowthStep)가 올라가고,
    /// 완전히 자란 후 Harvest() 호출 시 수확량을 반환한다.
    /// 적이 파괴 가능하도록 DefenseObject + HealthSystem을 함께 붙여도 된다.
    /// </summary>
    public class FarmPlot : MonoBehaviour
    {
        [Header("성장 단계")]
        [SerializeField] private int maxGrowthStep = 3;
        [SerializeField] private float secondsPerStep = 15f;

        [Header("수확")]
        [SerializeField] private int baseYield = 10;
        [SerializeField] private int yieldPerStepBonus = 5;
        [SerializeField] private GameObject[] stageVisuals; // 각 단계별 메시 (선택)

        public int GrowthStep { get; private set; }
        public bool IsFullyGrown => GrowthStep >= maxGrowthStep;
        public bool CanHarvest => IsFullyGrown && !harvested;
        public int MaxGrowthStep => maxGrowthStep;

        public event Action<FarmPlot, int> OnGrowthStepChanged;
        public event Action<FarmPlot, int> OnHarvested; // (plot, yieldAmount)

        private float growthTimer;
        private bool harvested;
        private DefenseObject defense;

        private void Awake()
        {
            defense = GetComponent<DefenseObject>();
            ApplyVisual();
        }

        private void OnEnable()
        {
            if (defense != null) defense.OnDestroyed += HandleDestroyed;
        }

        private void OnDisable()
        {
            if (defense != null) defense.OnDestroyed -= HandleDestroyed;
        }

        private void HandleDestroyed(DefenseObject _)
        {
            // 파괴 시 성장 정지 + 수확 불가
            harvested = true;
        }

        private void Update()
        {
            if (IsFullyGrown || harvested) return;
            if (defense != null && defense.IsDestroyed) return;

            growthTimer += Time.deltaTime;
            if (growthTimer >= secondsPerStep)
            {
                growthTimer -= secondsPerStep;
                GrowthStep = Mathf.Min(maxGrowthStep, GrowthStep + 1);
                OnGrowthStepChanged?.Invoke(this, GrowthStep);
                ApplyVisual();
            }
        }

        public int Harvest()
        {
            if (!CanHarvest) return 0;
            int amount = baseYield + yieldPerStepBonus * maxGrowthStep;
            harvested = true;
            GrowthStep = 0;
            growthTimer = 0f;
            OnHarvested?.Invoke(this, amount);
            ApplyVisual();
            harvested = false; // 다시 성장 사이클 시작
            return amount;
        }

        private void ApplyVisual()
        {
            if (stageVisuals == null || stageVisuals.Length == 0) return;
            for (int i = 0; i < stageVisuals.Length; i++)
            {
                if (stageVisuals[i] != null) stageVisuals[i].SetActive(i == GrowthStep);
            }
        }
    }
}
