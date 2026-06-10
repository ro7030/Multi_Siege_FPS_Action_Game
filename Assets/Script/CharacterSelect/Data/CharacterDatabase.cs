using UnityEngine;

namespace ProjectM.CharacterSelect
{
    [CreateAssetMenu(menuName = "ProjectM/CharacterSelect/CharacterDatabase", fileName = "CharacterDatabase")]
    public class CharacterDatabase : ScriptableObject
    {
        [SerializeField] private CharacterData[] characters;

        public int Count => characters != null ? characters.Length : 0;

        public CharacterData Get(int index)
        {
            if (Count == 0) return null;
            int wrapped = ((index % Count) + Count) % Count;
            return characters[wrapped];
        }

        public int Wrap(int index)
        {
            if (Count == 0) return 0;
            return ((index % Count) + Count) % Count;
        }
    }
}
