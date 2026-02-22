using UnityEngine;
using System.Linq;


[DefaultExecutionOrder(-50)] // Ensure this runs before Visualizers
public class RhythmInputInitializer : MonoBehaviour
{
    /*
    This just links the provider to the processor
    */
    [Header("Core System")]
    public RhythmInputProcessorT processor;

    void Awake()
    {
        if (processor == null) return;

        var providers = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                              .OfType<IRhythmInputT>();

        int count = 0;
        foreach (var provider in providers)
        {
            processor.Initialize(provider);
            count++;
        }

        if (count > 0)
            Debug.Log($"<color=cyan>[Initializer] Connected {count} input sources!</color>");
        else
            Debug.LogError("[Initializer] No IRhythmInputT sources found!");
    }
}