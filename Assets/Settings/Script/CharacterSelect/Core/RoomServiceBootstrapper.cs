using ProjectM.Network;
using Unity.Netcode;
using UnityEngine;

namespace ProjectM.CharacterSelect
{
    // 온라인 세션이면 NetworkRoomService, 아니면 LocalRoomService를 활성화한다.
    [DefaultExecutionOrder(-100)]
    public class RoomServiceBootstrapper : MonoBehaviour
    {
        [SerializeField] private LocalRoomService localRoomService;
        [SerializeField] private NetworkRoomService networkRoomService;

        public MonoBehaviour ActiveRoomService { get; private set; }

        private void Awake()
        {
            if (localRoomService == null) localRoomService = GetComponent<LocalRoomService>();
            if (networkRoomService == null) networkRoomService = GetComponent<NetworkRoomService>();

            bool online = IsOnlineSession();

            if (localRoomService != null) localRoomService.enabled = !online;
            if (networkRoomService != null) networkRoomService.enabled = online;

            ActiveRoomService = online
                ? (MonoBehaviour)networkRoomService
                : (MonoBehaviour)localRoomService;

            Debug.Log($"[RoomServiceBootstrapper] Mode={(online ? "Network" : "Local")}");
        }

        private static bool IsOnlineSession()
        {
            var lobby = LobbyRelayService.Instance;
            var nm = NetworkManager.Singleton;
            return lobby != null && lobby.IsInSession && nm != null && nm.IsListening;
        }
    }
}
