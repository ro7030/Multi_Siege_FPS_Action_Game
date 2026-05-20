using UnityEngine;
using UnityEngine.InputSystem;
using ProjectM.Player;

namespace ProjectM.Defense
{
    /// <summary>
    /// 플레이어가 트리거 안에서 키를 길게 눌러 방어 오브젝트를 수리한다.
    /// (밭 수확은 FarmPlot 이 자체적으로 F키 + FarmManager 팀 분배로 처리하므로 여기서 다루지 않는다.)
    /// 같은 GameObject 또는 부모에 DefenseObject 가 있어야 한다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DefenseInteractable : MonoBehaviour
    {
        [SerializeField] private DefenseObject defense;
        [SerializeField] private InteractKey interactKey = InteractKey.E;

        public enum InteractKey { E, F }

        private bool playerInRange;

        public bool PlayerInRange => playerInRange;
        public bool CanRepair => defense != null && !defense.IsDestroyed && defense.Health.CurrentHp < defense.Health.MaxHp;

        private void Awake()
        {
            if (defense == null) defense = GetComponentInParent<DefenseObject>();

            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
                Debug.LogWarning("[DefenseInteractable] Collider가 Trigger가 아닙니다.", this);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsLocalPlayer(other)) playerInRange = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsLocalPlayer(other)) playerInRange = false;
        }

        private static bool IsLocalPlayer(Collider c)
        {
            var pc = c.GetComponentInParent<PlayerController>();
            return pc != null && pc.IsLocalPlayer;
        }

        private void Update()
        {
            if (!playerInRange) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            bool held = interactKey switch
            {
                InteractKey.E => kb.eKey.isPressed,
                InteractKey.F => kb.fKey.isPressed,
                _ => false
            };

            // 수리는 길게 누름 (밭 수확은 FarmPlot 이 별도 처리)
            if (held && CanRepair)
            {
                defense.Repair(Time.deltaTime);
            }
        }
    }
}
