using System;

namespace ProjectM.UI
{
    /// <summary>
    /// 방 참여 UI에 한 줄로 표시되는 방 정보.
    /// 매치메이킹/디스커버리 레이어가 채워서 JoinRoomPanelController에 넘긴다.
    /// </summary>
    [Serializable]
    public class RoomListEntry
    {
        public string roomName;
        public int currentPlayers;
        public int maxPlayers;
        public bool hasPassword;

        public string lobbyId;
        public string lobbyCode;

        // Legacy TCP fields (deprecated)
        public string hostIp;
        public int hostPort;

        public string FormattedCount => $"{currentPlayers} / {maxPlayers}";
    }
}
