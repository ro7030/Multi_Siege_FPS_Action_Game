using System;
using System.Collections;
using System.Threading.Tasks;
using MySqlConnector;
using UnityEngine;

namespace ProjectM.Data
{
    // MySqlConnector 직접 연결 테스트 (로컬 개발/프로토타입용).
    // DB 작업은 백그라운드 스레드에서 실행하고, Unity 메인 스레드는 코루틴으로 대기한다.
    public class MySqlConnectionTester : MonoBehaviour
    {
        [SerializeField] private MySqlConnectionSettings settings;

        [Header("Options")]
        [SerializeField] private bool testOnStart;
        [SerializeField] private bool ensureSchemaOnConnect;

        public string LastStatus { get; private set; } = "Not tested";
        public bool IsConnected { get; private set; }

        private void Awake()
        {
            if (settings == null)
                settings = FindAnyObjectByType<MySqlConnectionSettings>();
        }

        private void Start()
        {
            if (testOnStart)
                StartCoroutine(TestConnectionCoroutine());
        }

        [ContextMenu("Test MySQL Connection")]
        public void TestConnection()
        {
            StartCoroutine(TestConnectionCoroutine());
        }

        [ContextMenu("Ensure Schema")]
        public void EnsureSchema()
        {
            StartCoroutine(EnsureSchemaCoroutine());
        }

        public IEnumerator TestConnectionCoroutine()
        {
            LastStatus = "Testing...";
            IsConnected = false;

            if (settings == null || !settings.IsConfigured)
            {
                LastStatus = "Failed: MySQL settings not configured";
                Debug.LogError("[MySQL] Connection failed: settings not configured");
                yield break;
            }

            string connectionString = settings.BuildConnectionString();
            ConnectionTestResult? result = null;
            Exception error = null;

            var task = Task.Run(async () =>
            {
                try
                {
                    await using var connection = new MySqlConnection(connectionString);
                    await connection.OpenAsync();

                    await using var command = connection.CreateCommand();
                    command.CommandText = "SELECT VERSION(), DATABASE();";
                    await using var reader = await command.ExecuteReaderAsync();

                    string version = null;
                    string db = null;
                    if (await reader.ReadAsync())
                    {
                        version = reader.GetString(0);
                        db = reader.IsDBNull(1) ? "(none)" : reader.GetString(1);
                    }

                    result = new ConnectionTestResult
                    {
                        Version = version,
                        Database = db
                    };
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });

            while (!task.IsCompleted)
                yield return null;

            if (error != null)
            {
                LastStatus = $"Failed: {error.Message}";
                Debug.LogError($"[MySQL] Connection failed: {error}");
                yield break;
            }

            if (!result.HasValue)
            {
                LastStatus = "Failed: no result returned";
                Debug.LogError("[MySQL] Connection failed: no result returned");
                yield break;
            }

            IsConnected = true;
            LastStatus = $"OK — MySQL {result.Value.Version}, DB={result.Value.Database}";
            Debug.Log($"[MySQL] {LastStatus}");

            if (ensureSchemaOnConnect)
                yield return EnsureSchemaCoroutine();
        }

        public IEnumerator EnsureSchemaCoroutine()
        {
            LastStatus = "Ensuring schema...";

            if (settings == null || !settings.IsConfigured)
            {
                LastStatus = "Schema failed: settings not configured";
                yield break;
            }

            string connectionString = settings.BuildConnectionString(includeDatabase: true);
            SchemaResult? result = null;
            Exception error = null;

            var task = Task.Run(async () =>
            {
                try
                {
                    await using var connection = new MySqlConnection(connectionString);
                    await connection.OpenAsync();

                    foreach (string statement in MySqlSchemaStatements.All)
                    {
                        await using var command = connection.CreateCommand();
                        command.CommandText = statement;
                        await command.ExecuteNonQueryAsync();
                    }

                    await using var verify = connection.CreateCommand();
                    verify.CommandText = "SHOW TABLES;";
                    await using var reader = await verify.ExecuteReaderAsync();

                    var tables = new System.Collections.Generic.List<string>();
                    while (await reader.ReadAsync())
                        tables.Add(reader.GetString(0));

                    result = new SchemaResult { Tables = tables.ToArray() };
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });

            while (!task.IsCompleted)
                yield return null;

            if (error != null)
            {
                LastStatus = $"Schema failed: {error.Message}";
                Debug.LogError($"[MySQL] Schema ensure failed: {error}");
                yield break;
            }

            if (!result.HasValue)
            {
                LastStatus = "Schema failed: no result returned";
                Debug.LogError("[MySQL] Schema ensure failed: no result returned");
                yield break;
            }

            LastStatus = $"Schema OK — tables: {string.Join(", ", result.Value.Tables)}";
            Debug.Log($"[MySQL] {LastStatus}");
        }

        private struct ConnectionTestResult
        {
            public string Version;
            public string Database;
        }

        private struct SchemaResult
        {
            public string[] Tables;
        }
    }

    internal static class MySqlSchemaStatements
    {
        public static readonly string[] All =
        {
            @"CREATE TABLE IF NOT EXISTS session_results (
              id BIGINT AUTO_INCREMENT PRIMARY KEY,
              session_id VARCHAR(64) NOT NULL,
              room_id VARCHAR(64),
              room_code VARCHAR(32),
              cleared TINYINT(1) NOT NULL DEFAULT 0,
              final_wave INT NOT NULL DEFAULT 0,
              max_wave INT NOT NULL DEFAULT 0,
              final_score INT NOT NULL DEFAULT 0,
              final_balance INT NOT NULL DEFAULT 0,
              play_seconds FLOAT NOT NULL DEFAULT 0,
              ended_at_utc DATETIME NULL,
              payload_json JSON NULL,
              created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
              UNIQUE KEY uq_session_id (session_id),
              KEY idx_created_at (created_at)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci",
            @"CREATE TABLE IF NOT EXISTS player_stats (
              id BIGINT AUTO_INCREMENT PRIMARY KEY,
              session_result_id BIGINT NOT NULL,
              client_id INT NOT NULL DEFAULT 0,
              nickname VARCHAR(64),
              kills INT NOT NULL DEFAULT 0,
              harvest_count INT NOT NULL DEFAULT 0,
              repair_count INT NOT NULL DEFAULT 0,
              revive_count INT NOT NULL DEFAULT 0,
              damage_dealt FLOAT NOT NULL DEFAULT 0,
              final_score INT NOT NULL DEFAULT 0,
              created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
              KEY idx_session_result_id (session_result_id),
              CONSTRAINT fk_player_stats_session
                FOREIGN KEY (session_result_id) REFERENCES session_results(id)
                ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci"
        };
    }
}
