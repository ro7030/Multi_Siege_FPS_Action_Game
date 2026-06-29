using Unity.Netcode.Components;

namespace ProjectM.Network
{
    /// <summary>
    /// 소유 클라이언트가 Transform을 동기화 (1인칭 이동).
    /// </summary>
    public class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
