using UnityEngine;
using ProjectM.Player;

namespace ProjectM.Defense
{
    /// <summary>
    /// 게이트 설치 지점. 평소엔 게이트가 비활성 상태이고, 플레이어가 문 키트를 들고
    /// 이 지점에서 F키(PlayerInteractor)로 상호작용하면 게이트를 활성화(설치)한다.
    ///
    /// 구조 (게이트는 비활성이면 PlayerInteractor 가 감지 못하므로 분리)
    ///   GateSlot (항상 활성) + [GateInstaller]  ← 상호작용 담당
    ///   └ Gate   (비활성)                        ← gateObject 로 연결
    /// </summary>
    public class GateInstaller : MonoBehaviour, IInteractable
    {
        [Header("설치 대상")]
        [Tooltip("비활성 상태로 둔 실제 게이트 오브젝트")]
        [SerializeField] private GameObject gateObject;

        [Header("필요 키트")]
        [Tooltip("설치에 필요한 키트 종류 (문 키트)")]
        [SerializeField] private KitType requiredKit = KitType.RepairKit;

        [Header("프롬프트")]
        [SerializeField] private string promptText = "문 설치";
        [SerializeField] private Sprite promptIcon;
        [SerializeField] private Transform promptAnchor; // 비우면 자기 위치

        [Header("옵션")]
        [Tooltip("게이트가 적에게 파괴되면 다시 설치 가능하게 할지")]
        [SerializeField] private bool reAllowAfterDestroyed = true;

        private bool installed;
        private DefenseObject gateDefense;

        private void Awake()
        {
            if (gateObject != null)
            {
                gateObject.SetActive(false);          // 시작 시 게이트 숨김
                gateDefense = gateObject.GetComponent<DefenseObject>();
            }
        }

        private void OnEnable()
        {
            if (reAllowAfterDestroyed && gateDefense != null)
                gateDefense.OnDestroyed += HandleGateDestroyed;
        }

        private void OnDisable()
        {
            if (gateDefense != null) gateDefense.OnDestroyed -= HandleGateDestroyed;
        }

        private void HandleGateDestroyed(DefenseObject _)
        {
            // 게이트 파괴 → 다시 설치 가능
            installed = false;
            if (gateObject != null) gateObject.SetActive(false);
        }

        // ── IInteractable ──
        public bool CanInteract(GameObject interactor)
        {
            if (installed || gateObject == null) return false;
            var inv = interactor != null ? interactor.GetComponent<KitInventory>() : null;
            return inv != null && inv.Has(requiredKit);   // 문 키트 보유 시에만 프롬프트
        }

        public string PromptText => promptText;
        public Sprite PromptIcon => promptIcon;
        public bool IsHold => false;
        public float HoldProgress01 => 0f;
        public Transform PromptAnchor => promptAnchor != null ? promptAnchor : transform;

        public void Interact(GameObject interactor)
        {
            if (installed || gateObject == null) return;
            var inv = interactor != null ? interactor.GetComponent<KitInventory>() : null;
            if (inv == null || !inv.TryConsume(requiredKit)) return;

            gateObject.SetActive(true);   // 게이트 설치(활성화)
            installed = true;
            Debug.Log($"[GateInstaller] 문 설치됨 ({gateObject.name})");
        }

        public void InteractHold(GameObject interactor, float deltaTime) { }
        public void InteractHoldCancel() { }
    }
}
