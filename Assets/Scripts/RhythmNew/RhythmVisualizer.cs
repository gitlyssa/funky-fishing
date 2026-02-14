using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class RhythmVisualizer : MonoBehaviour
{
    [Header("References")]
    public RhythmInputProcessorT processor; // Listens for events
    private IRhythmInputT provider;  

    [Header("Visual Settings")]
    public Color idleColor = Color.gray;
    public Color holdColor = Color.yellow;
    public Color flickColor = Color.green;
    public float flickFlashDuration = 0.15f;

    [Header("UI Elements")]
    public Image reelCenter;
    public List<Image> directionImages; // Order: R, UR, U, UL, L, DL, D, DR

    private Dictionary<FlickDirection, Image> _dirMap;
    private Dictionary<FlickDirection, float> _flickTimers = new Dictionary<FlickDirection, float>();

    void Start()
    {   
        // get provider component from processor's gameobject if active, otherwise keep looking
        provider = processor.GetComponent<IRhythmInputT>();
        if (provider == null)        {
            provider = FindObjectOfType<KeyboardRhythmProvider>();
            if (provider == null)
                provider = FindObjectOfType<GamepadRhythmProvider>();
        }
        if (provider == null)
        {
            Debug.LogError("[Visualizer] No rhythm input provider found!");
            return;
        }
        // Map the Enum to our UI Images
        _dirMap = new Dictionary<FlickDirection, Image>
        {
            { FlickDirection.Right,     directionImages[0] },
            { FlickDirection.UpRight,   directionImages[1] },
            { FlickDirection.Up,        directionImages[2] },
            { FlickDirection.UpLeft,    directionImages[3] },
            { FlickDirection.Left,      directionImages[4] },
            { FlickDirection.DownLeft,  directionImages[5] },
            { FlickDirection.Down,      directionImages[6] },
            { FlickDirection.DownRight, directionImages[7] }
        };

        // Subscribe to the Processor's flick event
        processor.OnValidFlick += HandleFlick;
    }

    private void HandleFlick(FlickDirection dir)
    {
        // Set the timer to "flash" the color
        _flickTimers[dir] = Time.time + flickFlashDuration;
        Debug.Log($"[Visualizer] Flick Flash for {dir} started at {Time.time}");
    }

    void Update()
    {
        UpdateDirectionVisuals();
        UpdateReelVisuals();
    }

    private void UpdateDirectionVisuals()
    {
        foreach (var pair in _dirMap)
        {
            FlickDirection dir = pair.Key;
            Image img = pair.Value;

            // Priority 1: Flick Flash (Impulse)
            if (_flickTimers.ContainsKey(dir) && Time.time < _flickTimers[dir])
            {
                img.color = flickColor;
                img.transform.localScale = Vector3.one * 1.3f; // Visual punch
            }
            // Priority 2: Hold State
            else if (provider.IsHoldingDirection(dir))
            {
                img.color = holdColor;
                img.transform.localScale = Vector3.one * 1.1f;
            }
            // Priority 3: Idle
            else
            {
                img.color = idleColor;
                img.transform.localScale = Vector3.one;
            }
        }
    }

    private void UpdateReelVisuals()
    {
        Vector2 reelDir = provider.GetReelStickDirection();
    
        if (reelDir.magnitude > 0.1f)
        {
            float angle = Mathf.Atan2(reelDir.y, reelDir.x) * Mathf.Rad2Deg;
            reelCenter.transform.localRotation = Quaternion.Euler(0, 0, angle - 90f); 
            reelCenter.color = holdColor;
        }
        else
        {
            reelCenter.color = idleColor;
        }
    }
    
    private void OnDestroy()
    {
        if (processor != null) processor.OnValidFlick -= HandleFlick;
    }
}