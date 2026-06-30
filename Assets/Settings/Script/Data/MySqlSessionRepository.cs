using System;
using System.Collections;
using System.Globalization;
using System.Threading.Tasks;
using MySqlConnector;
using UnityEngine;

namespace ProjectM.Data
{
    /// <summary>
    /// SessionResultDto를 session_results + player_stats 테이블에 트랜잭션으로 저장한다.
    /// </summary>
    public class MySqlSessionRepository : MonoBehaviour
    {
        [SerializeField] private MySqlConnectionSettings settings;

        [Header("Debug")]
        [SerializeField] private bool testSaveOnStart;

        public string LastStatus { get; private set; } = "Idle";
        public long LastSavedSessionResultId { get; private set; }

        private void Awake()
        {
            if (settings == null)
                settings = FindAnyObjectByType<MySqlConnectionSettings>();
        }

        private void Start()
        {
            if (testSaveOnStart)
                StartCoroutine(SaveSampleSessionCoroutine());
        }

        private IEnumerator SaveSampleSessionCoroutine()
        {
            var dto = new SessionResultDto
            {
                sessionId = Guid.NewGuid().ToString("N"),
                roomId = "unity_test",
                roomCode = "TEST",
                cleared = true,
                finalWave = 10,
                maxWave = 10,
                finalScore = 1234,
                finalBalance = 100,
                playSeconds = 60f,
                endedAtUtc = DateTime.UtcNow.ToString("o"),
                players = new[]
                {
                    new PlayerStatDto
                    {
                        clientId = 0,
                        nickname = "UnityTest",
                        kills = 1,
                        harvestCount = 2,
                        repairCount = 0,
                        reviveCount = 0,
                        damageDealt = 100f,
                        finalScore = 1234,
                    }
                }
            };

            yield return SaveSessionResultCoroutine(
                dto,
                _ => { },
                err => Debug.LogError($"[MySQL] Test save failed: {err}"));
        }

        public bool IsReady => settings != null && settings.IsConfigured;

        public IEnumerator SaveSessionResultCoroutine(
            SessionResultDto dto,
            Action<long> onSuccess,
            Action<string> onError)
        {
            if (!IsReady)
            {
                onError?.Invoke("MySQL 미구성");
                yield break;
            }

            LastStatus = "Saving...";
            string connectionString = settings.BuildConnectionString();
            long? sessionResultId = null;
            Exception error = null;

            var task = Task.Run(async () =>
            {
                try
                {
                    sessionResultId = await SaveSessionResultAsync(connectionString, dto);
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
                onError?.Invoke(error.Message);
                yield break;
            }

            if (!sessionResultId.HasValue)
            {
                LastStatus = "Failed: no id returned";
                onError?.Invoke("no id returned");
                yield break;
            }

            LastSavedSessionResultId = sessionResultId.Value;
            LastStatus = $"Saved id={sessionResultId.Value} sessionId={dto.sessionId}";
            Debug.Log($"[MySQL] 매치 결과 저장 완료 id={sessionResultId.Value} sessionId={dto.sessionId}");
            onSuccess?.Invoke(sessionResultId.Value);
        }

        private static async Task<long> SaveSessionResultAsync(string connectionString, SessionResultDto dto)
        {
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                long sessionResultId = await InsertSessionResultAsync(connection, transaction, dto);
                await InsertPlayerStatsAsync(connection, transaction, sessionResultId, dto.players);
                await transaction.CommitAsync();
                return sessionResultId;
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                await transaction.RollbackAsync();
                long existingId = await FindSessionResultIdAsync(connectionString, dto.sessionId);
                if (existingId > 0)
                    return existingId;
                throw;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static async Task<long> InsertSessionResultAsync(
            MySqlConnection connection,
            MySqlTransaction transaction,
            SessionResultDto dto)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
INSERT INTO session_results
  (session_id, room_id, room_code, cleared, final_wave, max_wave,
   final_score, final_balance, play_seconds, ended_at_utc, payload_json)
VALUES
  (@sessionId, @roomId, @roomCode, @cleared, @finalWave, @maxWave,
   @finalScore, @finalBalance, @playSeconds, @endedAtUtc, @payloadJson);
SELECT LAST_INSERT_ID();";

            string sessionId = NormalizeSessionId(dto.sessionId);
            command.Parameters.AddWithValue("@sessionId", sessionId);
            command.Parameters.AddWithValue("@roomId", dto.roomId ?? string.Empty);
            command.Parameters.AddWithValue("@roomCode", dto.roomCode ?? string.Empty);
            command.Parameters.AddWithValue("@cleared", dto.cleared ? 1 : 0);
            command.Parameters.AddWithValue("@finalWave", dto.finalWave);
            command.Parameters.AddWithValue("@maxWave", dto.maxWave);
            command.Parameters.AddWithValue("@finalScore", dto.finalScore);
            command.Parameters.AddWithValue("@finalBalance", dto.finalBalance);
            command.Parameters.AddWithValue("@playSeconds", dto.playSeconds);
            command.Parameters.AddWithValue("@endedAtUtc", ParseEndedAtUtc(dto.endedAtUtc));
            command.Parameters.AddWithValue("@payloadJson", JsonUtility.ToJson(dto));

            object scalar = await command.ExecuteScalarAsync();
            return Convert.ToInt64(scalar);
        }

        private static async Task InsertPlayerStatsAsync(
            MySqlConnection connection,
            MySqlTransaction transaction,
            long sessionResultId,
            PlayerStatDto[] players)
        {
            if (players == null || players.Length == 0)
                return;

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
INSERT INTO player_stats
  (session_result_id, client_id, nickname, kills, harvest_count,
   repair_count, revive_count, damage_dealt, final_score)
VALUES
  (@sessionResultId, @clientId, @nickname, @kills, @harvestCount,
   @repairCount, @reviveCount, @damageDealt, @finalScore);";

            var pSessionResultId = command.Parameters.Add("@sessionResultId", MySqlDbType.Int64);
            var pClientId = command.Parameters.Add("@clientId", MySqlDbType.Int32);
            var pNickname = command.Parameters.Add("@nickname", MySqlDbType.VarChar);
            var pKills = command.Parameters.Add("@kills", MySqlDbType.Int32);
            var pHarvestCount = command.Parameters.Add("@harvestCount", MySqlDbType.Int32);
            var pRepairCount = command.Parameters.Add("@repairCount", MySqlDbType.Int32);
            var pReviveCount = command.Parameters.Add("@reviveCount", MySqlDbType.Int32);
            var pDamageDealt = command.Parameters.Add("@damageDealt", MySqlDbType.Float);
            var pFinalScore = command.Parameters.Add("@finalScore", MySqlDbType.Int32);

            foreach (var player in players)
            {
                pSessionResultId.Value = sessionResultId;
                pClientId.Value = player.clientId;
                pNickname.Value = player.nickname ?? string.Empty;
                pKills.Value = player.kills;
                pHarvestCount.Value = player.harvestCount;
                pRepairCount.Value = player.repairCount;
                pReviveCount.Value = player.reviveCount;
                pDamageDealt.Value = player.damageDealt;
                pFinalScore.Value = player.finalScore;
                await command.ExecuteNonQueryAsync();
            }
        }

        private static async Task<long> FindSessionResultIdAsync(string connectionString, string sessionId)
        {
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT id FROM session_results WHERE session_id = @sessionId LIMIT 1;";
            command.Parameters.AddWithValue("@sessionId", NormalizeSessionId(sessionId));
            object scalar = await command.ExecuteScalarAsync();
            return scalar == null || scalar == DBNull.Value ? 0 : Convert.ToInt64(scalar);
        }

        private static string NormalizeSessionId(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                return Guid.NewGuid().ToString("N");
            if (sessionId.Length <= 64)
                return sessionId;
            return Guid.NewGuid().ToString("N");
        }

        private static object ParseEndedAtUtc(string endedAtUtc)
        {
            if (string.IsNullOrEmpty(endedAtUtc))
                return DBNull.Value;

            if (DateTime.TryParse(endedAtUtc, null, DateTimeStyles.RoundtripKind, out var parsed))
                return parsed.ToUniversalTime();

            return DBNull.Value;
        }
    }
}
