using UnityEngine;
using UnityEngine.InputSystem;

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

        private CharacterController cc;
        private HealthSystem health;
        private Vector3 velocity;
        private float pitch;
        private bool canControl = true;

        public bool IsLocalPlayer { get => isLocalPlayer; set => isLocalPlayer = value; }
        public bool CanControl { get => canControl; set => canControl = value; }

        private void Awake()
        {
            cc = GetComponent<CharacterController>();
            health = GetComponent<HealthSystem>();
            if (cameraPivot == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) cameraPivot = cam.transform;
            }
        }

        private void OnEnable()
        {
            if (health != null) health.OnDied += HandleDied;
            if (isLocalPlayer && lockCursor) SetCursorLocked(true);
        }

        private void OnDisable()
        {
            if (health != null) health.OnDied -= HandleDied;
            if (isLocalPlayer) SetCursorLocked(false);
        }

        private void HandleDied(GameObject _) => canControl = false;

        private void Update()
        {
            if (!isLocalPlayer || !canControl) return;

            HandleLook();
            HandleMove();
        }

        private void HandleLook()
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse == null || cameraPivot == null) return;

            // TAB으로 커서 잠금 토글 (디버그 UI 클릭용)
            if (kb != null && kb.tabKey.wasPressedThisFrame)
                SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
                SetCursorLocked(false);

            // 커서가 잠겨 있을 때만 시점 회전
            if (Cursor.lockState != CursorLockMode.Locked) return;

            Vector2 delta = mouse.delta.ReadValue() * lookSensitivity;
            transform.Rotate(0f, delta.x, 0f, Space.Self);

            pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
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
        }
    }
}
