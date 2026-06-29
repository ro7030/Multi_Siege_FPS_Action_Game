using UnityEngine;
using ProjectM.Network;
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
        [Tooltip("F키를 누르고 있어야 하는 시간(초). 완료되면 설치된다.")]
        [SerializeField] private float holdDuration = 1.5f;

        private bool installed;
        private DefenseObject gateDefense;
        private float holdProgress;

        public bool IsInstalled => installed;

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

        private bool IsSlotInstalled()
        {
            if (TryGetComponent<NetworkGateInstaller>(out var netBridge)
                && NetworkSessionHelper.IsMultiplayerSession
                && netBridge.IsSpawned)
                return netBridge.IsGateInstalled;

            return installed;
        }

        private void HandleGateDestroyed(DefenseObject _)
        {
            installed = false;
            if (gateObject != null) gateObject.SetActive(false);

            if (TryGetComponent<NetworkGateInstaller>(out var bridge) && NetworkSessionHelper.IsServer)
                bridge.ServerSetInstalled(false);
        }

        // ── IInteractable ──
        public bool CanInteract(GameObject interactor)
        {
            if (IsSlotInstalled() || gateObject == null) return false;
            if (interactor == null) return false;
            var equipper = interactor.GetComponent<KitEquipper>();
            var inventory = interactor.GetComponent<KitInventory>();
            return equipper != null
                && inventory != null
                && equipper.EquippedKit == requiredKit
                && inventory.Has(requiredKit);
        }

        public string PromptText => promptText;
        public Sprite PromptIcon => promptIcon;
        public bool IsHold => true;
        public float HoldProgress01 => holdDuration > 0f ? Mathf.Clamp01(holdProgress / holdDuration) : 0f;
        public Transform PromptAnchor => promptAnchor != null ? promptAnchor : transform;

        public void Interact(GameObject interactor) { } // 홀드형이므로 단발 입력은 사용 안 함

        public void InteractHold(GameObject interactor, float deltaTime)
        {
            if (IsSlotInstalled() || gateObject == null) return;
            if (interactor == null) { holdProgress = 0f; return; }

            var equipper = interactor.GetComponent<KitEquipper>();
            var inventory = interactor.GetComponent<KitInventory>();
            if (equipper == null
                || inventory == null
                || equipper.EquippedKit != requiredKit
                || !inventory.Has(requiredKit))
            {
                holdProgress = 0f;
                return;
            }

            holdProgress += deltaTime;
            if (holdProgress < holdDuration) return;

            holdProgress = 0f;
            if (!inventory.Has(requiredKit))
                return;
            if (TryGetComponent<NetworkGateInstaller>(out var bridge))
                bridge.RequestInstall(interactor);
            else
                TryInstallFromInteractor(interactor);
        }

        public bool TryInstallFromInteractor(GameObject interactor)
        {
            if (IsSlotInstalled() || gateObject == null || interactor == null)
                return false;

            var equipper = interactor.GetComponent<KitEquipper>();
            if (equipper == null || equipper.EquippedKit != requiredKit)
                return false;

            return TryInstallFromServer(interactor, requireEquippedKit: true);
        }

        /// <summary>서버 권한 설치. NGO 클라이언트는 장착 상태 대신 인벤토리만 검증한다.</summary>
        public bool TryInstallFromServer(GameObject interactor, bool requireEquippedKit = false)
        {
            return TryInstallFromServer(interactor, requireEquippedKit, out _);
        }

        public bool TryInstallFromServer(
            GameObject interactor,
            bool requireEquippedKit,
            out string failReason)
        {
            failReason = string.Empty;

            if (IsSlotInstalled() || gateObject == null || interactor == null)
            {
                failReason = "invalid_state";
                return false;
            }

            if (requireEquippedKit)
            {
                var equipper = interactor.GetComponent<KitEquipper>();
                if (equipper == null || equipper.EquippedKit != requiredKit)
                {
                    failReason = "kit_not_equipped";
                    return false;
                }
            }

            var inv = interactor.GetComponent<KitInventory>();
            if (inv == null)
            {
                failReason = "no_inventory";
                return false;
            }

            if (!inv.Has(requiredKit))
            {
                failReason = "no_kit";
                Debug.LogWarning(
                    $"[GateInstaller] {requiredKit} 없음 — local={inv.GetCount(requiredKit)}");
                return false;
            }

            if (!inv.TryConsume(requiredKit))
            {
                failReason = "consume_failed";
                Debug.LogWarning(
                    $"[GateInstaller] {requiredKit} 소모 실패 — local={inv.GetCount(requiredKit)}");
                return false;
            }

            ApplyInstalledLocal(true);
            Debug.Log($"[GateInstaller] 문 설치됨 ({gateObject.name})");
            return true;
        }

        public void ApplyNetworkInstalled(bool value)
        {
            installed = value;
            if (gateObject == null) return;

            if (value)
            {
                if (NetworkSessionHelper.IsServer
                    && gateObject.TryGetComponent<NetworkGateBodyBridge>(out var bodyBridge))
                {
                    bodyBridge.ServerPrepareForInstall();
                }
                else
                {
                    if (gateObject.TryGetComponent<HealthSystem>(out var health))
                        health.ResetHp();
                    if (gateObject.TryGetComponent<GateController>(out var gateController))
                        gateController.RestoreAliveState();
                }

                gateObject.SetActive(true);
            }
            else
            {
                gateObject.SetActive(false);
            }
        }

        private void ApplyInstalledLocal(bool value)
        {
            ApplyNetworkInstalled(value);

            if (TryGetComponent<NetworkGateInstaller>(out var bridge) && NetworkSessionHelper.IsServer)
                bridge.ServerSetInstalled(value);
        }

        public void InteractHoldCancel() { holdProgress = 0f; }
    }
}
