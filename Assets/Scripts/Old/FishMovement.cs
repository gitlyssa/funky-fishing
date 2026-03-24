using System.IO;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Rigidbody))]
public class FishMovement : MonoBehaviour
{
    [Header("Roaming")]
    [SerializeField, Min(0.05f)] private float roamSpeed = 0.7f;
    [SerializeField, Min(0.1f)] private float turnSpeed = 6f;
    [SerializeField, Min(0.1f)] private float directionChangeInterval = 1.8f;
    [SerializeField, Min(0f)] private float pondEdgeBuffer = 0.75f;

    [Header("Bobber Interaction")]
    [SerializeField] private bool enableBobberInteraction = true;
    [SerializeField, Min(0.1f)] private float engageRadius = 2.2f;
    [SerializeField, Min(0.05f)] private float contactDistance = 0.4f;
    [SerializeField, Min(1)] private int nibblesPerCycle = 4;
    [SerializeField, Min(0.1f)] private float nibbleFrequency = 2.2f;
    [SerializeField, Min(0.01f)] private float nibbleAmplitude = 0.1f;
    [SerializeField, Min(0.1f)] private float approachSpeedMultiplier = 1.5f;
    [SerializeField, Min(0.1f)] private float nibbleMoveSpeedMultiplier = 1f;
    [SerializeField, Min(0.1f)] private float dartSpeedMultiplier = 2.8f;
    [SerializeField, Min(0.05f)] private float dartDuration = 0.75f;
    [SerializeField, Min(0.1f)] private float cooldownDuration = 2f;
    [SerializeField, Min(0.05f)] private float minimumPostDartDistance = 1.2f;
    [SerializeField, Range(0f, 1f)] private float dartDirectionRandomness = 0.15f;
    [SerializeField] private bool ignoreBobberCollisionDuringInteraction = true;

    [Header("Water Gate")]
    [SerializeField, Min(0f)] private float bobberInWaterMargin = 0.2f;
    [Header("Bobber Nibble Pull")]
    [SerializeField] private bool pullBobberDownOnNibble = true;
    [SerializeField] private float nibbleBobberVerticalOffset = 0.015f;
    [SerializeField, Min(0.1f)] private float nibbleBobberPullDownSpeed = 18f;
    [SerializeField, Min(0.1f)] private float nibbleBobberReturnSpeed = 10f;

    public PondManager pondManager;

    // Hard safety clamps so fish behavior stays stable even with odd inspector values.
    private const float MinRoamSpeed = 0.45f;
    private const float MinInteractionSpeed = 0.6f;
    private const float MaxApproachTime = 1.25f;
    private const float MinNibbleTime = 1.8f;
    private const float MaxNibbleTime = 3.0f;
    private const float MaxDartTime = 1.2f;
    private const float MinCooldownTime = 1.5f;
    private const float StuckDistanceThreshold = 0.003f;
    private const float StuckSpeedThreshold = 0.08f;
    private const float MaxStillTime = 1.35f;
    private const string NibbleIndicatorAssetPath = "Assets/Images/exclamation.png";
    private const string NibbleIndicatorResourcePath = "Fishing/exclamation";
    private const string NibbleIndicatorCanvasName = "FishNibbleIndicatorCanvas";
    private const float NibbleIndicatorScreenOffsetY = 72f;
    private const float NibbleIndicatorWorldOffsetY = 0.4f;
    private const float NibbleIndicatorSize = 72f;

    private Rigidbody rb;
    private Collider fishCollider;
    private Transform bobber;
    private Collider bobberCollider;
    private BobberArcCaster bobberArcCaster;

    private enum State
    {
        Roam,
        Approach,
        Nibble,
        DartAway,
        CooldownRoam
    }

    private State state = State.Roam;
    private Vector3 roamDirection;
    private Vector3 dartDirection;
    private Vector3 nibbleAxis;
    private float roamTimer;
    private float stateTimer;
    private float cooldownTimer;
    private float nibblePhase;
    private Vector3 lastPlanarPosition;
    private float stillTimer;
    private bool forcedPanicEscape;
    private float forcedPanicDuration;
    private float panicRetargetTimer;

    private static FishMovement s_nibblePullOwner;
    private static Transform s_nibblePullBobber;
    private static float s_nibblePullRestY;
    private static float s_nibblePullTargetY;
    private static float s_nibblePullDownSpeed;
    private static float s_nibblePullReturnSpeed;
    private static bool s_nibblePullActive;
    private static bool s_nibblePullRestorePending;
    private static FishMovement s_recentNibblePullOwner;
    private static Transform s_recentNibblePullBobber;
    private static Canvas s_nibbleIndicatorCanvas;
    private static RectTransform s_nibbleIndicatorRect;
    private static Image s_nibbleIndicatorImage;
    private static Sprite s_nibbleIndicatorSprite;
    private static Transform s_nibbleIndicatorBobber;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        fishCollider = GetComponent<Collider>();
        if (fishCollider == null)
            fishCollider = GetComponentInChildren<Collider>();

        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
    }

    private void Start()
    {
        if (pondManager == null)
            pondManager = FindObjectOfType<PondManager>();

        ResolveBobberReference();
        PickNewRoamDirection();
        EnterRoam();
        lastPlanarPosition = new Vector3(rb.position.x, 0f, rb.position.z);
        stillTimer = 0f;
    }

    private void OnDisable()
    {
        EndBobberNibblePull();
        SetBobberCollisionIgnored(false);
    }

    private void FixedUpdate()
    {
        ResolveBobberReference();

        UpdateStillWatchdog();

        if (!CanUseBobberInteraction() && !forcedPanicEscape)
        {
            if (state != State.Roam)
                EnterRoam();

            TickRoam(canApproach: false);
            return;
        }

        switch (state)
        {
            case State.Roam:
                TickRoam(canApproach: true);
                break;
            case State.Approach:
                TickApproach();
                break;
            case State.Nibble:
                TickNibble();
                break;
            case State.DartAway:
                TickDartAway();
                break;
            case State.CooldownRoam:
                TickCooldownRoam();
                break;
        }
    }

    private void LateUpdate()
    {
        if (s_nibblePullOwner == this && s_nibblePullActive)
            UpdateNibbleIndicatorScreenPosition();
    }

    private void TickRoam(bool canApproach)
    {
        SetBobberCollisionIgnored(false);

        roamTimer -= Time.fixedDeltaTime;
        if (roamTimer <= 0f)
            PickNewRoamDirection();

        roamDirection = KeepDirectionInsidePond(roamDirection);
        ApplyVelocity(roamDirection, roamSpeed);

        if (!canApproach || bobber == null)
            return;

        Vector3 toBobber = GetPlanarDirection(rb.position, bobber.position, out float distance);
        float engage = Mathf.Clamp(engageRadius, 0.25f, 4f);
        if (distance <= engage)
            EnterApproach(toBobber);
    }

    private void TickApproach()
    {
        if (bobber == null)
        {
            EnterRoam();
            return;
        }

        SetBobberCollisionIgnored(true);

        Vector3 toBobber = GetPlanarDirection(rb.position, bobber.position, out float distance);
        float engage = Mathf.Clamp(engageRadius, 0.25f, 4f);
        float contact = Mathf.Clamp(contactDistance, 0.08f, 0.85f);
        stateTimer += Time.fixedDeltaTime;

        if (stateTimer >= MaxApproachTime)
        {
            EnterNibble(toBobber);
            return;
        }

        if (distance > engage * 1.35f)
        {
            EnterRoam();
            return;
        }

        if (distance <= contact)
        {
            EnterNibble(toBobber);
            return;
        }

        float moveSpeed = Mathf.Max(MinInteractionSpeed, roamSpeed * Mathf.Clamp(approachSpeedMultiplier, 0.5f, 4f));
        ApplyVelocity(toBobber, moveSpeed, faceOverride: toBobber);
    }

    private void TickNibble()
    {
        if (bobber == null)
        {
            EnterDartAway(Vector3.zero);
            return;
        }

        SetBobberCollisionIgnored(true);

        Vector3 toBobber = GetPlanarDirection(rb.position, bobber.position, out _);
        if (toBobber.sqrMagnitude > 0.0001f)
            nibbleAxis = -toBobber;

        if (nibbleAxis.sqrMagnitude < 0.0001f)
            nibbleAxis = -transform.forward;
        if (nibbleAxis.sqrMagnitude < 0.0001f)
            nibbleAxis = Vector3.back;
        nibbleAxis.Normalize();

        float contact = Mathf.Clamp(contactDistance, 0.08f, 0.85f);
        float safeNibbleFrequency = Mathf.Clamp(nibbleFrequency, 0.8f, 3.2f);
        float safeNibbleAmplitude = Mathf.Clamp(nibbleAmplitude, 0.02f, 0.25f);
        nibblePhase += Time.fixedDeltaTime * safeNibbleFrequency * Mathf.PI * 2f;
        float offset = Mathf.Sin(nibblePhase) * safeNibbleAmplitude;

        Vector3 bobberPos = bobber.position;
        bobberPos.y = rb.position.y;

        UpdateBobberNibblePull();

        Vector3 targetPos = bobberPos + nibbleAxis * (contact + offset);
        Vector3 toTarget = targetPos - rb.position;
        toTarget.y = 0f;

        Vector3 faceTowardBobber = -nibbleAxis;
        ApplyVelocity(toTarget, roamSpeed * Mathf.Max(0.1f, nibbleMoveSpeedMultiplier), faceOverride: faceTowardBobber);

        stateTimer += Time.fixedDeltaTime;
        int nibbleCount = Mathf.Clamp(nibblesPerCycle, 3, 6);
        float cycleDuration = Mathf.Max(0.12f, nibbleCount / Mathf.Max(0.5f, safeNibbleFrequency));
        float requiredNibbleTime = Mathf.Clamp(Mathf.Max(MinNibbleTime, cycleDuration), MinNibbleTime, MaxNibbleTime);
        if (stateTimer >= requiredNibbleTime)
            EnterDartAway(toBobber);
    }

    private void TickDartAway()
    {
        SetBobberCollisionIgnored(true);
        stateTimer += Time.fixedDeltaTime;
        bool isForcedPanic = forcedPanicEscape;

        if (bobber != null)
        {
            Vector3 awayFromBobber = GetPlanarDirection(bobber.position, rb.position, out float distance);
            float minDist = Mathf.Max(0.05f, minimumPostDartDistance);
            if (distance < minDist && awayFromBobber.sqrMagnitude > 0.0001f)
                dartDirection = Vector3.Slerp(dartDirection, awayFromBobber, 0.4f).normalized;
        }

        if (isForcedPanic)
        {
            panicRetargetTimer -= Time.fixedDeltaTime;
            if (panicRetargetTimer <= 0f)
            {
                panicRetargetTimer = Random.Range(0.07f, 0.16f);

                Vector3 awayFromBobber = bobber != null
                    ? GetPlanarDirection(bobber.position, rb.position, out _)
                    : Vector3.zero;
                if (awayFromBobber.sqrMagnitude < 0.0001f)
                    awayFromBobber = dartDirection.sqrMagnitude > 0.0001f ? dartDirection : RandomPlanarDirection();

                float yaw = Random.Range(-130f, 130f);
                Vector3 panicDir = Quaternion.Euler(0f, yaw, 0f) * awayFromBobber;
                panicDir = KeepDirectionInsidePond(panicDir);
                if (panicDir.sqrMagnitude > 0.0001f)
                    dartDirection = Vector3.Slerp(dartDirection, panicDir.normalized, 0.9f).normalized;
            }
        }

        float speedMultiplier = Mathf.Clamp(dartSpeedMultiplier, 1f, 6f);
        if (isForcedPanic)
            speedMultiplier = Mathf.Clamp(speedMultiplier * 2.0f, 1f, 10f);

        float dartSpeed = Mathf.Max(MinInteractionSpeed * 1.6f, roamSpeed * speedMultiplier);
        ApplyVelocity(dartDirection, dartSpeed);

        if (isForcedPanic)
        {
            float panicDuration = Mathf.Max(0.2f, forcedPanicDuration);
            if (stateTimer >= panicDuration)
            {
                forcedPanicEscape = false;
                forcedPanicDuration = 0f;
                panicRetargetTimer = 0f;
                EnterCooldownRoam();
            }

            return;
        }

        bool timeDone = stateTimer >= Mathf.Clamp(dartDuration, 0.2f, 1.3f);
        bool farEnough = true;
        if (bobber != null)
        {
            GetPlanarDirection(bobber.position, rb.position, out float dist);
            farEnough = dist >= Mathf.Max(0.05f, minimumPostDartDistance);
        }

        if ((timeDone && farEnough) || stateTimer >= MaxDartTime)
            EnterCooldownRoam();
    }

    private void TickCooldownRoam()
    {
        SetBobberCollisionIgnored(false);
        cooldownTimer -= Time.fixedDeltaTime;

        roamTimer -= Time.fixedDeltaTime;
        if (roamTimer <= 0f)
            PickNewRoamDirection();

        roamDirection = KeepDirectionInsidePond(roamDirection);
        ApplyVelocity(roamDirection, roamSpeed);

        if (cooldownTimer <= 0f)
            EnterRoam();
    }

    private void EnterRoam()
    {
        EndBobberNibblePull();
        forcedPanicEscape = false;
        forcedPanicDuration = 0f;
        panicRetargetTimer = 0f;
        state = State.Roam;
        stateTimer = 0f;
        nibblePhase = 0f;
        SetBobberCollisionIgnored(false);
    }

    private void EnterApproach(Vector3 toBobber)
    {
        EndBobberNibblePull();
        forcedPanicEscape = false;
        forcedPanicDuration = 0f;
        panicRetargetTimer = 0f;
        state = State.Approach;
        stateTimer = 0f;
        if (toBobber.sqrMagnitude > 0.0001f)
        {
            float moveSpeed = Mathf.Max(MinInteractionSpeed, roamSpeed * Mathf.Clamp(approachSpeedMultiplier, 0.5f, 4f));
            ApplyVelocity(toBobber, moveSpeed, faceOverride: toBobber);
        }
        SetBobberCollisionIgnored(true);
    }

    private void EnterNibble(Vector3 toBobber)
    {
        forcedPanicEscape = false;
        forcedPanicDuration = 0f;
        panicRetargetTimer = 0f;
        state = State.Nibble;
        stateTimer = 0f;
        nibblePhase = 0f;
        nibbleAxis = toBobber.sqrMagnitude > 0.0001f ? -toBobber.normalized : -transform.forward;
        BeginBobberNibblePull();
        SetBobberCollisionIgnored(true);
    }

    private void EnterDartAway(Vector3 toBobber)
    {
        EndBobberNibblePull();
        state = State.DartAway;
        stateTimer = 0f;
        dartDirection = GetDartDirection(toBobber);
        SetBobberCollisionIgnored(true);
    }

    private void EnterCooldownRoam()
    {
        EndBobberNibblePull();
        state = State.CooldownRoam;
        stateTimer = 0f;
        cooldownTimer = Mathf.Clamp(cooldownDuration, MinCooldownTime, 6f);
        PickNewRoamDirection();
        SetBobberCollisionIgnored(false);
    }

    public void ForceDartAwayFrom(Transform danger)
    {
        Vector3 toDanger = Vector3.zero;
        if (danger != null)
            toDanger = GetPlanarDirection(rb.position, danger.position, out _);

        EnterDartAway(toDanger);
    }

    public void ForcePanicSwimAwayFrom(Transform danger, float durationSeconds = 1.6f)
    {
        forcedPanicEscape = true;
        forcedPanicDuration = Mathf.Clamp(durationSeconds, 0.2f, 4f);
        panicRetargetTimer = 0f;

        Vector3 toDanger = Vector3.zero;
        if (danger != null)
            toDanger = GetPlanarDirection(rb.position, danger.position, out _);

        EnterDartAway(toDanger);
    }

    private void PickNewRoamDirection()
    {
        Vector2 r = Random.insideUnitCircle;
        if (r.sqrMagnitude < 0.0001f)
            r = Vector2.right;
        roamDirection = new Vector3(r.x, 0f, r.y).normalized;
        roamTimer = Mathf.Max(0.1f, directionChangeInterval) * Random.Range(0.7f, 1.3f);
    }

    private Vector3 KeepDirectionInsidePond(Vector3 currentDirection)
    {
        if (pondManager == null)
            return currentDirection.sqrMagnitude < 0.0001f ? Vector3.forward : currentDirection.normalized;

        Vector3 center = pondManager.transform.position;
        Vector3 offset = rb.position - center;
        offset.y = 0f;
        float maxRadius = Mathf.Max(0.5f, pondManager.radius - Mathf.Max(0f, pondEdgeBuffer));

        if (offset.sqrMagnitude <= maxRadius * maxRadius)
            return currentDirection.sqrMagnitude < 0.0001f ? Vector3.forward : currentDirection.normalized;

        Vector3 inward = (-offset).normalized;
        Vector3 blended = Vector3.Slerp(currentDirection.sqrMagnitude < 0.0001f ? inward : currentDirection.normalized, inward, 0.35f);
        return blended.sqrMagnitude < 0.0001f ? inward : blended.normalized;
    }

    private Vector3 GetDartDirection(Vector3 toBobber)
    {
        Vector3 away = toBobber.sqrMagnitude > 0.0001f ? -toBobber.normalized : KeepDirectionInsidePond(-transform.forward);
        if (away.sqrMagnitude < 0.0001f)
            away = Vector3.back;

        float maxYaw = Mathf.Lerp(0f, 60f, Mathf.Clamp01(dartDirectionRandomness));
        float yaw = Random.Range(-maxYaw, maxYaw);
        Vector3 dart = Quaternion.Euler(0f, yaw, 0f) * away;
        dart.y = 0f;
        if (dart.sqrMagnitude < 0.0001f)
            dart = away;
        return dart.normalized;
    }

    private void ApplyVelocity(Vector3 direction, float speed, Vector3? faceOverride = null)
    {
        Vector3 dir = direction;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            dir = roamDirection.sqrMagnitude > 0.0001f ? roamDirection : Vector3.forward;
        dir.Normalize();

        rb.linearVelocity = dir * Mathf.Max(MinRoamSpeed, speed);

        Vector3 faceDir = faceOverride ?? dir;
        faceDir.y = 0f;
        if (faceDir.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRot = Quaternion.LookRotation(faceDir.normalized, Vector3.up);
        float k = 1f - Mathf.Exp(-Mathf.Max(0.1f, turnSpeed) * Time.fixedDeltaTime);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, k));
    }

    private static Vector3 GetPlanarDirection(Vector3 from, Vector3 to, out float distance)
    {
        Vector3 d = to - from;
        d.y = 0f;
        distance = d.magnitude;
        if (distance <= 0.0001f)
            return Vector3.zero;
        return d / distance;
    }

    private bool CanUseBobberInteraction()
    {
        if (!enableBobberInteraction)
            return false;

        if (bobber == null)
            return false;

        if (bobberArcCaster == null)
            bobberArcCaster = FindObjectOfType<BobberArcCaster>();

        if (bobberArcCaster != null)
            return bobberArcCaster.CurrentState == BobberArcCaster.State.Landed;

        float waterLevel = pondManager != null ? pondManager.waterlevel : bobber.position.y;
        return bobber.position.y <= waterLevel + Mathf.Max(0f, bobberInWaterMargin);
    }

    private void UpdateStillWatchdog()
    {
        Vector3 currentPlanar = new Vector3(rb.position.x, 0f, rb.position.z);
        float moved = Vector3.Distance(currentPlanar, lastPlanarPosition);
        lastPlanarPosition = currentPlanar;

        // Only run anti-stuck logic when fish is in/near bobber interaction.
        float engage = Mathf.Clamp(engageRadius, 0.25f, 4f);
        bool nearBobber = false;
        if (bobber != null)
        {
            GetPlanarDirection(rb.position, bobber.position, out float bobberDist);
            nearBobber = bobberDist <= engage * 1.4f;
        }

        bool inInteractionState =
            state == State.Approach ||
            state == State.Nibble ||
            state == State.DartAway;

        if (!inInteractionState && !nearBobber)
        {
            stillTimer = 0f;
            return;
        }

        Vector3 planarVelocity = rb.linearVelocity;
        planarVelocity.y = 0f;
        float speed = planarVelocity.magnitude;

        if (moved < StuckDistanceThreshold && speed < StuckSpeedThreshold)
            stillTimer += Time.fixedDeltaTime;
        else
            stillTimer = 0f;

        if (stillTimer < MaxStillTime)
            return;

        stillTimer = 0f;
        Vector3 escapeDir = roamDirection.sqrMagnitude > 0.0001f ? roamDirection : RandomPlanarDirection();
        if (bobber != null)
        {
            Vector3 away = GetPlanarDirection(bobber.position, rb.position, out _);
            if (away.sqrMagnitude > 0.0001f)
                escapeDir = away;
        }

        rb.MovePosition(rb.position + (escapeDir.normalized * 0.18f));
        if (CanUseBobberInteraction() && inInteractionState)
        {
            if (state == State.Approach)
            {
                Vector3 toBobber = bobber != null ? GetPlanarDirection(rb.position, bobber.position, out _) : Vector3.zero;
                EnterNibble(toBobber);
            }
            else
            {
                EnterDartAway(-escapeDir);
            }
        }
        else
        {
            EnterRoam();
        }
    }

    private static Vector3 RandomPlanarDirection()
    {
        Vector2 r = Random.insideUnitCircle;
        if (r.sqrMagnitude < 0.0001f)
            r = Vector2.right;
        return new Vector3(r.x, 0f, r.y).normalized;
    }

    private void ResolveBobberReference()
    {
        if (pondManager == null)
            pondManager = FindObjectOfType<PondManager>();

        if (pondManager != null && pondManager.playerBobber != null)
            bobber = pondManager.playerBobber.transform;

        if (bobber != null && bobberCollider == null)
            bobberCollider = bobber.GetComponent<Collider>();
    }

    private void BeginBobberNibblePull()
    {
        if (!pullBobberDownOnNibble || bobber == null)
            return;

        if (s_nibblePullOwner != null && s_nibblePullOwner != this)
            return;

        if (s_nibblePullOwner != this)
            s_nibblePullRestY = bobber.position.y;

        s_nibblePullOwner = this;
        s_nibblePullBobber = bobber;
        s_nibblePullDownSpeed = Mathf.Max(0.1f, nibbleBobberPullDownSpeed);
        s_nibblePullReturnSpeed = Mathf.Max(0.1f, nibbleBobberReturnSpeed);
        s_nibblePullActive = true;
        s_recentNibblePullOwner = this;
        s_recentNibblePullBobber = bobber;

        FunkyAudioSettings.PlayOneShot("event:/Sfx/bite", transform.position, FunkyAudioCategory.Sfx);

        s_nibblePullRestorePending = false;
        ShowNibbleIndicator(bobber);
        UpdateBobberNibblePull();
    }

    private void UpdateBobberNibblePull()
    {
        if (!pullBobberDownOnNibble || bobber == null)
            return;

        if (s_nibblePullOwner != this || s_nibblePullBobber != bobber)
            return;

        s_nibblePullTargetY = rb.position.y + nibbleBobberVerticalOffset;
        s_nibblePullActive = true;
        s_recentNibblePullOwner = this;
        s_recentNibblePullBobber = bobber;
    }

    private void EndBobberNibblePull()
    {
        if (s_nibblePullOwner != this)
            return;

        HideNibbleIndicator();
        s_recentNibblePullOwner = this;
        s_recentNibblePullBobber = bobber;
        s_nibblePullOwner = null;
        s_nibblePullActive = false;
        s_nibblePullRestorePending = s_nibblePullBobber != null;
    }

    public static bool TryGetHookableNibbleFish(
        Transform bobberTransform,
        out GameObject fish,
        out bool fromActiveNibble)
    {
        fish = null;
        fromActiveNibble = false;

        if (bobberTransform == null)
            return false;

        FishMovement owner = null;
        if (s_nibblePullActive && s_nibblePullOwner != null && s_nibblePullBobber == bobberTransform)
        {
            owner = s_nibblePullOwner;
            fromActiveNibble = true;
        }
        else if (s_nibblePullRestorePending && s_nibblePullBobber == bobberTransform)
        {
            // Match the hook window to the bobber's physical return phase.
            if (Mathf.Abs(bobberTransform.position.y - s_nibblePullRestY) <= 0.01f)
            {
                s_nibblePullRestorePending = false;
                s_nibblePullBobber = null;
            }
            else
            {
                owner = s_recentNibblePullOwner;
            }
        }

        if (owner == null)
            return false;

        HideNibbleIndicator();
        fish = owner.gameObject;
        return fish != null && fish.activeInHierarchy;
    }

    public static bool TryGetBobberNibbleVerticalOverride(Transform bobberTransform, out float targetY, out float followSpeed)
    {
        targetY = 0f;
        followSpeed = 0f;

        if (bobberTransform == null)
            return false;

        if (s_nibblePullBobber == null)
        {
            s_nibblePullOwner = null;
            s_nibblePullRestorePending = false;
            s_nibblePullActive = false;
            return false;
        }

        if (s_nibblePullBobber != bobberTransform)
            return false;

        if (s_nibblePullActive)
        {
            targetY = s_nibblePullTargetY;
            followSpeed = Mathf.Max(0.1f, s_nibblePullDownSpeed);
            return true;
        }

        if (s_nibblePullRestorePending)
        {
            targetY = s_nibblePullRestY;
            followSpeed = Mathf.Max(0.1f, s_nibblePullReturnSpeed);

            if (Mathf.Abs(bobberTransform.position.y - s_nibblePullRestY) <= 0.01f)
            {
                s_nibblePullRestorePending = false;
                s_nibblePullBobber = null;
            }

            return true;
        }

        return false;
    }

    public static void ClearBobberNibbleVerticalOverride(Transform bobberTransform)
    {
        if (bobberTransform == null)
            return;

        if (s_nibblePullBobber != bobberTransform)
            return;

        HideNibbleIndicator();
        s_nibblePullOwner = null;
        s_nibblePullBobber = null;
        s_nibblePullActive = false;
        s_nibblePullRestorePending = false;

        if (s_recentNibblePullBobber == bobberTransform)
        {
            s_recentNibblePullOwner = null;
            s_recentNibblePullBobber = null;
        }
    }

    private void SetBobberCollisionIgnored(bool ignore)
    {
        if (!ignoreBobberCollisionDuringInteraction)
            return;

        if (fishCollider == null || bobberCollider == null)
            return;

        Physics.IgnoreCollision(fishCollider, bobberCollider, ignore);
    }

    private static void ShowNibbleIndicator(Transform bobberTransform)
    {
        if (bobberTransform == null)
            return;

        if (!EnsureNibbleIndicatorUi())
            return;

        s_nibbleIndicatorBobber = bobberTransform;
        s_nibbleIndicatorImage.enabled = true;
        s_nibbleIndicatorImage.sprite = s_nibbleIndicatorSprite;
        s_nibbleIndicatorRect.sizeDelta = new Vector2(NibbleIndicatorSize, NibbleIndicatorSize);
        UpdateNibbleIndicatorScreenPosition();
    }

    private static void HideNibbleIndicator()
    {
        s_nibbleIndicatorBobber = null;

        if (s_nibbleIndicatorImage != null)
            s_nibbleIndicatorImage.enabled = false;
    }

    private static void UpdateNibbleIndicatorScreenPosition()
    {
        if (s_nibbleIndicatorBobber == null || s_nibbleIndicatorRect == null || s_nibbleIndicatorImage == null)
            return;

        Camera cameraToUse = Camera.main;
        if (cameraToUse == null)
        {
            s_nibbleIndicatorImage.enabled = false;
            return;
        }

        Vector3 worldPos = s_nibbleIndicatorBobber.position + (Vector3.up * NibbleIndicatorWorldOffsetY);
        Vector3 screenPos = cameraToUse.WorldToScreenPoint(worldPos);
        bool visible = screenPos.z > 0f;
        s_nibbleIndicatorImage.enabled = visible;
        if (!visible)
            return;

        screenPos.y += NibbleIndicatorScreenOffsetY;
        s_nibbleIndicatorRect.position = screenPos;
    }

    private static bool EnsureNibbleIndicatorUi()
    {
        if (s_nibbleIndicatorImage != null && s_nibbleIndicatorRect != null)
            return true;

        if (s_nibbleIndicatorSprite == null)
            s_nibbleIndicatorSprite = LoadNibbleIndicatorSprite();

        if (s_nibbleIndicatorSprite == null)
        {
            Debug.LogWarning(
                "Could not load nibble indicator sprite. " +
                "Expected exclamation image at Assets/Images/exclamation.png or Resources/Fishing/exclamation.");
            return false;
        }

        GameObject canvasGo = new GameObject(
            NibbleIndicatorCanvasName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));

        s_nibbleIndicatorCanvas = canvasGo.GetComponent<Canvas>();
        s_nibbleIndicatorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        s_nibbleIndicatorCanvas.sortingOrder = 1200;
        s_nibbleIndicatorCanvas.overrideSorting = true;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject indicatorGo = new GameObject("NibbleIndicator", typeof(RectTransform), typeof(Image));
        indicatorGo.transform.SetParent(canvasGo.transform, false);
        s_nibbleIndicatorRect = indicatorGo.GetComponent<RectTransform>();
        s_nibbleIndicatorRect.anchorMin = new Vector2(0.5f, 0.5f);
        s_nibbleIndicatorRect.anchorMax = new Vector2(0.5f, 0.5f);
        s_nibbleIndicatorRect.pivot = new Vector2(0.5f, 0.5f);
        s_nibbleIndicatorRect.sizeDelta = new Vector2(NibbleIndicatorSize, NibbleIndicatorSize);

        s_nibbleIndicatorImage = indicatorGo.GetComponent<Image>();
        s_nibbleIndicatorImage.raycastTarget = false;
        s_nibbleIndicatorImage.preserveAspect = true;
        s_nibbleIndicatorImage.sprite = s_nibbleIndicatorSprite;
        s_nibbleIndicatorImage.enabled = false;
        return true;
    }

    private static Sprite LoadNibbleIndicatorSprite()
    {
        Sprite sprite = Resources.Load<Sprite>(NibbleIndicatorResourcePath);
        if (sprite != null)
            return sprite;

#if UNITY_EDITOR
        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(NibbleIndicatorAssetPath);
        if (sprite != null)
            return sprite;
#endif

        string absolutePath = Path.Combine(Application.dataPath, "Images", "exclamation.png");
        if (!File.Exists(absolutePath))
            return null;

        byte[] fileBytes = File.ReadAllBytes(absolutePath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
        if (!texture.LoadImage(fileBytes))
            return null;

        texture.name = "FishNibbleIndicatorTexture";
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.contactCount <= 0)
            return;

        Vector3 reflected = Vector3.Reflect(rb.linearVelocity.normalized, collision.GetContact(0).normal);
        reflected.y = 0f;
        if (reflected.sqrMagnitude < 0.0001f)
        {
            Vector2 r = Random.insideUnitCircle.normalized;
            reflected = new Vector3(r.x, 0f, r.y);
        }

        roamDirection = reflected.normalized;
        if (state == State.DartAway)
            dartDirection = roamDirection;
    }
}
