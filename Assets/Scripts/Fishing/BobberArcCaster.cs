using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;
using FMOD.Studio;

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

    [Header("Cast / Yank Rod Motion")]
    public bool actionRodMotionEnabled = true;
    [Min(0.01f)] public float castRodMotionDuration = 0.72f;
    [Range(0.05f, 0.95f)] public float castRodWindupPortion = 0.4f;
    [Range(-80f, 80f)] public float castRodWindupPitch = -38f;
    [Range(-80f, 80f)] public float castRodReleasePitch = 62f;
    public Vector3 castRodWindupLocalOffset = new Vector3(0f, -0.16f, 0.28f);
    public Vector3 castRodReleaseLocalOffset = new Vector3(0f, 0.14f, -0.32f);
    public Vector3 castBobberWindupLocalOffset = new Vector3(0f, 0.16f, 0.34f);
    [Min(0.01f)] public float yankRodMotionDuration = 0.42f;
    [Range(-80f, 80f)] public float yankRodJerkPitch = -58f;
    public Vector3 yankRodJerkLocalOffset = new Vector3(0f, -0.22f, -0.05f);
    [Min(0.01f)] public float rodActionPoseSpeed = 14f;
    [Min(0.01f)] public float rodActionReturnSpeed = 10f;
    [Header("Hook Detection")]
    public bool useNibbleBasedHooking = true;
    public bool allowRadiusHookFallback = false;

    [Header("Tension Entry (Input)")]
    public bool allowManualTensionEntry = false;
    [Header("Hooked Fish")]
    public bool lockHookedFishToBobber = true;
    public float hookedFishFrontPadding = 0f;
    [Min(0f)] public float hookedFishDepthBelowSurface = 0.35f;
    [Min(0f)] public float hookedFishSwimRadius = 0.35f;
    [Min(0f)] public float hookedFishSwimSpeed = 7f;
    [Range(0f, 1f)] public float hookedFishRandomMotionScale = 1f;
    [Min(0.1f)] public float hookedFishSwimToCenterSpeed = 3.5f;
    [Min(0.01f)] public float hookedFishCenterArrivalDistance = 0.08f;
    public float bobberFollowVerticalOffset = 0.02f;
    [Min(0.1f)] public float bobberFollowSmoothing = 18f;

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
    private enum RodActionMotion { None, Cast, Yank }

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
    private RodActionMotion _activeRodActionMotion = RodActionMotion.None;
    private float _rodActionMotionStartTime = -1f;
    private float _rodActionMotionDuration;
    private bool _rodActionWasDriving;
    private GameObject _hookedFish;
    private bool _hookedFishLockedToBobber;
    private float _hookedFishFrontDistance = 0.25f;
    private Rigidbody _hookedFishRb;
    private FishMovement _hookedFishMovement;
    private Vector3 _hookedFishTensionCenter;
    private Vector3 _hookedFishCenterTarget;
    private bool _hookedFishMovingToCenter;
    private float _hookedFishSwimSeed;
    private bool _isSuccessSequenceActive;

    [Header("Success Requirements")]
    public int minimumAccuracyForCatch = 65;
    public FishCatchAnimation catchAnimation; 
    [Header("Failed Catch Popup")]
    public FailedCatchPopup failedCatchPopup;
    public string failedCatchPopupLabel = "FISH ESCAPED!";
    public bool IsHookedFishDrivingBobber =>
        _hookedFishLockedToBobber &&
        CurrentState == State.Tension &&
        _hookedFish != null;
    public GameObject HookedFish => _hookedFish;
    public bool IsSuccessSequenceActive => _isSuccessSequenceActive;

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

        if (pondManager == null)
            pondManager = FindObjectOfType<PondManager>();

        TryResolveCatchAnimation();
        TryResolveFailedCatchPopup();
        CacheStableRodBasePose();
    }

    // Call this from your JoyCon gesture event
    public void Cast()
    {
        if (!rodTip || !bobber || !targetMarker) return;

        if (!TutorialStartGate.IsCastAllowedByTutorial())
            return;

        // Only allow a fresh cast from idle/hanging.
        if (CurrentState != State.Idle) return;
        _hookedFish = null;
        ClearHookedFishLockState();
        if (pondManager != null)
            pondManager.RestoreFishAfterTension();
        FishMovement.ClearBobberNibbleVerticalOverride(bobber);

        Vector3 to = targetMarker.position + Vector3.up * castTargetYOffset;

        FMODUnity.RuntimeManager.PlayOneShot("event:/Sfx/castPH");

        BeginRodActionMotion(RodActionMotion.Cast, castDuration);
        StartArcMove(
            bobber.position,
            to,
            castDuration,
            arcHeight,
            arcEase,
            GetCastReleaseLeadTime());
        CurrentState = State.InFlight;
    }

    // Call this from your JoyCon gesture event
    public void Yank()
    {
        if (!rodTip || !bobber) return;

        if (!TutorialStartGate.IsYankAllowedByTutorial())
            return;

        // Guard: don't yank if already idle/hanging
        if (CurrentState == State.Idle || _isPreparingYank) return;

        // Only allow yanks once the bobber is in-water (landed/tension flow).
        bool bobberInWater =
            CurrentState == State.Landed ||
            CurrentState == State.Tension ||
            _isRestoringFromTension;
        if (!bobberInWater)
            return;

        // If rod is displaced by tension feedback, let it settle before retracting bobber.
        if (CurrentState == State.Tension && IsBeatmapPlaying())
        {
            Debug.Log("Cannot yank out of tension while beatmap is playing.");
            return;
        }

        if (CurrentState == State.Tension || _isRestoringFromTension)
        {
            StartYankAfterRodRestore();
            return;
        }

        if (CurrentState == State.Landed)
        {
            if (TryFindHookableFish(out GameObject fish))
            {
                _hookedFish = fish;
                ClearRodActionMotionState();
                Debug.Log("Fish hooked! Entering tension state.");
                ToggleTension(); // enter tension state
                return;
            }
        }

        _hookedFish = null;
        ClearHookedFishLockState();
        if (pondManager != null)
            pondManager.RestoreFishAfterTension();
        Debug.Log("No hook condition met. Normal yank.");
        StartYank();
    }

    // Call this when a fish is hooked (or for now, a test key)
    public void ToggleTension()
    {
        if (CurrentState == State.Tension)
        {
            ConsumeHookedFish();
            CurrentState = State.Landed;
            return;
        }

        if (CurrentState == State.Landed)
        {
            if (_hookedFish == null && pondManager != null)
            {
                GameObject hookQueryBobber = GetHookQueryBobber();
                if (hookQueryBobber != null)
                    _hookedFish = pondManager.GetClosestFish(hookQueryBobber);
            }

            CurrentState = State.Tension;
            BeginHookedFishLock();
            if (pondManager != null)
                pondManager.HideFishForTension(_hookedFish);
        }
    }

    public void CompleteRhythmEncounter()
    {
        float accuracy = FishingSessionHud.GetCurrentRunAccuracyOrLast();
        string grade = FishingSessionHud.GetLetterGradeForAccuracy(accuracy);
        GameObject fishToResolve = ResolveFishForEncounter();
        bool catchSucceeded = FishingSessionHud.IsSuccessfulCatchAccuracy(accuracy) && fishToResolve != null;
        FishingSessionHud.RegisterCatchOutcome(catchSucceeded);

        if (catchSucceeded)
        {
            GameObject fishToShow = fishToResolve;
            GameObject migratedFish = SceneLoading.MigratedFish;

            // driveOverlayFromBobberTension can end rhythm as soon as we leave Tension.
            // Clear SceneLoading's migrated reference so overlay teardown won't destroy
            // the fish before the trophy animation consumes it.
            if (fishToShow != null && fishToShow == migratedFish)
                SceneLoading.MigratedFish = null;

            if (SceneLoading.Instance != null)
                SceneLoading.Instance.HideScoringCircleForCatchSequence();

            _isSuccessSequenceActive = true;
            BeginRodReturnForSuccessSequence();

            _hookedFish = null; // Remove reference so Consume/Restore doesn't touch it

            StartCoroutine(ExecuteSuccessSequence(fishToShow));
        }
        else
        {
            Debug.Log($"Catch failed ({grade}, {accuracy:F1}%). The fish got away.");
            ShowFailedCatchPopup();
            HandleFailedCatch(fishToResolve);
            FinishTensionState();
        }
    }

    private void ShowFailedCatchPopup()
    {
        if (!TryResolveFailedCatchPopup())
            return;

        failedCatchPopup.Show(failedCatchPopupLabel);
    }

    private GameObject ResolveFishForEncounter()
    {
        if (_hookedFish != null)
            return _hookedFish;

        return SceneLoading.MigratedFish;
    }

    private bool TryResolveFailedCatchPopup()
    {
        if (failedCatchPopup != null)
            return true;

        failedCatchPopup = FindObjectOfType<FailedCatchPopup>();
        if (failedCatchPopup != null)
            return true;

        if (Camera.main != null)
        {
            failedCatchPopup = Camera.main.GetComponent<FailedCatchPopup>();
            if (failedCatchPopup == null)
                failedCatchPopup = Camera.main.gameObject.AddComponent<FailedCatchPopup>();
        }

        return failedCatchPopup != null;
    }

    private void HandleFailedCatch(GameObject fish)
    {
        BeginRodReturnForSuccessSequence();
        FishMovement.ClearBobberNibbleVerticalOverride(bobber);

        if (fish == null)
        {
            SceneLoading.MigratedFish = null;
            _hookedFish = null;
            ClearHookedFishLockState();
            return;
        }

        ReleaseFishAfterFailedCatch(fish);
        _hookedFish = null;
    }

    private void ReleaseFishAfterFailedCatch(GameObject fish)
    {
        if (fish == null)
            return;

        if (SceneLoading.MigratedFish == fish)
            SceneLoading.MigratedFish = null;

        RestoreFishForGameplayAfterRhythm(fish);

        _hookedFish = fish;
        ClearHookedFishLockState();

        FishMovement movement = fish.GetComponentInChildren<FishMovement>(true);
        if (movement != null)
        {
            movement.enabled = true;
            float panicDuration = Random.Range(1.25f, 2f);
            movement.ForcePanicSwimAwayFrom(bobber, panicDuration);
            return;
        }

        Rigidbody fishRb = fish.GetComponent<Rigidbody>();
        if (fishRb != null)
        {
            fishRb.isKinematic = false;
            Vector3 away = bobber != null
                ? fish.transform.position - bobber.position
                : fish.transform.forward;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
            {
                Vector2 random = Random.insideUnitCircle;
                if (random.sqrMagnitude < 0.0001f)
                    random = Vector2.right;
                away = new Vector3(random.x, 0f, random.y);
            }

            fishRb.linearVelocity = away.normalized * 2.5f;
        }
    }

    private void RestoreFishForGameplayAfterRhythm(GameObject fish)
    {
        if (fish == null)
            return;

        Scene gameplayScene = gameObject.scene;
        if (gameplayScene.IsValid() && fish.scene != gameplayScene)
            SceneManager.MoveGameObjectToScene(fish, gameplayScene);

        Vector3 releasePos = bobber != null ? bobber.position : fish.transform.position;
        float surfaceY = pondManager != null ? pondManager.waterlevel : releasePos.y;
        releasePos.y = surfaceY - Mathf.Max(0.05f, hookedFishDepthBelowSurface);
        fish.transform.position = releasePos;

        Collider[] colliders = fish.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col != null)
                col.enabled = true;
        }

        Rigidbody[] rigidbodies = fish.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody rb = rigidbodies[i];
            if (rb != null)
                rb.isKinematic = false;
        }

        SetLayerRecursively(fish.transform, 0);
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null)
            return;

        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }

    private IEnumerator ExecuteSuccessSequence(GameObject fish)
    {
        yield return WaitForRodIdleBeforeSuccessAnimation();

        if (fish == null)
        {
            Debug.LogWarning("Success sequence skipped fish trophy animation because no hooked fish was available.");
        }
        else if (TryResolveCatchAnimation())
        {
            if (pondManager != null)
                pondManager.UnregisterFish(fish);
            yield return StartCoroutine(catchAnimation.TrophyRoutine(fish));
        }
        else
        {
            Debug.LogWarning("FishCatchAnimation reference is missing. Falling back to direct fish consume.");
            if (pondManager != null)
                pondManager.RemoveFish(fish);
            else
                Destroy(fish);
        }

        FinishTensionState();
    }

    private void FinishTensionState()
    {
        _isSuccessSequenceActive = false;
        SceneLoading.MigratedFish = null;
        if (SceneLoading.Instance != null)
            SceneLoading.Instance.EndRhythmEncounter();

        if (CurrentState != State.Idle &&
            CurrentState != State.Retracting &&
            !_isPreparingYank)
        {
            StartYankAfterRodRestore();
        }
        // retore fish
        if (pondManager != null)
                pondManager.RestoreFishAfterTension();
                
    }

    private void BeginRodReturnForSuccessSequence()
    {
        if (CurrentState == State.Tension)
            CurrentState = State.Landed;

        if (CurrentState != State.Idle &&
            CurrentState != State.Retracting &&
            !_isPreparingYank)
        {
            StartYankAfterRodRestore();
        }
    }

    private IEnumerator WaitForRodIdleBeforeSuccessAnimation()
    {
        const float timeout = 1.2f;
        float elapsed = 0f;
        while (CurrentState != State.Idle && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private bool TryResolveCatchAnimation()
    {
        if (catchAnimation != null)
            return true;

        catchAnimation = FindObjectOfType<FishCatchAnimation>();
        if (catchAnimation != null)
            return true;

        FishCatchAnimation[] allAnimations = Resources.FindObjectsOfTypeAll<FishCatchAnimation>();
        for (int i = 0; i < allAnimations.Length; i++)
        {
            FishCatchAnimation candidate = allAnimations[i];
            if (candidate == null)
                continue;
            if (!candidate.gameObject.scene.IsValid())
                continue;

            catchAnimation = candidate;
            break;
        }

        if (catchAnimation == null && Camera.main != null)
        {
            catchAnimation = Camera.main.GetComponent<FishCatchAnimation>();
            if (catchAnimation == null)
            {
                catchAnimation = Camera.main.gameObject.AddComponent<FishCatchAnimation>();
                Debug.LogWarning(
                    "Added runtime FishCatchAnimation to Camera.main because no scene reference was found. " +
                    "Assign BobberArcCaster.catchAnimation for authored UI bindings.");
            }
        }

        if (catchAnimation == null)
        {
            Debug.LogWarning(
                "Could not resolve FishCatchAnimation. " +
                "Assign BobberArcCaster.catchAnimation in the inspector to enable trophy animation.");
        }

        return catchAnimation != null;
    }

    private void ConsumeHookedFish()
    {
        if (_hookedFish == null)
        {
            ClearHookedFishLockState();
            if (pondManager != null)
                pondManager.RestoreFishAfterTension();
            return;
        }

        ClearHookedFishLockState();

        if (pondManager != null)
            pondManager.RemoveFish(_hookedFish);
        else
            Destroy(_hookedFish);

        _hookedFish = null;
        if (pondManager != null)
            pondManager.RestoreFishAfterTension();
    }

    public void RequestTensionToggleFromInput()
    {
        if (!allowManualTensionEntry)
        {
            Debug.Log("Manual tension toggle disabled: hook a fish to enter, song end exits.");
            return;
        }

        if (CurrentState == State.Tension)
        {
            ToggleTension();
            RhythmMusicPlayer musicPlayer = RhythmConductor.rhythmMusicPlayer;
            if (musicPlayer != null)
                musicPlayer.ForceStopPlaybackAndBeatmap();
            if (SceneLoading.Instance != null)
                SceneLoading.Instance.EndRhythmEncounter();
            return;
        }

        if (CurrentState != State.Landed)
        {
            Debug.Log("Manual tension toggle requires landed/tension state.");
            return;
        }

        ToggleTension();
        if (SceneLoading.Instance != null)
            SceneLoading.Instance.StartRhythmEncounter(_hookedFish);
    }

    private GameObject GetHookQueryBobber()
    {
        if (bobber != null)
            return bobber.gameObject;

        if (pondManager != null && pondManager.playerBobber != null)
            return pondManager.playerBobber;

        return null;
    }

    private bool TryFindHookableFish(out GameObject fish)
    {
        fish = null;

        GameObject hookQueryBobber = GetHookQueryBobber();
        if (hookQueryBobber == null)
            return false;

        if (useNibbleBasedHooking)
        {
            if (FishMovement.TryGetHookableNibbleFish(
                    hookQueryBobber.transform,
                    out fish,
                    out bool fromActiveNibble))
            {
                Debug.Log(fromActiveNibble
                    ? "Fish hooked from active nibble."
                    : "Fish hooked from nibble timing window.");
                return true;
            }

            if (!allowRadiusHookFallback)
                return false;
        }

        if (pondManager == null)
            return false;

        fish = pondManager.GetClosestFish(hookQueryBobber);
        return fish != null;
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
        FishMovement.ClearBobberNibbleVerticalOverride(bobber);

        Vector3 to = bobberHangPoint ? bobberHangPoint.position : rodTip.position;
        BeginRodActionMotion(RodActionMotion.Yank, yankDuration);
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

    private void StartArcMove(
        Vector3 from,
        Vector3 to,
        float duration,
        float height,
        AnimationCurve ease,
        float startDelay = 0f)
    {
        if (_moveRoutine != null) StopCoroutine(_moveRoutine);
        _moveRoutine = StartCoroutine(ArcMove(from, to, duration, height, ease, startDelay));
    }

    private void StartLinearMove(Vector3 from, Vector3 to, float duration, AnimationCurve ease)
    {
        if (_moveRoutine != null) StopCoroutine(_moveRoutine);
        _moveRoutine = StartCoroutine(LinearMove(from, to, duration, ease));
    }

    private IEnumerator ArcMove(
        Vector3 from,
        Vector3 to,
        float duration,
        float height,
        AnimationCurve ease,
        float startDelay)
    {
        float delayRemaining = Mathf.Max(0f, startDelay);
        while (delayRemaining > 0f)
        {
            if (bobber != null)
            {
                Transform hangAnchor = bobberHangPoint != null ? bobberHangPoint : rodTip;
                if (hangAnchor != null)
                {
                    float progress = 1f - Mathf.Clamp01(delayRemaining / Mathf.Max(0.0001f, startDelay));
                    Vector3 localBobberOffset = Vector3.LerpUnclamped(
                        Vector3.zero,
                        castBobberWindupLocalOffset,
                        EaseOutCubic(progress));
                    bobber.position = hangAnchor.position + GetCastBobberWindupWorldOffset(hangAnchor, localBobberOffset);
                }
            }

            delayRemaining -= Time.deltaTime;
            yield return null;
        }

        if (bobber != null)
            from = bobber.position;

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
        UpdateHookedFishLock();
    }

    private void BeginHookedFishLock()
    {
        if (!lockHookedFishToBobber || CurrentState != State.Tension || _hookedFish == null || bobber == null)
            return;

        _hookedFishRb = _hookedFish.GetComponent<Rigidbody>();
        if (_hookedFishRb != null)
        {
            _hookedFishRb.linearVelocity = Vector3.zero;
            _hookedFishRb.angularVelocity = Vector3.zero;
            _hookedFishRb.isKinematic = true;
        }

        _hookedFishMovement = _hookedFish.GetComponent<FishMovement>();
        if (_hookedFishMovement != null)
            _hookedFishMovement.enabled = false;

        _hookedFishTensionCenter = _hookedFish.transform.position;
        float surfaceY = pondManager != null ? pondManager.waterlevel : _hookedFishTensionCenter.y;
        _hookedFishTensionCenter.y = surfaceY - Mathf.Max(0f, hookedFishDepthBelowSurface);
        _hookedFishCenterTarget = _hookedFishTensionCenter;
        if (pondManager != null)
        {
            Vector3 pondCenter = pondManager.transform.position;
            _hookedFishCenterTarget.x = pondCenter.x;
            _hookedFishCenterTarget.z = pondCenter.z;
        }
        _hookedFishMovingToCenter = true;
        _hookedFishSwimSeed = Random.Range(0f, 1000f);
        _hookedFishFrontDistance = EstimateFishFrontDistance(_hookedFish.transform);
        _hookedFishLockedToBobber = true;
        UpdateHookedFishLock();
    }

    private void UpdateHookedFishLock()
    {
        if (!_hookedFishLockedToBobber)
            return;

        if (!lockHookedFishToBobber || CurrentState != State.Tension || _hookedFish == null || bobber == null)
        {
            ClearHookedFishLockState();
            return;
        }

        UpdateHookedFishTensionMotion();
        FollowBobberToHookedFishFront();
    }

    private void UpdateHookedFishTensionMotion()
    {
        if (_hookedFish == null)
            return;

        Transform fishTransform = _hookedFish.transform;
        if (_hookedFishMovingToCenter)
        {
            Vector3 toCenter = _hookedFishCenterTarget - fishTransform.position;
            toCenter.y = 0f;

            float arriveDistance = Mathf.Max(0.01f, hookedFishCenterArrivalDistance);
            if (toCenter.sqrMagnitude <= arriveDistance * arriveDistance)
            {
                _hookedFishMovingToCenter = false;
                _hookedFishTensionCenter = _hookedFishCenterTarget;
            }
            else
            {
                Vector3 moveDir = toCenter.normalized;
                float speed = Mathf.Max(0.1f, hookedFishSwimToCenterSpeed);
                fishTransform.position += moveDir * speed * Time.deltaTime;
                fishTransform.position = new Vector3(
                    fishTransform.position.x,
                    _hookedFishCenterTarget.y,
                    fishTransform.position.z);

                Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                fishTransform.rotation = Quaternion.Slerp(
                    fishTransform.rotation,
                    targetRot,
                    1f - Mathf.Exp(-8f * Time.deltaTime));
                return;
            }
        }

        float t = Time.time * Mathf.Max(0.01f, hookedFishSwimSpeed);
        float radius = Mathf.Max(0f, hookedFishSwimRadius * Mathf.Clamp01(hookedFishRandomMotionScale));

        Vector3 localOffset = new Vector3(
            Mathf.Sin(t + _hookedFishSwimSeed),
            0f,
            Mathf.Cos((t * 1.27f) + (_hookedFishSwimSeed * 0.73f)));

        Vector3 targetPos = _hookedFishTensionCenter + (localOffset * radius);
        targetPos.y = _hookedFishTensionCenter.y;
        Vector3 previousPos = fishTransform.position;
        fishTransform.position = targetPos;

        Vector3 planarVel = targetPos - previousPos;
        planarVel.y = 0f;

        if (planarVel.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(planarVel.normalized, Vector3.up);
            fishTransform.rotation = Quaternion.Slerp(
                fishTransform.rotation,
                targetRot,
                1f - Mathf.Exp(-8f * Time.deltaTime));
        }
    }

    private void FollowBobberToHookedFishFront()
    {
        if (_hookedFish == null || bobber == null)
            return;

        Transform fishTransform = _hookedFish.transform;
        Vector3 forward = fishTransform.forward;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        float offset = Mathf.Max(0f, _hookedFishFrontDistance + hookedFishFrontPadding);
        Vector3 hookFrontPos = fishTransform.position + (forward.normalized * offset);
        float surfaceY = pondManager != null ? pondManager.waterlevel : hookFrontPos.y;
        Vector3 targetBobberPos = new Vector3(
            hookFrontPos.x,
            surfaceY + bobberFollowVerticalOffset,
            hookFrontPos.z);

        float k = 1f - Mathf.Exp(-Mathf.Max(0.1f, bobberFollowSmoothing) * Time.deltaTime);
        bobber.position = Vector3.Lerp(bobber.position, targetBobberPos, k);
    }

    private static float EstimateFishFrontDistance(Transform fishTransform)
    {
        if (fishTransform == null)
            return 0.25f;

        Collider col = fishTransform.GetComponentInChildren<Collider>();
        if (col == null)
            return 0.25f;

        Bounds b = col.bounds;
        Vector3 c = b.center;
        Vector3 e = b.extents;
        Vector3 forward = fishTransform.forward.normalized;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        float maxProjection = 0.05f;
        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = c + new Vector3(e.x * x, e.y * y, e.z * z);
                    float projection = Vector3.Dot(corner - fishTransform.position, forward);
                    if (projection > maxProjection)
                        maxProjection = projection;
                }
            }
        }

        return Mathf.Max(0.05f, maxProjection);
    }

    private void ClearHookedFishLockState()
    {
        if (_hookedFishMovement != null)
            _hookedFishMovement.enabled = true;

        if (_hookedFishRb != null)
            _hookedFishRb.isKinematic = false;

        _hookedFishLockedToBobber = false;
        _hookedFishFrontDistance = 0.25f;
        _hookedFishRb = null;
        _hookedFishMovement = null;
        _hookedFishMovingToCenter = false;
        _hookedFishSwimSeed = 0f;
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
        bool actionMotionActive = IsRodActionMotionActive();

        if (!inTension && !_wasInTension && !_isRestoringFromTension && !actionMotionActive && !_rodActionWasDriving)
            CacheStableRodBasePose();

        if (inTension && !_wasInTension)
            BeginTensionFeedback();

        if (inTension)
        {
            ApplyTensionRodPose();
        }
        else if (_isRestoringFromTension || _wasInTension)
        {
            RestoreFromTension();
        }
        else
        {
            ApplyActionRodPoseOrRestore();
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

        ClearRodActionMotionState();
        ResetDirectionalSwingState();
    }

    private void BeginRodActionMotion(RodActionMotion motion, float linkedBobberDuration)
    {
        if (!actionRodMotionEnabled)
            return;

        if (rodRoot == null && rodTip != null)
            rodRoot = rodTip.parent;
        if (rodRoot == null)
            return;

        if (!_hasStableRodBasePose)
            CacheStableRodBasePose();
        if (!_hasStableRodBasePose)
            return;

        _activeRodActionMotion = motion;
        _rodActionMotionStartTime = Time.time;

        float configuredDuration = motion == RodActionMotion.Cast
            ? castRodMotionDuration
            : yankRodMotionDuration;
        _rodActionMotionDuration = Mathf.Clamp(
            configuredDuration,
            0.01f,
            Mathf.Max(0.01f, linkedBobberDuration));
        _rodActionWasDriving = true;
    }

    private bool IsRodActionMotionActive()
    {
        return _activeRodActionMotion != RodActionMotion.None &&
               (Time.time - _rodActionMotionStartTime) < _rodActionMotionDuration;
    }

    private void ApplyActionRodPoseOrRestore()
    {
        if (rodRoot == null)
            return;

        if (!_hasStableRodBasePose)
            CacheStableRodBasePose();
        if (!_hasStableRodBasePose)
            return;

        bool actionActive = IsRodActionMotionActive();
        float pitchAngle = 0f;
        Vector3 localOffset = Vector3.zero;

        if (actionActive)
        {
            float normalized = Mathf.Clamp01((Time.time - _rodActionMotionStartTime) / Mathf.Max(0.01f, _rodActionMotionDuration));
            EvaluateRodActionMotion(normalized, out pitchAngle, out localOffset);
        }
        else
        {
            _activeRodActionMotion = RodActionMotion.None;
        }

        Vector3 desiredBasePos = _stableRodBasePos + (_stableRodBaseRot * localOffset);
        Quaternion desiredRot =
            Quaternion.AngleAxis(pitchAngle, _stableRodBaseRot * Vector3.right) *
            _stableRodBaseRot;

        Vector3 desiredRootPos = desiredBasePos;
        if (rodSwingPivot != null)
        {
            Vector3 desiredPivotPos = desiredBasePos + (_stableRodBaseRot * _stableSwingPivotLocalOffsetFromRoot);
            desiredRootPos = desiredPivotPos - (desiredRot * _stableSwingPivotLocalOffsetFromRoot);
        }

        float followSpeed = actionActive ? rodActionPoseSpeed : rodActionReturnSpeed;
        float k = 1f - Mathf.Exp(-Mathf.Max(0.01f, followSpeed) * Mathf.Max(Time.deltaTime, 0.0001f));
        rodRoot.position = Vector3.Lerp(rodRoot.position, desiredRootPos, k);
        rodRoot.rotation = Quaternion.Slerp(rodRoot.rotation, desiredRot, k);

        if (!actionActive && IsRodAtStablePose())
        {
            rodRoot.position = _stableRodBasePos;
            rodRoot.rotation = _stableRodBaseRot;
            _rodActionWasDriving = false;
            CacheStableRodBasePose();
        }
    }

    private void EvaluateRodActionMotion(float normalizedTime, out float pitchAngle, out Vector3 localOffset)
    {
        pitchAngle = 0f;
        localOffset = Vector3.zero;

        if (_activeRodActionMotion == RodActionMotion.Cast)
        {
            float windupPortion = Mathf.Clamp01(castRodWindupPortion);
            if (normalizedTime < windupPortion)
            {
                float t = EaseOutCubic(normalizedTime / Mathf.Max(0.001f, windupPortion));
                pitchAngle = Mathf.LerpUnclamped(0f, castRodWindupPitch, t);
                localOffset = Vector3.LerpUnclamped(Vector3.zero, castRodWindupLocalOffset, t);
                return;
            }

            const float releasePortion = 0.76f;
            if (normalizedTime < releasePortion)
            {
                float t = EaseInCubic((normalizedTime - windupPortion) / Mathf.Max(0.001f, releasePortion - windupPortion));
                pitchAngle = Mathf.LerpUnclamped(castRodWindupPitch, castRodReleasePitch, t);
                localOffset = Vector3.LerpUnclamped(castRodWindupLocalOffset, castRodReleaseLocalOffset, t);
                return;
            }

            float returnT = EaseOutCubic((normalizedTime - releasePortion) / Mathf.Max(0.001f, 1f - releasePortion));
            pitchAngle = Mathf.LerpUnclamped(castRodReleasePitch, 0f, returnT);
            localOffset = Vector3.LerpUnclamped(castRodReleaseLocalOffset, Vector3.zero, returnT);
            return;
        }

        if (_activeRodActionMotion == RodActionMotion.Yank)
        {
            const float jerkPortion = 0.58f;
            if (normalizedTime < jerkPortion)
            {
                float t = EaseOutCubic(normalizedTime / jerkPortion);
                pitchAngle = Mathf.LerpUnclamped(0f, yankRodJerkPitch, t);
                localOffset = Vector3.LerpUnclamped(Vector3.zero, yankRodJerkLocalOffset, t);
                return;
            }

            float returnT = EaseOutCubic((normalizedTime - jerkPortion) / Mathf.Max(0.001f, 1f - jerkPortion));
            pitchAngle = Mathf.LerpUnclamped(yankRodJerkPitch, 0f, returnT);
            localOffset = Vector3.LerpUnclamped(yankRodJerkLocalOffset, Vector3.zero, returnT);
        }
    }

    private bool IsRodAtStablePose()
    {
        if (rodRoot == null || !_hasStableRodBasePose)
            return true;

        bool posDone = (rodRoot.position - _stableRodBasePos).sqrMagnitude <= 0.000004f;
        bool rotDone = Quaternion.Angle(rodRoot.rotation, _stableRodBaseRot) <= 0.15f;
        return posDone && rotDone;
    }

    private void ClearRodActionMotionState()
    {
        _activeRodActionMotion = RodActionMotion.None;
        _rodActionMotionStartTime = -1f;
        _rodActionMotionDuration = 0f;
        _rodActionWasDriving = false;
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float inv = 1f - t;
        return 1f - (inv * inv * inv);
    }

    private static float EaseInCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
    }

    private float GetCastReleaseLeadTime()
    {
        if (!actionRodMotionEnabled)
            return 0f;

        float motionDuration = Mathf.Max(0.01f, castRodMotionDuration);
        return motionDuration * Mathf.Clamp01(castRodWindupPortion);
    }

    private Vector3 GetCastBobberWindupWorldOffset(Transform hangAnchor, Vector3 localOffset)
    {
        if (hangAnchor == null)
            return localOffset;

        Vector3 alongRod = Vector3.zero;
        if (rodRoot != null)
            alongRod = rodRoot.position - hangAnchor.position;
        else if (rodTip != null && rodTip != hangAnchor)
            alongRod = rodTip.position - hangAnchor.position;

        if (alongRod.sqrMagnitude < 0.0001f)
            alongRod = -hangAnchor.forward;
        alongRod.Normalize();

        Vector3 upAxis = Vector3.ProjectOnPlane(hangAnchor.up, alongRod);
        if (upAxis.sqrMagnitude < 0.0001f)
            upAxis = Vector3.up;
        upAxis.Normalize();

        Vector3 lateralAxis = Vector3.Cross(upAxis, alongRod);
        if (lateralAxis.sqrMagnitude < 0.0001f)
            lateralAxis = hangAnchor.right;
        lateralAxis.Normalize();

        return
            (lateralAxis * localOffset.x) +
            (upAxis * localOffset.y) +
            (alongRod * localOffset.z);
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

    private bool IsBeatmapPlaying()
    {
        RhythmMusicPlayer musicPlayer = RhythmConductor.rhythmMusicPlayer;
        if (musicPlayer == null)
        {
            return false;
        }

        musicPlayer.musicInstance.getPlaybackState(out PLAYBACK_STATE playbackState);
        return playbackState == PLAYBACK_STATE.PLAYING ||
               playbackState == PLAYBACK_STATE.STARTING ||
               playbackState == PLAYBACK_STATE.SUSTAINING;
    }
}
