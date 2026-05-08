using UnityEngine;
using UnityEngine.Rendering;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CursorCastTargeting : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public LayerMask waterMask;
    public Transform castMarker;
    public Collider waterCollider;
    public BobberArcCaster bobberArcCaster;

    [Header("Joy-Con Input Source")]
    public JslStickInput jslInput;

    [Header("External Stick Input")]
    public float externalStickTimeout = 0.2f;

    [Header("Joy-Con Cursor (stick-driven)")]
    public float cursorSpeed = 1200f;     // pixels/sec at full stick
    public float worldMoveSpeed = 6f;     // units/sec on the water plane

    [Header("Raycast")]
    public float maxDistance = 100f;
    [Header("Startup")]
    public bool startAtPondCenter = true;
    public float markerSurfaceOffset = 0.02f;

    [Header("Cast Marker Visual")]
    [Min(1f)] public float markerSizeMultiplier = 1.35f;
    [Min(0f)] public float markerPulseScale = 0.42f;
    [Min(0f)] public float markerPulseSpeed = 0.42f;
    public Color markerHighlightColor = new Color(0.82f, 0.08f, 0.08f, 1f);
    public Color markerFlashColor = new Color(1f, 0.1f, 0.1f, 1f);
    [Min(0f)] public float markerGlowMinIntensity = 0.02f;
    [Min(0f)] public float markerGlowMaxIntensity = 0.18f;

    public Vector3 CurrentTargetPoint { get; private set; }
    public bool HasTarget { get; private set; }

    // Screen-space cursor in pixels
    public Vector2 CursorPixel { get; private set; }

    private bool _warnedMissingJsl;
    private BobberArcCaster.State _lastCasterState = BobberArcCaster.State.Idle;
    private Vector3 _lastTargetPoint;
    private bool _hasLastTarget;
    private Vector2 _externalStick;
    private float _externalStickExpiresAt = -1f;
    private Transform _configuredMarker;
    private Transform _markerVisualTransform;
    private Vector3 _markerVisualBaseLocalScale = Vector3.one;
    private Material[] _markerVisualMaterials = new Material[0];

    void Reset()
    {
        cam = Camera.main;
    }

    void Start()
    {
        if (!cam) cam = Camera.main;
        if (bobberArcCaster == null)
            bobberArcCaster = FindObjectOfType<BobberArcCaster>();

        EnsureMarkerVisual();

        // start centered
        CursorPixel = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        if (startAtPondCenter)
        {
            InitializeTargetOnWater();
        }
        else if (cam != null)
        {
            UpdateMarkerFromCursor();
            if (!HasTarget)
                InitializeTargetOnWater();
        }
    }

    void Update()
    {
        if (!cam) { HasTarget = false; return; }

        if (bobberArcCaster != null && bobberArcCaster.CurrentState != BobberArcCaster.State.Idle)
        {
            SetCastMarkerVisible(false);
            if (HasTarget)
            {
                _lastTargetPoint = CurrentTargetPoint;
                _hasLastTarget = true;
            }
            _lastCasterState = bobberArcCaster.CurrentState;
            return;
        }
        else if (bobberArcCaster != null && _lastCasterState != BobberArcCaster.State.Idle)
        {
            _lastCasterState = BobberArcCaster.State.Idle;
            if (_hasLastTarget)
            {
                CurrentTargetPoint = _lastTargetPoint;
                HasTarget = true;
                SetCastMarkerPosition(CurrentTargetPoint);
            }
            else
            {
                InitializeTargetOnWater();
            }
        }

        bool mouseActive = UpdateCursorPixel();

        Vector2 stick = Vector2.zero;
        bool stickActive = TryGetStick(out stick);

        // Only apply the input source that's actively being used.
        // This prevents snapping back to the mouse cursor when the stick returns to zero.
        if (mouseActive)
        {
            UpdateMarkerFromCursor();
        }
        else if (stickActive)
        {
            UpdateTargetFromStick(stick);
        }

        UpdateMarkerVisual();
    }

    bool UpdateCursorPixel()
    {
#if ENABLE_INPUT_SYSTEM
        bool rightHeld = false;
        bool rightDown = false;
        if (Mouse.current != null)
        {
            rightHeld = Mouse.current.rightButton.isPressed;
            rightDown = Mouse.current.rightButton.wasPressedThisFrame;
            if (rightHeld || rightDown)
                CursorPixel = Mouse.current.position.ReadValue();
        }
#else
        bool rightHeld = Input.GetMouseButton(1);
        bool rightDown = Input.GetMouseButtonDown(1);
        if (rightHeld || rightDown)
            CursorPixel = Input.mousePosition;
#endif

        // Clamp cursor to the screen bounds (CursorPixel is a property, so assign a new Vector2)
        CursorPixel = new Vector2(
            Mathf.Clamp(CursorPixel.x, 0, Screen.width),
            Mathf.Clamp(CursorPixel.y, 0, Screen.height)
        );

        return rightHeld || rightDown;
    }

    void UpdateMarkerFromCursor()
    {
        Ray ray = cam.ScreenPointToRay(CursorPixel);

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, waterMask))
        {
            HasTarget = true;
            CurrentTargetPoint = hit.point;
            SetCastMarkerPosition(hit.point);
        }
        else
        {
            if (TryClampToWaterEdge(ray, out Vector3 clampedPoint))
            {
                HasTarget = true;
                CurrentTargetPoint = clampedPoint;
                SetCastMarkerPosition(clampedPoint);
            }
            else
            {
                HasTarget = false;
                SetCastMarkerVisible(false);
            }
        }
    }

    bool TryClampToWaterEdge(Ray ray, out Vector3 clampedPoint)
    {
        clampedPoint = default;
        if (waterCollider == null) return false;

        var plane = new Plane(waterCollider.transform.up, waterCollider.bounds.center);
        if (!plane.Raycast(ray, out float enter)) return false;

        Vector3 hitPoint = ray.GetPoint(enter);
        Bounds b = waterCollider.bounds;
        clampedPoint = new Vector3(
            Mathf.Clamp(hitPoint.x, b.min.x, b.max.x),
            Mathf.Clamp(hitPoint.y, b.min.y, b.max.y),
            Mathf.Clamp(hitPoint.z, b.min.z, b.max.z)
        );
        return true;
    }

    bool TryGetStick(out Vector2 stick)
    {
        stick = Vector2.zero;

        // Optional external stick feed (e.g., Xbox right stick) can drive targeting.
        if (Time.unscaledTime <= _externalStickExpiresAt)
        {
            stick = _externalStick;
            if (stick != Vector2.zero)
                return true;
        }

        if (jslInput == null)
        {
            jslInput = FindObjectOfType<JslStickInput>();
            if (jslInput == null)
            {
                if (!_warnedMissingJsl)
                {
                    Debug.LogWarning("CursorCastTargeting: No JslStickInput found in scene.");
                    _warnedMissingJsl = true;
                }
                return false;
            }
        }

        if (!jslInput.Connected) return false;

        stick = jslInput.Stick;
        return stick != Vector2.zero;
    }

    public void SetExternalStickInput(Vector2 stick)
    {
        _externalStick = stick;
        _externalStickExpiresAt = Time.unscaledTime + Mathf.Max(0.02f, externalStickTimeout);
    }

    void UpdateCursorFromStick(Vector2 stick)
    {
        CursorPixel += stick * cursorSpeed * Time.deltaTime;
    }

    void UpdateTargetFromStick(Vector2 stick)
    {
        if (waterCollider == null)
        {
            UpdateCursorFromStick(stick);
            UpdateMarkerFromCursor();
            return;
        }

        if (!HasTarget)
        {
            Ray centerRay = cam.ScreenPointToRay(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            if (Physics.Raycast(centerRay, out RaycastHit hit, maxDistance, waterMask))
            {
                HasTarget = true;
                CurrentTargetPoint = hit.point;
            }
            else if (TryClampToWaterEdge(centerRay, out Vector3 clampedPoint))
            {
                HasTarget = true;
                CurrentTargetPoint = clampedPoint;
            }
            else
            {
                InitializeTargetOnWater();
            }
        }

        if (!HasTarget) return;

        Vector3 planeNormal = waterCollider.transform.up;
        Vector3 planeRight = Vector3.ProjectOnPlane(cam.transform.right, planeNormal).normalized;
        Vector3 planeForward = Vector3.ProjectOnPlane(cam.transform.forward, planeNormal).normalized;

        Vector3 delta = (planeRight * stick.x + planeForward * stick.y) * worldMoveSpeed * Time.deltaTime;
        Vector3 newPoint = CurrentTargetPoint + delta;

        Bounds b = waterCollider.bounds;
        newPoint = new Vector3(
            Mathf.Clamp(newPoint.x, b.min.x, b.max.x),
            Mathf.Clamp(newPoint.y, b.min.y, b.max.y),
            Mathf.Clamp(newPoint.z, b.min.z, b.max.z)
        );

        CurrentTargetPoint = newPoint;
        SetCastMarkerPosition(newPoint);
    }

    void InitializeTargetOnWater()
    {
        if (!TryGetInitialTargetPoint(out Vector3 initialTarget))
            return;

        CurrentTargetPoint = initialTarget;
        HasTarget = true;
        _lastTargetPoint = CurrentTargetPoint;
        _hasLastTarget = true;
        SetCastMarkerPosition(CurrentTargetPoint);

        if (cam != null)
        {
            Vector3 projected = cam.WorldToScreenPoint(CurrentTargetPoint);
            CursorPixel = new Vector2(
                Mathf.Clamp(projected.x, 0f, Screen.width),
                Mathf.Clamp(projected.y, 0f, Screen.height));
        }
    }

    bool TryGetInitialTargetPoint(out Vector3 point)
    {
        if (TryGetPondCenterPoint(out point))
            return true;

        if (waterCollider != null)
        {
            point = waterCollider.bounds.center;
            return true;
        }

        point = default;
        return false;
    }

    bool TryGetPondCenterPoint(out Vector3 point)
    {
        PondManager pond = null;
        if (bobberArcCaster != null)
            pond = bobberArcCaster.pondManager;

        if (pond == null)
            pond = FindObjectOfType<PondManager>();

        if (pond != null)
        {
            point = pond.transform.position;

            // Prefer actual water surface Y so the marker sits on visible water.
            if (waterCollider != null)
                point.y = waterCollider.bounds.center.y;
            else
                point.y = pond.waterlevel;

            return true;
        }

        point = default;
        return false;
    }

    // Optional: simple on-screen dot showing where the virtual cursor is
    // void OnGUI()
    // {
    //     const float size = 8f;
    //     GUI.Box(
    //         new Rect(CursorPixel.x - size * 0.5f,
    //                  (Screen.height - CursorPixel.y) - size * 0.5f,
    //                  size, size),
    //         ""
    //     );
    // }

    void SetCastMarkerPosition(Vector3 worldPoint)
    {
        if (!castMarker)
            return;

        EnsureMarkerVisual();
        SetCastMarkerVisible(true);
        castMarker.position = worldPoint + Vector3.up * markerSurfaceOffset;
        UpdateMarkerVisual();
    }

    void SetCastMarkerVisible(bool visible)
    {
        if (!castMarker)
            return;

        EnsureMarkerVisual();
        if (castMarker.gameObject.activeSelf != visible)
            castMarker.gameObject.SetActive(visible);
    }

    void EnsureMarkerVisual()
    {
        if (!castMarker)
            return;

        if (_configuredMarker == castMarker && _markerVisualTransform != null)
            return;

        _configuredMarker = castMarker;
        _markerVisualTransform = castMarker;
        _markerVisualBaseLocalScale = castMarker.localScale * markerSizeMultiplier;
        _markerVisualMaterials = new Material[0];

        MeshFilter sourceFilter = castMarker.GetComponent<MeshFilter>();
        MeshRenderer sourceRenderer = castMarker.GetComponent<MeshRenderer>();
        if (sourceFilter != null && sourceRenderer != null)
        {
            const string runtimeVisualName = "CastMarkerVisualRuntime";
            Transform visualTransform = castMarker.Find(runtimeVisualName);
            if (visualTransform == null)
            {
                GameObject visual = new GameObject(runtimeVisualName, typeof(MeshFilter), typeof(MeshRenderer));
                visualTransform = visual.transform;
                visualTransform.SetParent(castMarker, false);
            }

            visualTransform.localPosition = Vector3.zero;
            visualTransform.localRotation = Quaternion.identity;
            visualTransform.localScale = Vector3.one * markerSizeMultiplier;

            MeshFilter visualFilter = visualTransform.GetComponent<MeshFilter>();
            visualFilter.sharedMesh = sourceFilter.sharedMesh;

            MeshRenderer visualRenderer = visualTransform.GetComponent<MeshRenderer>();
            visualRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
            visualRenderer.shadowCastingMode = ShadowCastingMode.Off;
            visualRenderer.receiveShadows = false;
            visualRenderer.lightProbeUsage = sourceRenderer.lightProbeUsage;
            visualRenderer.reflectionProbeUsage = sourceRenderer.reflectionProbeUsage;
            visualRenderer.renderingLayerMask = sourceRenderer.renderingLayerMask;
            visualRenderer.allowOcclusionWhenDynamic = sourceRenderer.allowOcclusionWhenDynamic;

            sourceRenderer.enabled = false;

            _markerVisualTransform = visualTransform;
            _markerVisualBaseLocalScale = Vector3.one * markerSizeMultiplier;
            _markerVisualMaterials = visualRenderer.materials;
            return;
        }

        Renderer fallbackRenderer = castMarker.GetComponentInChildren<Renderer>(true);
        if (fallbackRenderer != null)
        {
            _markerVisualTransform = fallbackRenderer.transform;
            _markerVisualBaseLocalScale = fallbackRenderer.transform.localScale * markerSizeMultiplier;
            _markerVisualMaterials = fallbackRenderer.materials;
        }
    }

    void UpdateMarkerVisual()
    {
        if (!castMarker)
            return;

        EnsureMarkerVisual();

        if (_markerVisualTransform == null || !castMarker.gameObject.activeInHierarchy)
            return;

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * markerPulseSpeed * Mathf.PI * 2f);
        float pulseScale = 1f + markerPulseScale * pulse;
        _markerVisualTransform.localScale = _markerVisualBaseLocalScale * pulseScale;

        if (_markerVisualMaterials == null || _markerVisualMaterials.Length == 0)
            return;

        Color litColor = Color.Lerp(markerHighlightColor * 0.92f, markerHighlightColor, pulse);
        litColor.a = 1f;
        Color emissionColor = markerFlashColor * Mathf.Lerp(markerGlowMinIntensity, markerGlowMaxIntensity, pulse);
        emissionColor.a = 1f;

        for (int i = 0; i < _markerVisualMaterials.Length; i++)
        {
            Material material = _markerVisualMaterials[i];
            if (material == null)
                continue;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", litColor);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", litColor);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emissionColor);
            }
        }
    }
}
