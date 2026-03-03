using UnityEngine;

public static class WindowsDisplayStability
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Prefer exclusive fullscreen for stable presentation on systems that flicker in borderless mode.
        Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;

        // Keep presentation synchronized to avoid compositor/tearing artifacts.
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = -1;
#endif
    }
}
