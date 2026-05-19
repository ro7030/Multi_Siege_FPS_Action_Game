using UnityEngine;

namespace ProjectM.Player
{
    /// <summary>
    /// Phase 3 임시 디버그 UI. 체력/탄약/다운 상태를 화면에 출력하고 테스트 버튼을 제공한다.
    /// </summary>
    public class PlayerDebugUI : MonoBehaviour
    {
        [SerializeField] private HealthSystem health;
        [SerializeField] private WeaponController weapon;
        [SerializeField] private ReviveSystem revive;

        private void Awake()
        {
            if (health == null) health = GetComponentInChildren<HealthSystem>();
            if (weapon == null) weapon = GetComponentInChildren<WeaponController>();
            if (revive == null) revive = GetComponentInChildren<ReviveSystem>();
        }

        private void OnGUI()
        {
            GUI.skin.label.fontSize = 14;
            GUI.skin.button.fontSize = 13;

            GUILayout.BeginArea(new Rect(Screen.width - 280, 10, 270, 300), GUI.skin.box);
            GUILayout.Label("=== Phase 3 Player Debug ===");

            if (health != null)
            {
                GUILayout.Label($"HP    : {health.CurrentHp:F0} / {health.MaxHp:F0}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("- 10 HP")) health.TakeDamage(10f, null);
                if (GUILayout.Button("+ 10 HP")) health.Heal(10f);
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Reset HP")) health.ResetHp();
            }

            GUILayout.Space(6);
            if (weapon != null)
            {
                GUILayout.Label($"Ammo  : {weapon.CurrentMagazine} / {weapon.ReserveAmmo}");
                GUILayout.Label($"Reload: {(weapon.IsReloading ? "..." : "OK")}");
                if (GUILayout.Button("Manual Reload")) weapon.StartReload();
                if (GUILayout.Button("+ 30 Ammo")) weapon.AddReserveAmmo(30);
            }

            GUILayout.Space(6);
            if (revive != null)
            {
                GUILayout.Label($"Down  : {revive.IsDown}  Dead: {revive.IsDead}");
                if (revive.IsDown) GUILayout.Label($"DownTimer: {revive.DownTimer:F1}s  Progress: {revive.ReviveProgress:F1}/{revive.ReviveDuration}");
            }

            GUILayout.EndArea();
        }
    }
}
