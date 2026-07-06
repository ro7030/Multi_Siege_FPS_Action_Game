using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectM.UI
{
    // 방 참여 패널의 한 줄. 방 이름과 대기자 수를 표시하고, 클릭되면 부모 패널에 알린다.
    public class JoinRoomItem : MonoBehaviour
    {
        [Header("표시")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text countText;
        [Tooltip("선택 상태를 시각적으로 표시할 오브젝트 (예: 강조 테두리). 없으면 비워둬도 됨.")]
        [SerializeField] private GameObject selectedHighlight;

        [Header("입력")]
        [Tooltip("줄 전체를 누를 수 있는 Button. 보통 이 오브젝트의 Button 컴포넌트.")]
        [SerializeField] private Button selectButton;

        public RoomListEntry Entry { get; private set; }
        public event Action<JoinRoomItem> OnSelected;

        private void Awake()
        {
            if (selectButton == null) selectButton = GetComponent<Button>();
            if (selectButton != null) selectButton.onClick.AddListener(HandleClick);
            SetSelected(false);
        }

        public void Bind(RoomListEntry entry)
        {
            Entry = entry;
            if (nameText != null) nameText.text = entry?.roomName ?? "(empty)";
            if (countText != null) countText.text = entry != null ? entry.FormattedCount : "0 / 0";

            bool full = entry != null && entry.currentPlayers >= entry.maxPlayers;
            if (selectButton != null) selectButton.interactable = !full;
        }

        public void SetSelected(bool selected)
        {
            if (selectedHighlight != null) selectedHighlight.SetActive(selected);

            if (selectButton != null)
            {
                var colors = selectButton.colors;
                colors.normalColor = selected ? new Color(0.85f, 0.92f, 1f, 1f) : Color.white;
                selectButton.colors = colors;
            }
        }

        private void HandleClick() => OnSelected?.Invoke(this);
    }
}
