using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ProjectM.Core;
using ProjectM.Economy;
using ProjectM.Network;
using ProjectM.Player;

namespace ProjectM.Data
{
    /// <summary>
    /// 매치 종료 시 결과 DTO를 빌드하여 서버에 전송.
    /// 실패 시 PersistentDataPath/pending_results.json에 큐로 저장하고 다음 매치 종료 시 재시도.
    /// </summary>
    public class ResultUploader : MonoBehaviour
    {
        [SerializeField] private GameSessionManager session;
        [SerializeField] private CurrencyWallet wallet;
        [SerializeField] private RoomManager room;
        [SerializeField] private PlayerStatsTracker stats;
        [SerializeField] private DbApiClient apiClient;

        [SerializeField] private string resultEndpoint = "/sessions/result";
        [SerializeField] private int maxRetryQueueSize = 32;

        public string PendingFilePath => Path.Combine(Application.persistentDataPath, "pending_results.json");

        public event Action<SessionResultDto> OnUploadSuccess;
        public event Action<SessionResultDto, string> OnUploadFailed;
        public event Action<SessionResultDto> OnSavedLocally;

        private void Awake()
        {
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
            if (wallet == null) wallet = LocalPlayerUtility.FindLocalCurrencyWallet();
            if (room == null) room = FindAnyObjectByType<RoomManager>();
            if (stats == null) stats = FindAnyObjectByType<PlayerStatsTracker>();
            if (apiClient == null) apiClient = FindAnyObjectByType<DbApiClient>();
        }

        private void OnEnable()
        {
            if (session != null) session.OnMatchEnded += HandleMatchEnded;
        }

        private void OnDisable()
        {
            if (session != null) session.OnMatchEnded -= HandleMatchEnded;
        }

        private void HandleMatchEnded(bool cleared)
        {
            var dto = BuildSessionResultDto(cleared);
            StartCoroutine(UploadAsync(dto));
            StartCoroutine(RetryPendingAsync()); // 큐에 쌓인 이전 결과도 재시도
        }

        public SessionResultDto BuildSessionResultDto(bool cleared)
        {
            var dto = new SessionResultDto
            {
                sessionId = Guid.NewGuid().ToString("N"),
                roomId = "",
                roomCode = room != null ? room.RoomCode : "",
                cleared = cleared,
                finalWave = session != null ? session.State.CurrentWave : 0,
                maxWave = session != null ? session.State.MaxWave : 10,
                finalScore = stats != null ? stats.FinalScore : 0,
                finalBalance = wallet != null ? wallet.Balance : 0,
                playSeconds = session != null ? (Time.time - session.State.MatchStartTime) : 0f,
                endedAtUtc = DateTime.UtcNow.ToString("o"),
                players = BuildPlayerStats(),
            };
            return dto;
        }

        private PlayerStatDto[] BuildPlayerStats()
        {
            // MVP: 로컬 플레이어 1명만 기록. 멀티 시점에서는 Host가 모든 클라이언트의 통계를 수집해야 함.
            var list = new List<PlayerStatDto>();
            if (stats != null)
            {
                list.Add(new PlayerStatDto
                {
                    clientId = room != null ? room.LocalClientId : 0,
                    nickname = stats.LocalNickname,
                    kills = stats.Kills,
                    harvestCount = stats.HarvestCount,
                    repairCount = stats.RepairCount,
                    reviveCount = stats.ReviveCount,
                    damageDealt = stats.DamageDealt,
                    finalScore = stats.FinalScore,
                });
            }
            return list.ToArray();
        }

        public IEnumerator UploadAsync(SessionResultDto dto)
        {
            if (apiClient == null || !apiClient.IsConfigured)
            {
                Debug.LogWarning("[Upload] API 미구성 — 로컬 폴백 저장");
                SaveLocalFallback(dto);
                yield break;
            }

            bool done = false; string err = null;
            yield return apiClient.PostJson(resultEndpoint, dto,
                onSuccess: _ => { done = true; },
                onError: e => { err = e; });

            if (done)
            {
                OnUploadSuccess?.Invoke(dto);
                Debug.Log($"[Upload] 결과 전송 성공 sessionId={dto.sessionId}");
            }
            else
            {
                OnUploadFailed?.Invoke(dto, err);
                Debug.LogWarning($"[Upload] 결과 전송 실패: {err} → 로컬 폴백");
                SaveLocalFallback(dto);
            }
        }

        public void SaveLocalFallback(SessionResultDto dto)
        {
            var queue = LoadPendingQueue();
            queue.Add(dto);
            if (queue.Count > maxRetryQueueSize) queue.RemoveAt(0); // 오래된 것 폐기

            var wrapper = new PendingResultsWrapper { items = queue.ToArray() };
            try
            {
                File.WriteAllText(PendingFilePath, JsonUtility.ToJson(wrapper, prettyPrint: true));
                OnSavedLocally?.Invoke(dto);
                Debug.Log($"[Upload] 로컬 저장 완료: {PendingFilePath} (대기 {queue.Count}건)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Upload] 로컬 저장 실패: {e.Message}");
            }
        }

        public List<SessionResultDto> LoadPendingQueue()
        {
            if (!File.Exists(PendingFilePath)) return new List<SessionResultDto>();
            try
            {
                string json = File.ReadAllText(PendingFilePath);
                var wrapper = JsonUtility.FromJson<PendingResultsWrapper>(json);
                if (wrapper?.items == null) return new List<SessionResultDto>();
                return new List<SessionResultDto>(wrapper.items);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Upload] 폴백 로드 실패: {e.Message}");
                return new List<SessionResultDto>();
            }
        }

        public IEnumerator RetryPendingAsync()
        {
            if (apiClient == null || !apiClient.IsConfigured) yield break;
            var queue = LoadPendingQueue();
            if (queue.Count == 0) yield break;

            Debug.Log($"[Upload] 대기 중인 {queue.Count}건 재전송 시도");
            var remaining = new List<SessionResultDto>();
            foreach (var dto in queue)
            {
                bool ok = false;
                yield return apiClient.PostJson(resultEndpoint, dto,
                    onSuccess: _ => ok = true,
                    onError: _ => { });
                if (!ok) remaining.Add(dto);
            }

            // 남은 것만 다시 저장 (전부 성공이면 파일 삭제)
            if (remaining.Count == 0)
            {
                try { if (File.Exists(PendingFilePath)) File.Delete(PendingFilePath); } catch { }
                Debug.Log("[Upload] 대기 큐 비움");
            }
            else
            {
                var wrapper = new PendingResultsWrapper { items = remaining.ToArray() };
                File.WriteAllText(PendingFilePath, JsonUtility.ToJson(wrapper, prettyPrint: true));
                Debug.Log($"[Upload] 재전송 후 {remaining.Count}건 남음");
            }
        }
    }
}
