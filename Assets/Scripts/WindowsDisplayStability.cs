using UnityEngine;

public static class WindowsDisplayStability
{
    private const string FullscreenPrefKey = "FunkyFishing.Options.FullscreenEnabled";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        bool fullscreenEnabled = PlayerPrefs.GetInt(FullscreenPrefKey, 1) != 0;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (fullscreenEnabled)
        {
            // Prefer exclusive fullscreen for stable presentation on systems that flicker in borderless mode.
            Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
            Screen.fullScreen = true;
        }
        else
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.fullScreen = false;
        }

        // Keep presentation synchronized to avoid compositor/tearing artifacts.
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = -1;
#else
        Screen.fullScreenMode = fullscreenEnabled ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        Screen.fullScreen = fullscreenEnabled;
#endif
    }
}
