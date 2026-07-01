using UnityEngine;
using ProjectM.Network;

namespace ProjectM.Player
{
    /// <summary>
    /// NetworkPlayer 비주얼 Animator를 이동·견착·재장전·투척과 동기화한다.
    /// Owner는 로컬 입력을 적용 후 NetworkPlayerAnimationBridge로 복제한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerAnimationController : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
        private static readonly int IsAimingHash = Animator.StringToHash("IsAiming");
        private static readonly int ReloadHash = Animator.StringToHash("Reload");
        private static readonly int IsReloadingHash = Animator.StringToHash("IsReloading");
        private static readonly int ThrowHash = Animator.StringToHash("Throw");

        [SerializeField] private CharacterController characterController;
        [SerializeField] private WeaponController weaponController;
        [SerializeField] private ThrowableEquipper throwableEquipper;
        [SerializeField] private CharacterVisualBinder visualBinder;
        [SerializeField] private NetworkPlayerAnimationBridge animBridge;
        [Tooltip("visual 프리팹의 Animator에 Controller가 비어있을 때 강제로 채워 넣을 안전망.")]
        [SerializeField] private RuntimeAnimatorController fallbackAnimatorController;

        private Animator animator;
        private Vector3 lastPosition;
        private bool hasLastPosition;
        private bool lastSyncedIsReloading;
        private bool hasForcedInitialPublish;
        private bool hasWarnedMissingController;

        private bool UseRemoteDriver =>
            animBridge != null
            && animBridge.IsSpawned
            && NetworkSessionHelper.IsMultiplayerSession
            && !animBridge.IsOwner;

        private bool UseLocalDriver => !UseRemoteDriver;

        private void Awake()
        {
            if (characterController == null)
                characterController = GetComponent<CharacterController>();
            if (weaponController == null)
                weaponController = GetComponent<WeaponController>();
            if (throwableEquipper == null)
                throwableEquipper = GetComponent<ThrowableEquipper>();
            if (visualBinder == null)
                visualBinder = GetComponentInChildren<CharacterVisualBinder>(true);
            if (animBridge == null)
                animBridge = GetComponent<NetworkPlayerAnimationBridge>();
        }

        private void OnEnable()
        {
            if (visualBinder != null)
                visualBinder.OnVisualApplied += HandleVisualApplied;

            if (weaponController != null)
            {
                weaponController.OnReloadStart += HandleReloadStart;
                weaponController.OnReloadEnd += HandleReloadEnd;
            }

            if (throwableEquipper != null)
                throwableEquipper.OnThrown += HandleThrown;

            if (animBridge != null)
            {
                animBridge.OnSyncedStateChanged += HandleSyncedStateChanged;
                animBridge.OnThrowRequested += HandleThrowSynced;
            }

            TryBindExistingVisual();
        }

        private void OnDisable()
        {
            if (visualBinder != null)
                visualBinder.OnVisualApplied -= HandleVisualApplied;

            if (weaponController != null)
            {
                weaponController.OnReloadStart -= HandleReloadStart;
                weaponController.OnReloadEnd -= HandleReloadEnd;
            }

            if (throwableEquipper != null)
                throwableEquipper.OnThrown -= HandleThrown;

            if (animBridge != null)
            {
                animBridge.OnSyncedStateChanged -= HandleSyncedStateChanged;
                animBridge.OnThrowRequested -= HandleThrowSynced;
            }

            hasForcedInitialPublish = false;
        }

        private void Update()
        {
            if (animator == null) return;

            if (UseLocalDriver)
            {
                var state = BuildLocalState();
                ApplyState(state, triggerReloadOnStart: false);

                if (animBridge != null && animBridge.IsSpawned && animBridge.IsOwner)
                {
                    if (!hasForcedInitialPublish)
                    {
                        animBridge.ForcePublish(
                            state.Speed,
                            state.Grounded,
                            state.VerticalSpeed,
                            state.IsAiming,
                            state.IsReloading);
                        hasForcedInitialPublish = true;
                    }
                    else
                    {
                        animBridge.Publish(
                            state.Speed,
                            state.Grounded,
                            state.VerticalSpeed,
                            state.IsAiming,
                            state.IsReloading);
                    }
                }
            }
            else
            {
                ApplyRemoteState();
            }
        }

        private void HandleVisualApplied(GameObject visual, Transform _)
        {
            BindAnimator(visual);
            if (UseLocalDriver)
                ApplyState(BuildLocalState(), triggerReloadOnStart: false);
            else
                ApplyRemoteState(forceReloadTrigger: true);
        }

        private void HandleSyncedStateChanged()
        {
            if (UseRemoteDriver)
                ApplyRemoteState(forceReloadTrigger: true);
        }

        private void HandleThrown()
        {
            if (!UseLocalDriver) return;

            animator?.SetTrigger(ThrowHash);
            if (animBridge != null && animBridge.IsSpawned && animBridge.IsOwner)
                animBridge.PublishThrow();
        }

        private void HandleThrowSynced()
        {
            if (UseRemoteDriver)
                animator?.SetTrigger(ThrowHash);
        }

        private void TryBindExistingVisual()
        {
            if (visualBinder != null && visualBinder.CurrentVisual != null)
                BindAnimator(visualBinder.CurrentVisual);
            else
                BindAnimator(FindVisualRoot());
        }

        private GameObject FindVisualRoot()
        {
            var childAnimator = GetComponentInChildren<Animator>(true);
            return childAnimator != null ? childAnimator.gameObject : null;
        }

        private void BindAnimator(GameObject visual)
        {
            if (visual == null) return;
            animator = visual.GetComponentInChildren<Animator>(true);
            hasLastPosition = false;
            lastSyncedIsReloading = false;
            hasWarnedMissingController = false;

            EnsureAnimatorController();
        }

        private void EnsureAnimatorController()
        {
            if (animator == null) return;

            if (animator.runtimeAnimatorController == null && fallbackAnimatorController != null)
                animator.runtimeAnimatorController = fallbackAnimatorController;

            if (animator.runtimeAnimatorController == null && !hasWarnedMissingController)
            {
                Debug.LogWarning(
                    $"[{nameof(PlayerAnimationController)}] Animator Controller 없음 — 애니메이션 적용 불가. object={name}",
                    this);
                hasWarnedMissingController = true;
            }
        }

        private AnimState BuildLocalState()
        {
            bool grounded = characterController != null && characterController.isGrounded;
            float speed = ResolveHorizontalSpeed();
            float verticalSpeed = ResolveVerticalSpeed();
            bool isAiming = weaponController != null && weaponController.IsAimHeld;
            bool isReloading = weaponController != null && weaponController.IsReloading;

            return new AnimState(speed, grounded, verticalSpeed, isAiming, isReloading);
        }

        private void ApplyRemoteState(bool forceReloadTrigger = false)
        {
            if (animBridge == null) return;

            bool isReloading = animBridge.SyncedIsReloading;
            bool triggerReload = forceReloadTrigger && isReloading && !lastSyncedIsReloading;
            lastSyncedIsReloading = isReloading;

            ApplyState(
                new AnimState(
                    animBridge.SyncedSpeed,
                    animBridge.SyncedGrounded,
                    animBridge.SyncedVerticalSpeed,
                    animBridge.SyncedIsAiming,
                    isReloading),
                triggerReloadOnStart: triggerReload);
        }

        private void ApplyState(AnimState state, bool triggerReloadOnStart)
        {
            if (animator == null) return;
            if (animator.runtimeAnimatorController == null)
            {
                EnsureAnimatorController();
                if (animator.runtimeAnimatorController == null) return;
            }

            animator.SetFloat(SpeedHash, state.Speed);
            animator.SetBool(GroundedHash, state.Grounded);
            animator.SetFloat(VerticalSpeedHash, state.VerticalSpeed);
            animator.SetBool(IsAimingHash, state.IsAiming);
            animator.SetBool(IsReloadingHash, state.IsReloading);

            if (triggerReloadOnStart)
                animator.SetTrigger(ReloadHash);
        }

        private float ResolveHorizontalSpeed()
        {
            if (characterController != null && characterController.enabled)
            {
                Vector3 velocity = characterController.velocity;
                velocity.y = 0f;
                return velocity.magnitude;
            }

            if (!hasLastPosition)
            {
                lastPosition = transform.position;
                hasLastPosition = true;
                return 0f;
            }

            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            delta.y = 0f;
            return delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        }

        private float ResolveVerticalSpeed()
        {
            if (characterController != null && characterController.enabled)
                return characterController.velocity.y;

            return 0f;
        }

        private void HandleReloadStart()
        {
            if (!UseLocalDriver || animator == null) return;
            animator.SetTrigger(ReloadHash);
            animator.SetBool(IsReloadingHash, true);
        }

        private void HandleReloadEnd()
        {
            if (!UseLocalDriver) return;
            animator?.SetBool(IsReloadingHash, false);
        }

        private readonly struct AnimState
        {
            public readonly float Speed;
            public readonly bool Grounded;
            public readonly float VerticalSpeed;
            public readonly bool IsAiming;
            public readonly bool IsReloading;

            public AnimState(float speed, bool grounded, float verticalSpeed, bool isAiming, bool isReloading)
            {
                Speed = speed;
                Grounded = grounded;
                VerticalSpeed = verticalSpeed;
                IsAiming = isAiming;
                IsReloading = isReloading;
            }
        }
    }
}
