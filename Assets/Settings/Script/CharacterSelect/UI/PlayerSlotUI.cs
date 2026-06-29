using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectM.CharacterSelect
{
    public class PlayerSlotUI : MonoBehaviour
    {
        [SerializeField] private int slotIndex;

        [Header("Text Targets")]
        [Tooltip("폰트/크기/색상/위치는 Inspector에서 직접 설정하세요. 코드는 .text만 갱신합니다.")]
        [SerializeField] private TMP_Text nicknameText;
        [SerializeField] private TMP_Text scoreText;

        [Header("Indicators (assign your own GameObjects)")]
        [Tooltip("준비 상태일 때 활성화됩니다. Image / Animation / Particle 등 자유.")]
        [FormerlySerializedAs("readyCheckIcon")]
        [SerializeField] private GameObject readyIndicator;

        [Tooltip("로컬 플레이어 슬롯에서만 활성화됩니다. 사용하지 않으면 비워두세요.")]
        [SerializeField] private GameObject localIndicator;

        [Tooltip("슬롯이 비어있을 때 활성화됩니다 (대기 표시 등). 사용하지 않으면 비워두세요.")]
        [SerializeField] private GameObject emptyStateRoot;

        [Header("Text Format")]
        [SerializeField] private string emptyNicknameText = "Waiting...";
        [SerializeField] private string scoreSuffix = "P";

        public int SlotIndex => slotIndex;

        public void Bind(RoomPlayerData data, bool isLocal)
        {
            if (nicknameText != null)
                nicknameText.text = data.IsOccupied ? data.Nickname : emptyNicknameText;

            if (scoreText != null)
            {
                scoreText.gameObject.SetActive(data.IsOccupied);
                if (data.IsOccupied) scoreText.text = data.Score + scoreSuffix;
            }

            if (readyIndicator != null)
                readyIndicator.SetActive(data.IsOccupied && data.IsReady);

            if (localIndicator != null)
                localIndicator.SetActive(data.IsOccupied && isLocal);

            if (emptyStateRoot != null)
                emptyStateRoot.SetActive(!data.IsOccupied);
        }
    }
}
