using UnityEngine;

namespace ProjectM.Core
{
    /// <summary>
    /// Phase 1 임시 디버그 UI. 게임 세션 흐름을 화면 버튼으로 직접 호출하여 검증한다.
    /// MVP 검증 완료 후 제거 예정.
    /// </summary>
    public class GameDebugUI : MonoBehaviour
    {
        [SerializeField] private GameSessionManager session;

        private string log = "";

        private void Awake()
        {
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
        }

        private void OnEnable()
        {
            if (session == null) return;
            session.OnMatchStarted += HandleMatchStarted;
            session.OnWaveStarted += HandleWaveStarted;
            session.OnWaveEnded += HandleWaveEnded;
            session.OnMatchEnded += HandleMatchEnded;
        }

        private void OnDisable()
        {
            if (session == null) return;
            session.OnMatchStarted -= HandleMatchStarted;
            session.OnWaveStarted -= HandleWaveStarted;
            session.OnWaveEnded -= HandleWaveEnded;
            session.OnMatchEnded -= HandleMatchEnded;
        }

        private void HandleMatchStarted()         => AddLog("Match Started");
        private void HandleWaveStarted(int wave)  => AddLog($"Wave {wave} Started");
        private void HandleWaveEnded(int wave)    => AddLog($"Wave {wave} Ended");
        private void HandleMatchEnded(bool c)     => AddLog($"Match Ended (cleared={c})");

        private void AddLog(string msg)
        {
            log = $"[{Time.time:F1}s] {msg}\n" + log;
            if (log.Length > 1000) log = log.Substring(0, 1000);
        }

        private void OnGUI()
        {
            if (session == null)
            {
                GUI.Label(new Rect(10, 10, 400, 30), "GameSessionManager not found in scene.");
                return;
            }

            GUI.skin.label.fontSize = 16;
            GUI.skin.button.fontSize = 14;
            GUI.skin.box.fontSize = 14;

            var s = session.State;
            GUILayout.BeginArea(new Rect(10, 10, 360, Screen.height - 20), GUI.skin.box);

            GUILayout.Label("=== Project M / Phase 1 Debug ===");
            GUILayout.Label($"Phase  : {s.CurrentPhase}");
            GUILayout.Label($"Wave   : {s.CurrentWave} / {s.MaxWave}");
            GUILayout.Label($"Cleared: {s.IsCleared}   Failed: {s.IsFailed}");

            GUILayout.Space(8);
            if (GUILayout.Button("StartMatch  (Lobby → Preparation)"))   session.StartMatch();
            if (GUILayout.Button("StartWave   (Preparation → Wave)"))    session.StartWave();
            if (GUILayout.Button("EndWave     (Wave → Next/Result)"))    session.EndWave();
            if (GUILayout.Button("EndMatch (Force Clear)"))              session.EndMatch(true);
            if (GUILayout.Button("EndMatch (Force Fail)"))               session.EndMatch(false);
            if (GUILayout.Button("ReturnToLobby"))                       session.ReturnToLobby();

            GUILayout.Space(8);
            GUILayout.Label("--- Event Log ---");
            GUILayout.TextArea(log, GUILayout.ExpandHeight(true));

            GUILayout.EndArea();
        }
    }
}
