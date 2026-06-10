using UnityEngine;

namespace ProjectM.CharacterSelect
{
    public class CharacterPreviewSpawner : MonoBehaviour
    {
        [SerializeField] private CharacterDatabase database;
        [SerializeField] private Transform[] slotAnchors;
        [SerializeField] private MonoBehaviour roomServiceObject;

        private IRoomService _room;
        private GameObject[] _spawned;
        private int[] _spawnedCharIndex;

        private void Awake()
        {
            _room = roomServiceObject as IRoomService;
            if (_room == null)
            {
                Debug.LogError($"[{nameof(CharacterPreviewSpawner)}] roomServiceObject must implement IRoomService.", this);
                enabled = false;
                return;
            }

            int slots = Mathf.Min(_room.MaxSlots, slotAnchors != null ? slotAnchors.Length : 0);
            _spawned = new GameObject[slots];
            _spawnedCharIndex = new int[slots];
            for (int i = 0; i < slots; i++) _spawnedCharIndex[i] = -1;
        }

        private void OnEnable()
        {
            if (_room != null) _room.OnPlayerChanged += HandlePlayerChanged;
        }

        private void OnDisable()
        {
            if (_room != null) _room.OnPlayerChanged -= HandlePlayerChanged;
        }

        private void Start()
        {
            if (_room == null) return;
            for (int i = 0; i < _spawned.Length; i++) HandlePlayerChanged(i);
        }

        private void HandlePlayerChanged(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _spawned.Length) return;
            var data = _room.GetPlayer(slotIndex);

            if (!data.IsOccupied)
            {
                Despawn(slotIndex);
                return;
            }

            if (_spawnedCharIndex[slotIndex] == data.CharacterIndex && _spawned[slotIndex] != null) return;

            Despawn(slotIndex);

            var charData = database != null ? database.Get(data.CharacterIndex) : null;
            if (charData == null || charData.previewPrefab == null) return;

            var anchor = slotAnchors[slotIndex];
            _spawned[slotIndex] = Instantiate(charData.previewPrefab, anchor.position, anchor.rotation, anchor);
            _spawnedCharIndex[slotIndex] = data.CharacterIndex;
        }

        private void Despawn(int slotIndex)
        {
            if (_spawned[slotIndex] != null) Destroy(_spawned[slotIndex]);
            _spawned[slotIndex] = null;
            _spawnedCharIndex[slotIndex] = -1;
        }
    }
}
