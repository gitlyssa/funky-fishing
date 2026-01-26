using UnityEngine;

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

    [Header("Input Mode")]
    public bool useJoyCon = false;

    [Header("Joy-Con Input Source")]
    public JslStickInput jslInput;

    [Header("Joy-Con Cursor (stick-driven)")]
    public float cursorSpeed = 1200f;     // pixels/sec at full stick
    public float worldMoveSpeed = 6f;     // units/sec on the water plane

    [Header("Raycast")]
    public float maxDistance = 100f;

    public Vector3 CurrentTargetPoint { get; private set; }
    public bool HasTarget { get; private set; }

    // Screen-space cursor in pixels
    public Vector2 CursorPixel { get; private set; }

    private bool _warnedMissingJsl;

    void Reset()
    {
        cam = Camera.main;
    }

    void Start()
    {
        if (!cam) cam = Camera.main;

        // start centered
        CursorPixel = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        if (useJoyCon)
        {
            InitializeTargetOnWater();
        }
    }

    void Update()
    {
        if (!cam) { HasTarget = false; return; }

        if (useJoyCon)
            UpdateTargetFromStick();
        else
        {
            UpdateCursorPixel();
            UpdateMarkerFromCursor();
        }
    }

    void UpdateCursorPixel()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            bool rightHeld = Mouse.current.rightButton.isPressed;
            bool rightDown = Mouse.current.rightButton.wasPressedThisFrame;
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
    }

    void UpdateMarkerFromCursor()
    {
        Ray ray = cam.ScreenPointToRay(CursorPixel);

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, waterMask))
        {
            HasTarget = true;
            CurrentTargetPoint = hit.point;

            if (castMarker)
            {
                castMarker.gameObject.SetActive(true);
                castMarker.position = hit.point + Vector3.up * 0.02f; // tiny lift
            }
        }
        else
        {
            if (TryClampToWaterEdge(ray, out Vector3 clampedPoint))
            {
                HasTarget = true;
                CurrentTargetPoint = clampedPoint;
                if (castMarker)
                {
                    castMarker.gameObject.SetActive(true);
                    castMarker.position = clampedPoint + Vector3.up * 0.02f; // tiny lift
                }
            }
            else
            {
                HasTarget = false;
                if (castMarker) castMarker.gameObject.SetActive(false);
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

    void UpdateCursorFromStick()
    {
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
                return;
            }
        }

        if (!jslInput.Connected) return;

        Vector2 stick = jslInput.Stick;
        if (stick == Vector2.zero) return;

        CursorPixel += stick * cursorSpeed * Time.deltaTime;
    }

    void UpdateTargetFromStick()
    {
        if (waterCollider == null)
        {
            UpdateCursorFromStick();
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
                return;
            }
        }

        if (!jslInput.Connected) return;

        Vector2 stick = jslInput.Stick;
        if (stick == Vector2.zero) return;

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

        if (castMarker)
        {
            castMarker.gameObject.SetActive(true);
            castMarker.position = newPoint + Vector3.up * 0.02f;
        }
    }

    void InitializeTargetOnWater()
    {
        if (waterCollider == null) return;

        CurrentTargetPoint = waterCollider.bounds.center;
        HasTarget = true;

        if (castMarker)
        {
            castMarker.gameObject.SetActive(true);
            castMarker.position = CurrentTargetPoint + Vector3.up * 0.02f;
        }
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
}
