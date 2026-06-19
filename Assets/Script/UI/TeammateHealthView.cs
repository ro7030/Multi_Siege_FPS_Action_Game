using System;
using UnityEngine;
using ProjectM.Network;
using ProjectM.Player;

namespace ProjectM.UI
{
    /// <summary>
    /// 팀원 체력바. Canvas 에 직접 배치한 행(Row)들을 연결해서 사용한다.
    /// </summary>
    public class TeammateHealthView : MonoBehaviour
    {
        [Header("직접 배치한 행 (위치/디자인 자유)")]
        [SerializeField] private TeammateHealthRow[] rows;

        [Header("탐색")]
        [SerializeField] private PlayerController localPlayer;
        [SerializeField] private float rescanInterval = 0.5f;

        [Header("체력바 색상 (이미지로 대체 시 끄기)")]
        [Tooltip("켜면 HP 비율에 따라 색을 바꿈. 분절 이미지 바를 쓰면 끄세요.")]
        [SerializeField] private bool tintByRatio = false;
        [SerializeField] private Color highColor = new(0.4f, 0.85f, 0.4f);
        [SerializeField] private Color midColor = new(1f, 0.7f, 0.2f);
        [SerializeField] private Color lowColor = new(0.9f, 0.3f, 0.3f);
        [SerializeField] private float midThreshold = 0.6f;
        [SerializeField] private float lowThreshold = 0.3f;

        private HealthSystem[] boundHealth;
        private ReviveSystem[] boundRevive;
        private NetworkPlayer[] boundNetworkPlayers;
        private string[] boundDisplayNames;
        private Action<float, float>[] hpHandlersByRow;
        private Action[] reviveHandlersByRow;
        private float rescanTimer;

        private void Awake()
        {
            int n = rows != null ? rows.Length : 0;
            boundHealth = new HealthSystem[n];
            boundRevive = new ReviveSystem[n];
            boundNetworkPlayers = new NetworkPlayer[n];
            boundDisplayNames = new string[n];
            hpHandlersByRow = new Action<float, float>[n];
            reviveHandlersByRow = new Action[n];
        }

        private void Start()
        {
            if (rows != null)
                foreach (var r in rows)
                    if (r != null) r.gameObject.SetActive(false);

            Rescan();
        }

        private void OnDisable() => UnbindAll();

        private void Update()
        {
            rescanTimer += Time.deltaTime;
            if (rescanTimer >= rescanInterval)
            {
                rescanTimer = 0f;
                Rescan();
            }

            RefreshActiveRows();
        }

        private void RefreshActiveRows()
        {
            if (rows == null) return;

            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null || !row.gameObject.activeSelf) continue;
                var hs = boundHealth[i];
                if (hs == null) continue;

                string displayName = ResolveTeammateDisplayName(i, hs);
                if (boundDisplayNames[i] != displayName)
                {
                    boundDisplayNames[i] = displayName;
                    row.SetName(displayName);
                }

                float ratio = hs.HpRatio;
                row.SetFill(ratio);
                if (tintByRatio) row.SetFillColor(RatioColor(ratio));
                row.SetStatus(GetStatus(hs, boundRevive[i]));
            }
        }

        private void Rescan()
        {
            if (rows == null) return;

            if (localPlayer == null)
                localPlayer = LocalPlayerUtility.FindLocalComponent<PlayerController>();

            var teammates = CollectTeammates();
            bool bindingChanged = false;

            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null) continue;

                HealthSystem nextHealth = i < teammates.Count ? teammates[i] : null;
                if (boundHealth[i] != nextHealth)
                    bindingChanged = true;
            }

            if (bindingChanged)
                UnbindAll();

            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null) continue;

                if (i < teammates.Count)
                {
                    var hs = teammates[i];
                    boundHealth[i] = hs;
                    boundRevive[i] = hs.GetComponent<ReviveSystem>();
                    boundNetworkPlayers[i] = hs.GetComponent<NetworkPlayer>();
                    boundDisplayNames[i] = null;
                    row.SetName(ResolveTeammateDisplayName(i, hs));
                    row.gameObject.SetActive(true);
                    BindRow(i);
                }
                else
                {
                    boundHealth[i] = null;
                    boundRevive[i] = null;
                    boundNetworkPlayers[i] = null;
                    boundDisplayNames[i] = null;
                    row.gameObject.SetActive(false);
                }
            }

            RefreshActiveRows();
        }

        private System.Collections.Generic.List<HealthSystem> CollectTeammates()
        {
            var teammates = new System.Collections.Generic.List<HealthSystem>();
            var local = NetworkPlayerRegistry.LocalPlayer;

            foreach (var player in NetworkPlayerRegistry.All)
            {
                if (player == null || player == local) continue;
                if (!player.TryGetComponent(out HealthSystem hs)) continue;
                teammates.Add(hs);
            }

            if (teammates.Count == 0)
            {
                foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                {
                    if (pc == null || pc.IsLocalPlayer || pc == localPlayer) continue;
                    var hs = pc.GetComponent<HealthSystem>();
                    if (hs != null) teammates.Add(hs);
                }
            }

            return teammates;
        }

        private string ResolveTeammateDisplayName(int index, HealthSystem hs)
        {
            var netPlayer = boundNetworkPlayers != null && index < boundNetworkPlayers.Length
                ? boundNetworkPlayers[index]
                : null;
            if (netPlayer == null && hs != null)
                hs.TryGetComponent(out netPlayer);

            if (netPlayer != null)
                return PlayerDisplayNameUtility.GetDisplayName(netPlayer);

            return PlayerDisplayNameUtility.GetDisplayName(hs);
        }

        private void BindRow(int index)
        {
            UnbindRow(index);

            var hs = boundHealth[index];
            if (hs == null) return;

            hpHandlersByRow[index] = (_, __) => RefreshActiveRows();
            hs.OnHpChanged += hpHandlersByRow[index];

            var revive = boundRevive[index];
            if (revive != null)
            {
                reviveHandlersByRow[index] = RefreshActiveRows;
                revive.OnDowned += reviveHandlersByRow[index];
                revive.OnRevived += reviveHandlersByRow[index];
                revive.OnFullDeath += reviveHandlersByRow[index];
            }
        }

        private void UnbindRow(int index)
        {
            if (rows == null || index < 0 || index >= rows.Length) return;

            var hs = boundHealth != null && index < boundHealth.Length ? boundHealth[index] : null;
            if (hs != null && hpHandlersByRow[index] != null)
                hs.OnHpChanged -= hpHandlersByRow[index];
            hpHandlersByRow[index] = null;

            var revive = boundRevive != null && index < boundRevive.Length ? boundRevive[index] : null;
            if (revive != null && reviveHandlersByRow[index] != null)
            {
                revive.OnDowned -= reviveHandlersByRow[index];
                revive.OnRevived -= reviveHandlersByRow[index];
                revive.OnFullDeath -= reviveHandlersByRow[index];
            }
            reviveHandlersByRow[index] = null;
        }

        private void UnbindAll()
        {
            if (rows == null) return;
            for (int i = 0; i < rows.Length; i++)
                UnbindRow(i);
        }

        private TeammateStatus GetStatus(HealthSystem hs, ReviveSystem revive)
        {
            if (revive != null)
            {
                if (revive.IsDead) return TeammateStatus.Dead;
                if (revive.IsDown) return TeammateStatus.Down;
                return TeammateStatus.Alive;
            }

            return (hs != null && hs.IsAlive) ? TeammateStatus.Alive : TeammateStatus.Dead;
        }

        private Color RatioColor(float ratio)
        {
            if (ratio <= lowThreshold) return lowColor;
            if (ratio <= midThreshold) return midColor;
            return highColor;
        }
    }
}
