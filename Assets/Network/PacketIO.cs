using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace ProjectM.Network
{
    /// <summary>
    /// 와이어 포맷 [4바이트 길이 (little-endian)][UTF-8 JSON 본문] 인코딩/디코딩.
    /// </summary>
    public static class PacketIO
    {
        private const int MAX_PACKET_SIZE = 1024 * 1024; // 1MB

        public static void Write(NetworkStream stream, Packet packet)
        {
            if (stream == null || packet == null) return;
            string json = JsonUtility.ToJson(packet);
            byte[] body = Encoding.UTF8.GetBytes(json);
            byte[] header = BitConverter.GetBytes(body.Length);
            lock (stream)
            {
                stream.Write(header, 0, 4);
                stream.Write(body, 0, body.Length);
                stream.Flush();
            }
        }

        public static Packet Read(NetworkStream stream)
        {
            byte[] header = ReadExact(stream, 4);
            if (header == null) return null;
            int len = BitConverter.ToInt32(header, 0);
            if (len <= 0 || len > MAX_PACKET_SIZE) throw new IOException($"Invalid packet length {len}");
            byte[] body = ReadExact(stream, len);
            if (body == null) return null;
            string json = Encoding.UTF8.GetString(body);
            return JsonUtility.FromJson<Packet>(json);
        }

        private static byte[] ReadExact(NetworkStream stream, int count)
        {
            byte[] buf = new byte[count];
            int read = 0;
            while (read < count)
            {
                int n = stream.Read(buf, read, count - read);
                if (n <= 0) return null;
                read += n;
            }
            return buf;
        }
    }
}
