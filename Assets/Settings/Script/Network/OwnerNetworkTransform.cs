using Unity.Netcode.Components;

namespace ProjectM.Network
{
    // 소유 클라이언트가 Transform을 동기화 (1인칭 이동).
    public class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
