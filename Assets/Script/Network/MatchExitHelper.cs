using System.Collections;
using ProjectM.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectM.Network
{
    /// <summary>
    /// 매치 종료 후 NGO 세션·로비 정리 및 캐릭터 선택 씬 복귀.
    /// </summary>
    public static class MatchExitHelper
    {
        public const string CharacterSelectScene = "CharacterSelect";

        private static bool exitInProgress;

        public static void ExitToCharacterSelect()
        {
            if (exitInProgress) return;
            exitInProgress = true;
            Time.timeScale = 1f;

            if (GameSessionManager.Instance != null)
                GameSessionManager.Instance.ReturnToLobby();

            var relay = LobbyRelayService.Instance;
            if (relay != null)
                relay.StartCoroutine(ExitRoutine(relay));
            else
                FinishExit();
        }

        private static IEnumerator ExitRoutine(LobbyRelayService relay)
        {
            var leaveTask = relay.LeaveSessionAsync();
            while (!leaveTask.IsCompleted)
                yield return null;

            FinishExit();
        }

        private static void FinishExit()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            exitInProgress = false;
            SceneManager.LoadScene(CharacterSelectScene);
        }
    }
}
