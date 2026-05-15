using UnityEngine;
using UnityEngine.InputSystem;
using ProjectM.Player;

namespace ProjectM.Defense
{
    /// <summary>
    /// 플레이어가 트리거 안에서 E키를 길게 눌러 방어 오브젝트를 수리하거나 작물을 수확한다.
    /// 같은 GameObject 또는 부모에 DefenseObject / FarmPlot이 있어야 한다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DefenseInteractable : MonoBehaviour
    {
        [SerializeField] private DefenseObject defense;
        [SerializeField] private FarmPlot farm;
        [SerializeField] private InteractKey interactKey = InteractKey.E;

        public enum InteractKey { E, F }

        private bool playerInRange;

        public bool PlayerInRange => playerInRange;
        public bool CanRepair => defense != null && !defense.IsDestroyed && defense.Health.CurrentHp < defense.Health.MaxHp;
        public bool CanHarvest => farm != null && farm.CanHarvest;

        private void Awake()
        {
            if (defense == null) defense = GetComponentInParent<DefenseObject>();
            if (farm == null) farm = GetComponentInParent<FarmPlot>();

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

            bool pressedNow = interactKey switch
            {
                InteractKey.E => kb.eKey.wasPressedThisFrame,
                InteractKey.F => kb.fKey.wasPressedThisFrame,
                _ => false
            };
            bool held = interactKey switch
            {
                InteractKey.E => kb.eKey.isPressed,
                InteractKey.F => kb.fKey.isPressed,
                _ => false
            };

            // 수확은 단발 입력, 수리는 길게 누름
            if (pressedNow && CanHarvest)
            {
                int yield = farm.Harvest();
                Debug.Log($"[Harvest] +{yield}");
            }

            if (held && CanRepair)
            {
                defense.Repair(Time.deltaTime);
            }
        }
    }
}
