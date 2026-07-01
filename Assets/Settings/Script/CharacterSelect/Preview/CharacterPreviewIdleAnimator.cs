using UnityEngine;

namespace ProjectM.CharacterSelect
{
    /// <summary>
    /// CharacterSelect 프리뷰 Chibi를 Idle 상태로 고정한다.
    /// 프리뷰 인스턴스의 Animator에 Controller가 비어있는 경우를 대비해
    /// 스포너가 전달하는 fallback Controller로 보정한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterPreviewIdleAnimator : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
        private static readonly int IsAimingHash = Animator.StringToHash("IsAiming");
        private static readonly int IsReloadingHash = Animator.StringToHash("IsReloading");

        private Animator animator;
        private RuntimeAnimatorController fallbackController;
        private bool hasSnappedIdle;

        public void Initialize(RuntimeAnimatorController fallback)
        {
            fallbackController = fallback;
            EnsureAnimatorBound();
            EnsureController();
        }

        private void Awake()
        {
            EnsureAnimatorBound();
        }

        private void OnEnable()
        {
            hasSnappedIdle = false;
            ApplyIdleState();
        }

        private void LateUpdate()
        {
            ApplyIdleState();
        }

        private void EnsureAnimatorBound()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
        }

        private void EnsureController()
        {
            if (animator == null) return;

            if (animator.runtimeAnimatorController == null && fallbackController != null)
                animator.runtimeAnimatorController = fallbackController;
        }

        private void ApplyIdleState()
        {
            EnsureAnimatorBound();
            EnsureController();

            if (animator == null || animator.runtimeAnimatorController == null)
            {
                if (!hasSnappedIdle)
                    Debug.LogWarning(
                        $"[{nameof(CharacterPreviewIdleAnimator)}] Animator/Controller 없음 — Idle 적용 불가. object={name}",
                        this);
                return;
            }

            animator.SetFloat(SpeedHash, 0f);
            animator.SetBool(GroundedHash, true);
            animator.SetFloat(VerticalSpeedHash, 0f);
            animator.SetBool(IsAimingHash, false);
            animator.SetBool(IsReloadingHash, false);

            if (hasSnappedIdle) return;

            animator.Play("Idle", 0, 0f);
            hasSnappedIdle = true;
        }
    }
}
