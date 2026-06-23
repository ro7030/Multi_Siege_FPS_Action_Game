using System;
using ProjectM.CharacterSelect;
using UnityEngine;

namespace ProjectM.Network
{
    /// <summary>
    /// 플레이어 프리팹의 비주얼 슬롯. 선택된 캐릭터 인덱스에 해당하는
    /// CharacterData.gameplayPrefab을 visualRoot 아래에 인스턴스화한다.
    /// 네트워크 동기화는 NetworkPlayer가 담당하고, 이 컴포넌트는 비주얼 교체만 수행.
    /// </summary>
    public class CharacterVisualBinder : MonoBehaviour
    {
        [SerializeField] private CharacterDatabase database;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private string eyeAnchorName = "EyeAnchor";

        private int currentIndex = int.MinValue;
        private GameObject currentVisual;
        private Transform currentEyeAnchor;

        public Transform VisualRoot => visualRoot;
        public GameObject CurrentVisual => currentVisual;
        public Transform CurrentEyeAnchor => currentEyeAnchor;

        public event Action<GameObject, Transform> OnVisualApplied;

        public void ApplyCharacter(int characterIndex)
        {
            if (database == null)
            {
                Debug.LogWarning($"[{nameof(CharacterVisualBinder)}] database 미할당", this);
                return;
            }
            if (visualRoot == null)
            {
                Debug.LogWarning($"[{nameof(CharacterVisualBinder)}] visualRoot 미할당", this);
                return;
            }

            int wrapped = database.Wrap(characterIndex);
            if (wrapped == currentIndex && currentVisual != null) return;

            var data = database.Get(wrapped);
            if (data == null || data.gameplayPrefab == null)
            {
                Debug.LogWarning($"[{nameof(CharacterVisualBinder)}] index={wrapped} 의 gameplayPrefab 없음", this);
                return;
            }

            ClearVisual();
            currentVisual = Instantiate(data.gameplayPrefab, visualRoot);
            currentVisual.transform.localPosition = Vector3.zero;
            currentVisual.transform.localRotation = Quaternion.identity;
            currentVisual.transform.localScale = Vector3.one;
            currentEyeAnchor = FindEyeAnchor(currentVisual.transform);
            currentIndex = wrapped;

            OnVisualApplied?.Invoke(currentVisual, currentEyeAnchor);
        }

        private Transform FindEyeAnchor(Transform root)
        {
            if (string.IsNullOrEmpty(eyeAnchorName)) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == eyeAnchorName) return t;
            return null;
        }

        private void ClearVisual()
        {
            currentEyeAnchor = null;
            if (currentVisual != null)
            {
                Destroy(currentVisual);
                currentVisual = null;
            }

            for (int i = visualRoot.childCount - 1; i >= 0; i--)
                Destroy(visualRoot.GetChild(i).gameObject);
        }
    }
}
