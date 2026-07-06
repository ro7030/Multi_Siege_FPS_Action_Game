using TMPro;
using UnityEngine;

namespace ProjectM.CharacterSelect
{
    // CharacterSelect Canvas 하위 TMP UI에 Jalnan2 폰트를 일괄 적용한다.
    public class CharacterSelectFontBinder : MonoBehaviour
    {
        private const string DefaultFontResourcePath = "Fonts/Jalnan2/Jalnan2TTF SDF";

        [SerializeField] private TMP_FontAsset uiFont;
        [SerializeField] private bool includeInactive = true;

        private void Awake()
        {
            if (uiFont == null)
                uiFont = Resources.Load<TMP_FontAsset>(DefaultFontResourcePath);

            if (uiFont == null) return;

            foreach (var text in GetComponentsInChildren<TMP_Text>(includeInactive))
                text.font = uiFont;

            foreach (var input in GetComponentsInChildren<TMP_InputField>(includeInactive))
            {
                if (input.textComponent != null) input.textComponent.font = uiFont;
                if (input.placeholder is TMP_Text placeholder) placeholder.font = uiFont;
            }
        }
    }
}
