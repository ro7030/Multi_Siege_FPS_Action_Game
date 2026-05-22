using UnityEngine;
using UnityEngine.Events;

namespace ProjectM.Player
{
    /// <summary>
    /// 범용 상호작용 컴포넌트. 코드 없이 Inspector 에서 메시지/아이콘/동작을 설정한다.
    /// 게이트 설치, 버튼, 임의 트리거 등 커스텀 상호작용에 붙여 쓴다.
    ///
    /// [편집 가능]
    ///   - promptText : 프롬프트 메시지 (직접 입력)
    ///   - promptIcon : 아이콘 Sprite (직접 연결)
    ///   - isHold + holdDuration : 홀드형 여부와 필요 시간
    ///   - onInteract / onHoldComplete : 실행할 동작 (UnityEvent 로 연결)
    /// </summary>
    public class SimpleInteractable : MonoBehaviour, IInteractable
    {
        [Header("프롬프트 (직접 편집)")]
        [SerializeField] private string promptText = "상호작용";
        [SerializeField] private Sprite promptIcon;
        [SerializeField] private Transform promptAnchor; // 비우면 자기 위치

        [Header("동작 방식")]
        [SerializeField] private bool isHold = false;
        [SerializeField] private float holdDuration = 2f;

        [Header("활성 조건 (선택)")]
        [Tooltip("이 값이 false 면 상호작용 불가. 외부 스크립트가 SetEnabled 로 제어 가능.")]
        [SerializeField] private bool interactable = true;

        [Header("이벤트")]
        public UnityEvent onInteract;        // 단발형 실행 시
        public UnityEvent onHoldComplete;    // 홀드 완료 시
        public UnityEvent onHoldCancel;      // 홀드 취소 시

        private float holdProgress;

        // ── 외부 제어 ──
        public void SetInteractable(bool value) => interactable = value;
        public void SetPromptText(string text) => promptText = text;

        // ── IInteractable ──
        public bool CanInteract(GameObject interactor) => interactable;
        public string PromptText => promptText;
        public Sprite PromptIcon => promptIcon;
        public bool IsHold => isHold;
        public float HoldProgress01 => holdDuration > 0f ? Mathf.Clamp01(holdProgress / holdDuration) : 0f;
        public Transform PromptAnchor => promptAnchor != null ? promptAnchor : transform;

        public void Interact(GameObject interactor)
        {
            if (!interactable) return;
            onInteract?.Invoke();
        }

        public void InteractHold(GameObject interactor, float deltaTime)
        {
            if (!interactable) return;
            holdProgress += deltaTime;
            if (holdProgress >= holdDuration)
            {
                holdProgress = 0f;
                onHoldComplete?.Invoke();
            }
        }

        public void InteractHoldCancel()
        {
            if (holdProgress > 0f) onHoldCancel?.Invoke();
            holdProgress = 0f;
        }
    }
}
