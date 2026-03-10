using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GamepadMenuNavigation : MonoBehaviour
{
    [Header("Enabled Scenes")]
    [SerializeField] private string[] enabledSceneNames =
    {
        "MainMenu",
        "PondSelect",
        "ControllerMenu",
        "Pond_Level_1",
        "Tutorial_Level"
    };

    [Header("Navigation Input")]
    [SerializeField, Range(0.1f, 0.95f)] private float stickDeadzone = 0.55f;
    [SerializeField, Range(0.05f, 0.95f)] private float joyConStickDeadzone = 0.22f;
    [SerializeField, Min(0.01f)] private float firstRepeatDelay = 0.25f;
    [SerializeField, Min(0.01f)] private float repeatDelay = 0.12f;
    [SerializeField, Min(0.01f)] private float buttonRefreshInterval = 0.2f;

    [Header("Selection Dot")]
    [SerializeField] private string indicatorSymbol = "\u25CF";
    [SerializeField] private float indicatorFontSize = 34f;
    [SerializeField] private Color indicatorColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField, Range(0f, 0.5f)] private float indicatorLeftAnchor = 0.08f;
    [SerializeField, Range(-0.5f, 0.5f)] private float indicatorVerticalAnchorOffset = 0f;
    [SerializeField, Min(0.1f)] private float indicatorFollowSpeed = 20f;

    private readonly HashSet<string> enabledScenes = new HashSet<string>();
    private readonly List<Button> activeButtons = new List<Button>();

    private Canvas indicatorCanvas;
    private RectTransform indicatorRect;
    private TextMeshProUGUI indicatorText;
    private bool indicatorHasPosition;

    private Button currentButton;
    private Vector2Int heldDirection;
    private bool holdingDirection;
    private float nextMoveTime;
    private float nextButtonRefreshTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindObjectOfType<GamepadMenuNavigation>() != null)
            return;

        GameObject go = new GameObject("GamepadMenuNavigation");
        DontDestroyOnLoad(go);
        go.AddComponent<GamepadMenuNavigation>();
    }

    private void Awake()
    {
        for (int i = 0; i < enabledSceneNames.Length; i++)
        {
            string name = enabledSceneNames[i];
            if (!string.IsNullOrWhiteSpace(name))
                enabledScenes.Add(name);
        }

        BuildIndicatorCanvas();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        currentButton = null;
        holdingDirection = false;
        heldDirection = Vector2Int.zero;
        nextMoveTime = 0f;
        nextButtonRefreshTime = 0f;
        SetIndicatorVisible(false);
    }

    private void Update()
    {
        if (IsTutorialGateBlockingUiNavigation())
        {
            EventSystem tutorialEvt = EventSystem.current;
            if (tutorialEvt != null)
            {
                tutorialEvt.sendNavigationEvents = false;
                if (tutorialEvt.currentSelectedGameObject != null)
                    tutorialEvt.SetSelectedGameObject(null);
            }

            currentButton = null;
            SetIndicatorVisible(false);
            return;
        }

        if (!IsEnabledScene())
        {
            EventSystem inactiveEvt = EventSystem.current;
            if (inactiveEvt != null)
                inactiveEvt.sendNavigationEvents = true;

            SetIndicatorVisible(false);
            return;
        }

        EventSystem evt = EventSystem.current;
        if (evt == null)
        {
            SetIndicatorVisible(false);
            return;
        }

        Gamepad gamepad = Gamepad.current;
        bool joyConConnected = JoyConMenuInput.AnyConnected;
        if (gamepad == null && !joyConConnected)
        {
            // Keep mouse/keyboard behavior untouched when no gamepad is connected.
            evt.sendNavigationEvents = true;
            SetIndicatorVisible(false);
            return;
        }

        // Prevent built-in UI navigation from fighting this custom controller navigation.
        evt.sendNavigationEvents = false;

        bool shouldRefreshButtons =
            Time.unscaledTime >= nextButtonRefreshTime ||
            activeButtons.Count == 0 ||
            currentButton == null ||
            !activeButtons.Contains(currentButton);

        if (shouldRefreshButtons)
        {
            RefreshActiveButtons();
            nextButtonRefreshTime = Time.unscaledTime + Mathf.Max(0.01f, buttonRefreshInterval);
        }
        if (activeButtons.Count == 0)
        {
            currentButton = null;
            SetIndicatorVisible(false);
            return;
        }

        SyncCurrentSelection(evt);
        if (currentButton == null)
        {
            SetIndicatorVisible(false);
            return;
        }

        bool submitPressed =
            (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame) ||
            JoyConMenuInput.SubmitPressedThisFrame;

        HandleDirectionalNavigation(evt, gamepad, JoyConMenuInput.NavigationStick);
        HandleSubmit(evt, submitPressed);
        UpdateIndicator();
    }

    private bool IsEnabledScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (!enabledScenes.Contains(sceneName))
            return false;

        if (sceneName == "Pond_Level_1" || sceneName == "Tutorial_Level")
            return Time.timeScale <= 0f;

        return true;
    }

    private static bool IsTutorialGateBlockingUiNavigation()
    {
        return TutorialStartGate.IsOverlayGateActive() || RhythmTutorialCoach.IsOverlayGateActive();
    }

    private void RefreshActiveButtons()
    {
        activeButtons.Clear();

        Button[] allButtons = FindObjectsOfType<Button>(true);
        for (int i = 0; i < allButtons.Length; i++)
        {
            Button button = allButtons[i];
            if (button == null)
                continue;

            if (!button.gameObject.activeInHierarchy)
                continue;

            if (!button.IsInteractable())
                continue;

            activeButtons.Add(button);
        }

        activeButtons.Sort((a, b) =>
        {
            Vector2 pa = GetScreenCenter(a.transform as RectTransform);
            Vector2 pb = GetScreenCenter(b.transform as RectTransform);

            int yCompare = pb.y.CompareTo(pa.y); // top first
            if (yCompare != 0)
                return yCompare;

            return pa.x.CompareTo(pb.x); // left first
        });
    }

    private void SyncCurrentSelection(EventSystem evt)
    {
        if (evt.currentSelectedGameObject != null)
        {
            Button selectedButton = evt.currentSelectedGameObject.GetComponent<Button>();
            if (selectedButton != null && activeButtons.Contains(selectedButton))
                currentButton = selectedButton;
        }

        if (currentButton != null && activeButtons.Contains(currentButton))
            return;

        GameObject first = evt.firstSelectedGameObject;
        if (first != null)
        {
            Button firstButton = first.GetComponent<Button>();
            if (firstButton != null && activeButtons.Contains(firstButton))
            {
                SetCurrentButton(evt, firstButton);
                return;
            }
        }

        SetCurrentButton(evt, activeButtons[0]);
    }

    private void HandleDirectionalNavigation(EventSystem evt, Gamepad gamepad, Vector2 joyConStick)
    {
        Vector2Int direction = ReadMoveDirection(gamepad, joyConStick);
        if (direction == Vector2Int.zero)
        {
            holdingDirection = false;
            heldDirection = Vector2Int.zero;
            return;
        }

        bool triggerMove = false;
        float now = Time.unscaledTime;
        if (!holdingDirection || direction != heldDirection)
        {
            holdingDirection = true;
            heldDirection = direction;
            nextMoveTime = now + Mathf.Max(0.01f, firstRepeatDelay);
            triggerMove = true;
        }
        else if (now >= nextMoveTime)
        {
            nextMoveTime = now + Mathf.Max(0.01f, repeatDelay);
            triggerMove = true;
        }

        if (!triggerMove)
            return;

        Button next = FindNextButton(currentButton, direction);
        if (next != null)
            SetCurrentButton(evt, next);
    }

    private Vector2Int ReadMoveDirection(Gamepad gamepad, Vector2 joyConStick)
    {
        Vector2 raw = Vector2.zero;
        bool usingJoyConInput = false;
        if (gamepad != null)
        {
            Vector2 dpad = gamepad.dpad.ReadValue();
            Vector2 stick = gamepad.leftStick.ReadValue();
            raw = dpad.sqrMagnitude > 0.01f ? dpad : stick;
            if (raw.sqrMagnitude < (stickDeadzone * stickDeadzone))
            {
                raw = joyConStick;
                usingJoyConInput = true;
            }
        }
        else
        {
            raw = joyConStick;
            usingJoyConInput = true;
        }

        float deadzone = usingJoyConInput ? joyConStickDeadzone : stickDeadzone;
        if (raw.sqrMagnitude < (deadzone * deadzone))
            return Vector2Int.zero;

        if (Mathf.Abs(raw.x) > Mathf.Abs(raw.y))
            return raw.x > 0f ? Vector2Int.right : Vector2Int.left;

        return raw.y > 0f ? Vector2Int.up : Vector2Int.down;
    }

    private Button FindNextButton(Button from, Vector2Int direction)
    {
        if (from == null)
            return activeButtons.Count > 0 ? activeButtons[0] : null;

        RectTransform fromRect = from.transform as RectTransform;
        if (fromRect == null)
            return null;

        Vector2 fromPos = GetScreenCenter(fromRect);
        Vector2 dir = new Vector2(direction.x, direction.y).normalized;

        Button best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < activeButtons.Count; i++)
        {
            Button candidate = activeButtons[i];
            if (candidate == null || candidate == from)
                continue;

            RectTransform targetRect = candidate.transform as RectTransform;
            if (targetRect == null)
                continue;

            Vector2 delta = GetScreenCenter(targetRect) - fromPos;
            float distance = delta.magnitude;
            if (distance < 0.01f)
                continue;

            float primary = Vector2.Dot(delta, dir);
            if (primary <= 0f)
                continue;

            float alignment = Vector2.Dot(delta.normalized, dir);
            if (alignment < 0.35f)
                continue;

            float score = (alignment * 1000f) - distance;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best != null)
            return best;

        // Fallback to linear wrap if no directional match.
        int index = activeButtons.IndexOf(from);
        if (index < 0)
            return activeButtons.Count > 0 ? activeButtons[0] : null;

        bool forward = direction == Vector2Int.right || direction == Vector2Int.down;
        int nextIndex = forward ? (index + 1) % activeButtons.Count : (index - 1 + activeButtons.Count) % activeButtons.Count;
        return activeButtons[nextIndex];
    }

    private void HandleSubmit(EventSystem evt, bool submitPressed)
    {
        if (currentButton == null)
            return;

        if (!submitPressed)
            return;

        ExecuteEvents.Execute(currentButton.gameObject, new BaseEventData(evt), ExecuteEvents.submitHandler);
    }

    private void SetCurrentButton(EventSystem evt, Button button)
    {
        if (evt == null || button == null)
            return;

        currentButton = button;
        evt.SetSelectedGameObject(button.gameObject);
    }

    private Vector2 GetScreenCenter(RectTransform rect)
    {
        if (rect == null)
            return Vector2.zero;

        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        return RectTransformUtility.WorldToScreenPoint(cam, rect.TransformPoint(rect.rect.center));
    }

    private void UpdateIndicator()
    {
        if (currentButton == null)
        {
            SetIndicatorVisible(false);
            return;
        }

        RectTransform targetRect = currentButton.transform as RectTransform;
        if (targetRect == null)
        {
            SetIndicatorVisible(false);
            return;
        }

        if (!TryGetScreenBounds(targetRect, out Vector2 min, out Vector2 max))
        {
            SetIndicatorVisible(false);
            return;
        }

        float width = max.x - min.x;
        float height = max.y - min.y;
        float targetX = min.x + (width * indicatorLeftAnchor);
        float targetY = (min.y + (height * 0.5f)) + (height * indicatorVerticalAnchorOffset);
        Vector2 targetScreen = new Vector2(targetX, targetY);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                indicatorCanvas.transform as RectTransform,
                targetScreen,
                null,
                out Vector2 targetLocal))
        {
            SetIndicatorVisible(false);
            return;
        }

        SetIndicatorVisible(true);

        if (!indicatorHasPosition)
        {
            indicatorRect.anchoredPosition = targetLocal;
            indicatorHasPosition = true;
            return;
        }

        float k = 1f - Mathf.Exp(-Mathf.Max(0.1f, indicatorFollowSpeed) * Time.unscaledDeltaTime);
        indicatorRect.anchoredPosition = Vector2.Lerp(indicatorRect.anchoredPosition, targetLocal, k);
    }

    private bool TryGetScreenBounds(RectTransform rect, out Vector2 min, out Vector2 max)
    {
        min = Vector2.zero;
        max = Vector2.zero;
        if (rect == null)
            return false;

        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);

        Vector2 p0 = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 p1 = RectTransformUtility.WorldToScreenPoint(cam, corners[1]);
        Vector2 p2 = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
        Vector2 p3 = RectTransformUtility.WorldToScreenPoint(cam, corners[3]);

        min = Vector2.Min(Vector2.Min(p0, p1), Vector2.Min(p2, p3));
        max = Vector2.Max(Vector2.Max(p0, p1), Vector2.Max(p2, p3));
        return true;
    }

    private void BuildIndicatorCanvas()
    {
        GameObject canvasGo = new GameObject("GamepadMenuDotCanvas");
        canvasGo.transform.SetParent(transform, false);

        indicatorCanvas = canvasGo.AddComponent<Canvas>();
        indicatorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        indicatorCanvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>().enabled = false;

        GameObject dotGo = new GameObject("SelectionDot");
        dotGo.transform.SetParent(canvasGo.transform, false);

        indicatorRect = dotGo.AddComponent<RectTransform>();
        indicatorRect.anchorMin = new Vector2(0.5f, 0.5f);
        indicatorRect.anchorMax = new Vector2(0.5f, 0.5f);
        indicatorRect.pivot = new Vector2(0.5f, 0.5f);
        indicatorRect.sizeDelta = new Vector2(40f, 40f);

        indicatorText = dotGo.AddComponent<TextMeshProUGUI>();
        indicatorText.text = indicatorSymbol;
        indicatorText.fontSize = indicatorFontSize;
        indicatorText.color = indicatorColor;
        indicatorText.alignment = TextAlignmentOptions.Center;
        indicatorText.raycastTarget = false;

        SetIndicatorVisible(false);
    }

    private void SetIndicatorVisible(bool visible)
    {
        if (indicatorText != null)
            indicatorText.enabled = visible;

        if (!visible)
            indicatorHasPosition = false;
    }
}
