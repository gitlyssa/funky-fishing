using UnityEngine;

public class RhythmWheel3D : MonoBehaviour
{
    [Header("3D Mesh References")]
    [SerializeField] private Transform rotatingWheelMesh; // The actual 3D object to spin
    [SerializeField] private Renderer wheelRenderer;      // To change colors/emissions

    [Header("Rotation Settings")]
    public float maxRotationSpeed = 800f; 
    public float acceleration = 1200f;
    
    [Header("Visual Feedback")]
    public string emissionColorPropertyName = "_EmissionColor"; // Standard Shader name
    [ColorUsage(true, true)] public Color idleColor = Color.black;
    [ColorUsage(true, true)] public Color warmupColor = Color.white;
    [ColorUsage(true, true)] public Color activeColor = new Color(2f, 1.5f, 0f); // High intensity for bloom

    private float _currentRotation;
    private float _currentSpeed;
    private float _targetSpeed;
    private Material _wheelMaterial;

    private void Awake()
    {
        if (wheelRenderer != null)
            _wheelMaterial = wheelRenderer.material;
    }

    private void Update()
    {
        var conductor = RhythmConductor.Instance;
        var reel = (conductor != null) ? conductor.activeReel : null;

        if (reel == null)
        {
            // UpdateIdle();
        }
        else
        {
            UpdateReelLogic(reel);
        }


        _currentSpeed = Mathf.MoveTowards(_currentSpeed, _targetSpeed, acceleration * Time.deltaTime);


        if (rotatingWheelMesh != null && Mathf.Abs(_currentSpeed) > 0.01f)
        {

            float angleThisFrame = _currentSpeed * Time.deltaTime;
            rotatingWheelMesh.Rotate(Vector3.forward, angleThisFrame, Space.Self);
        }
    }

    private void UpdateReelLogic(RhythmReelNote reel)
    {
        float direction = (reel.Data.goalDegrees >= 0) ? 1f : -1f;

        switch (reel.CurrentPhase)
        {
            case ReelPhase.LeadIn:
                float intensity = reel.GetLeadInIntensity();
                SetWheelVisuals(Color.Lerp(idleColor, warmupColor, intensity), intensity * 0.5f);
                _targetSpeed = (maxRotationSpeed * 0.3f * intensity) * direction;
                break;

            case ReelPhase.Active:
                SetWheelVisuals(activeColor, 1.0f);
                _targetSpeed = maxRotationSpeed * direction;
                break;
        }
    }

    private void UpdateIdle()
    {
        _targetSpeed = 0f;
        Color nextColor = Color.Lerp(GetCurrentColor(), idleColor, Time.deltaTime * 8f);
        

        if (Vector4.Distance(nextColor, idleColor) < 0.01f) nextColor = idleColor;
        
        SetWheelVisuals(nextColor, 0f);
    }

    private void SetWheelVisuals(Color color, float glowIntensity)
    {
        if (_wheelMaterial == null) return;
        
        _wheelMaterial.color = color; 
    }

    private Color GetCurrentColor() => _wheelMaterial != null ? _wheelMaterial.GetColor(emissionColorPropertyName) : idleColor;
}