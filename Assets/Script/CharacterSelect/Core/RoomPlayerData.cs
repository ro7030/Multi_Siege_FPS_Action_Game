namespace ProjectM.CharacterSelect
{
    public struct RoomPlayerData
    {
        public bool IsOccupied;
        public string Nickname;
        public int CharacterIndex;
        public bool IsReady;
        public int Score;

        public static RoomPlayerData Empty => new RoomPlayerData
        {
            IsOccupied = false,
            Nickname = string.Empty,
            CharacterIndex = 0,
            IsReady = false,
            Score = 0
        };
    }
}
