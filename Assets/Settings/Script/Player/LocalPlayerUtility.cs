using UnityEngine;
using ProjectM.Economy;
using ProjectM.Network;

namespace ProjectM.Player
{
    // 씬에 <see cref="HealthSystem"/> 이 여러 개일 때,
    // <see cref="PlayerController.IsLocalPlayer"/> 가 true 인 오브젝트를 우선한다.
    public static class LocalPlayerUtility
    {
        public static GameObject FindLocalPlayerObject()
        {
            var netLocal = NetworkPlayerRegistry.LocalPlayer;
            if (netLocal != null) return netLocal.gameObject;

            foreach (var pc in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (pc.IsLocalPlayer) return pc.gameObject;
            }

            return GameObject.FindGameObjectWithTag("Player");
        }

        public static T FindLocalComponent<T>() where T : Component
        {
            var go = FindLocalPlayerObject();
            if (go != null && go.TryGetComponent(out T component))
                return component;

            foreach (var pc in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (!pc.IsLocalPlayer) continue;
                if (pc.TryGetComponent(out T fromPlayer)) return fromPlayer;
            }

            return null;
        }

        // 로컬 플레이어의 지갑을 반환. 멀티플레이 환경에서 IsLocalPlayer=true 인 Player 의 CurrencyWallet 을 우선.
        // HUD/상점/보상 UI 가 "내 잔액"을 표시할 때 사용. 팀 전체에 분배할 때는 직접 FindObjectsByType 사용.
        public static CurrencyWallet FindLocalCurrencyWallet()
        {
            var wallet = FindLocalComponent<CurrencyWallet>();
            if (wallet != null) return wallet;

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
            var health = FindLocalComponent<HealthSystem>();
            if (health != null) return health;

            var any = Object.FindAnyObjectByType<HealthSystem>();
            if (any != null)
            {
                Debug.LogWarning(
                    "[LocalPlayerUtility] 로컬 PlayerController 를 찾지 못해 임의의 HealthSystem 을 사용합니다. " +
                    "HUD·상점 등에 playerHealth 를 직접 연결하는 것을 권장합니다.");
            }

            return any;
        }

        public static WeaponController FindLocalWeaponController()
        {
            var weapon = FindLocalComponent<WeaponController>();
            if (weapon != null) return weapon;

            foreach (var w in Object.FindObjectsByType<WeaponController>(FindObjectsSortMode.None))
            {
                if (w.IsLocalPlayer) return w;
            }

            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null && tagged.TryGetComponent(out WeaponController wc))
                return wc;

            return null;
        }
    }
}
