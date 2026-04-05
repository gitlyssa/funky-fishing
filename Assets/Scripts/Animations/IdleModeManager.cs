using UnityEngine;
using UnityEngine.UI;

public class IdleModeManager : MonoBehaviour
{
    [Header("Idle Settings")]
    public float idleThreshold = 300f;
    private float _idleTimer = 0f;
    public bool IsIdling { get; private set; } = false;

    [Header("Dynamic UI Settings")]
    public Sprite titleCardSprite; 
    public Vector2 titleCardSize = new Vector2(1000f, 500f);
    public Vector2 titleCardPosition = new Vector2(0f, 100f);
    public float floatSpeed = 2f;
    public float floatAmplitude = 15f;

    [Header("Day/Night Cycle")]
    public float idleTimeSpeed = 0.8f; 
    private float _originalTimeSpeed;

    [Header("References")]
    public BobberArcCaster arcCaster;
    public PondManager pondManager;
    public FishingSessionHud fishingSessionHud;

    private Vector3 _lastMousePos;
    private Vector3 _lastTargetMarkerPos;
    private BobberArcCaster.State _lastBobberState;
    
    // Dynamic UI References
    private Canvas _idleCanvas;
    private Image _titleCardImage;
    private RectTransform _titleCardRect;

    

    void Start()
    {
        _lastMousePos = Input.mousePosition;
        EnsureUi();
    }

    void Update()
    {
        // Use our new drift-proof activity checker
        if (HasGameplayActivity())
        {
            _idleTimer = 0f;
            _lastMousePos = Input.mousePosition;

            if (IsIdling)
            {
                ExitIdleMode();
            }
        }
        else
        {
            if (!IsIdling)
            {
                // This correctly pauses the timer if they are in the rhythm/tension section!
                if (CanEnterIdleMode())
                {
                    _idleTimer += Time.deltaTime;
                    if (_idleTimer >= idleThreshold)
                    {
                        EnterIdleMode();
                    }
                }
                else
                {
                    _idleTimer = 0f; 
                }
            }
            else
            {
                AnimateTitleCard();
            }
        }
    }

    private bool HasGameplayActivity()
    {
        bool activity = false;

        if (Input.anyKey || Input.mousePosition != _lastMousePos || Input.GetAxis("Mouse ScrollWheel") != 0f)
            activity = true;

        for (int i = 0; i < 4; i++)
        {
            if (JSL.JslStillConnected(i))
            {
                JSL.JOY_SHOCK_STATE state = JSL.JslGetSimpleState(i);
                if (state.buttons != 0) activity = true;
            }
        }

        if (arcCaster != null)
        {
        
            if (arcCaster.targetMarker != null)
            {
                if (Vector3.Distance(_lastTargetMarkerPos, arcCaster.targetMarker.position) > 0.001f)
                {
                    activity = true;
                }
                _lastTargetMarkerPos = arcCaster.targetMarker.position;
            }

            if (arcCaster.CurrentState != _lastBobberState)
            {
                activity = true;
            }
            _lastBobberState = arcCaster.CurrentState;
        }

        return activity;
    }

    private void EnsureUi()
    {
        if (_titleCardImage != null) return;

        _idleCanvas = GetOrCreateIdleCanvas();

        if (titleCardSprite != null)
        {
            _titleCardImage = CreateImage(
                "IdleTitleCardImage",
                _idleCanvas.transform,
                new Vector2(0.5f, 0.5f), // Anchor Min (Center)
                new Vector2(0.5f, 0.5f), // Anchor Max (Center)
                new Vector2(0.5f, 0.5f), // Pivot (Center)
                titleCardPosition,
                titleCardSize,
                titleCardSprite);

            _titleCardRect = _titleCardImage.rectTransform;
            _titleCardImage.gameObject.SetActive(false); // Hide until idle
        }
        else
        {
            Debug.LogWarning("IdleModeManager: No Title Card Sprite assigned in the inspector!");
        }
    }

    private Canvas GetOrCreateIdleCanvas()
    {
        Transform existing = transform.Find("IdleModeCanvas");
        if (existing != null)
        {
            Canvas existingCanvas = existing.GetComponent<Canvas>();
            if (existingCanvas != null) return existingCanvas;
        }

        GameObject canvasGo = new GameObject(
            "IdleModeCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000; // Put it above absolutely everything

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private Image CreateImage(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size,
        Sprite sprite)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            Image existingImage = existing.GetComponent<Image>();
            if (existingImage != null) return existingImage;
        }

        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;

        return image;
    }

    private bool HasAnyInput()
    {
        if (Input.anyKey || Input.mousePosition != _lastMousePos || Input.GetAxis("Mouse ScrollWheel") != 0f)
            return true;

        for (int i = 0; i < 4; i++)
        {
            if (JSL.JslStillConnected(i))
            {
                JSL.JOY_SHOCK_STATE state = JSL.JslGetSimpleState(i);
                if (state.buttons != 0) return true;
                
                if (Mathf.Abs(state.stickLX) > 0.1f || Mathf.Abs(state.stickLY) > 0.1f ||
                    Mathf.Abs(state.stickRX) > 0.1f || Mathf.Abs(state.stickRY) > 0.1f)
                    return true;
            }
        }
        
        return false;
    }

    private bool CanEnterIdleMode()
    {
        if (FishCatchAnimation.IsAnyCatchScreenActive) return false;
        if (arcCaster != null && arcCaster.CurrentState == BobberArcCaster.State.Tension) return false;

        return true;
    }

    private void EnterIdleMode()
    {
        IsIdling = true;

        if (_titleCardImage != null)
        {
            _titleCardImage.gameObject.SetActive(true);
            _titleCardRect.anchoredPosition = titleCardPosition;
        }

        if (GlobalLightingManager.Instance != null)
        {
            _originalTimeSpeed = GlobalLightingManager.Instance.timeSpeed;
            GlobalLightingManager.Instance.timeSpeed = idleTimeSpeed;
        }

        if (pondManager != null)
        {
            pondManager.ResetPondToDefault();
        }
        FishingSessionHud.ResetSessionForFreshPlay();
    
        
        if (arcCaster != null && arcCaster.CurrentState == BobberArcCaster.State.Landed)
        {
           arcCaster.ForceRetractBobber();
        }
        
    }

    private void ExitIdleMode()
    {
        IsIdling = false;
        _idleTimer = 0f;

        if (_titleCardImage != null)
        {
            _titleCardImage.gameObject.SetActive(false);
            _titleCardRect.anchoredPosition = titleCardPosition;
        }

        if (GlobalLightingManager.Instance != null)
        {
            GlobalLightingManager.Instance.timeSpeed = _originalTimeSpeed;
        }
    }

    private void AnimateTitleCard()
    {
        if (_titleCardRect == null) return;

        float newY = titleCardPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
        _titleCardRect.anchoredPosition = new Vector2(titleCardPosition.x, newY);
    }
}