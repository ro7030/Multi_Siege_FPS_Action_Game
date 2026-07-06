using UnityEngine;
using UnityEngine.UI;

namespace ProjectM.Audio
{
    public enum UiSoundKind
    {
        Default,
        Purchase,
    }

    /// <summary>
    /// Button 클릭 시 UI 효과음을 재생한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class UIButtonSfx : MonoBehaviour
    {
        [SerializeField] private UiSoundKind soundKind = UiSoundKind.Default;

        private Button button;

        public UiSoundKind SoundKind
        {
            get => soundKind;
            set => soundKind = value;
        }

        private void Awake()
        {
            button = GetComponent<Button>();
            if (button != null)
                button.onClick.AddListener(PlaySound);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(PlaySound);
        }

        public static UIButtonSfx Ensure(Button btn, UiSoundKind kind)
        {
            if (btn == null)
                return null;

            var sfx = btn.GetComponent<UIButtonSfx>();
            if (sfx == null)
                sfx = btn.gameObject.AddComponent<UIButtonSfx>();

            sfx.soundKind = kind;
            return sfx;
        }

        private void PlaySound()
        {
            var manager = GameSoundManager.EnsureInstance();
            switch (soundKind)
            {
                case UiSoundKind.Purchase:
                    manager.PlayUIPurchase();
                    break;
                default:
                    manager.PlayUIClick();
                    break;
            }
        }
    }
}
