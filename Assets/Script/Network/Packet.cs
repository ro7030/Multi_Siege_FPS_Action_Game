using System;

namespace ProjectM.Network
{
    public enum PacketType
    {
        None = 0,

        // 로비 / 연결
        JoinRequest       = 100, // Guest → Host : 닉네임 전달
        JoinAccepted      = 101, // Host  → Guest: 할당된 ClientId 통보
        PlayerListUpdate  = 102, // Host  → ALL  : 현재 플레이어 목록
        ReadyState        = 103, // Guest → Host : Ready 토글
        StartGame         = 110, // Host  → ALL  : 게임 시작 신호

        // 게임플레이 (Phase 3~ 에서 사용)
        PlayerInput       = 200,
        PlayerStateSync   = 201,

        // 공용
        Ping              = 900,
        Chat              = 901,
    }

    /// <summary>
    /// 와이어 포맷: [4바이트 길이][UTF-8 JSON 본문]
    /// 본문은 이 클래스를 JsonUtility로 직렬화한 결과이며 payload는 자식 DTO의 JSON 문자열을 담는다.
    /// </summary>
    [Serializable]
    public class Packet
    {
        public int type;      // PacketType
        public int senderId;  // 보낸 사람 ClientId (0 = Host)
        public string payload;// 자식 DTO를 JsonUtility로 직렬화한 문자열

        public PacketType Type => (PacketType)type;

        public static Packet Make(PacketType t, int sender, object dto)
        {
            return new Packet
            {
                type = (int)t,
                senderId = sender,
                payload = dto != null ? UnityEngine.JsonUtility.ToJson(dto) : string.Empty
            };
        }

        public T GetPayload<T>() where T : class
        {
            if (string.IsNullOrEmpty(payload)) return null;
            return UnityEngine.JsonUtility.FromJson<T>(payload);
        }
    }

    // ─── 페이로드 DTO ──────────────────────────────────────────────
    [Serializable] public class JoinRequestDto      { public string nickname; }
    [Serializable] public class JoinAcceptedDto     { public int clientId; }
    [Serializable] public class PlayerInfo          { public int clientId; public string nickname; public bool isReady; public bool isHost; }
    [Serializable] public class PlayerListDto       { public PlayerInfo[] players; }
    [Serializable] public class ReadyStateDto       { public bool isReady; }
    [Serializable] public class ChatDto             { public string text; }
    [Serializable] public class StartGameDto        { public int seed; }
}
