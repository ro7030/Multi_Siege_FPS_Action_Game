using System.IO;
using UnityEngine;

namespace ProjectM.Data
{
    /// <summary>
    /// Phase 8 임시 디버그 UI. 누적 통계, API 구성 여부, 로컬 대기 큐 정보를 표시한다.
    /// </summary>
    public class DataDebugUI : MonoBehaviour
    {
        [SerializeField] private PlayerStatsTracker stats;
        [SerializeField] private ResultUploader uploader;
        [SerializeField] private DbApiClient api;

        private string apiUrl = "";

        private void Awake()
        {
            if (stats == null) stats = FindAnyObjectByType<PlayerStatsTracker>();
            if (uploader == null) uploader = FindAnyObjectByType<ResultUploader>();
            if (api == null) api = FindAnyObjectByType<DbApiClient>();
            if (api != null) apiUrl = api.BaseUrl;
        }

        private void OnGUI()
        {
            GUI.skin.label.fontSize = 13;
            GUI.skin.button.fontSize = 12;
            GUI.skin.textField.fontSize = 12;

            GUILayout.BeginArea(new Rect(380, 320, 380, 300), GUI.skin.box);
            GUILayout.Label("=== Phase 8 Data Debug ===");

            if (stats != null)
            {
                GUILayout.Label($"Kills    : {stats.Kills}");
                GUILayout.Label($"Harvest  : {stats.HarvestCount}");
                GUILayout.Label($"Repair   : {stats.RepairCount}");
                GUILayout.Label($"Revive   : {stats.ReviveCount}");
                GUILayout.Label($"Damage   : {stats.DamageDealt:F0}");
                GUILayout.Label($"Score    : {stats.FinalScore}");
                if (GUILayout.Button("Reset Stats")) stats.ResetAll();
            }

            GUILayout.Space(6);
            GUILayout.Label("-- API --");
            GUILayout.BeginHorizontal();
            GUILayout.Label("URL:", GUILayout.Width(40));
            apiUrl = GUILayout.TextField(apiUrl);
            if (GUILayout.Button("Set", GUILayout.Width(50)) && api != null) api.BaseUrl = apiUrl;
            GUILayout.EndHorizontal();
            if (api != null) GUILayout.Label($"Configured: {api.IsConfigured}");

            if (uploader != null)
            {
                int pending = uploader.LoadPendingQueue().Count;
                GUILayout.Label($"Pending  : {pending}");
                GUILayout.Label($"File     : {Path.GetFileName(uploader.PendingFilePath)}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Test Upload (Cleared)"))
                    StartCoroutine(uploader.UploadAsync(uploader.BuildSessionResultDto(true)));
                if (GUILayout.Button("Retry Pending"))
                    StartCoroutine(uploader.RetryPendingAsync());
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Open Persistent Folder"))
                {
                    Application.OpenURL("file://" + Application.persistentDataPath);
                }
            }

            GUILayout.EndArea();
        }
    }
}
