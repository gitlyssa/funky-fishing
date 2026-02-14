using UnityEngine;

public class RhythmArcNote : MonoBehaviour
{
    public enum NoteType { Flick, Slide }

    [Header("Note Data")]
    public NoteType type;
    public FlickDirection direction;
    public float targetHitTime;
    public float travelDuration; // How long it takes to go from center to ring

    [Header("Visuals")]
    public AnimationCurve scaleCurve; // Growth from center to ring
    public float outerRingRadius = 500f; // UI pixels or world units

    private float _spawnTime;
    private bool _isInitialized = false;
    private bool _wasHit = false;

    public void Initialize(float hitTime, NoteType noteType, FlickDirection dir, float duration)
    {
        targetHitTime = hitTime;
        type = noteType;
        direction = dir;
        travelDuration = duration;
        
        _spawnTime = targetHitTime - travelDuration;
        
        transform.localRotation = Quaternion.Euler(0, 0, GetRotationFromDirection(dir));
        
        _isInitialized = true;
    }

    void Update()
    {
        if (!_isInitialized || _wasHit) return;

        float currentTime = Time.time; // This should eventually be synced to your Song Manager
        float t = (currentTime - _spawnTime) / travelDuration;

        // 1. Position & Scaling
        // t = 0 (Spawn/Center), t = 1 (Hit Line/Ring)
        UpdatePositionAndScale(t);

        // 2. Logic: Automatic Miss
        // If the note passes the line by more than 150ms, it's a miss
        if (t > 1.0f && (currentTime - targetHitTime) > 0.15f)
        {
            OnMiss();
        }
    }

    private void UpdatePositionAndScale(float t)
    {
        // Calculate radius: move outwards from center
        float currentRadius = t * outerRingRadius;
        
        // Use the rotation of the note to determine its 2D vector
        // We use transform.up because we rotated the note to face its lane in Initialize
        transform.localPosition = transform.up * currentRadius;

        // Apply scale curve (makes notes "approach" the player)
        float s = scaleCurve.Evaluate(t);
        transform.localScale = new Vector3(s, s, 1);
    }

    public void OnHit()
    {
        _wasHit = true;
        // Trigger VFX, animations, etc.
        Destroy(gameObject); 
    }

    private void OnMiss()
    {
        Debug.Log($"Missed {type} at {direction}");
        Destroy(gameObject);
    }

    private float GetRotationFromDirection(FlickDirection dir)
    {
        return dir switch
        {
            FlickDirection.Right => -90f,
            FlickDirection.Up => 0f,
            FlickDirection.Left => 90f,
            FlickDirection.Down => 180f,
            // Add diagonals if you end up using them
            _ => 0f
        };
    }
}