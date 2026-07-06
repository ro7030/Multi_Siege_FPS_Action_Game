using MySqlConnector;
using UnityEngine;

namespace ProjectM.Data
{
    // MySQL 연결 설정. MySqlConnectionTester, MySqlSessionRepository 등에서 공유한다.
    public class MySqlConnectionSettings : MonoBehaviour
    {
        [SerializeField] private string server = "127.0.0.1";
        [SerializeField] private int port = 3306;
        [SerializeField] private string userId = "game_dev";
        [SerializeField] private string password = "game_dev";
        [SerializeField] private string database = "multi_siege_fps";

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(server) &&
            !string.IsNullOrWhiteSpace(userId) &&
            !string.IsNullOrWhiteSpace(database);

        public string BuildConnectionString(bool includeDatabase = true)
        {
            var builder = new MySqlConnectionStringBuilder
            {
                Server = server,
                Port = (uint)port,
                UserID = userId,
                Password = password
            };

            if (includeDatabase && !string.IsNullOrEmpty(database))
                builder.Database = database;

            return builder.ConnectionString;
        }
    }
}
