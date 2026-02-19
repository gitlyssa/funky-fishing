using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class BobberArcCaster : MonoBehaviour
{
    public PondManager pondManager;

    [Header("References")]
    public Transform rodTip;
    public Transform bobber;

    // Where the bobber rests when idle (hangs from the tip)
    public Transform bobberHangPoint;

    // We will cast to this (your CastMarker transform)
    public Transform targetMarker;

    [Header("Cast Target Offset")]
    public float castTargetYOffset = 0f;

    [Header("Cast Arc")]
    public float castDuration = 0.75f;
    public float arcHeight = 3.0f; // extra height above straight line
    public AnimationCurve arcEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Yank / Retract")]
    public float yankDuration = 0.25f;
    public AnimationCurve yankEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Tension Rod Feedback")]
    [FormerlySerializedAs("tensionRodFeedbackEnabled")]
    public bool tensionBobbingEnabled = true;
    public Transform rodRoot;                  // defaults to rodTip.parent if empty
    public Camera tensionCamera;               // defaults to Camera.main if empty
    public bool centerRodWithCamera = true;
    public float rodCenterSpeed = 8f;
    public float cameraCenterDepth = -1f;      // <= 0 keeps current camera depth
    public Vector3 cameraCenterOffset = new Vector3(0.22f, -0.08f, -0.18f);
    [Range(-80f, 80f)] public float tensionYawAngle = 0f;
    [Range(-80f, 80f)] public float tensionPitchDownAngle = -18f;
    public float rodBendAngle = 5f;
    public float rodBendSpeed = 7f;
    public float rodBendNoiseAngle = 1.5f;
    public float rodBendResponsiveness = 16f;

    [Header("Directional Swing (Tension)")]
    public bool directionalSwingEnabled = true;
    public Transform rodSwingPivot;            // set near reel; used only for directional swing pivoting
    public KeyCode swingUpKey = KeyCode.W;
    public KeyCode swingLeftKey = KeyCode.A;
    public KeyCode swingRightKey = KeyCode.D;
    [Range(-120f, 120f)] public float swingUpPitchAngle = -36f;
    [Range(-120f, 120f)] public float swingLeftYawAngle = -28f;
    [Range(-120f, 120f)] public float swingRightYawAngle = 28f;
    public bool useCameraRelativeSwingAxes = true;
    public float directionalSwingInSpeed = 20f;
    public float directionalSwingOutSpeed = 14f;
    public float directionalTapThreshold = 0.12f;
    public float directionalTapDuration = 0.16f;
    public AnimationCurve directionalTapCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 4f),
        new Keyframe(0.5f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, -4f, 0f));

    public enum State { Idle, InFlight, Landed, Tension, Retracting }
    public State CurrentState { get; private set; } = State.Idle;

    private enum SwingDirection { None, Up, Left, Right }

    private Coroutine _moveRoutine;
    private Coroutine _preYankRoutine;
    private bool _isPreparingYank;
    private bool _wasInTension;
    private bool _isRestoringFromTension;
    private float _currentBendAngle;
    private Vector3 _rodBasePos;
    private Quaternion _rodBaseRot;
    private Quaternion _rodCamRotOffset;
    private float _rodCameraDepth;
    private float _tensionSeedA;
    private float _tensionSeedB;
    private float _tensionPoseBlend;
    private Vector3 _swingPivotLocalOffsetFromRoot;
    private SwingDirection _activeSwingDirection = SwingDirection.None;
    private SwingDirection _lastSwingPoseDirection = SwingDirection.None;
    private SwingDirection _lastPressedSwingDirection = SwingDirection.None;
    private SwingDirection _tapSwingDirection = SwingDirection.None;
    private float _tapSwingStartTime = -1f;
    private float _swingStrength;
    private float _upKeyDownTime = -1f;
    private float _leftKeyDownTime = -1f;
    private float _rightKeyDownTime = -1f;
    private bool _useExternalDirectionalInput;
    private bool _externalUpHeld;
    private bool _externalLeftHeld;
    private bool _externalRightHeld;
    private bool _upHeldLastFrame;
    private bool _leftHeldLastFrame;
    private bool _rightHeldLastFrame;
    private bool _hasStableRodBasePose;
    private Vector3 _stableRodBasePos;
    private Quaternion _stableRodBaseRot;
    private Vector3 _stableSwingPivotLocalOffsetFromRoot;

    void Start()
    {
        // Start bobber at hang point (preferred), otherwise at rod tip
        if (bobber != null)
        {
            if (bobberHangPoint != null) bobber.position = bobberHangPoint.position;
            else if (rodTip != null) bobber.position = rodTip.position;
        }

        if (rodRoot == null && rodTip != null)
            rodRoot = rodTip.parent;

        if (tensionCamera == null)
            tensionCamera = Camera.main;

        CacheStableRodBasePose();
    }

    // Call this from your JoyCon gesture event
    public void Cast()
    {
        if (!rodTip || !bobber || !targetMarker) return;

        // Only allow a fresh cast from idle/hanging.
        if (CurrentState != State.Idle) return;

        Vector3 from = bobber.position;            // launch from current (hanging) position
        Vector3 to = targetMarker.position + Vector3.up * castTargetYOffset;

        StartArcMove(from, to, castDuration, arcHeight, arcEase);
        CurrentState = State.InFlight;
    }

    // Call this from your JoyCon gesture event
    public void Yank()
    {
        if (!rodTip || !bobber) return;

        // Guard: don't yank if already idle/hanging
        if (CurrentState == State.Idle || _isPreparingYank) return;

        // If rod is displaced by tension feedback, let it settle before retracting bobber.
        if (CurrentState == State.Tension || _isRestoringFromTension)
        {
            StartYankAfterRodRestore();
            return;
        }

        if (CurrentState == State.Landed && pondManager != null && pondManager.playerBobber != null)
        {
            GameObject fish = pondManager.GetClosestFish(pondManager.playerBobber);
            if (fish != null)
            {
                Debug.Log("Fish hooked! Entering tension state.");
                ToggleTension(); // enter tension state
                return;
            }
        }

        Debug.Log("No fish nearby. Normal yank.");
        StartYank();
    }

    // Call this when a fish is hooked (or for now, a test key)
    public void ToggleTension()
    {
        if (CurrentState == State.Tension)
        {
            CurrentState = State.Landed;
            return;
        }

        if (CurrentState == State.Landed)
        {
            CurrentState = State.Tension;
        }
    }

    // Optional external driver (e.g., Joy-Con motion) for W/A/D directional swing.
    public void SetDirectionalSwingHeld(bool upHeld, bool leftHeld, bool rightHeld)
    {
        _useExternalDirectionalInput = true;
        _externalUpHeld = upHeld;
        _externalLeftHeld = leftHeld;
        _externalRightHeld = rightHeld;
    }

    public void ClearDirectionalSwingHeld()
    {
        _useExternalDirectionalInput = false;
        _externalUpHeld = false;
        _externalLeftHeld = false;
        _externalRightHeld = false;
    }

    private void StartYank()
    {
        if (_preYankRoutine != null)
        {
            StopCoroutine(_preYankRoutine);
            _preYankRoutine = null;
        }
        _isPreparingYank = false;

        Vector3 to = bobberHangPoint ? bobberHangPoint.position : rodTip.position;
        StartLinearMove(bobber.position, to, yankDuration, yankEase);
        CurrentState = State.Retracting;
    }

    private void StartYankAfterRodRestore()
    {
        if (_preYankRoutine != null) StopCoroutine(_preYankRoutine);
        _preYankRoutine = StartCoroutine(PrepareRodThenYank());
    }

    private IEnumerator PrepareRodThenYank()
    {
        _isPreparingYank = true;

        // Ensure a tension frame has captured the base transform before we restore.
        if (CurrentState == State.Tension && !_wasInTension)
            BeginTensionFeedback();

        // Exit tension so normal restore logic can run.
        if (CurrentState == State.Tension)
            CurrentState = State.Landed;

        float waitTimeout = Mathf.Max(0.15f, 3f / Mathf.Max(0.01f, rodCenterSpeed));
        float t = 0f;

        while (t < waitTimeout && !IsRodAtBasePose())
        {
            t += Time.deltaTime;
            yield return null;
        }

        _isPreparingYank = false;
        _preYankRoutine = null;

        if (CurrentState != State.Idle)
            StartYank();
    }

    private bool IsRodAtBasePose()
    {
        if (rodRoot == null) return true;

        bool posDone = (rodRoot.position - _rodBasePos).sqrMagnitude <= 0.000004f;
        bool rotDone = Quaternion.Angle(rodRoot.rotation, _rodBaseRot) <= 0.15f;
        bool bendDone = Mathf.Abs(_currentBendAngle) <= 0.05f;
        bool poseDone = Mathf.Abs(_tensionPoseBlend) <= 0.01f;
        bool swingDone = Mathf.Abs(_swingStrength) <= 0.01f;
        return posDone && rotDone && bendDone && poseDone && swingDone && !_isRestoringFromTension;
    }

    private void StartArcMove(Vector3 from, Vector3 to, float duration, float height, AnimationCurve ease)
    {
        if (_moveRoutine != null) StopCoroutine(_moveRoutine);
        _moveRoutine = StartCoroutine(ArcMove(from, to, duration, height, ease));
    }

    private void StartLinearMove(Vector3 from, Vector3 to, float duration, AnimationCurve ease)
    {
        if (_moveRoutine != null) StopCoroutine(_moveRoutine);
        _moveRoutine = StartCoroutine(LinearMove(from, to, duration, ease));
    }

    private IEnumerator ArcMove(Vector3 from, Vector3 to, float duration, float height, AnimationCurve ease)
    {
        duration = Mathf.Max(0.01f, duration);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float u = ease.Evaluate(Mathf.Clamp01(t));

            Vector3 p = Vector3.Lerp(from, to, u);

            // Parabolic "up" offset peaking at u=0.5
            float parabola = 4f * u * (1f - u); // 0..1..0
            p += Vector3.up * (parabola * height);

            bobber.position = p;
            yield return null;
        }

        bobber.position = to;
        CurrentState = State.Landed;
        _moveRoutine = null;
    }

    private IEnumerator LinearMove(Vector3 from, Vector3 to, float duration, AnimationCurve ease)
    {
        duration = Mathf.Max(0.01f, duration);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float u = ease.Evaluate(Mathf.Clamp01(t));
            bobber.position = Vector3.Lerp(from, to, u);
            yield return null;
        }

        bobber.position = to;
        CurrentState = State.Idle;
        _moveRoutine = null;
    }

    void LateUpdate()
    {
        UpdateTensionRodFeedback();
    }

    private void UpdateTensionRodFeedback()
    {
        if (rodTip == null)
            return;

        if (rodRoot == null)
            rodRoot = rodTip.parent;
        if (rodRoot == null)
            return;

        bool inTension = CurrentState == State.Tension;

        if (!inTension && !_wasInTension && !_isRestoringFromTension)
            CacheStableRodBasePose();

        if (inTension && !_wasInTension)
            BeginTensionFeedback();

        if (inTension)
        {
            ApplyTensionRodPose();
        }
        else
        {
            RestoreFromTension();
        }

        _wasInTension = inTension;
    }

    private void BeginTensionFeedback()
    {
        if (rodRoot == null && rodTip != null)
            rodRoot = rodTip.parent;

        if (tensionCamera == null)
            tensionCamera = Camera.main;

        if (!_hasStableRodBasePose)
            CacheStableRodBasePose();

        if (_hasStableRodBasePose)
        {
            _rodBasePos = _stableRodBasePos;
            _rodBaseRot = _stableRodBaseRot;
            _swingPivotLocalOffsetFromRoot = _stableSwingPivotLocalOffsetFromRoot;
        }
        else if (rodRoot != null)
        {
            _rodBasePos = rodRoot.position;
            _rodBaseRot = rodRoot.rotation;
            if (rodSwingPivot != null)
            {
                _swingPivotLocalOffsetFromRoot =
                    Quaternion.Inverse(rodRoot.rotation) * (rodSwingPivot.position - rodRoot.position);
            }
            else
            {
                _swingPivotLocalOffsetFromRoot = Vector3.zero;
            }
        }
        else
        {
            _swingPivotLocalOffsetFromRoot = Vector3.zero;
        }

        if (rodRoot != null && tensionCamera != null)
        {
            Vector3 toRod = rodRoot.position - tensionCamera.transform.position;
            _rodCameraDepth = Vector3.Dot(toRod, tensionCamera.transform.forward);
            if (_rodCameraDepth <= 0.05f)
                _rodCameraDepth = Mathf.Max(0.25f, toRod.magnitude);

            _rodCamRotOffset = Quaternion.Inverse(tensionCamera.transform.rotation) * rodRoot.rotation;
        }

        _currentBendAngle = 0f;
        _tensionPoseBlend = 1f;
        _lastSwingPoseDirection = SwingDirection.None;
        _isRestoringFromTension = false;
        _tensionSeedA = Random.Range(0f, 100f);
        _tensionSeedB = Random.Range(0f, 100f);

        ResetDirectionalSwingState();
    }

    private void ApplyTensionRodPose()
    {
        if (rodRoot == null) return;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        UpdateDirectionalSwingInput();

        float targetBend = 0f;
        bool directionalMotionActive =
            directionalSwingEnabled &&
            (_activeSwingDirection != SwingDirection.None || _swingStrength > 0.001f || IsTapSwingActive());
        if (tensionBobbingEnabled && !directionalMotionActive)
        {
            float t = Time.time * rodBendSpeed;
            float wave = Mathf.Sin(t + _tensionSeedA);
            float noise = (Mathf.PerlinNoise(_tensionSeedB, Time.time * 1.6f) * 2f) - 1f;
            targetBend = (wave * rodBendAngle) + (noise * rodBendNoiseAngle);
        }
        float bendK = 1f - Mathf.Exp(-rodBendResponsiveness * dt);
        _currentBendAngle = Mathf.Lerp(_currentBendAngle, targetBend, bendK);

        bool hasSwingDirection = _activeSwingDirection != SwingDirection.None;
        bool heldSwing = hasSwingDirection && IsSwingDirectionHeld(_activeSwingDirection);
        bool tapSwing = hasSwingDirection && !heldSwing && IsTapSwingActive() && _tapSwingDirection == _activeSwingDirection;

        if (heldSwing)
        {
            float swingK = 1f - Mathf.Exp(-Mathf.Max(0.01f, directionalSwingInSpeed) * dt);
            _swingStrength = Mathf.Lerp(_swingStrength, 1f, swingK);
        }
        else if (tapSwing)
        {
            // Follow a single authored tap curve to avoid a second "settle" pass after release.
            _swingStrength = EvaluateTapSwingStrength();
        }
        else
        {
            float swingK = 1f - Mathf.Exp(-Mathf.Max(0.01f, directionalSwingOutSpeed) * dt);
            _swingStrength = Mathf.Lerp(_swingStrength, 0f, swingK);
            if (_swingStrength <= 0.001f)
            {
                _swingStrength = 0f;
                _lastSwingPoseDirection = SwingDirection.None;
            }
        }
        _tensionPoseBlend = 1f;

        if (_activeSwingDirection != SwingDirection.None)
            _lastSwingPoseDirection = _activeSwingDirection;

        Vector3 desiredPos = _rodBasePos;
        Quaternion desiredBaseRot = _rodBaseRot;

        if (centerRodWithCamera)
        {
            if (tensionCamera == null) tensionCamera = Camera.main;
            if (tensionCamera != null)
            {
                Transform cam = tensionCamera.transform;
                float depth = cameraCenterDepth > 0f ? cameraCenterDepth : Mathf.Max(0.25f, _rodCameraDepth);

                desiredPos =
                    cam.position +
                    (cam.right * cameraCenterOffset.x) +
                    (cam.up * cameraCenterOffset.y) +
                    (cam.forward * (depth + cameraCenterOffset.z));
                desiredBaseRot = cam.rotation * _rodCamRotOffset;
            }
        }

        Quaternion baseWithBend =
            desiredBaseRot *
            Quaternion.AngleAxis(tensionYawAngle * _tensionPoseBlend, Vector3.up) *
            Quaternion.AngleAxis(tensionPitchDownAngle * _tensionPoseBlend, Vector3.right) *
            Quaternion.AngleAxis(_currentBendAngle, Vector3.right);

        Quaternion desiredRot = baseWithBend;
        SwingDirection swingDirectionForPose = _activeSwingDirection != SwingDirection.None
            ? _activeSwingDirection
            : _lastSwingPoseDirection;
        if (directionalSwingEnabled && swingDirectionForPose != SwingDirection.None && _swingStrength > 0.001f)
        {
            Quaternion swingTargetRot = GetSwingRotationForDirection(swingDirectionForPose, baseWithBend);
            desiredRot = Quaternion.Slerp(baseWithBend, swingTargetRot, _swingStrength);
        }

        bool swingPivotActive =
            directionalSwingEnabled &&
            rodSwingPivot != null &&
            (swingDirectionForPose != SwingDirection.None && (_activeSwingDirection != SwingDirection.None || _swingStrength > 0.001f));

        Vector3 desiredRootPos = desiredPos;
        if (swingPivotActive)
        {
            // Keep the reel/pivot anchored while allowing rod body to rotate around it.
            Vector3 desiredPivotPos = desiredPos + (baseWithBend * _swingPivotLocalOffsetFromRoot);
            desiredRootPos = desiredPivotPos - (desiredRot * _swingPivotLocalOffsetFromRoot);
        }

        float k = 1f - Mathf.Exp(-rodCenterSpeed * dt);
        rodRoot.position = Vector3.Lerp(rodRoot.position, desiredRootPos, k);
        rodRoot.rotation = Quaternion.Slerp(rodRoot.rotation, desiredRot, k);
    }

    private void RestoreFromTension()
    {
        if (rodRoot == null) return;
        if (!_wasInTension && !_isRestoringFromTension) return;

        if (!_isRestoringFromTension)
        {
            _isRestoringFromTension = true;
            ClearDirectionalSwingInputState();
        }

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float bendK = 1f - Mathf.Exp(-rodBendResponsiveness * dt);
        _currentBendAngle = Mathf.Lerp(_currentBendAngle, 0f, bendK);
        float poseK = 1f - Mathf.Exp(-Mathf.Max(0.01f, rodCenterSpeed) * dt);
        _tensionPoseBlend = Mathf.Lerp(_tensionPoseBlend, 0f, poseK);
        float swingK = 1f - Mathf.Exp(-Mathf.Max(0.01f, directionalSwingOutSpeed) * dt);
        _swingStrength = Mathf.Lerp(_swingStrength, 0f, swingK);
        if (_swingStrength <= 0.001f)
        {
            _swingStrength = 0f;
            _lastSwingPoseDirection = SwingDirection.None;
        }

        Quaternion baseWithBend =
            _rodBaseRot *
            Quaternion.AngleAxis(tensionYawAngle * _tensionPoseBlend, Vector3.up) *
            Quaternion.AngleAxis(tensionPitchDownAngle * _tensionPoseBlend, Vector3.right) *
            Quaternion.AngleAxis(_currentBendAngle, Vector3.right);

        Quaternion desiredRot = baseWithBend;
        if (directionalSwingEnabled && _lastSwingPoseDirection != SwingDirection.None && _swingStrength > 0.001f)
        {
            Quaternion swingTargetRot = GetSwingRotationForDirection(_lastSwingPoseDirection, baseWithBend);
            desiredRot = Quaternion.Slerp(baseWithBend, swingTargetRot, _swingStrength);
        }

        Vector3 desiredRootPos = _rodBasePos;
        bool swingPivotActive =
            directionalSwingEnabled &&
            rodSwingPivot != null &&
            (_lastSwingPoseDirection != SwingDirection.None && _swingStrength > 0.001f);
        if (swingPivotActive)
        {
            Vector3 desiredPivotPos = _rodBasePos + (baseWithBend * _swingPivotLocalOffsetFromRoot);
            desiredRootPos = desiredPivotPos - (desiredRot * _swingPivotLocalOffsetFromRoot);
        }

        float k = 1f - Mathf.Exp(-rodCenterSpeed * dt);
        rodRoot.position = Vector3.Lerp(rodRoot.position, desiredRootPos, k);
        rodRoot.rotation = Quaternion.Slerp(rodRoot.rotation, desiredRot, k);

        bool posDone = (rodRoot.position - _rodBasePos).sqrMagnitude <= 0.000004f;
        bool rotDone = Quaternion.Angle(rodRoot.rotation, _rodBaseRot) <= 0.15f;
        bool bendDone = Mathf.Abs(_currentBendAngle) <= 0.05f;
        bool poseDone = Mathf.Abs(_tensionPoseBlend) <= 0.01f;
        bool swingDone = Mathf.Abs(_swingStrength) <= 0.01f;

        if (posDone && rotDone && bendDone && poseDone && swingDone)
        {
            rodRoot.position = _rodBasePos;
            rodRoot.rotation = _rodBaseRot;
            _currentBendAngle = 0f;
            _tensionPoseBlend = 0f;
            _lastSwingPoseDirection = SwingDirection.None;
            _swingStrength = 0f;
            _isRestoringFromTension = false;
            CacheStableRodBasePose();
        }
    }

    private void CacheStableRodBasePose()
    {
        if (rodRoot == null && rodTip != null)
            rodRoot = rodTip.parent;
        if (rodRoot == null)
            return;

        _stableRodBasePos = rodRoot.position;
        _stableRodBaseRot = rodRoot.rotation;

        if (rodSwingPivot != null)
        {
            _stableSwingPivotLocalOffsetFromRoot =
                Quaternion.Inverse(_stableRodBaseRot) * (rodSwingPivot.position - _stableRodBasePos);
        }
        else
        {
            _stableSwingPivotLocalOffsetFromRoot = Vector3.zero;
        }

        _hasStableRodBasePose = true;
    }

    private void UpdateDirectionalSwingInput()
    {
        if (!directionalSwingEnabled)
        {
            _activeSwingDirection = SwingDirection.None;
            _upHeldLastFrame = false;
            _leftHeldLastFrame = false;
            _rightHeldLastFrame = false;
            return;
        }

        bool upHeld = Input.GetKey(swingUpKey) || (_useExternalDirectionalInput && _externalUpHeld);
        bool leftHeld = Input.GetKey(swingLeftKey) || (_useExternalDirectionalInput && _externalLeftHeld);
        bool rightHeld = Input.GetKey(swingRightKey) || (_useExternalDirectionalInput && _externalRightHeld);

        if (upHeld && !_upHeldLastFrame)
        {
            _upKeyDownTime = Time.time;
            _lastPressedSwingDirection = SwingDirection.Up;
        }
        if (leftHeld && !_leftHeldLastFrame)
        {
            _leftKeyDownTime = Time.time;
            _lastPressedSwingDirection = SwingDirection.Left;
        }
        if (rightHeld && !_rightHeldLastFrame)
        {
            _rightKeyDownTime = Time.time;
            _lastPressedSwingDirection = SwingDirection.Right;
        }

        if (!upHeld && _upHeldLastFrame)
        {
            TryLatchDirectionalTap(SwingDirection.Up, _upKeyDownTime);
            _upKeyDownTime = -1f;
        }
        if (!leftHeld && _leftHeldLastFrame)
        {
            TryLatchDirectionalTap(SwingDirection.Left, _leftKeyDownTime);
            _leftKeyDownTime = -1f;
        }
        if (!rightHeld && _rightHeldLastFrame)
        {
            TryLatchDirectionalTap(SwingDirection.Right, _rightKeyDownTime);
            _rightKeyDownTime = -1f;
        }

        _upHeldLastFrame = upHeld;
        _leftHeldLastFrame = leftHeld;
        _rightHeldLastFrame = rightHeld;

        SwingDirection heldDirection = GetHeldSwingDirection(upHeld, leftHeld, rightHeld);
        if (heldDirection != SwingDirection.None)
        {
            _activeSwingDirection = heldDirection;
            _tapSwingDirection = SwingDirection.None;
            _tapSwingStartTime = -1f;
            return;
        }

        if (IsTapSwingActive())
        {
            _activeSwingDirection = _tapSwingDirection;
            return;
        }

        _activeSwingDirection = SwingDirection.None;
        _tapSwingDirection = SwingDirection.None;
        _tapSwingStartTime = -1f;
    }

    private void TryLatchDirectionalTap(SwingDirection direction, float keyDownTime)
    {
        if (keyDownTime < 0f) return;

        float held = Time.time - keyDownTime;
        if (held <= directionalTapThreshold)
        {
            _tapSwingDirection = direction;
            _tapSwingStartTime = Time.time;
        }
    }

    private SwingDirection GetHeldSwingDirection(bool upHeld, bool leftHeld, bool rightHeld)
    {
        bool lastHeld =
            (_lastPressedSwingDirection == SwingDirection.Up && upHeld) ||
            (_lastPressedSwingDirection == SwingDirection.Left && leftHeld) ||
            (_lastPressedSwingDirection == SwingDirection.Right && rightHeld);

        if (lastHeld)
            return _lastPressedSwingDirection;

        if (upHeld) return SwingDirection.Up;
        if (leftHeld) return SwingDirection.Left;
        if (rightHeld) return SwingDirection.Right;
        return SwingDirection.None;
    }

    private bool IsTapSwingActive()
    {
        if (_tapSwingDirection == SwingDirection.None || _tapSwingStartTime < 0f)
            return false;

        return (Time.time - _tapSwingStartTime) < Mathf.Max(0.01f, directionalTapDuration);
    }

    private bool IsSwingDirectionHeld(SwingDirection direction)
    {
        return
            (direction == SwingDirection.Up && (Input.GetKey(swingUpKey) || (_useExternalDirectionalInput && _externalUpHeld))) ||
            (direction == SwingDirection.Left && (Input.GetKey(swingLeftKey) || (_useExternalDirectionalInput && _externalLeftHeld))) ||
            (direction == SwingDirection.Right && (Input.GetKey(swingRightKey) || (_useExternalDirectionalInput && _externalRightHeld)));
    }

    private float EvaluateTapSwingStrength()
    {
        if (!IsTapSwingActive())
            return 0f;

        float duration = Mathf.Max(0.01f, directionalTapDuration);
        float t = Mathf.Clamp01((Time.time - _tapSwingStartTime) / duration);
        if (directionalTapCurve == null || directionalTapCurve.length == 0)
            return 1f - t;

        return Mathf.Clamp01(directionalTapCurve.Evaluate(t));
    }

    private Quaternion GetSwingRotationForDirection(SwingDirection direction, Quaternion baseRot)
    {
        if (direction == SwingDirection.None)
            return baseRot;

        float pitch = 0f;
        float yaw = 0f;

        switch (direction)
        {
            case SwingDirection.Up:
                pitch = swingUpPitchAngle;
                break;
            case SwingDirection.Left:
                yaw = swingLeftYawAngle;
                break;
            case SwingDirection.Right:
                yaw = swingRightYawAngle;
                break;
        }

        Transform axisRef = null;
        if (useCameraRelativeSwingAxes)
        {
            if (tensionCamera == null)
                tensionCamera = Camera.main;
            if (tensionCamera != null)
                axisRef = tensionCamera.transform;
        }

        Vector3 pitchAxis = axisRef != null ? axisRef.right : (baseRot * Vector3.right);
        Vector3 yawAxis = axisRef != null ? axisRef.up : (baseRot * Vector3.up);
        Quaternion offsetRot =
            Quaternion.AngleAxis(yaw, yawAxis) *
            Quaternion.AngleAxis(pitch, pitchAxis);

        return offsetRot * baseRot;
    }

    private void ResetDirectionalSwingState()
    {
        _activeSwingDirection = SwingDirection.None;
        _lastPressedSwingDirection = SwingDirection.None;
        _tapSwingDirection = SwingDirection.None;
        _tapSwingStartTime = -1f;
        _lastSwingPoseDirection = SwingDirection.None;
        _swingStrength = 0f;
        _upKeyDownTime = -1f;
        _leftKeyDownTime = -1f;
        _rightKeyDownTime = -1f;
        _upHeldLastFrame = false;
        _leftHeldLastFrame = false;
        _rightHeldLastFrame = false;
    }

    private void ClearDirectionalSwingInputState()
    {
        _activeSwingDirection = SwingDirection.None;
        _lastPressedSwingDirection = SwingDirection.None;
        _tapSwingDirection = SwingDirection.None;
        _tapSwingStartTime = -1f;
        _upKeyDownTime = -1f;
        _leftKeyDownTime = -1f;
        _rightKeyDownTime = -1f;
        _upHeldLastFrame = false;
        _leftHeldLastFrame = false;
        _rightHeldLastFrame = false;
    }
}
