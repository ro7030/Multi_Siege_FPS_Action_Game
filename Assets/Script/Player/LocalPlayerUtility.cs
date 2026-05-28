using UnityEngine;
using ProjectM.Economy;

namespace ProjectM.Player
{
    /// <summary>
    /// 씬에 플레이어/훈련 더미 등 <see cref="HealthSystem"/> 이 여러 개일 때,
    /// <see cref="PlayerController.IsLocalPlayer"/> 가 true 인 오브젝트를 우선한다.
    /// </summary>
    public static class LocalPlayerUtility
    {
        /// <summary>
        /// 로컬 플레이어의 지갑을 반환. 멀티플레이 환경에서 IsLocalPlayer=true 인 Player 의 CurrencyWallet 을 우선.
        /// HUD/상점/보상 UI 가 "내 잔액"을 표시할 때 사용. 팀 전체에 분배할 때는 직접 FindObjectsByType 사용.
        /// </summary>
        public static CurrencyWallet FindLocalCurrencyWallet()
        {
            foreach (var pc in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (!pc.IsLocalPlayer) continue;
                var w = pc.GetComponent<CurrencyWallet>();
                if (w != null) return w;
            }

            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null && tagged.TryGetComponent<CurrencyWallet>(out var byTag))
                return byTag;

            var any = Object.FindAnyObjectByType<CurrencyWallet>();
            if (any != null)
            {
                Debug.LogWarning(
                    "[LocalPlayerUtility] 로컬 PlayerController 의 CurrencyWallet 을 찾지 못해 임의의 지갑을 사용합니다. " +
                    "Player 에 CurrencyWallet 이 붙어 있는지, 또는 HUD/상점에 wallet 슬롯을 직접 연결했는지 확인하세요.");
            }

            return any;
        }

        public static HealthSystem FindLocalHealthSystem()
        {
            foreach (var pc in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (!pc.IsLocalPlayer) continue;
                var h = pc.GetComponent<HealthSystem>();
                if (h != null) return h;
            }

            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null && tagged.TryGetComponent<HealthSystem>(out var byTag))
                return byTag;

            var any = Object.FindAnyObjectByType<HealthSystem>();
            if (any != null)
            {
                Debug.LogWarning(
                    "[LocalPlayerUtility] 로컬 PlayerController 를 찾지 못해 임의의 HealthSystem 을 사용합니다. " +
                    "훈련 더미와 충돌할 수 있으니 HUD·상점 등에 playerHealth 를 직접 연결하는 것을 권장합니다.");
            }

            return any;
        }

        public static WeaponController FindLocalWeaponController()
        {
            foreach (var w in Object.FindObjectsByType<WeaponController>(FindObjectsSortMode.None))
            {
                if (!w.IsLocalPlayer) continue;
                return w;
            }

            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null && tagged.TryGetComponent<WeaponController>(out var wc))
                return wc;

            return Object.FindAnyObjectByType<WeaponController>();
        }
    }
}
