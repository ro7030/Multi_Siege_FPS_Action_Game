using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace ProjectM.Network
{
    /// <summary>
    /// Guest 측. Host에 TCP 접속하여 패킷을 주고받는다.
    /// </summary>
    public class NetworkClient : MonoBehaviour
    {
        public bool IsConnected { get; private set; }
        public int AssignedClientId { get; private set; } = -1;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<Packet> OnPacketReceived; // 메인 스레드

        private TcpClient client;
        private NetworkStream stream;
        private Thread recvThread;
        private readonly CancellationTokenSource cts = new();
        private readonly ConcurrentQueue<Action> mainThreadQueue = new();

        public bool Connect(string host, int port)
        {
            if (IsConnected) { Debug.LogWarning("[Client] 이미 연결됨"); return false; }
            try
            {
                client = new TcpClient();
                client.Connect(host, port);
                stream = client.GetStream();
                IsConnected = true;
                recvThread = new Thread(RecvLoop) { IsBackground = true };
                recvThread.Start();
                Debug.Log($"[Client] {host}:{port} 접속 성공");
                mainThreadQueue.Enqueue(() => OnConnected?.Invoke());
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Client] 접속 실패: {e.Message}");
                IsConnected = false;
                return false;
            }
        }

        public void Disconnect()
        {
            if (!IsConnected) return;
            IsConnected = false;
            try { cts.Cancel(); } catch { }
            try { stream?.Close(); } catch { }
            try { client?.Close(); } catch { }
            Debug.Log("[Client] 연결 종료");
        }

        public void SetAssignedId(int id) => AssignedClientId = id;

        private void OnDestroy() => Disconnect();
        private void OnApplicationQuit() => Disconnect();

        private void Update()
        {
            while (mainThreadQueue.TryDequeue(out var act))
            {
                try { act(); } catch (Exception e) { Debug.LogError(e); }
            }
        }

        private void RecvLoop()
        {
            try
            {
                while (IsConnected && client.Connected)
                {
                    var packet = PacketIO.Read(stream);
                    if (packet == null) break;
                    mainThreadQueue.Enqueue(() => OnPacketReceived?.Invoke(packet));
                }
            }
            catch (Exception e)
            {
                Debug.Log($"[Client] 수신 종료: {e.Message}");
            }
            finally
            {
                IsConnected = false;
                mainThreadQueue.Enqueue(() => OnDisconnected?.Invoke());
            }
        }

        public bool Send(Packet packet)
        {
            if (!IsConnected) return false;
            try { PacketIO.Write(stream, packet); return true; }
            catch (Exception e) { Debug.LogWarning($"[Client] 송신 실패: {e.Message}"); return false; }
        }
    }
}
