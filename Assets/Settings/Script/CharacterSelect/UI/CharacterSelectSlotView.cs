using TMPro;
using UnityEngine;

namespace ProjectM.CharacterSelect
{
    // 3D 슬롯 World Space 이름표 + Ready 뱃지 + 프리뷰 앵커.
    public class CharacterSelectSlotView : MonoBehaviour
    {
        [SerializeField] private int slotIndex;
        [SerializeField] private Transform previewAnchor;
        [SerializeField] private GameObject nameTagRoot;
        [SerializeField] private TMP_Text nameTagText;
        [SerializeField] private GameObject readyBadge;

        public int SlotIndex => slotIndex;
        public Transform PreviewAnchor => previewAnchor != null ? previewAnchor : transform;

        public void ApplyPlayer(RoomPlayerData data)
        {
            if (!data.IsOccupied)
            {
                ClearSlot();
                return;
            }

            if (nameTagRoot != null) nameTagRoot.SetActive(true);
            if (nameTagText != null) nameTagText.text = data.Nickname;
            if (readyBadge != null) readyBadge.SetActive(data.IsReady);
        }

        public void ClearSlot()
        {
            if (nameTagRoot != null) nameTagRoot.SetActive(false);
            if (nameTagText != null) nameTagText.text = string.Empty;
            if (readyBadge != null) readyBadge.SetActive(false);
        }
    }
}
