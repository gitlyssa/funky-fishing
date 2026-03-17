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
        [SerializeField] private DynamicArc _borderVisuals;
        [SerializeField] private MeshRenderer _renderer;
        [SerializeField] private MeshRenderer _borderRenderer;
        [SerializeField] private Material flickMaterial;
        [SerializeField] private Material slideMaterial;
        [SerializeField] private Material goldenMaterial;

        [Header("Directional Colors")]
    [SerializeField] private Material LeftMat;
    [SerializeField] private Material RightMat;
    [SerializeField] private Material UpMat;

    [Header("Border Config")]
    [SerializeField] private float borderPadding = 0.3f; // How much thicker the border is
    [SerializeField] private float borderAnglePadding = 5f; // How much wider the border arc is
    [SerializeField] private Color perfectBorderColor = new Color(1f, 0.85f, 0f); // Gold
    [SerializeField] private Color goodBorderColor = Color.white;
    [SerializeField] private Color defaultBorderColor = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Dim gray

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
            if (data.isGolden)
            {
                _renderer.material = goldenMaterial;
            }
            else
            {
                _renderer.sharedMaterial = direction switch {
                    FlickDirection.Left  => sapphireLeftMat,
                    FlickDirection.Right => rubyRightMat,
                    FlickDirection.Up    => emeraldUpMat,
                    _ => flickMaterial
                };
            }
        }

        _visuals = GetComponent<DynamicArc>();
        _visuals.Setup(meshSegments);
        _visuals.SetMaterial(_renderer.material);

        if (_borderVisuals != null)
        {
        _borderVisuals.Setup(meshSegments);

        }

    _isInitialized = true;

         _isInitialized = true;
    }

    void Update()
    {
        if (!_isInitialized) return;

        // Use the Conductor's song time so they are all synced up
        float elapsed = RhythmConductor.Instance.songTime - _spawnTime;
        float linearT = Mathf.Clamp01(elapsed / _travelDuration);

        float curvedT = _scaleCurve.Evaluate(linearT);

        UpdateBorderState(RhythmConductor.Instance.songTime);
        UpdatePositionAndScale(curvedT);
        
        
    }

    private void UpdatePositionAndScale(float t)
    {
        
        float currentRadius = Mathf.Lerp(_spawnRadius, _outerRingRadius, t);
        _visuals.Redraw(currentRadius, noteThickness, laneAngle, meshSegments);
        if (_borderVisuals != null)
        {
            _borderVisuals.Redraw(currentRadius, noteThickness + borderPadding, laneAngle + borderAnglePadding, meshSegments);
        }
    }

    private void UpdateBorderState(float songTime)
    {
        if (_borderRenderer == null || RhythmJudge.Instance == null) return;

        float absDiff = Mathf.Abs(targetHitTime - songTime);

        // Change border color based on global timing windows
        if (absDiff <= RhythmJudge.Instance.PerfectWindow)
        {
            _borderRenderer.material.color = perfectBorderColor;
        }
        else if (absDiff <= RhythmJudge.Instance.GoodWindow)
        {
            _borderRenderer.material.color = goodBorderColor;
        }
        else
        {
    
            float t = Mathf.InverseLerp(RhythmJudge.Instance.GoodWindow, RhythmJudge.Instance.PerfectWindow, absDiff);
            _borderRenderer.material.color = Color.Lerp(defaultBorderColor, goodBorderColor, t);
        }
    }

    public void OnPerfectHit()
    {
        // PLAY HIT ANIMATIONS AND SOUNDS HERE
        FunkyAudioSettings.PlayOneShot(perfectHitSoundEvent, transform.position, FunkyAudioCategory.Sfx);
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
        
        FunkyAudioSettings.PlayOneShot(goodHitSoundEvent, transform.position, FunkyAudioCategory.Sfx);
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
        FunkyAudioSettings.PlayOneShot(missSoundEvent, transform.position, FunkyAudioCategory.Sfx);
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
