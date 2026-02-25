using UnityEngine;
using FMODUnity;
public class RhythmArcNote : MonoBehaviour
{
    public enum NoteType { Flick, Slide }
    /*
    An arc note moves down the lane and is either a flick or a slide. But since theyre animated the same they both use
    this same script.

    The visuals determine how thick or wide each note is. Each note has a material or texture that can be applied.
    The logic is contained in RhythmJudge and the spawning is done through Rhythm Conductor
    This script mostly updates the visuals and handles any animations when the note is hit or missed
    */
    [Header("State (Debug Visible)")]
    [SerializeField] private NoteType type;
    [SerializeField] private FlickDirection direction;
    [SerializeField] private float targetHitTime;

    public NoteType Type => type;
    public FlickDirection Direction => direction;
    public float TargetHitTime => targetHitTime;

    [Header("Visual Config")]
    public float noteThickness = 0.2f;
    public float laneAngle = 90f;
    public int meshSegments = 16; 

    [Header("Effects")]
    [SerializeField] private EventReference perfectHitSoundEvent;
    [SerializeField] private EventReference goodHitSoundEvent;
    [SerializeField] private EventReference missSoundEvent;

    [Header("Particle Effects")]
    [SerializeField] private GameObject perfectHitParticleEffect;
    [SerializeField] private GameObject goodHitParticleEffect;
    [SerializeField] private GameObject missParticleEffect;

    private float _travelDuration;
    private float _spawnTime;
    private bool _isInitialized = false;

    private float _spawnRadius;
    private float _outerRingRadius;
     private AnimationCurve _scaleCurve;
    
    [Header("Visuals")]
        [SerializeField] private DynamicArc _visuals;
        [SerializeField] private MeshRenderer _renderer;
        [SerializeField] private Material flickMaterial;
        [SerializeField] private Material slideMaterial;

    public void Initialize(NoteData data, float duration, float sRadius, float oRadius, AnimationCurve sCurve)
    {
        targetHitTime = data.hitTime;
        type = data.type;
        direction = data.direction;
        _travelDuration = duration;
        
        // Configuration from Conductor
        _spawnRadius = sRadius;
        _outerRingRadius = oRadius;
        _scaleCurve = sCurve;
        
        _spawnTime = targetHitTime - _travelDuration;
        
        // Orientation
        transform.localRotation = Quaternion.Euler(0, 0, GetRotationFromDirection(direction));
        
        // Appearance
        _renderer = GetComponent<MeshRenderer>();
        if (_renderer != null)
        {
            _renderer.sharedMaterial = (type == NoteType.Flick) ? flickMaterial : slideMaterial;
        }

        _visuals = GetComponent<DynamicArc>();
        _visuals.Setup(meshSegments);
        _visuals.SetMaterial(_renderer.sharedMaterial);

         _isInitialized = true;
    }

    void Update()
    {
        if (!_isInitialized) return;

        // Use the Conductor's song time so they are all synced up
        float elapsed = RhythmConductor.Instance.songTime - _spawnTime;
        float linearT = Mathf.Clamp01(elapsed / _travelDuration);

        float curvedT = _scaleCurve.Evaluate(linearT);
        
        UpdatePositionAndScale(curvedT);
    }

    private void UpdatePositionAndScale(float t)
    {
        
        float currentRadius = Mathf.Lerp(_spawnRadius, _outerRingRadius, t);
        _visuals.Redraw(currentRadius, noteThickness, laneAngle, meshSegments);
    }

    public void OnPerfectHit()
    {
        // PLAY HIT ANIMATIONS AND SOUNDS HERE
        RuntimeManager.PlayOneShot(perfectHitSoundEvent, transform.position);
        if (perfectHitParticleEffect != null)        {
            Vector3 effectPosition = transform.position + (Vector3)(GetDirectionVector(direction) * _outerRingRadius);
            GameObject effect = Instantiate(perfectHitParticleEffect, effectPosition, Quaternion.identity);
            effect.transform.SetParent(transform.parent);
            effect.layer = gameObject.layer;
        }
        Destroy(gameObject); 
        
    }

    public void OnGoodHit()
    {
        // PLAY GOOD HIT ANIMATIONS AND SOUNDS HERE
        
        RuntimeManager.PlayOneShot(goodHitSoundEvent, transform.position);
        if (goodHitParticleEffect != null)        {
            Vector3 effectPosition = transform.position + (Vector3)(GetDirectionVector(direction) * _outerRingRadius);
            GameObject effect = Instantiate(goodHitParticleEffect, effectPosition, Quaternion.identity);
            effect.transform.SetParent(transform.parent);
            effect.layer = gameObject.layer;
        }
        Destroy(gameObject);
    }

    public void OnMiss()
    {
        // PLAY MISS ANIMATIONS AND SOUNDS HERE
        RuntimeManager.PlayOneShot(missSoundEvent, transform.position);
        if (missParticleEffect != null)        {
            Vector3 effectPosition = transform.position + (Vector3)(GetDirectionVector(direction) * _outerRingRadius);
            GameObject effect = Instantiate(missParticleEffect, effectPosition, Quaternion.identity);
            effect.transform.SetParent(transform.parent);
            effect.layer = gameObject.layer;
        }
        Destroy(gameObject);
        
    }

    private float GetRotationFromDirection(FlickDirection dir) => dir switch {
        FlickDirection.Right => -90f,
        FlickDirection.Up => 0f,
        FlickDirection.Left => 90f,
        FlickDirection.Down => 180f,
        _ => 0f
    };

    private Vector2 GetDirectionVector(FlickDirection dir) => dir switch {
        FlickDirection.Right => Vector2.right,
        FlickDirection.Up => Vector2.up,
        FlickDirection.Left => Vector2.left,
        FlickDirection.Down => Vector2.down,
        _ => Vector2.zero
    };
}