using System.Collections.Generic;
using UnityEngine;

namespace ProjectM.Defense
{
    /// <summary>
    /// Phase 5 임시 디버그 UI. 씬 안의 모든 DefenseObject / FarmPlot 상태를 표시한다.
    /// </summary>
    public class DefenseDebugUI : MonoBehaviour
    {
        private readonly List<DefenseObject> defenses = new();
        private readonly List<FarmPlot> farms = new();
        private float refreshTimer;
        private Vector2 scroll;

        private void Awake() => Refresh();

        private void Refresh()
        {
            defenses.Clear();
            farms.Clear();
            defenses.AddRange(FindObjectsByType<DefenseObject>(FindObjectsSortMode.None));
            farms.AddRange(FindObjectsByType<FarmPlot>(FindObjectsSortMode.None));
        }

        private void Update()
        {
            refreshTimer += Time.deltaTime;
            if (refreshTimer >= 2f) { refreshTimer = 0; Refresh(); }
        }

        private void OnGUI()
        {
            GUI.skin.label.fontSize = 13;
            GUI.skin.button.fontSize = 12;

            GUILayout.BeginArea(new Rect(Screen.width - 280, 560, 270, 300), GUI.skin.box);
            GUILayout.Label("=== Phase 5 Defense Debug ===");

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));

            GUILayout.Label("-- Defense Objects --");
            foreach (var d in defenses)
            {
                if (d == null) continue;
                float hp = d.Health.CurrentHp;
                float max = d.Health.MaxHp;
                GUILayout.Label($"{d.DisplayName}  {hp:F0}/{max:F0} {(d.IsDestroyed ? "(DESTROYED)" : "")}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("-20")) d.Health.TakeDamage(20f, null);
                if (GUILayout.Button("Repair 20")) d.RepairInstant(20f);
                if (GUILayout.Button("Reset")) d.Health.ResetHp();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);
            GUILayout.Label("-- Farm Plots --");
            foreach (var f in farms)
            {
                if (f == null) continue;
                GUILayout.Label($"{f.name}  누적={f.AccumulatedYield} (웨이브당 +{f.YieldPerWave}) {(f.HasYieldToHarvest ? "(READY)" : "")} {(f.State == FarmPlot.FarmState.Destroyed ? "(DESTROYED)" : "")}");
                if (f.HasYieldToHarvest && GUILayout.Button("Harvest (팀 분배)"))
                {
                    Economy.FarmManager.Instance?.HarvestFarm(f);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
