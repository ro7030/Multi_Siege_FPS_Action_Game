using UnityEngine;
using ProjectM.Player;

namespace ProjectM.UI
{
    /// <summary>
    /// 팀원 체력바. Canvas 에 직접 배치한 행(Row)들을 연결해서 사용한다.
    ///
    /// 사용법
    ///   1) Canvas 에 행을 원하는 위치/디자인으로 직접 만든다 (이름 Text + 체력바 Image)
    ///      각 행 오브젝트에 TeammateHealthRow 를 붙이고 슬롯을 연결
    ///   2) 이 컴포넌트의 rows 배열에 그 행들을 드래그
    ///
    /// 동작
    ///   - 팀원이 없으면 해당 행은 비활성화
    ///   - 팀원이 있으면 활성화 + 이름 표시 + 체력바 갱신
    /// </summary>
    public class TeammateHealthView : MonoBehaviour
    {
        [Header("직접 배치한 행 (위치/디자인 자유)")]
        [SerializeField] private TeammateHealthRow[] rows;

        [Header("탐색")]
        [SerializeField] private PlayerController localPlayer; // 제외 대상 (자동 탐색)
        [SerializeField] private float rescanInterval = 1.5f;

        [Header("체력바 색상 (이미지로 대체 시 끄기)")]
        [Tooltip("켜면 HP 비율에 따라 색을 바꿈. 분절 이미지 바를 쓰면 끄세요.")]
        [SerializeField] private bool tintByRatio = false;
        [SerializeField] private Color highColor = new(0.4f, 0.85f, 0.4f);
        [SerializeField] private Color midColor = new(1f, 0.7f, 0.2f);
        [SerializeField] private Color lowColor = new(0.9f, 0.3f, 0.3f);
        [SerializeField] private float midThreshold = 0.6f;
        [SerializeField] private float lowThreshold = 0.3f;

        // 각 행에 바인딩된 팀원 (rows 와 같은 인덱스)
        private HealthSystem[] boundHealth;
        private ReviveSystem[] boundRevive;
        private float rescanTimer;

        private void Awake()
        {
            if (localPlayer == null)
            {
                foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                    if (pc.IsLocalPlayer) { localPlayer = pc; break; }
            }

            int n = rows != null ? rows.Length : 0;
            boundHealth = new HealthSystem[n];
            boundRevive = new ReviveSystem[n];
        }

        private void Start()
        {
            // 시작 시 전부 비활성화
            if (rows != null)
                foreach (var r in rows)
                    if (r != null) r.gameObject.SetActive(false);

            Rescan();
        }

        private void Update()
        {
            rescanTimer += Time.deltaTime;
            if (rescanTimer >= rescanInterval) { rescanTimer = 0f; Rescan(); }

            if (rows == null) return;

            // 활성 행 체력바 갱신
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null || !row.gameObject.activeSelf) continue;
                var hs = boundHealth[i];
                if (hs == null) continue;

                float ratio = hs.HpRatio;
                row.SetFill(ratio);
                if (tintByRatio) row.SetFillColor(RatioColor(ratio));
                row.SetStatus(GetStatus(hs, boundRevive[i]));
            }
        }

        // ─────────────────────────────────────────────────────────────
        private void Rescan()
        {
            if (rows == null) return;

            // 팀원 수집 (PlayerController 가 있는 HealthSystem 중 로컬 제외)
            var teammates = new System.Collections.Generic.List<HealthSystem>();
            foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (pc == localPlayer || pc.IsLocalPlayer) continue;
                var hs = pc.GetComponent<HealthSystem>();
                if (hs != null) teammates.Add(hs);
            }

            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null) continue;

                if (i < teammates.Count)
                {
                    var hs = teammates[i];
                    boundHealth[i] = hs;
                    boundRevive[i] = hs.GetComponent<ReviveSystem>();
                    row.SetName(hs.gameObject.name);   // 이름으로 변경
                    row.gameObject.SetActive(true);    // 활성화
                }
                else
                {
                    boundHealth[i] = null;
                    boundRevive[i] = null;
                    row.gameObject.SetActive(false);   // 팀원 없으면 비활성화
                }
            }
        }

        private TeammateStatus GetStatus(HealthSystem hs, ReviveSystem revive)
        {
            if (revive != null)
            {
                if (revive.IsDead) return TeammateStatus.Dead;
                if (revive.IsDown) return TeammateStatus.Down;
                return TeammateStatus.Alive;
            }
            // ReviveSystem 이 없으면 HP 로만 판단
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
