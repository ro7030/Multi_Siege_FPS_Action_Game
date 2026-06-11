using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectM.CharacterSelect
{
    public class LocalRoomService : MonoBehaviour, IRoomService
    {
        [SerializeField] private int maxSlots = 4;
        [SerializeField] private string localNickname = "YOU";
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private CharacterDatabase database;

        private RoomPlayerData[] _slots;

        public int LocalSlotIndex => 0;
        public int MaxSlots => maxSlots;
        public bool IsLocalHost => true;

        public event Action<int> OnPlayerChanged;
        public event Action OnAllPlayersReady;

        private void Awake()
        {
            _slots = new RoomPlayerData[maxSlots];
            for (int i = 0; i < maxSlots; i++) _slots[i] = RoomPlayerData.Empty;

            _slots[LocalSlotIndex] = new RoomPlayerData
            {
                IsOccupied = true,
                Nickname = localNickname,
                CharacterIndex = 0,
                IsReady = false,
                Score = 0
            };
        }

        private void Start()
        {
            for (int i = 0; i < maxSlots; i++) OnPlayerChanged?.Invoke(i);
        }

        public RoomPlayerData GetPlayer(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= maxSlots) return RoomPlayerData.Empty;
            return _slots[slotIndex];
        }

        public void SelectCharacter(int characterIndex)
        {
            var data = _slots[LocalSlotIndex];
            if (!data.IsOccupied) return;

            int wrapped = database != null ? database.Wrap(characterIndex) : characterIndex;
            if (data.CharacterIndex == wrapped) return;

            data.CharacterIndex = wrapped;
            _slots[LocalSlotIndex] = data;
            OnPlayerChanged?.Invoke(LocalSlotIndex);
        }

        public void SetReady(bool ready)
        {
            var data = _slots[LocalSlotIndex];
            if (!data.IsOccupied) return;
            if (data.IsReady == ready) return;

            data.IsReady = ready;
            _slots[LocalSlotIndex] = data;
            OnPlayerChanged?.Invoke(LocalSlotIndex);

            if (ready && AllOccupiedReady()) OnAllPlayersReady?.Invoke();
        }

        public void LeaveRoom()
        {
            if (!string.IsNullOrEmpty(mainMenuSceneName))
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
        }

        private bool AllOccupiedReady()
        {
            for (int i = 0; i < maxSlots; i++)
            {
                if (_slots[i].IsOccupied && !_slots[i].IsReady) return false;
            }
            return true;
        }
    }
}
