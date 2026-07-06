using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using ProjectM.Enemy;

namespace ProjectM.Network
{
    // NGO 적 루트. 서버만 AI/NavMesh, 클라이언트는 Transform 동기화 표시.
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkEnemy : NetworkBehaviour
    {
        private NavMeshAgent agent;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        public override void OnNetworkSpawn()
        {
            ConfigureSimulationAuthority();
        }

        private void ConfigureSimulationAuthority()
        {
            bool simulate = !NetworkSessionHelper.IsMultiplayerSession || IsServer;

            foreach (var ai in GetComponentsInChildren<EnemyAIController>(true))
                ai.SetSimulationEnabled(simulate);
        }
    }
}
