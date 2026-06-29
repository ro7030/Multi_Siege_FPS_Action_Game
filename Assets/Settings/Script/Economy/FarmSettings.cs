using UnityEngine;

namespace ProjectM.Economy
{
    /// <summary>
    /// 밭 시스템 전역 설정. 웨이브 구간 × 인원수 별 수확량과 한도를 정의.
    /// 기획서 10-2 밭 수익 데이터 기준.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectM/Economy/FarmSettings", fileName = "FarmSettings")]
    public class FarmSettings : ScriptableObject
    {
        [Header("배치 제한")]
        [Tooltip("동시에 설치 가능한 밭 최대 개수")]
        public int maxActiveFarms = 4;

        [Tooltip("설치 후 수확 가능까지 걸리는 웨이브 수 (기획서: 1웨이브 성장)")]
        public int wavesToGrow = 1;

        [Header("밭 기본 스탯")]
        public float maxHp = 100f;

        [Header("수확량 — 1인당 지급액 (밭 1개당)")]
        [Tooltip("초반 (Wave 1~3)")]
        public YieldByPlayers earlyYield  = new YieldByPlayers { solo = 25, duo = 25, trio = 20, quad = 20 };

        [Tooltip("중반 (Wave 4~6)")]
        public YieldByPlayers midYield    = new YieldByPlayers { solo = 45, duo = 40, trio = 40, quad = 35 };

        [Tooltip("후반 (Wave 7~10)")]
        public YieldByPlayers lateYield   = new YieldByPlayers { solo = 70, duo = 65, trio = 60, quad = 55 };

        [Header("웨이브 구간")]
        public int earlyEndWave = 3;   // 1~3: early
        public int midEndWave   = 6;   // 4~6: mid

        /// <summary>현재 웨이브와 인원수로 1인당 지급액 계산.</summary>
        public int GetYieldPerPlayer(int waveNumber, int playerCount)
        {
            YieldByPlayers band;
            if (waveNumber <= earlyEndWave)      band = earlyYield;
            else if (waveNumber <= midEndWave)   band = midYield;
            else                                  band = lateYield;

            return band.For(playerCount);
        }
    }

    [System.Serializable]
    public struct YieldByPlayers
    {
        public int solo;
        public int duo;
        public int trio;
        public int quad;

        public int For(int playerCount)
        {
            return playerCount switch
            {
                <= 1 => solo,
                2    => duo,
                3    => trio,
                _    => quad
            };
        }
    }
}
