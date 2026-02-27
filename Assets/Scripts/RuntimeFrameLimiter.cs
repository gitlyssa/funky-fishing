using UnityEngine;

public static class RuntimeFrameLimiter
{
    // Lightweight global cap to reduce frame-time spikes from unbounded rendering.
    private const bool enforceFrameCap = true;
    private const int targetFrameRate = 120;
    private const bool disableVsyncWhenCapped = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        if (!enforceFrameCap)
            return;

        if (disableVsyncWhenCapped)
            QualitySettings.vSyncCount = 0;

        Application.targetFrameRate = Mathf.Max(30, targetFrameRate);
    }
}
