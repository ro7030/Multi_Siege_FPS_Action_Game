using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using ProjectM.Core;
using ProjectM.Network;

namespace ProjectM.UI
{
    /// <summary>
    /// 인게임 스코어보드. Tab 키 길게 누름으로 표시.
    /// NGO: NetworkPlayerRegistry + NetworkMatchStats.
    /// </summary>
    public class ScoreboardView : MonoBehaviour
    {
        private const string DefaultFontResourcePath = "Fonts/Jalnan2/Jalnan2TTF SDF";

        [SerializeField] private GameSessionManager session;
        [SerializeField] private Key holdKey = Key.Tab;

        private GameObject panelGo;
        private TMP_Text bodyText;

        private void Awake()
        {
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
            var uiFont = Resources.Load<TMP_FontAsset>(DefaultFontResourcePath);

            var root = UIRoot.Instance.RootTransform;
            var bg = UIRoot.CreatePanel("Scoreboard", root, new Color(0, 0, 0, 0.72f));
            panelGo = bg.gameObject;
            var rt = bg.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(920, 480);

            var header = UIRoot.CreateText("Header", rt, 32, TextAnchor.UpperCenter);
            if (uiFont != null) header.font = uiFont;
            header.fontStyle = FontStyles.Bold;
            header.color = new Color(1, 0.9f, 0.4f);
            header.text = "scoreboard";
            var hrt = header.rectTransform;
            hrt.anchorMin = new Vector2(0, 1); hrt.anchorMax = new Vector2(1, 1);
            hrt.offsetMin = new Vector2(20, -70); hrt.offsetMax = new Vector2(-20, -10);

            bodyText = UIRoot.CreateText("Body", rt, 20, TextAnchor.UpperLeft);
            if (uiFont != null) bodyText.font = uiFont;
            var brt = bodyText.rectTransform;
            brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 1);
            brt.offsetMin = new Vector2(40, 30); brt.offsetMax = new Vector2(-40, -80);
        }

        private void RefreshBody()
        {
            var sb = new System.Text.StringBuilder();
            if (session != null)
            {
                string phase = session.State.CurrentPhase.ToString().ToLowerInvariant();
                sb.AppendLine($"phase: {phase}    wave: {session.State.CurrentWave}/{session.State.MaxWave}\n");
            }

            sb.AppendLine($"{"nickname",-16} {"kills",5} {"damage",8} {"harvest",7} {"revive",6} {"score",6}");
            sb.AppendLine(new string('-', 62));

            var netStats = NetworkMatchStats.Instance;
            var players = NetworkPlayerRegistry.All;

            if (NetworkSessionHelper.IsMultiplayerSession && players.Count > 0)
            {
                foreach (var player in players)
                {
                    if (player == null) continue;
                    AppendPlayerRow(sb, player.DisplayName, player.OwnerClientId, netStats);
                }
            }
            else if (netStats != null)
            {
                for (int i = 0; i < netStats.Count; i++)
                    AppendEntryRow(sb, netStats.GetEntryAt(i));
            }
            else
            {
                sb.AppendLine("(통계 없음 — 솔로 플레이)");
            }

            bodyText.text = sb.ToString();
        }

        private static void AppendPlayerRow(System.Text.StringBuilder sb, string nickname, ulong clientId, NetworkMatchStats netStats)
        {
            if (netStats != null && netStats.TryGetStat(clientId, out var entry))
            {
                AppendEntryRow(sb, entry);
                return;
            }

            sb.AppendLine($"{nickname,-16} {0,5} {0f,8:F0} {0,7} {0,6} {0,6}");
        }

        private static void AppendEntryRow(System.Text.StringBuilder sb, MatchStatEntry entry)
        {
            string name = entry.Nickname.IsEmpty ? $"player{entry.ClientId}" : entry.Nickname.ToString();
            sb.AppendLine($"{name,-16} {entry.Kills,5} {entry.DamageDealt,8:F0} {entry.HarvestCount,7} {entry.ReviveCount,6} {entry.Score,6}");
        }
    }
}
