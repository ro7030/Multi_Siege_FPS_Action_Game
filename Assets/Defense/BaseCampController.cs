using UnityEngine;
using ProjectM.Core;

namespace ProjectM.Defense
{
    /// <summary>
    /// 베이스 캠프. HP가 0이 되면 즉시 매치 패배 처리.
    /// GameSessionManager.EndMatch(false)를 호출한다.
    /// </summary>
    [RequireComponent(typeof(DefenseObject))]
    public class BaseCampController : MonoBehaviour
    {
        [SerializeField] private GameSessionManager session;
        [SerializeField] private bool endMatchOnDestroy = true;

        private DefenseObject defense;

        private void Awake()
        {
            defense = GetComponent<DefenseObject>();
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
        }

        private void OnEnable() => defense.OnDestroyed += HandleDestroyed;
        private void OnDisable() => defense.OnDestroyed -= HandleDestroyed;

        private void HandleDestroyed(DefenseObject _)
        {
            Debug.Log("[BaseCamp] 베이스 캠프 파괴 — 매치 패배");
            if (!endMatchOnDestroy || session == null) return;
            if (session.State.CurrentPhase == GamePhase.Wave ||
                session.State.CurrentPhase == GamePhase.Preparation ||
                session.State.CurrentPhase == GamePhase.WaveCleared)
            {
                session.EndMatch(false);
            }
        }
    }
}
