using System.Collections.Generic;
using ProjectM.CharacterSelect;
using Unity.Netcode;

namespace ProjectM.Network
{
    // CharacterSelect → GamePlay 씬 전환 후에도 유지되는 캐릭터 선택 스냅샷.
    public static class MatchLoadoutContext
    {
        private static readonly Dictionary<ulong, int> CharacterIndexByClient = new();
        private static int offlineCharacterIndex;
        private static bool hasOfflineSelection;

        public static bool HasSnapshot => CharacterIndexByClient.Count > 0 || hasOfflineSelection;

        public static void CaptureFromLobby(CharacterLobbyNetwork lobby)
        {
            CharacterIndexByClient.Clear();
            hasOfflineSelection = false;
            offlineCharacterIndex = 0;

            if (lobby == null || NetworkManager.Singleton == null)
                return;

            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
                CharacterIndexByClient[clientId] = lobby.GetCharacterIndexForClient(clientId);
        }

        public static void CaptureOffline(int characterIndex)
        {
            CharacterIndexByClient.Clear();
            offlineCharacterIndex = characterIndex;
            hasOfflineSelection = true;
        }

        public static bool TryGetCharacterIndex(ulong clientId, out int characterIndex)
        {
            return CharacterIndexByClient.TryGetValue(clientId, out characterIndex);
        }

        public static int GetCharacterIndex(ulong clientId)
        {
            return CharacterIndexByClient.TryGetValue(clientId, out int index) ? index : 0;
        }

        public static int GetOfflineCharacterIndex()
        {
            return hasOfflineSelection ? offlineCharacterIndex : 0;
        }

        public static void Clear()
        {
            CharacterIndexByClient.Clear();
            offlineCharacterIndex = 0;
            hasOfflineSelection = false;
        }
    }
}
