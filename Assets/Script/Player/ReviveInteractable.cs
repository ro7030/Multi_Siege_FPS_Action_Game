using UnityEngine;

namespace ProjectM.Player
{
    /// <summary>
    /// 다운된 동료 부활 상호작용 (홀드형). PlayerInteractor 가 F키 입력을 전달한다.
    /// 자기 자신은 부활 불가. 다운 상태일 때만 프롬프트가 뜬다.
    /// </summary>
    public class ReviveInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private ReviveSystem target;

        [Header("프롬프트")]
        [Tooltip("{name} 은 대상 이름으로 치환된다. 예: \"{name} 부활\"")]
        [SerializeField] private string promptFormat = "{name} 부활";
        [SerializeField] private Sprite promptIcon;
        [SerializeField] private Transform promptAnchor; // 비우면 자기 위치

        private void Awake()
        {
            if (target == null) target = GetComponentInParent<ReviveSystem>();
        }

        // ── IInteractable ──
        public bool CanInteract(GameObject interactor)
        {
            if (target == null || !target.IsDown || target.IsDead) return false;
            // 자기 자신은 부활 불가
            if (interactor == target.gameObject) return false;
            return true;
        }

        public string PromptText
        {
            get
            {
                string n = target != null ? target.gameObject.name : "Player";
                return promptFormat.Replace("{name}", n);
            }
        }

        public Sprite PromptIcon => promptIcon;
        public bool IsHold => true;
        public float HoldProgress01 =>
            (target != null && target.ReviveDuration > 0f) ? Mathf.Clamp01(target.ReviveProgress / target.ReviveDuration) : 0f;
        public Transform PromptAnchor => promptAnchor != null ? promptAnchor : transform;

        public void Interact(GameObject interactor) { } // 홀드형이므로 사용 안 함

        public void InteractHold(GameObject interactor, float deltaTime)
        {
            if (target == null) return;
            target.ProgressRevive(deltaTime);
        }

        public void InteractHoldCancel()
        {
            target?.CancelRevive();
        }
    }
}
