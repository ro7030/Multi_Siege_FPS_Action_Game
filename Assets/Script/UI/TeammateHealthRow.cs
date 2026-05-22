using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProjectM.UI
{
    public enum TeammateStatus { Alive, Down, Dead }

    /// <summary>
    /// 팀원 체력바 1줄의 UI 참조 묶음.
    /// 상태 아이콘은 다운/사망 두 개를 각각 둔다 (Canvas 에서 이미지 직접 넣기).
    ///   - 생존: 둘 다 숨김
    ///   - 다운: downIcon 표시
    ///   - 사망: deadIcon 표시
    /// </summary>
    public class TeammateHealthRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image fillImage;     // Filled Horizontal 권장

        [Header("상태 아이콘 (이름 옆, 이미지 직접 넣기)")]
        [SerializeField] private GameObject downIcon; // 다운 시 표시할 이미지
        [SerializeField] private GameObject deadIcon; // 사망 시 표시할 이미지

        public void SetName(string n) { if (nameText != null) nameText.text = n; }
        public void SetFill(float ratio01) { if (fillImage != null) fillImage.fillAmount = Mathf.Clamp01(ratio01); }
        public void SetFillColor(Color c) { if (fillImage != null) fillImage.color = c; }

        /// <summary>상태에 맞는 아이콘만 켠다.</summary>
        public void SetStatus(TeammateStatus status)
        {
            if (downIcon != null) downIcon.SetActive(status == TeammateStatus.Down);
            if (deadIcon != null) deadIcon.SetActive(status == TeammateStatus.Dead);
        }
    }
}
