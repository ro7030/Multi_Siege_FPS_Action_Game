using System;

namespace ProjectM.Data
{
    // ─── 서버 API 요청/응답에 사용되는 직렬화 전용 데이터 클래스 ───
    // JsonUtility로 직렬화 → 백엔드(API Layer)가 그대로 파싱.
    // 필드 이름은 서버 담당자와 협의하여 맞춰야 한다.

    [Serializable]
    public class SessionResultDto
    {
        public string sessionId;        // 클라이언트가 생성한 GUID 또는 서버에서 발급
        public string roomId;
        public string roomCode;
        public bool cleared;            // true=승리, false=패배
        public int finalWave;
        public int maxWave;
        public int finalScore;
        public int finalBalance;
        public float playSeconds;
        public string endedAtUtc;       // ISO 8601
        public PlayerStatDto[] players; // 참여 플레이어들의 기여도
    }

    [Serializable]
    public class PlayerStatDto
    {
        public int clientId;
        public string nickname;
        public int kills;
        public int harvestCount;
        public int repairCount;
        public int reviveCount;
        public float damageDealt;
        public int finalScore;
    }

    // ─── API 응답 예시 (서버 구현 시 형식 확정) ─────────────────────
    [Serializable]
    public class UploadResponseDto
    {
        public bool ok;
        public string serverSessionId;
        public string message;
    }

    [Serializable]
    public class PendingResultsWrapper
    {
        // JsonUtility는 최상위 배열을 지원하지 않으므로 래퍼 사용.
        public SessionResultDto[] items;
    }
}
