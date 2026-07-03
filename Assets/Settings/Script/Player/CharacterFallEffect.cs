using UnityEngine;
using ProjectM.Enemy;
using ProjectM.Network;

namespace ProjectM.Player
{
    /// <summary>
    /// 캐릭터(플레이어/적)가 쓰러질 때 지면 충격 파티클을 재생한다. 플레이어/적 공용 컴포넌트.
    /// - 플레이어: ReviveSystem.OnDowned (호스트/게스트 모두 NetworkDamageBridge가 이미 동기화해줌)
    ///   → 다운 중 플립북 반복, OnRevived / OnFullDeath 시 중지
    /// - 적(서버/싱글플레이): EnemyAIController.OnDeath
    /// - 적(순수 게스트 클라이언트): NetworkDamageBridge.OnClientVisualDeath (ReviveSystem이 없는 경우에만 발생)
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterFallEffect : MonoBehaviour
    {
        [Tooltip("쓰러짐 지면 충격 VFX 프리팹. 비우면 Resources 경로에서 자동으로 불러온다.")]
        [SerializeField] private GameObject groundImpactPrefab;
        [SerializeField] private string resourcesFallbackPath = "Effect/GroundImpact";
        [Tooltip("파티클이 실제 바닥면과 겹치도록 살짝 띄우는 높이 오프셋.")]
        [SerializeField] private float groundOffsetY = 0.03f;

        private ReviveSystem revive;
        private EnemyAIController enemyAi;
        private NetworkDamageBridge damageBridge;
        private GameObject activeGroundImpact;

        private void Awake()
        {
            revive = GetComponent<ReviveSystem>();
            enemyAi = GetComponent<EnemyAIController>();
            damageBridge = GetComponent<NetworkDamageBridge>();
        }

        private void OnEnable()
        {
            if (revive != null)
            {
                revive.OnDowned += HandleFallTriggered;
                revive.OnRevived += StopGroundImpact;
                revive.OnFullDeath += StopGroundImpact;
            }

            if (enemyAi != null)
                enemyAi.OnDeath += HandleEnemyDeath;

            // ReviveSystem이 없는 엔티티에 대해서만 브리지가 이 이벤트를 발생시키므로 항상 구독해도 안전.
            if (damageBridge != null)
                damageBridge.OnClientVisualDeath += HandleFallTriggered;
        }

        private void OnDisable()
        {
            StopGroundImpact();

            if (revive != null)
            {
                revive.OnDowned -= HandleFallTriggered;
                revive.OnRevived -= StopGroundImpact;
                revive.OnFullDeath -= StopGroundImpact;
            }

            if (enemyAi != null)
                enemyAi.OnDeath -= HandleEnemyDeath;

            if (damageBridge != null)
                damageBridge.OnClientVisualDeath -= HandleFallTriggered;
        }

        private void HandleEnemyDeath(EnemyAIController _) => HandleFallTriggered();

        private void HandleFallTriggered() => StartGroundImpact();

        private void StartGroundImpact()
        {
            if (activeGroundImpact != null)
                return;

            var prefab = groundImpactPrefab;
            if (prefab == null && !string.IsNullOrEmpty(resourcesFallbackPath))
                prefab = Resources.Load<GameObject>(resourcesFallbackPath);

            if (prefab == null)
            {
                Debug.LogWarning("[CharacterFallEffect] GroundImpact 프리팹을 찾을 수 없습니다.");
                return;
            }

            activeGroundImpact = Instantiate(prefab, transform);
            activeGroundImpact.transform.localPosition = Vector3.up * groundOffsetY;
            activeGroundImpact.transform.localRotation = Quaternion.identity;
        }

        private void StopGroundImpact()
        {
            if (activeGroundImpact == null)
                return;

            Destroy(activeGroundImpact);
            activeGroundImpact = null;
        }
    }
}
