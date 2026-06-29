using Unity.Netcode.Components;

namespace ProjectM.Network
{
    /// <summary>
    /// 서버가 Transform을 동기화 (적 등 NPC).
    /// </summary>
    public class ServerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => true;
    }
}
