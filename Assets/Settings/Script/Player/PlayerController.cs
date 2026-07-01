using System;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectM.UI;

namespace ProjectM.Player
{
    /// <summary>
    /// 1인칭 캐릭터 이동/시점 컨트롤. CharacterController 기반.
    /// Input System 1.19의 Keyboard/Mouse를 직접 폴링한다 (InputAction asset 의존 없음).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("이동")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float sprintSpeed = 8f;
        [SerializeField] private float jumpHeight = 1.4f;
        [SerializeField] private float gravity = -20f;

        [Header("시점")]
        [SerializeField] private Transform cameraPivot;     // 비워두면 자식에서 자동 탐색
        [SerializeField] private float lookSensitivity = 0.12f;
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;
        [SerializeField] private bool lockCursor = true;

        [Header("로컬 권한")]
        [SerializeField] private bool isLocalPlayer = true; // 원격 플레이어는 입력 무시

        [Header("사격 반동")]
        [SerializeField] private WeaponController weaponController;
        [Tooltip("발당 반동으로 카메라가 위로 튀는 각도(도).")]
        [SerializeField] private float recoilKick = 1.2f;
        [Tooltip("발당 좌우로 살짝 흔들리는 최대 각도(도).")]
        [SerializeField] private float recoilRandomYaw = 0.3f;
        [Tooltip("반동 오프셋이 원래 시점으로 복귀하는 속도(도/초).")]
        [SerializeField] private float recoilRecoverySpeed = 8f;

        private CharacterController cc;
        private HealthSystem health;
        private ReviveSystem revive;
        private KitEquipper kitEquipper;
        private ThrowableEquipper throwableEquipper;
        private Vector3 velocity;
        private float pitch;
        private float recoilPitchOffset;
        private float recoilYawOffset;
        private bool canControl = true;

        public bool IsLocalPlayer { get => isLocalPlayer; set => isLocalPlayer = value; }
        public bool CanControl { get => canControl; set => canControl = value; }
        public Transform CameraPivot => cameraPivot;

        public void AlignCameraPivotTo(Transform anchor)
        {
            if (cameraPivot == null || anchor == null) return;
            cameraPivot.position = anchor.position;
        }

        private void Awake()
        {
            cc = GetComponent<CharacterController>();
            health = GetComponent<HealthSystem>();
            revive = GetComponent<ReviveSystem>();
            kitEquipper = GetComponent<KitEquipper>();
            throwableEquipper = GetComponent<ThrowableEquipper>();
            if (weaponController == null)
                weaponController = GetComponent<WeaponController>();
            if (cameraPivot == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) cameraPivot = cam.transform;
            }
        }

        private void OnEnable()
        {
            if (health != null) health.OnDied += HandleDied;
            if (revive != null)
            {
                revive.OnDowned += HandleDowned;
                revive.OnRevived += HandleRevived;
                revive.OnFullDeath += HandleFullDeath;
                SyncControlFromRevive();
            }
            if (weaponController != null) weaponController.OnFired += HandleFired;
            if (isLocalPlayer && lockCursor) SetCursorLocked(true);
        }

        private void OnDisable()
        {
            if (health != null) health.OnDied -= HandleDied;
            if (revive != null)
            {
                revive.OnDowned -= HandleDowned;
                revive.OnRevived -= HandleRevived;
                revive.OnFullDeath -= HandleFullDeath;
            }
            if (weaponController != null) weaponController.OnFired -= HandleFired;
            if (isLocalPlayer) SetCursorLocked(false);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!isLocalPlayer || !hasFocus) return;
            RefreshInputDevices();
        }

        private void HandleDied(GameObject _)
        {
            if (revive == null) canControl = false;
        }

        private void HandleDowned() => canControl = false;
        private void HandleFullDeath() => canControl = false;
        private void HandleRevived() => canControl = true;

        private void HandleFired()
        {
            if (!isLocalPlayer) return;
            recoilPitchOffset -= recoilKick;
            recoilYawOffset += UnityEngine.Random.Range(-recoilRandomYaw, recoilRandomYaw);
        }

        private void SyncControlFromRevive()
        {
            if (revive == null) return;
            canControl = !revive.IsDown && !revive.IsDead;
        }

        private void Update()
        {
            if (!isLocalPlayer || !canControl) return;
            if (UIInputModal.IsBlockingGameplayInput) return;

            HandleCursorInput();
            HandleRecoilRecovery();
            HandleLook();
            HandleMove();
        }

        private void HandleRecoilRecovery()
        {
            float recovery = recoilRecoverySpeed * Time.deltaTime;
            recoilPitchOffset = Mathf.MoveTowards(recoilPitchOffset, 0f, recovery);
            recoilYawOffset = Mathf.MoveTowards(recoilYawOffset, 0f, recovery);
        }

        private void HandleCursorInput()
        {
            if (UIInputModal.IsBlockingGameplayInput) return;
            if (!lockCursor) return;

            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame)
                SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);

            if (kb != null && kb.escapeKey.wasPressedThisFrame)
                SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);

            if (Cursor.lockState == CursorLockMode.Locked) return;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                SetCursorLocked(true);
        }

        private void HandleLook()
        {
            var mouse = Mouse.current;
            if (mouse == null || cameraPivot == null) return;

            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (kitEquipper != null && kitEquipper.IsSelecting) return;
            if (throwableEquipper != null && throwableEquipper.IsSelecting) return;

            Vector2 delta = mouse.delta.ReadValue() * lookSensitivity;
            transform.Rotate(0f, delta.x, 0f, Space.Self);

            pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);

            float appliedPitch = Mathf.Clamp(pitch + recoilPitchOffset, minPitch, maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(appliedPitch, recoilYawOffset, 0f);
        }

        private void HandleMove()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            bool sprint = kb.leftShiftKey.isPressed;

            Vector3 input = transform.right * x + transform.forward * z;
            if (input.sqrMagnitude > 1f) input.Normalize();
            float speed = sprint ? sprintSpeed : walkSpeed;

            if (cc.isGrounded)
            {
                if (velocity.y < 0f) velocity.y = -2f;
                if (kb.spaceKey.wasPressedThisFrame)
                    velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            else
            {
                velocity.y += gravity * Time.deltaTime;
            }

            Vector3 motion = input * speed + Vector3.up * velocity.y;
            cc.Move(motion * Time.deltaTime);
        }

        private void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;

            if (locked)
                RefreshInputDevices();
        }

        private static void RefreshInputDevices()
        {
            if (Keyboard.current != null)
                InputSystem.EnableDevice(Keyboard.current);
            if (Mouse.current != null)
                InputSystem.EnableDevice(Mouse.current);
        }
    }
}
