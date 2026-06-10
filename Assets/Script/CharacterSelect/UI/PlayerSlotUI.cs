using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectM.CharacterSelect
{
    public class PlayerSlotUI : MonoBehaviour
    {
        [SerializeField] private int slotIndex;
        [SerializeField] private TMP_Text nicknameText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private GameObject readyCheckIcon;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Colors")]
        [SerializeField] private Color occupiedColor = Color.white;
        [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color localColor = new Color(0.4f, 1f, 0.55f);

        public int SlotIndex => slotIndex;

        public void Bind(RoomPlayerData data, bool isLocal)
        {
            if (canvasGroup != null) canvasGroup.alpha = data.IsOccupied ? 1f : 0.4f;

            if (nicknameText != null)
            {
                nicknameText.text = data.IsOccupied ? data.Nickname : "Waiting...";
                nicknameText.color = !data.IsOccupied ? emptyColor : (isLocal ? localColor : occupiedColor);
            }

            if (scoreText != null)
            {
                scoreText.gameObject.SetActive(data.IsOccupied);
                scoreText.text = $"{data.Score}P";
            }

            if (readyCheckIcon != null)
            {
                readyCheckIcon.SetActive(data.IsOccupied && data.IsReady);
            }
        }
    }
}
