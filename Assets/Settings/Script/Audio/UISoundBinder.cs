using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectM.Audio
{
    // 씬의 Button에 기본 클릭 효과음 컴포넌트를 자동 부착한다.
    public class UISoundBinder : MonoBehaviour
    {
        private static bool sceneHookRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            RegisterSceneHook();
            RefreshActiveScene();
        }

        public static void EnsureOn(GameSoundManager manager)
        {
            if (manager == null)
                return;

            RegisterSceneHook();
            if (manager.GetComponent<UISoundBinder>() == null)
                manager.gameObject.AddComponent<UISoundBinder>();
        }

        private static void RegisterSceneHook()
        {
            if (sceneHookRegistered)
                return;

            sceneHookRegistered = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshActiveScene();
        }

        public static void RefreshActiveScene()
        {
            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var btn in buttons)
            {
                if (btn == null || btn.GetComponent<UIButtonSfx>() != null)
                    continue;

                UIButtonSfx.Ensure(btn, UiSoundKind.Default);
            }
        }
    }
}
