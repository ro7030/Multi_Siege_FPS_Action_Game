using Unity.Netcode.Components;

namespace ProjectM.Network
{
    // 서버가 Transform을 동기화 (적 등 NPC).
    public class ServerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => true;
    }
}
