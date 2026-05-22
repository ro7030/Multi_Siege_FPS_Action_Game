using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectM.Player
{
    /// <summary>
    /// 플레이어 주변의 IInteractable 을 찾아 F키 상호작용을 처리한다.
    /// - 매 프레임 가장 적합한 대상(가깝고 정면에 가까운)을 골라 OnTargetChanged 로 알림
    /// - InteractionPromptView 가 이 이벤트를 받아 "(F) 메시지" 를 표시
    /// - F키: 단발형은 누르는 순간 Interact(), 홀드형은 누르는 동안 InteractHold()
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("탐지")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float interactRange = 3f;
        [SerializeField] private LayerMask interactMask = ~0;
        [Tooltip("정면 우선 가중치. 클수록 '바라보는' 대상을 더 우선.")]
        [SerializeField] private float facingWeight = 1.5f;

        [Header("입력")]
        [SerializeField] private bool isLocalPlayer = true;
        [SerializeField] private Key interactKey = Key.F;

        /// <summary>현재 조준 중인 상호작용 대상 (없으면 null).</summary>
        public IInteractable Current { get; private set; }

        /// <summary>대상이 바뀔 때 발생 (UI 가 구독).</summary>
        public event Action<IInteractable> OnTargetChanged;

        private readonly Collider[] buffer = new Collider[16];

        private void Awake()
        {
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
        }

        private void Update()
        {
            if (!isLocalPlayer) return;

            var best = FindBest();
            if (!ReferenceEquals(best, Current))
            {
                // 대상이 바뀌면 이전 홀드 취소
                Current?.InteractHoldCancel();
                Current = best;
                OnTargetChanged?.Invoke(Current);
            }

            HandleInput();
        }

        private void HandleInput()
        {
            if (Current == null) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (Cursor.lockState != CursorLockMode.Locked) return; // UI 열림 중엔 무시

            var key = kb[interactKey];

            if (Current.IsHold)
            {
                if (key.isPressed) Current.InteractHold(gameObject, Time.deltaTime);
                else if (key.wasReleasedThisFrame) Current.InteractHoldCancel();
            }
            else
            {
                if (key.wasPressedThisFrame) Current.Interact(gameObject);
            }
        }

        private IInteractable FindBest()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, interactRange, buffer, interactMask, QueryTriggerInteraction.Collide);

            IInteractable best = null;
            float bestScore = float.MaxValue;
            Vector3 fwd = viewCamera != null ? viewCamera.transform.forward : transform.forward;
            fwd.y = 0; fwd.Normalize();

            for (int i = 0; i < count; i++)
            {
                var col = buffer[i];
                if (col == null) continue;
                if (col.transform.IsChildOf(transform)) continue; // 자기 자신 제외

                var inter = col.GetComponentInParent<IInteractable>();
                if (inter == null) continue;
                if (!inter.CanInteract(gameObject)) continue;

                Vector3 to = col.bounds.center - transform.position;
                float dist = to.magnitude;
                to.y = 0;

                // 점수 = 거리 + 정면에서 벗어난 각도 가중치 (낮을수록 우선)
                float angle = Vector3.Angle(fwd, to.normalized);
                float score = dist + angle * 0.01f * facingWeight;

                if (score < bestScore) { bestScore = score; best = inter; }
            }

            return best;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactRange);
        }
    }
}
