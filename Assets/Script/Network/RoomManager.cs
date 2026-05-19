using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectM.Network
{
    /// <summary>
    /// 로비/방 조율자. NetworkHost/NetworkClient 위에서 입장/Ready/시작 흐름을 처리한다.
    /// MVP에서는 Room Code는 표시용이며, 실제 접속은 IP+포트로 직접 한다.
    /// (추후 API Layer의 매치메이킹이 Code→IP를 해석하게 됨.)
    /// </summary>
    public class RoomManager : MonoBehaviour
    {
        [SerializeField] private NetworkHost host;
        [SerializeField] private NetworkClient client;
        [SerializeField] private int defaultPort = 7777;
        [SerializeField] private int maxPlayers = 4;

        public bool IsHost { get; private set; }
        public bool IsInRoom { get; private set; }
        public string RoomCode { get; private set; } = "";
        public int LocalClientId { get; private set; } = -1;
        public string LocalNickname { get; private set; } = "Player";

        private readonly Dictionary<int, PlayerInfo> players = new();
        public IReadOnlyDictionary<int, PlayerInfo> Players => players;

        public event Action OnRoomStateChanged;
        public event Action OnGameStartSignal;

        private PacketHandler clientHandlers;

        private void Awake()
        {
            if (host == null) host = GetComponent<NetworkHost>();
            if (client == null) client = GetComponent<NetworkClient>();
            if (host == null) host = gameObject.AddComponent<NetworkHost>();
            if (client == null) client = gameObject.AddComponent<NetworkClient>();

            // Host 측: connection ID가 필요하므로 OnPacketReceived 직접 구독
            host.OnPacketReceived += OnHostPacket;
            host.OnClientConnected += OnHostClientConnected;
            host.OnClientDisconnected += OnHostClientDisconnected;

            // Client 측: PacketHandler 디스패치 사용
            clientHandlers = new PacketHandler();
            clientHandlers.Register(PacketType.JoinAccepted, OnClientJoinAccepted);
            clientHandlers.Register(PacketType.PlayerListUpdate, OnClientPlayerList);
            clientHandlers.Register(PacketType.StartGame, OnClientStartGame);
            clientHandlers.Register(PacketType.Chat, OnClientChat);

            client.OnPacketReceived += pkt => clientHandlers.Dispatch(pkt);
            client.OnDisconnected += OnClientDisconnected;
        }

        // ─── Host 측 API ──────────────────────────────────────────
        public bool CreateRoom(string nickname)
        {
            if (IsInRoom) return false;
            LocalNickname = string.IsNullOrEmpty(nickname) ? "Host" : nickname;
            if (!host.StartHost(defaultPort)) return false;

            IsHost = true;
            IsInRoom = true;
            RoomCode = GenerateRoomCode();
            LocalClientId = 0;
            players.Clear();
            players[0] = new PlayerInfo { clientId = 0, nickname = LocalNickname, isHost = true, isReady = false };

            OnRoomStateChanged?.Invoke();
            Debug.Log($"[Room] 방 생성됨. Code = {RoomCode}");
            return true;
        }

        public void StartGame()
        {
            if (!IsHost) return;
            if (!AreAllReady())
            {
                Debug.LogWarning("[Room] 일부 플레이어가 Ready 상태가 아닙니다.");
                return;
            }
            var dto = new StartGameDto { seed = UnityEngine.Random.Range(1, int.MaxValue) };
            host.Broadcast(Packet.Make(PacketType.StartGame, 0, dto));
            OnGameStartSignal?.Invoke();
            Debug.Log($"[Room] StartGame 송출 seed={dto.seed}");
        }

        public void ToggleLocalReady()
        {
            if (!IsInRoom) return;
            if (IsHost)
            {
                var me = players[0];
                me.isReady = !me.isReady;
                players[0] = me;
                BroadcastPlayerList();
                OnRoomStateChanged?.Invoke();
            }
            else
            {
                bool next = !(players.ContainsKey(LocalClientId) && players[LocalClientId].isReady);
                client.Send(Packet.Make(PacketType.ReadyState, LocalClientId, new ReadyStateDto { isReady = next }));
            }
        }

        private void OnHostClientConnected(int connId)
        {
            // 접속만 들어옴. 실제 입장은 JoinRequest 수신 후 처리.
        }

        private void OnHostClientDisconnected(int connId)
        {
            if (players.Remove(connId))
            {
                BroadcastPlayerList();
                OnRoomStateChanged?.Invoke();
            }
        }

        private void OnHostPacket(int connId, Packet pkt)
        {
            switch (pkt.Type)
            {
                case PacketType.JoinRequest:
                {
                    var dto = pkt.GetPayload<JoinRequestDto>();
                    if (dto == null) return;

                    if (players.Count >= maxPlayers)
                    {
                        Debug.LogWarning("[Room/Host] 정원 초과로 입장 거부");
                        return;
                    }

                    var info = new PlayerInfo
                    {
                        clientId = connId,
                        nickname = string.IsNullOrEmpty(dto.nickname) ? $"Guest{connId}" : dto.nickname,
                        isHost = false,
                        isReady = false
                    };
                    players[connId] = info;

                    host.SendTo(connId, Packet.Make(PacketType.JoinAccepted, 0, new JoinAcceptedDto { clientId = connId }));
                    BroadcastPlayerList();
                    OnRoomStateChanged?.Invoke();
                    Debug.Log($"[Room/Host] {info.nickname} 입장 (id={connId})");
                    break;
                }
                case PacketType.ReadyState:
                {
                    if (!players.ContainsKey(connId)) return;
                    var dto = pkt.GetPayload<ReadyStateDto>();
                    if (dto == null) return;
                    var p = players[connId];
                    p.isReady = dto.isReady;
                    players[connId] = p;
                    BroadcastPlayerList();
                    OnRoomStateChanged?.Invoke();
                    break;
                }
                case PacketType.Chat:
                {
                    host.Broadcast(pkt); // 그대로 중계
                    break;
                }
            }
        }

        private void BroadcastPlayerList()
        {
            var dto = new PlayerListDto { players = players.Values.ToArray() };
            host.Broadcast(Packet.Make(PacketType.PlayerListUpdate, 0, dto));
        }

        private bool AreAllReady()
        {
            if (players.Count == 0) return false;
            foreach (var p in players.Values)
                if (!p.isHost && !p.isReady) return false;
            return true; // Host만 있어도 시작 가능 (MVP)
        }

        // ─── Client 측 API ────────────────────────────────────────
        public bool JoinRoom(string ip, string nickname)
        {
            if (IsInRoom) return false;
            LocalNickname = string.IsNullOrEmpty(nickname) ? "Guest" : nickname;
            if (!client.Connect(ip, defaultPort)) return false;

            IsHost = false;
            IsInRoom = true;
            client.Send(Packet.Make(PacketType.JoinRequest, 0, new JoinRequestDto { nickname = LocalNickname }));
            OnRoomStateChanged?.Invoke();
            return true;
        }

        public void LeaveRoom()
        {
            if (IsHost) host.StopHost();
            else client.Disconnect();
            ResetState();
        }

        private void ResetState()
        {
            IsInRoom = false;
            IsHost = false;
            RoomCode = "";
            LocalClientId = -1;
            players.Clear();
            OnRoomStateChanged?.Invoke();
        }

        private void OnClientJoinAccepted(Packet pkt)
        {
            var dto = pkt.GetPayload<JoinAcceptedDto>();
            if (dto == null) return;
            LocalClientId = dto.clientId;
            client.SetAssignedId(dto.clientId);
            Debug.Log($"[Room/Client] ClientId 할당 = {dto.clientId}");
            OnRoomStateChanged?.Invoke();
        }

        private void OnClientPlayerList(Packet pkt)
        {
            var dto = pkt.GetPayload<PlayerListDto>();
            if (dto?.players == null) return;
            players.Clear();
            foreach (var p in dto.players) players[p.clientId] = p;
            OnRoomStateChanged?.Invoke();
        }

        private void OnClientStartGame(Packet pkt)
        {
            Debug.Log("[Room/Client] StartGame 수신");
            OnGameStartSignal?.Invoke();
        }

        private void OnClientChat(Packet pkt)
        {
            var dto = pkt.GetPayload<ChatDto>();
            if (dto != null) Debug.Log($"[Chat] id={pkt.senderId}: {dto.text}");
        }

        private void OnClientDisconnected()
        {
            Debug.Log("[Room/Client] 연결 종료");
            ResetState();
        }

        public void SendChat(string text)
        {
            if (!IsInRoom || string.IsNullOrEmpty(text)) return;
            var dto = new ChatDto { text = text };
            var pkt = Packet.Make(PacketType.Chat, IsHost ? 0 : LocalClientId, dto);
            if (IsHost) host.Broadcast(pkt);
            else client.Send(pkt);
        }

        private static string GenerateRoomCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var sb = new System.Text.StringBuilder(6);
            var rng = new System.Random();
            for (int i = 0; i < 6; i++) sb.Append(chars[rng.Next(chars.Length)]);
            return sb.ToString();
        }
    }
}
