using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace ProjectM.Network
{
    /// <summary>
    /// Listen Server의 Host 측. TcpListener로 Guest를 받고, 패킷을 메인 스레드 큐로 전달한다.
    /// </summary>
    public class NetworkHost : MonoBehaviour
    {
        public bool IsRunning { get; private set; }
        public int Port { get; private set; }

        public event Action<int> OnClientConnected;     // clientId
        public event Action<int> OnClientDisconnected;  // clientId
        public event Action<int, Packet> OnPacketReceived; // senderClientId, packet (메인 스레드)

        private TcpListener listener;
        private Thread acceptThread;
        private readonly CancellationTokenSource cts = new();

        private class Conn
        {
            public int Id;
            public TcpClient Client;
            public NetworkStream Stream;
            public Thread RecvThread;
        }

        private readonly ConcurrentDictionary<int, Conn> connections = new();
        private int nextClientId = 1; // 0 = Host 자신

        // 메인 스레드 디스패치용 큐
        private readonly ConcurrentQueue<Action> mainThreadQueue = new();

        public IReadOnlyCollection<int> ConnectedClientIds => (IReadOnlyCollection<int>)connections.Keys;

        public bool StartHost(int port)
        {
            if (IsRunning) { Debug.LogWarning("[Host] 이미 실행 중"); return false; }

            try
            {
                Port = port;
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                IsRunning = true;
                acceptThread = new Thread(AcceptLoop) { IsBackground = true };
                acceptThread.Start();
                Debug.Log($"[Host] 시작됨 (포트 {port})");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Host] 시작 실패: {e.Message}");
                IsRunning = false;
                return false;
            }
        }

        public void StopHost()
        {
            if (!IsRunning) return;
            IsRunning = false;
            try { cts.Cancel(); } catch { }
            try { listener?.Stop(); } catch { }
            foreach (var kv in connections) CloseConn(kv.Value);
            connections.Clear();
            Debug.Log("[Host] 중지됨");
        }

        private void OnDestroy() => StopHost();
        private void OnApplicationQuit() => StopHost();

        private void Update()
        {
            while (mainThreadQueue.TryDequeue(out var act))
            {
                try { act(); } catch (Exception e) { Debug.LogError(e); }
            }
        }

        private void AcceptLoop()
        {
            while (IsRunning && !cts.IsCancellationRequested)
            {
                try
                {
                    var tcp = listener.AcceptTcpClient();
                    int id = Interlocked.Increment(ref nextClientId);
                    var conn = new Conn
                    {
                        Id = id,
                        Client = tcp,
                        Stream = tcp.GetStream()
                    };
                    conn.RecvThread = new Thread(() => RecvLoop(conn)) { IsBackground = true };
                    conn.RecvThread.Start();
                    connections[id] = conn;

                    mainThreadQueue.Enqueue(() =>
                    {
                        Debug.Log($"[Host] 클라이언트 접속: id={id}");
                        OnClientConnected?.Invoke(id);
                    });
                }
                catch (SocketException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception e) { Debug.LogError($"[Host] Accept 예외: {e.Message}"); break; }
            }
        }

        private void RecvLoop(Conn conn)
        {
            try
            {
                while (IsRunning && conn.Client.Connected)
                {
                    var packet = PacketIO.Read(conn.Stream);
                    if (packet == null) break;
                    mainThreadQueue.Enqueue(() => OnPacketReceived?.Invoke(conn.Id, packet));
                }
            }
            catch (Exception e)
            {
                Debug.Log($"[Host] 수신 종료 id={conn.Id}: {e.Message}");
            }
            finally
            {
                connections.TryRemove(conn.Id, out _);
                CloseConn(conn);
                mainThreadQueue.Enqueue(() =>
                {
                    Debug.Log($"[Host] 클라이언트 종료: id={conn.Id}");
                    OnClientDisconnected?.Invoke(conn.Id);
                });
            }
        }

        private static void CloseConn(Conn c)
        {
            try { c.Stream?.Close(); } catch { }
            try { c.Client?.Close(); } catch { }
        }

        public void SendTo(int clientId, Packet packet)
        {
            if (!connections.TryGetValue(clientId, out var conn)) return;
            try { PacketIO.Write(conn.Stream, packet); }
            catch (Exception e) { Debug.LogWarning($"[Host] 송신 실패 id={clientId}: {e.Message}"); }
        }

        public void Broadcast(Packet packet, int excludeClientId = -1)
        {
            foreach (var kv in connections)
            {
                if (kv.Key == excludeClientId) continue;
                try { PacketIO.Write(kv.Value.Stream, packet); }
                catch (Exception e) { Debug.LogWarning($"[Host] Broadcast 실패 id={kv.Key}: {e.Message}"); }
            }
        }
    }
}
