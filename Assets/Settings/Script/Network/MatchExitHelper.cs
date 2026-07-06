using ProjectM.Core;
using UnityEngine;

namespace ProjectM.Network
{
    // ResultView Retry/Home → MatchRematchCoordinator 위임.
    public static class MatchExitHelper
    {
        public const string CharacterSelectScene = MatchRematchCoordinator.CharacterSelectScene;
        public const string MainMenuScene = MatchRematchCoordinator.MainMenuScene;

        public static void ExitToCharacterSelect()
        {
            if (NetworkSessionHelper.IsMultiplayerSession)
            {
                var director = Object.FindAnyObjectByType<NetworkMatchDirector>();
                if (director != null && director.IsSpawned)
                {
                    director.RegisterRematchIntent();
                    return;
                }
            }

            ResolveCoordinator()?.RequestRematchOffline();
        }

        public static void ExitToMainMenu()
        {
            ResolveCoordinator()?.RequestHome();
        }

        private static MatchRematchCoordinator ResolveCoordinator()
        {
            if (MatchRematchCoordinator.Instance != null)
                return MatchRematchCoordinator.Instance;

            var relay = LobbyRelayService.Instance;
            if (relay != null)
                return relay.GetComponent<MatchRematchCoordinator>()
                       ?? relay.gameObject.AddComponent<MatchRematchCoordinator>();

            var go = new UnityEngine.GameObject(nameof(MatchRematchCoordinator));
            return go.AddComponent<MatchRematchCoordinator>();
        }
    }
}
