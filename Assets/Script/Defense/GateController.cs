using UnityEngine;
using UnityEngine.AI;

namespace ProjectM.Defense
{
    /// <summary>
    /// 게이트. 파괴되면 NavMeshObstacle을 비활성화하여 적의 통행을 허용한다.
    /// 같은 GameObject에 NavMeshObstacle(Carve=true)을 두면 살아있을 때 적의 경로를 막는다.
    /// </summary>
    [RequireComponent(typeof(DefenseObject))]
    public class GateController : MonoBehaviour
    {
        [SerializeField] private NavMeshObstacle navObstacle;
        [SerializeField] private GameObject closedVisual;
        [SerializeField] private GameObject openVisual;

        private DefenseObject defense;

        private void Awake()
        {
            defense = GetComponent<DefenseObject>();
            if (navObstacle == null) navObstacle = GetComponentInChildren<NavMeshObstacle>();
        }

        private void OnEnable()
        {
            defense.OnDestroyed += HandleDestroyed;
            ApplyState(false);
        }

        private void OnDisable()
        {
            defense.OnDestroyed -= HandleDestroyed;
        }

        private void HandleDestroyed(DefenseObject _) => ApplyState(true);

        private void ApplyState(bool destroyed)
        {
            if (navObstacle != null) navObstacle.enabled = !destroyed;
            if (closedVisual != null) closedVisual.SetActive(!destroyed);
            if (openVisual != null) openVisual.SetActive(destroyed);
        }
    }
}
