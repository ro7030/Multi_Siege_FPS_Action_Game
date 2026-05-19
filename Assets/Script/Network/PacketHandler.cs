using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectM.Network
{
    /// <summary>
    /// PacketType → 콜백 매핑. 메인 스레드에서 호출된다고 가정한다.
    /// </summary>
    public class PacketHandler
    {
        private readonly Dictionary<PacketType, Action<Packet>> map = new();

        public void Register(PacketType type, Action<Packet> handler)
        {
            map[type] = handler;
        }

        public void Unregister(PacketType type)
        {
            map.Remove(type);
        }

        public void Dispatch(Packet packet)
        {
            if (packet == null) return;
            if (map.TryGetValue(packet.Type, out var handler))
            {
                try { handler(packet); }
                catch (Exception e) { Debug.LogError($"[PacketHandler] {packet.Type} 처리 중 예외: {e}"); }
            }
            else
            {
                Debug.LogWarning($"[PacketHandler] 등록되지 않은 PacketType: {packet.Type}");
            }
        }
    }
}
