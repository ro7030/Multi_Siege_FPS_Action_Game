using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProjectM.UI
{
    /// <summary>
    /// 무기/키트 슬롯 1칸.
    /// 선택(활성) 강조는 색이 아니라 "이미지 자체"를 교체하는 방식.
    ///   - Highlight Target Image 의 sprite 를 평소(normal) / 선택(selected) 로 바꿔치기.
    /// 코드는 강조 상태, 표시/숨김, 정보 텍스트만 제어한다.
    /// </summary>
    public class WeaponKitSlot : MonoBehaviour
    {
        [Header("연결 (전부 선택)")]
        [SerializeField] private TMP_Text infoText;            // 탄약/개수
        [SerializeField] private Image highlightTargetImage;   // 상태에 따라 이미지가 바뀔 대상 (보통 슬롯 배경/프레임)

        [Header("강조 이미지 (Inspector 에서 연결)")]
        [SerializeField] private Sprite normalSprite;          // 평소 이미지
        [SerializeField] private Sprite selectedSprite;        // 선택 시 이미지

        private bool isHighlighted;

        private void Awake()
        {
            if (highlightTargetImage == null)
                highlightTargetImage = GetComponent<Image>();

            // normalSprite 미지정 시 현재 슬롯 이미지를 평소 상태로 기억
            if (normalSprite == null && highlightTargetImage != null)
                normalSprite = highlightTargetImage.sprite;
        }

        public void SetInfo(string text)
        {
            if (infoText != null) infoText.text = text;
        }

        /// <summary>활성(선택) 강조 — 이미지 교체.</summary>
        public void SetHighlight(bool on)
        {
            isHighlighted = on;
            ApplyCurrentSprite();
        }

        /// <summary>
        /// 슬롯이 표시할 normal/selected 스프라이트 쌍을 동적으로 교체한다.
        /// (슬롯 3·4 의 장착 종류별 아이콘용. WeaponKitHUD 가 호출)
        /// null 을 넘기면 비활성(없음) 상태로 간주하고 보유 중인 기본 스프라이트로 폴백.
        /// </summary>
        public void SetSpritePair(Sprite normal, Sprite selected)
        {
            normalSprite = normal;
            selectedSprite = selected != null ? selected : normal;
            ApplyCurrentSprite();
        }

        private void ApplyCurrentSprite()
        {
            if (highlightTargetImage == null) return;
            var sprite = isHighlighted ? selectedSprite : normalSprite;
            if (sprite != null) highlightTargetImage.sprite = sprite;
        }

        /// <summary>슬롯 전체 표시/숨김 (힐 키트 보유 여부 등).</summary>
        public void SetShown(bool shown)
        {
            if (gameObject.activeSelf != shown) gameObject.SetActive(shown);
        }
    }
}
