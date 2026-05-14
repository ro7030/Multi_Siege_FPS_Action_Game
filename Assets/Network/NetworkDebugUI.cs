using UnityEngine;

namespace ProjectM.Network
{
    /// <summary>
    /// Phase 2 임시 디버그 UI. RoomManager 위에서 Host/Guest 입장 흐름을 OnGUI 버튼으로 확인한다.
    /// </summary>
    public class NetworkDebugUI : MonoBehaviour
    {
        [SerializeField] private RoomManager room;

        private string nickname = "Player1";
        private string hostIp = "127.0.0.1";
        private string chatInput = "";
        private Vector2 logScroll;
        private string log = "";

        private void Awake()
        {
            if (room == null) room = FindAnyObjectByType<RoomManager>();
        }

        private void OnEnable()
        {
            if (room == null) return;
            room.OnRoomStateChanged += HandleRoomChanged;
            room.OnGameStartSignal += HandleGameStart;
        }

        private void OnDisable()
        {
            if (room == null) return;
            room.OnRoomStateChanged -= HandleRoomChanged;
            room.OnGameStartSignal -= HandleGameStart;
        }

        private void HandleRoomChanged() => AddLog($"Room state changed (players={room.Players.Count})");
        private void HandleGameStart()   => AddLog("StartGame signal received");

        private void AddLog(string msg)
        {
            log = $"[{Time.time:F1}s] {msg}\n" + log;
            if (log.Length > 2000) log = log.Substring(0, 2000);
        }

        private void OnGUI()
        {
            if (room == null)
            {
                GUI.Label(new Rect(380, 10, 400, 30), "RoomManager not found in scene.");
                return;
            }

            GUI.skin.label.fontSize = 14;
            GUI.skin.button.fontSize = 13;
            GUI.skin.textField.fontSize = 13;

            GUILayout.BeginArea(new Rect(380, 10, 380, Screen.height - 20), GUI.skin.box);
            GUILayout.Label("=== Phase 2 Network Debug ===");

            GUILayout.Label($"InRoom : {room.IsInRoom}   Host: {room.IsHost}");
            GUILayout.Label($"Code   : {room.RoomCode}");
            GUILayout.Label($"MyId   : {room.LocalClientId}   Nick: {room.LocalNickname}");

            GUILayout.Space(6);
            GUILayout.Label("Nickname:");
            nickname = GUILayout.TextField(nickname);

            if (!room.IsInRoom)
            {
                if (GUILayout.Button("Create Room (Host)")) room.CreateRoom(nickname);
                GUILayout.Label("Host IP:");
                hostIp = GUILayout.TextField(hostIp);
                if (GUILayout.Button("Join Room (Guest)")) room.JoinRoom(hostIp, nickname);
            }
            else
            {
                if (GUILayout.Button("Toggle Ready")) room.ToggleLocalReady();
                if (room.IsHost && GUILayout.Button("Start Game (Host only)")) room.StartGame();
                if (GUILayout.Button("Leave Room")) room.LeaveRoom();
            }

            GUILayout.Space(8);
            GUILayout.Label("--- Players ---");
            foreach (var kv in room.Players)
            {
                var p = kv.Value;
                GUILayout.Label($"  id={p.clientId} {p.nickname}{(p.isHost ? " [HOST]" : "")} {(p.isReady ? "READY" : "...")}");
            }

            GUILayout.Space(8);
            GUILayout.Label("--- Chat ---");
            GUILayout.BeginHorizontal();
            chatInput = GUILayout.TextField(chatInput);
            if (GUILayout.Button("Send", GUILayout.Width(60)))
            {
                room.SendChat(chatInput);
                chatInput = "";
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.Label("--- Event Log ---");
            logScroll = GUILayout.BeginScrollView(logScroll, GUILayout.ExpandHeight(true));
            GUILayout.TextArea(log, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }
    }
}
