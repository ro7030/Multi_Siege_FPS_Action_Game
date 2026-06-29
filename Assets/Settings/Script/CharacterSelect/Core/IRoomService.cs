using System;

namespace ProjectM.CharacterSelect
{
    public interface IRoomService
    {
        int LocalSlotIndex { get; }
        int MaxSlots { get; }
        bool IsLocalHost { get; }

        RoomPlayerData GetPlayer(int slotIndex);

        bool CanStartGame { get; }

        void SelectCharacter(int characterIndex);
        void SetReady(bool ready);
        void TryStartGame();
        void LeaveRoom();

        event Action<int> OnPlayerChanged;
        event Action OnAllPlayersReady;
    }
}
