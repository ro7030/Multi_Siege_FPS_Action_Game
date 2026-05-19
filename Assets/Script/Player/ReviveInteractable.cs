using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectM.Player
{
    /// <summary>
    /// 다운된 동료에게 다가가 E키를 길게 눌러 부활시킨다.
    /// 자기 자신은 부활 불가. 트리거 콜라이더 안에 들어와야 인터랙션 가능.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ReviveInteractable : MonoBehaviour
    {
        [SerializeField] private ReviveSystem target;
        [SerializeField] private KeyboardKey reviveKey = KeyboardKey.E;

        private bool localPlayerInRange;
        private GameObject localRescuer;

        public enum KeyboardKey { E, F, Space }

        private void Awake()
        {
            if (target == null) target = GetComponentInParent<ReviveSystem>();
            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
                Debug.LogWarning("[Revive] Collider가 Trigger가 아닙니다. isTrigger=true 권장.", this);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsLocalPlayer(other)) return;
            localPlayerInRange = true;
            localRescuer = other.gameObject;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsLocalPlayer(other)) return;
            localPlayerInRange = false;
            localRescuer = null;
            target?.CancelRevive();
        }

        private static bool IsLocalPlayer(Collider c)
        {
            var pc = c.GetComponentInParent<PlayerController>();
            return pc != null && pc.IsLocalPlayer;
        }

        private void Update()
        {
            if (!localPlayerInRange || target == null) return;
            if (!target.IsDown || target.IsDead) return;
            if (IsSelf()) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            bool pressed = reviveKey switch
            {
                KeyboardKey.E => kb.eKey.isPressed,
                KeyboardKey.F => kb.fKey.isPressed,
                KeyboardKey.Space => kb.spaceKey.isPressed,
                _ => false
            };

            if (pressed) target.ProgressRevive(Time.deltaTime);
            else target.CancelRevive();
        }

        private bool IsSelf()
        {
            // 부활 대상이 같은 오브젝트의 ReviveSystem이라면 본인.
            return localRescuer != null
                && target != null
                && target.gameObject == localRescuer;
        }
    }
}
