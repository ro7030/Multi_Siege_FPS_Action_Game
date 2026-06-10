using UnityEngine;

namespace ProjectM.CharacterSelect
{
    [CreateAssetMenu(menuName = "ProjectM/CharacterSelect/CharacterData", fileName = "CharacterData")]
    public class CharacterData : ScriptableObject
    {
        public string displayName;
        public GameObject previewPrefab;
        public GameObject gameplayPrefab;
    }
}
