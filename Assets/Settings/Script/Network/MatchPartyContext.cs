using System.Collections.Generic;
using UnityEngine;

namespace ProjectM.Network
{
    // 매치 종료 후 Retry/Home/rematch 조율에 쓰는 세션 컨텍스트 (ClientRpc 미러 + UI).
    public static class MatchPartyContext
    {
        public static string RematchGroupId { get; private set; } = string.Empty;
        public static string OriginalHostAuthPlayerId { get; private set; } = string.Empty;
        public static bool HostLeftViaHome { get; set; }
        public static bool RematchOrchestrationStarted { get; set; }
        public static string RematchLobbyId { get; set; } = string.Empty;
        public static string RematchHostAuthPlayerId { get; set; } = string.Empty;
        public static ulong RematchHostClientId { get; set; }

        public static RematchStatusPayload LastStatusPayload { get; private set; }

        public static int RequiredRematchCount => LastStatusPayload.RequiredCount;
        public static int RegisteredRematchCount => LastStatusPayload.RegisteredCount;

        public static bool IsRematchReady =>
            RequiredRematchCount > 0 && RegisteredRematchCount >= RequiredRematchCount;

        public static void SetRematchGroup(string lobbyId)
        {
            if (string.IsNullOrEmpty(lobbyId)) return;
            RematchGroupId = lobbyId;
        }

        public static void SetOriginalHost(string authPlayerId)
        {
            if (string.IsNullOrEmpty(authPlayerId)) return;
            OriginalHostAuthPlayerId = authPlayerId;
        }

        public static void ApplyStatusPayload(RematchStatusPayload payload)
        {
            LastStatusPayload = payload;
        }

        public static void ResetRematchSession()
        {
            RematchOrchestrationStarted = false;
            RematchLobbyId = string.Empty;
            RematchHostAuthPlayerId = string.Empty;
            RematchHostClientId = 0;
        }

        public static void FailOrchestration(string reason)
        {
            Debug.LogWarning($"[Rematch] Orchestration failed: {reason}");
            ResetRematchSession();
        }

        public static void Clear()
        {
            RematchGroupId = string.Empty;
            OriginalHostAuthPlayerId = string.Empty;
            HostLeftViaHome = false;
            LastStatusPayload = default;
            ResetRematchSession();
            MatchLoadoutContext.Clear();
        }

        public static string FormatStatusText()
        {
            var payload = LastStatusPayload;
            if (payload.PlayerCount <= 0)
                return string.Empty;

            var lines = new List<string>
            {
                $"다시하기 준비 {payload.RegisteredCount}/{payload.RequiredCount}",
                new string('─', 22)
            };

            for (int i = 0; i < payload.PlayerCount; i++)
            {
                var entry = payload.GetPlayer(i);
                string name = entry.Nickname.IsEmpty ? "player" : entry.Nickname.ToString();
                string status = entry.PlayerState switch
                {
                    RematchPlayerState.RetryReady => "완료 · 다시하기",
                    RematchPlayerState.LeftHome => "홈으로 나감",
                    _ => "(대기)"
                };
                lines.Add($"{name,-14} {status}");
            }

            return string.Join("\n", lines);
        }
    }
}
