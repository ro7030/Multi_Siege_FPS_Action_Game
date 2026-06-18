using System.Collections.Generic;
using UnityEngine;
using ProjectM.Core;
using ProjectM.Network;

namespace ProjectM.Player
{
    /// <summary>
    /// 모든 플레이어가 행동 불능(다운 또는 사망) 상태가 되면 매치 패배 처리.
    /// 솔로 플레이의 경우 부활 가능한 동료가 없으므로 다운 = 즉시 패배.
    /// 멀티 플레이는 누군가 한 명이라도 살아있으면 게임 진행 유지.
    /// </summary>
    public class PlayerWipeoutDetector : MonoBehaviour
    {
        [Header("연결 (비우면 자동 탐색)")]
        [SerializeField] private GameSessionManager session;

        [Header("플레이어 탐색")]
        [Tooltip("매치 시작 시 씬의 모든 ReviveSystem 을 자동 등록")]
        [SerializeField] private bool autoRegisterOnMatchStart = true;
        [Tooltip("일정 주기로 새로 들어온 플레이어 재탐색 (멀티 조인용)")]
        [SerializeField] private bool autoRescanPeriodically = true;
        [SerializeField] private float rescanInterval = 2f;

        [Header("동작")]
        [Tooltip("켜면 다운 상태도 행동 불능으로 간주 (솔로 즉시 패배 핵심)\n끄면 완전 사망(IsDead) 만 카운트")]
        [SerializeField] private bool countDownAsIncapacitated = true;

        private readonly HashSet<ReviveSystem> tracked = new HashSet<ReviveSystem>();
        private float rescanTimer;
        private bool ended;

        private void Awake()
        {
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
        }

        private void OnEnable()
        {
            if (session != null)
            {
                session.OnMatchStarted += HandleMatchStarted;
                session.OnMatchEnded   += HandleMatchEnded;
            }
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.OnMatchStarted -= HandleMatchStarted;
                session.OnMatchEnded   -= HandleMatchEnded;
            }
            UnregisterAll();
        }

        private void Start()
        {
            if (autoRegisterOnMatchStart) Rescan();
        }

        private void HandleMatchStarted()
        {
            ended = false;
            Rescan();
        }

        private void HandleMatchEnded(bool _)
        {
            ended = true;
        }

        private void Update()
        {
            if (!autoRescanPeriodically) return;
            rescanTimer -= Time.deltaTime;
            if (rescanTimer <= 0f)
            {
                rescanTimer = rescanInterval;
                Rescan();
            }
        }

        private void Rescan()
        {
            var systems = FindObjectsByType<ReviveSystem>(FindObjectsSortMode.None);
            foreach (var rs in systems)
            {
                if (rs == null) continue;
                if (tracked.Add(rs))
                {
                    rs.OnDowned    += CheckWipeout;
                    rs.OnFullDeath += CheckWipeout;
                }
            }
        }

        private void UnregisterAll()
        {
            foreach (var rs in tracked)
            {
                if (rs == null) continue;
                rs.OnDowned    -= CheckWipeout;
                rs.OnFullDeath -= CheckWipeout;
            }
            tracked.Clear();
        }

        private void CheckWipeout()
        {
            if (ended) return;
            if (NetworkSessionHelper.IsMultiplayerSession && !NetworkSessionHelper.IsServer) return;
            if (session == null) return;
            if (tracked.Count == 0) return; // 플레이어를 한 번도 못 찾았으면 무시

            // 정상 진행 중인 페이즈에서만 패배 처리
            var phase = session.State.CurrentPhase;
            if (phase != GamePhase.Wave &&
                phase != GamePhase.Preparation &&
                phase != GamePhase.WaveCleared)
                return;

            int aliveCount = 0;
            foreach (var rs in tracked)
            {
                if (rs == null) continue;
                bool incapacitated = rs.IsDead || (countDownAsIncapacitated && rs.IsDown);
                if (!incapacitated) aliveCount++;
            }

            if (aliveCount > 0) return;

            Debug.Log($"[Wipeout] 모든 플레이어 행동 불능 ({tracked.Count}명) — 매치 패배");
            ended = true;
            session.EndMatch(false);
        }
    }
}
