using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ProjectM.Core;
using ProjectM.Network;

namespace ProjectM.UI
{
    /// <summary>
    /// 인게임 스코어보드. Tab 키 길게 누름으로 표시.
    /// (상점이 H 키를 사용하므로 스코어보드는 Tab 으로 이동. 커서 토글은 PlayerController 에서 F1 로 변경됨)
    /// 현재는 RoomManager의 플레이어 목록만 표시. Phase 8에서 PlayerStatsTracker 데이터와 결합.
    /// </summary>
    public class ScoreboardView : MonoBehaviour
    {
        [SerializeField] private RoomManager room;
        [SerializeField] private GameSessionManager session;
        [SerializeField] private Key holdKey = Key.Tab;

        private GameObject panelGo;
        private Text bodyText;

        private void Awake()
        {
            if (room == null) room = FindAnyObjectByType<RoomManager>();
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
        }

        private void Start()
        {
            if (UIRoot.Instance == null) { enabled = false; return; }
            BuildPanel();
            panelGo.SetActive(false);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            bool show = kb[holdKey].isPressed;
            if (panelGo.activeSelf != show) panelGo.SetActive(show);
            if (show) RefreshBody();
        }

        private void BuildPanel()
        {
            var root = UIRoot.Instance.RootTransform;
            var bg = UIRoot.CreatePanel("Scoreboard", root, new Color(0, 0, 0, 0.72f));
            panelGo = bg.gameObject;
            var rt = bg.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(720, 480);

            var header = UIRoot.CreateText("Header", rt, 32, TextAnchor.UpperCenter);
            header.fontStyle = FontStyle.Bold;
            header.color = new Color(1, 0.9f, 0.4f);
            header.text = "SCOREBOARD";
            var hrt = header.rectTransform;
            hrt.anchorMin = new Vector2(0, 1); hrt.anchorMax = new Vector2(1, 1);
            hrt.offsetMin = new Vector2(20, -70); hrt.offsetMax = new Vector2(-20, -10);

            bodyText = UIRoot.CreateText("Body", rt, 22, TextAnchor.UpperLeft);
            var brt = bodyText.rectTransform;
            brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 1);
            brt.offsetMin = new Vector2(40, 30); brt.offsetMax = new Vector2(-40, -80);
        }

        private void RefreshBody()
        {
            var sb = new System.Text.StringBuilder();
            if (session != null)
                sb.AppendLine($"Phase: {session.State.CurrentPhase}    Wave: {session.State.CurrentWave}/{session.State.MaxWave}\n");

            if (room != null && room.IsInRoom && room.Players.Count > 0)
            {
                sb.AppendLine($"{"Player",-20} {"Ready",-8} {"Host",-6}");
                sb.AppendLine(new string('-', 50));
                foreach (var kv in room.Players)
                {
                    var p = kv.Value;
                    sb.AppendLine($"{p.nickname,-20} {(p.isReady ? "✓" : " "),-8} {(p.isHost ? "★" : " "),-6}");
                }
            }
            else
            {
                sb.AppendLine("(로컬 솔로 플레이 — 룸 정보 없음)");
            }

            bodyText.text = sb.ToString();
        }
    }
}
