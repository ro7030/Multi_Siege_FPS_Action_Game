using System;

namespace ProjectM.CharacterSelect
{
    public interface IRoomService
    {
        int LocalSlotIndex { get; }
        int MaxSlots { get; }

        RoomPlayerData GetPlayer(int slotIndex);

        void SelectCharacter(int characterIndex);
        void SetReady(bool ready);
        void LeaveRoom();

        event Action<int> OnPlayerChanged;
        event Action OnAllPlayersReady;
    }
}
