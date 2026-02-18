using UnityEngine;

[DefaultExecutionOrder(-50)] // Ensure this runs before Visualizers
public class RhythmInputInitializer : MonoBehaviour
{
    [Header("Core System")]
    public RhythmInputProcessorT processor;

    void Awake()
    {
        IRhythmInputT provider = GetComponent<IRhythmInputT>();

        if (provider == null)
        {
            provider = Object.FindAnyObjectByType<MonoBehaviour>() as IRhythmInputT;
        }

        // 3. Connect them
        if (provider != null && processor != null)
        {
            processor.Initialize(provider);
            Debug.Log($"<color=cyan>[Initializer] Success! Linked {provider.GetType().Name} to {processor.name}</color>");
        }
        else
        {
            if (provider == null) Debug.LogError("[Initializer] Failed: No IRhythmInputT found in scene!");
            if (processor == null) Debug.LogError("[Initializer] Failed: Processor reference is missing!");
        }
    }
}