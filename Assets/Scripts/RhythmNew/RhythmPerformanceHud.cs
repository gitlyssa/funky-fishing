using System.Collections.Generic;
using FMOD.Studio;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class RhythmPerformanceHud : MonoBehaviour
{
    [Header("HUD Toggle")]
    [SerializeField] private bool hudEnabled = true;
    [SerializeField] private bool judgementFeedbackOnlyEnabled = false;

    [Header("Points")]
    [SerializeField] private int perfectPoints = 100;
    [SerializeField] private int goodPoints = 70;
    [SerializeField] private int missPoints = 0;

    [Header("Judgement Text")]
    [SerializeField] private Vector2 judgementAnchoredPosition = new Vector2(0f, -120f);
    [SerializeField] private int judgementFontSize = 70;
    [SerializeField] private Sprite perfectJudgementSprite;
    [SerializeField] private Vector2 perfectJudgementImageSize = new Vector2(520f, 130f);
    [SerializeField] private Vector2 perfectJudgementImageOffset = new Vector2(0f, -30f);
    [SerializeField] private Sprite goodJudgementSprite;
    [SerializeField] private Vector2 goodJudgementImageSize = new Vector2(420f, 120f);
    [SerializeField] private Vector2 goodJudgementImageOffset = new Vector2(24f, -28f);
    [SerializeField] private Sprite missJudgementSprite;
    [SerializeField] private Vector2 missJudgementImageSize = new Vector2(420f, 120f);
    [SerializeField] private Vector2 missJudgementImageOffset = new Vector2(-20f, 0f);
    [SerializeField] private Color perfectColor = new Color(0.95f, 1f, 0.35f, 1f);
    [SerializeField] private Color goodColor = new Color(0.4f, 0.95f, 1f, 1f);
    [SerializeField] private Color missColor = new Color(1f, 0.45f, 0.45f, 1f);
    [SerializeField] private float judgementVisibleDuration = 0.12f;
    [SerializeField] private float judgementFadeDuration = 0.28f;
    [SerializeField] private float judgementPopReturnDuration = 0.12f;
    [SerializeField] private float judgementPopScale = 1.28f;
    [SerializeField] private float judgementRiseDistance = 26f;

    [Header("Combo Counter")]
    [SerializeField] private int comboShowThreshold = 2;
    [SerializeField] private Vector2 comboAnchoredPosition = new Vector2(0f, -200f);
    [SerializeField] private int comboFontSize = 88;
    [SerializeField] private Color comboColor = new Color(0.35f, 0.95f, 0.45f, 1f);
    [SerializeField] private string comboSuffix = " COMBO";
    [SerializeField] private float comboBaseScaleMin = 0.7f;
    [SerializeField] private float comboBaseScaleMax = 1.6f;
    [SerializeField] private int comboGrowthMaxCombo = 18;
    [SerializeField] private float comboPulseReturnDuration = 0.14f;
    [SerializeField] private float comboPulseScale = 1.34f;
    [SerializeField] private float comboPulseRiseDistance = 10f;

    [Header("Detailed Text")]
    [SerializeField] private Vector2 detailAnchoredPosition = new Vector2(-28f, -28f);
    [SerializeField] private int detailFontSize = 34;

    [Header("Timing Indicators")]
    [SerializeField] private Vector2 timingIndicatorSize = new Vector2(540f, 180f);
    [SerializeField] private int timingIndicatorFontSize = 88;
    [SerializeField] private Sprite earlyTimingIndicatorSprite;
    [SerializeField] private Rect earlyTimingIndicatorUvRect = new Rect(0.336f, 0.522f, 0.306f, 0.077f);
    [SerializeField] private Sprite lateTimingIndicatorSprite;
    [SerializeField] private Rect lateTimingIndicatorUvRect = new Rect(0.337f, 0.522f, 0.266f, 0.079f);
    [SerializeField] private Color timingIndicatorImageColor = Color.white;
    [SerializeField] private float timingIndicatorImageHeight = 75f;
    [SerializeField] private float timingIndicatorImageMaxWidth = 640f;
    [SerializeField] private float timingIndicatorImageUpInwardNudge = 60f;
    [SerializeField] private float timingIndicatorImageSideInwardNudge = 85f;
    [FormerlySerializedAs("timingIndicatorRadiusOffset")]
    [SerializeField] private float timingIndicatorScreenOffset = -42f;
    [SerializeField] private float timingIndicatorSideScreenOffset = -36f;
    [SerializeField] private float timingIndicatorScreenMargin = 24f;
    [SerializeField] private Color earlyTimingIndicatorColor = new Color(0.45f, 0.95f, 1f, 1f);
    [SerializeField] private Color lateTimingIndicatorColor = new Color(1f, 0.78f, 0.35f, 1f);
    [SerializeField] private float timingIndicatorVisibleDuration = 0.42f;
    [SerializeField] private float timingIndicatorFadeDuration = 0.45f;
    [SerializeField] private float timingIndicatorPopReturnDuration = 0.16f;
    [SerializeField] private float timingIndicatorPopScale = 1.12f;
    [SerializeField] private float timingIndicatorRiseDistance = 10f;

    private RhythmConductor _conductor;
    private RhythmJudge _judge;
    private RhythmMusicPlayer _musicPlayer;
    private Canvas _canvas;

    private TextMeshProUGUI _judgementText;
    private TextMeshProUGUI _comboText;
    private TextMeshProUGUI _detailText;
    private RectTransform _comboRect;
    private Vector2 _comboBasePosition;
    private float _comboCurrentBaseScale = 1f;
    private float _comboPulseTime = -1f;
    private bool _comboVisible;
    private RectTransform _judgementRect;
    private Vector2 _judgementBasePosition;
    private Color _judgementBaseColor;
    private float _judgementAnimTime = -1f;
    private Image _perfectJudgementImage;
    private RectTransform _perfectJudgementRect;
    private Image _goodJudgementImage;
    private RectTransform _goodJudgementRect;
    private Image _missJudgementImage;
    private RectTransform _missJudgementRect;
    private JudgementVisual _judgementVisual = JudgementVisual.Text;

    private readonly Dictionary<int, float> _trackedNotes = new Dictionary<int, float>();
    private readonly HashSet<int> _activeNoteIdsBuffer = new HashSet<int>();

    private bool _wasPlaybackActive;

    private int _perfectCount;
    private int _goodCount;
    private int _missCount;
    private int _combo;
    private int _maxCombo;
    private int _score;
    private int _baseScore;
    private float _lastFiredMultiplier = 1f;
    private float _maxPowerTimer = 0f; // New timer for the visual loop
    [SerializeField] private float maxPowerFlashInterval = 0.5f;

    [Header("Reel Status (Pulsing)")]
    [SerializeField] private Sprite reelStatusSprite;
    [SerializeField] private Vector2 reelStatusPosition = new Vector2(0f, 250f); // Higher up
    [SerializeField] private Vector2 reelStatusSize = new Vector2(300f, 100f);
    [SerializeField] private float reelPulseSpeed = 12f;
    [SerializeField] private float reelPulseAmount = 0.15f;

    [Header("Reel Judgement (Multiplier)")]
    [SerializeField] private Color multiplierColor = new Color(1f, 0.8f, 0.2f); // Golden/Orange
    [SerializeField] private string multiplierFormat = "{0:F1}x"; // Displays as 1.1x, 1.2x etc.


    [Header("Reel Progress Circle")]
    public GameObject timingRingPrefab; 
    public float reelArcThickness = 0.05f;

    [SerializeField] private Color reelArcColor = new Color(0.4f, 0.95f, 1f, 0.5f); // Transparent Sapphire
    private DynamicArc _currentReelArc;
    private GameObject _reelArcObj;


    private Image _reelStatusImage;
    private RectTransform _reelStatusRect;

    public int CurrentScore => _score;
    public float CurrentAccuracy {
        get {
            int totalJudged = _perfectCount + _goodCount + _missCount;
            return totalJudged > 0 ? ((_perfectCount * 1f) + (_goodCount * 0.7f)) / totalJudged * 100f : 0f;
        }
    }

    public int CurrentCombo => _combo;
    public int PerfectCount => _perfectCount;
    public int GoodCount => _goodCount;
    public int MissCount => _missCount;
    public int MaxCombo => _maxCombo;

    private enum ResultType
    {
        Perfect,
        Good,
        Miss
    }

    private enum JudgementVisual
    {
        Text,
        PerfectImage,
        GoodImage,
        MissImage
    }

    private sealed class TimingIndicatorState
    {
        public TextMeshProUGUI Text;
        public RectTransform TextRect;
        public RawImage Image;
        public RectTransform ImageRect;
        public Vector2 BasePosition;
        public Color BaseColor;
        public float AnimTime = -1f;
        public bool UsingImage;
    }

    private readonly Dictionary<FlickDirection, TimingIndicatorState> _timingIndicators =
        new Dictionary<FlickDirection, TimingIndicatorState>
        {
            { FlickDirection.Left, new TimingIndicatorState() },
            { FlickDirection.Right, new TimingIndicatorState() },
            { FlickDirection.Up, new TimingIndicatorState() }
        };

    private void Awake()
    {
        EnsureReferences();
        EnsureUi();
        ResetRunStats();
        RefreshDetailText();
        ApplyHudVisibility();
    }

    private void OnEnable()
    {
        RhythmJudge.OnDetailedNoteJudged += HandleDetailedNoteJudged;
    }

    private void OnDisable()
    {
        RhythmJudge.OnDetailedNoteJudged -= HandleDetailedNoteJudged;
    }

    private void OnValidate()
    {
        ApplyHudVisibility();
    }

    private void Update()
    {
        EnsureReferences();
        EnsureUi();

        if (!hudEnabled && !judgementFeedbackOnlyEnabled)
        {
            _wasPlaybackActive = false;
            _trackedNotes.Clear();
            HideJudgementImmediate();
            HideComboImmediate();
            HideAllTimingIndicatorsImmediate();
            return;
        }

        if (_conductor == null || _judge == null)
            return;

        RefreshTimingIndicatorBasePositions();

        bool playbackActive = IsRhythmPlaybackActive();
        bool isReeling = _conductor != null && _conductor.activeReel != null;

        if (playbackActive && !_wasPlaybackActive)
        {
            ResetRunStats();
            if (hudEnabled)
                ShowReadyJudgement();
            else
                HideJudgementImmediate();
            RefreshDetailText();
        }

        if (!playbackActive && _wasPlaybackActive)
        {
            _trackedNotes.Clear();
            HideComboImmediate();
        }

        _wasPlaybackActive = playbackActive;

        bool hasActiveFeedback = _judgementAnimTime >= 0f || _comboPulseTime >= 0f || HasActiveTimingIndicatorAnimation();
        if (!playbackActive && !isReeling && !hasActiveFeedback)
            return;

        TickJudgementAnimation();
        TickTimingIndicatorAnimations();
        TickComboPulseAnimation();
        TickReelVisuals();
    }

    private void EnsureReferences()
    {
        if (_conductor == null)
            _conductor = FindObjectOfType<RhythmConductor>();
        if (_judge == null)
            _judge = FindObjectOfType<RhythmJudge>();
        if (_musicPlayer == null)
            _musicPlayer = FindObjectOfType<RhythmMusicPlayer>();
    }

    private void EnsureUi()
    {
        if (_judgementText != null &&
            _detailText != null &&
            _comboText != null &&
            _missJudgementImage != null &&
            _perfectJudgementImage != null &&
            _goodJudgementImage != null &&
            TimingIndicatorsReady())
            return;

        _canvas = GetOrCreateHudCanvas();

        _comboText = CreateText(
            "ComboCounterText",
            _canvas.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            comboAnchoredPosition,
            new Vector2(700f, 140f),
            comboFontSize,
            TextAlignmentOptions.Center,
            string.Empty);

        _judgementText = CreateText(
            "JudgementText",
            _canvas.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            judgementAnchoredPosition,
            new Vector2(600f, 140f),
            judgementFontSize,
            TextAlignmentOptions.Center,
            "-");

        _perfectJudgementImage = CreateImage(
            "PerfectJudgementImage",
            _canvas.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            judgementAnchoredPosition,
            perfectJudgementImageSize,
            perfectJudgementSprite);

        _goodJudgementImage = CreateImage(
            "GoodJudgementImage",
            _canvas.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            judgementAnchoredPosition,
            goodJudgementImageSize,
            goodJudgementSprite);

        _missJudgementImage = CreateImage(
            "MissJudgementImage",
            _canvas.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            judgementAnchoredPosition,
            missJudgementImageSize,
            missJudgementSprite);

        _detailText = CreateText(
            "DetailedScoreText",
            _canvas.transform,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            detailAnchoredPosition,
            new Vector2(620f, 420f),
            detailFontSize,
            TextAlignmentOptions.TopRight,
            string.Empty);

        EnsureTimingIndicator(FlickDirection.Left, "LeftTimingIndicator");
        EnsureTimingIndicator(FlickDirection.Right, "RightTimingIndicator");
        EnsureTimingIndicator(FlickDirection.Up, "UpTimingIndicator");

        _reelStatusImage = CreateImage(
        "ReelStatusImage",
        _canvas.transform,
        new Vector2(0.5f, 0.5f),
        new Vector2(0.5f, 0.5f),
        new Vector2(0.5f, 0.5f),
        reelStatusPosition,
        reelStatusSize,
        reelStatusSprite);

        _reelStatusRect = _reelStatusImage.rectTransform;
        _reelStatusImage.color = new Color(1, 1, 1, 0);
            

        _comboText.gameObject.layer = _canvas.gameObject.layer;
        _judgementText.gameObject.layer = _canvas.gameObject.layer;
        _perfectJudgementImage.gameObject.layer = _canvas.gameObject.layer;
        _goodJudgementImage.gameObject.layer = _canvas.gameObject.layer;
        _missJudgementImage.gameObject.layer = _canvas.gameObject.layer;
        _detailText.gameObject.layer = _canvas.gameObject.layer;
        _comboRect = _comboText.rectTransform;
        Vector2 resolvedComboPosition = comboAnchoredPosition;
        float minBelowJudgementY = judgementAnchoredPosition.y - Mathf.Max(52f, judgementFontSize * 0.9f);
        if (resolvedComboPosition.y > minBelowJudgementY)
            resolvedComboPosition.y = minBelowJudgementY;
        _comboRect.anchoredPosition = resolvedComboPosition;
        _comboBasePosition = resolvedComboPosition;
        _judgementRect = _judgementText.rectTransform;
        _perfectJudgementRect = _perfectJudgementImage.rectTransform;
        _goodJudgementRect = _goodJudgementImage.rectTransform;
        _missJudgementRect = _missJudgementImage.rectTransform;
        _judgementBasePosition = _judgementRect.anchoredPosition;
        if (_perfectJudgementRect != null)
            _perfectJudgementRect.anchoredPosition = _judgementBasePosition + perfectJudgementImageOffset;
        if (_goodJudgementRect != null)
            _goodJudgementRect.anchoredPosition = _judgementBasePosition + goodJudgementImageOffset;
        if (_missJudgementRect != null)
            _missJudgementRect.anchoredPosition = _judgementBasePosition + missJudgementImageOffset;

        Color perfectImageColor = _perfectJudgementImage.color;
        perfectImageColor.a = 0f;
        _perfectJudgementImage.color = perfectImageColor;

        Color goodImageColor = _goodJudgementImage.color;
        goodImageColor.a = 0f;
        _goodJudgementImage.color = goodImageColor;

        Color imageColor = _missJudgementImage.color;
        imageColor.a = 0f;
        _missJudgementImage.color = imageColor;

        RefreshTimingIndicatorBasePositions(true);
        HideComboImmediate();
        HideJudgementImmediate();
        HideAllTimingIndicatorsImmediate();

        ApplyHudVisibility();
    }

    private Canvas GetOrCreateHudCanvas()
    {
        Transform existing = transform.Find("RhythmPerformanceCanvas");
        if (existing != null)
        {
            Canvas existingCanvas = existing.GetComponent<Canvas>();
            if (existingCanvas != null)
                return existingCanvas;
        }

        GameObject canvasGo = new GameObject(
            "RhythmPerformanceCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform rect = canvasGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return canvas;
    }

    private TextMeshProUGUI CreateText(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size,
        int fontSize,
        TextAlignmentOptions alignment,
        string initialText)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            TextMeshProUGUI existingText = existing.GetComponent<TextMeshProUGUI>();
            if (existingText != null)
                return existingText;
        }

        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = initialText;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.raycastTarget = false;

        return text;
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
            if (existingImage != null)
                return existingImage;
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

    private RawImage CreateRawImage(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size,
        Texture texture,
        Rect uvRect)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            RawImage existingImage = existing.GetComponent<RawImage>();
            if (existingImage != null)
                return existingImage;
        }

        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        RawImage image = go.GetComponent<RawImage>();
        image.texture = texture;
        image.uvRect = uvRect;
        image.raycastTarget = false;

        return image;
    }

    private bool TimingIndicatorsReady()
    {
        foreach (TimingIndicatorState state in _timingIndicators.Values)
        {
            if (state.Text == null || state.TextRect == null || state.Image == null || state.ImageRect == null)
                return false;
        }

        return true;
    }

    private void EnsureTimingIndicator(FlickDirection direction, string objectName)
    {
        if (!_timingIndicators.TryGetValue(direction, out TimingIndicatorState state))
            return;

        state.Text = CreateText(
            objectName,
            _canvas.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            GetTimingIndicatorPivot(direction),
            Vector2.zero,
            timingIndicatorSize,
            timingIndicatorFontSize,
            GetTimingIndicatorAlignment(direction),
            string.Empty);
        state.TextRect = state.Text.rectTransform;
        state.Text.alignment = GetTimingIndicatorAlignment(direction);
        state.Text.gameObject.layer = _canvas.gameObject.layer;
        Color color = state.Text.color;
        color.a = 0f;
        state.Text.color = color;

        state.Image = CreateRawImage(
            objectName + "Image",
            _canvas.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            GetTimingIndicatorPivot(direction),
            Vector2.zero,
            new Vector2(timingIndicatorImageMaxWidth, timingIndicatorImageHeight),
            GetDefaultTimingIndicatorTexture(),
            new Rect(0f, 0f, 1f, 1f));
        state.ImageRect = state.Image.rectTransform;
        state.Image.gameObject.layer = _canvas.gameObject.layer;
        Color imageColor = state.Image.color;
        imageColor.a = 0f;
        state.Image.color = imageColor;
        state.UsingImage = false;
    }

    private void RefreshTimingIndicatorBasePositions(bool snapToBase = false)
    {
        foreach (KeyValuePair<FlickDirection, TimingIndicatorState> entry in _timingIndicators)
        {
            TimingIndicatorState state = entry.Value;
            RectTransform activeRect = GetTimingIndicatorRect(state);
            if (activeRect == null)
                continue;

            if (!TryGetTimingIndicatorAnchoredPosition(entry.Key, activeRect, out Vector2 anchoredPosition))
                continue;

            anchoredPosition = ApplyTimingIndicatorImageInwardNudge(entry.Key, state, anchoredPosition);
            state.BasePosition = anchoredPosition;
            if (snapToBase || state.AnimTime < 0f)
                ApplyTimingIndicatorPosition(state, anchoredPosition);
        }
    }

    private bool TryGetTimingIndicatorAnchoredPosition(
        FlickDirection direction,
        RectTransform indicatorRect,
        out Vector2 anchoredPosition)
    {
        anchoredPosition = Vector2.zero;
        if (_canvas == null || _conductor == null || indicatorRect == null)
            return false;

        RectTransform canvasRect = _canvas.transform as RectTransform;
        if (canvasRect == null)
            return false;

        Camera projectionCamera = GetProjectionCamera();
        if (projectionCamera == null)
            return false;

        Camera canvasCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : projectionCamera;
        Vector3 centerWorldPosition = _conductor.transform.position;
        Vector3 hitWorldPosition = centerWorldPosition +
            (Vector3)(GetDirectionVector(direction) * _conductor.hitRingRadius);
        Vector2 centerScreenPoint = RectTransformUtility.WorldToScreenPoint(projectionCamera, centerWorldPosition);
        Vector2 hitScreenPoint = RectTransformUtility.WorldToScreenPoint(projectionCamera, hitWorldPosition);
        Vector2 screenDirection = hitScreenPoint - centerScreenPoint;
        if (screenDirection.sqrMagnitude < 0.001f)
            screenDirection = GetDirectionVector(direction);
        else
            screenDirection.Normalize();

        float directionalScreenOffset = GetTimingIndicatorDirectionalScreenOffset(direction);
        Vector2 indicatorScreenPoint = hitScreenPoint + (screenDirection * directionalScreenOffset);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            indicatorScreenPoint,
            canvasCamera,
            out anchoredPosition))
        {
            return false;
        }

        anchoredPosition = ClampTimingIndicatorPosition(canvasRect, indicatorRect, anchoredPosition);
        return true;
    }

    private Camera GetProjectionCamera()
    {
        if (_canvas != null && _canvas.worldCamera != null)
            return _canvas.worldCamera;

        Camera sceneCamera = FindCameraInScene(GetReferenceScene());
        if (sceneCamera != null)
            return sceneCamera;

        return Camera.main;
    }

    private Scene GetReferenceScene()
    {
        if (_conductor != null)
            return _conductor.gameObject.scene;

        return gameObject.scene;
    }

    private static Camera FindCameraInScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == null)
                continue;

            Camera[] cameras = roots[i].GetComponentsInChildren<Camera>(true);
            for (int j = 0; j < cameras.Length; j++)
            {
                Camera camera = cameras[j];
                if (camera != null && camera.isActiveAndEnabled)
                    return camera;
            }
        }

        return null;
    }

    private float GetTimingIndicatorDirectionalScreenOffset(FlickDirection direction)
    {
        if (direction != FlickDirection.Left && direction != FlickDirection.Right)
            return timingIndicatorScreenOffset;

        return timingIndicatorSideScreenOffset;
    }

    private Vector2 GetTimingIndicatorPivot(FlickDirection direction)
    {
        return direction switch
        {
            FlickDirection.Left => new Vector2(1f, 0.5f),
            FlickDirection.Right => new Vector2(0f, 0.5f),
            FlickDirection.Up => new Vector2(0.5f, 0f),
            _ => new Vector2(0.5f, 0.5f)
        };
    }

    private TextAlignmentOptions GetTimingIndicatorAlignment(FlickDirection direction)
    {
        return direction switch
        {
            FlickDirection.Left => TextAlignmentOptions.MidlineRight,
            FlickDirection.Right => TextAlignmentOptions.MidlineLeft,
            FlickDirection.Up => TextAlignmentOptions.Center,
            _ => TextAlignmentOptions.Center
        };
    }

    private Texture GetDefaultTimingIndicatorTexture()
    {
        if (earlyTimingIndicatorSprite != null)
            return earlyTimingIndicatorSprite.texture;

        if (lateTimingIndicatorSprite != null)
            return lateTimingIndicatorSprite.texture;

        return Texture2D.whiteTexture;
    }

    private bool TryGetTimingIndicatorSpriteData(bool isEarly, out Texture texture, out Rect uvRect)
    {
        Sprite sprite = isEarly ? earlyTimingIndicatorSprite : lateTimingIndicatorSprite;
        if (sprite == null)
        {
            texture = null;
            uvRect = new Rect(0f, 0f, 1f, 1f);
            return false;
        }

        texture = sprite.texture;
        uvRect = isEarly ? earlyTimingIndicatorUvRect : lateTimingIndicatorUvRect;
        if (uvRect.width <= 0f || uvRect.height <= 0f)
            uvRect = new Rect(0f, 0f, 1f, 1f);

        return true;
    }

    private Vector2 GetTimingIndicatorImageSize(Rect uvRect)
    {
        float safeHeight = Mathf.Max(1f, timingIndicatorImageHeight);
        float width = safeHeight;
        if (uvRect.height > 0.0001f)
            width = safeHeight * (uvRect.width / uvRect.height);

        width = Mathf.Min(width, Mathf.Max(1f, timingIndicatorImageMaxWidth));
        return new Vector2(width, safeHeight);
    }

    private RectTransform GetTimingIndicatorRect(TimingIndicatorState state)
    {
        if (state == null)
            return null;

        if (state.UsingImage && state.ImageRect != null)
            return state.ImageRect;

        if (state.TextRect != null)
            return state.TextRect;

        return state.ImageRect;
    }

    private Vector2 ApplyTimingIndicatorImageInwardNudge(
        FlickDirection direction,
        TimingIndicatorState state,
        Vector2 anchoredPosition)
    {
        if (state == null || !state.UsingImage)
            return anchoredPosition;

        float inwardNudge = direction == FlickDirection.Up
            ? timingIndicatorImageUpInwardNudge
            : timingIndicatorImageSideInwardNudge;
        return anchoredPosition - (GetDirectionVector(direction) * inwardNudge);
    }

    private void ApplyTimingIndicatorPosition(TimingIndicatorState state, Vector2 anchoredPosition)
    {
        if (state.TextRect != null)
            state.TextRect.anchoredPosition = anchoredPosition;
        if (state.ImageRect != null)
            state.ImageRect.anchoredPosition = anchoredPosition;
    }

    private void ApplyTimingIndicatorScale(TimingIndicatorState state, float scale)
    {
        Vector3 scaleVector = Vector3.one * scale;
        if (state.TextRect != null)
            state.TextRect.localScale = scaleVector;
        if (state.ImageRect != null)
            state.ImageRect.localScale = scaleVector;
    }

    private Vector2 ClampTimingIndicatorPosition(
        RectTransform canvasRect,
        RectTransform indicatorRect,
        Vector2 anchoredPosition)
    {
        Rect canvasBounds = canvasRect.rect;
        Vector2 size = indicatorRect.sizeDelta;
        Vector2 pivot = indicatorRect.pivot;

        float minX = canvasBounds.xMin + (size.x * pivot.x) + timingIndicatorScreenMargin;
        float maxX = canvasBounds.xMax - (size.x * (1f - pivot.x)) - timingIndicatorScreenMargin;
        float minY = canvasBounds.yMin + (size.y * pivot.y) + timingIndicatorScreenMargin;
        float maxY = canvasBounds.yMax - (size.y * (1f - pivot.y)) - timingIndicatorScreenMargin;

        anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, minX, maxX);
        anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, minY, maxY);
        return anchoredPosition;
    }

    private Vector2 GetDirectionVector(FlickDirection direction)
    {
        return direction switch
        {
            FlickDirection.Left => Vector2.left,
            FlickDirection.Right => Vector2.right,
            FlickDirection.Up => Vector2.up,
            FlickDirection.Down => Vector2.down,
            _ => Vector2.zero
        };
    }

    private bool IsRhythmPlaybackActive()
    {
        if (_musicPlayer == null || !_musicPlayer.musicInstance.isValid())
            return false;

        PLAYBACK_STATE playbackState;
        _musicPlayer.musicInstance.getPlaybackState(out playbackState);

        return playbackState == PLAYBACK_STATE.PLAYING
            || playbackState == PLAYBACK_STATE.STARTING
            || playbackState == PLAYBACK_STATE.SUSTAINING;
    }

    private void SyncTrackedNotesToCurrent()
    {
        _trackedNotes.Clear();
        if (_conductor == null)
            return;

        for (int i = 0; i < _conductor.activeNotes.Count; i++)
        {
            RhythmArcNote note = _conductor.activeNotes[i];
            if (note == null)
                continue;

            int id = note.GetInstanceID();
            _trackedNotes[id] = note.TargetHitTime;
        }
    }

    private void CollectNewNotes()
    {
        for (int i = 0; i < _conductor.activeNotes.Count; i++)
        {
            RhythmArcNote note = _conductor.activeNotes[i];
            if (note == null)
                continue;

            int id = note.GetInstanceID();
            if (!_trackedNotes.ContainsKey(id))
            {
                _trackedNotes[id] = note.TargetHitTime;
            }
        }
    }

    private void ResolveRemovedNotes()
    {
        _activeNoteIdsBuffer.Clear();

        for (int i = 0; i < _conductor.activeNotes.Count; i++)
        {
            RhythmArcNote note = _conductor.activeNotes[i];
            if (note == null)
                continue;

            int id = note.GetInstanceID();
            _activeNoteIdsBuffer.Add(id);
        }

        List<int> trackedIds = ListPool.Get();
        foreach (int trackedId in _trackedNotes.Keys)
            trackedIds.Add(trackedId);

        for (int i = 0; i < trackedIds.Count; i++)
        {
            int trackedId = trackedIds[i];
            if (_activeNoteIdsBuffer.Contains(trackedId))
                continue;

            float targetHitTime = _trackedNotes[trackedId];
            EvaluateNoteResult(targetHitTime);
            _trackedNotes.Remove(trackedId);
        }

        ListPool.Release(trackedIds);
    }

    private void EvaluateNoteResult(float targetHitTime)
    {
        float absDiff = Mathf.Abs(_conductor.songTime - targetHitTime);
        ResultType result = ResultType.Miss;

        if (absDiff <= _judge.perfectWindow)
            result = ResultType.Perfect;
        else if (absDiff <= _judge.goodWindow)
            result = ResultType.Good;

        ApplyResult(result);
    }

    private void HandleDetailedNoteJudged(
        RhythmJudge.JudgeRating rating,
        RhythmArcNote.NoteType noteType,
        FlickDirection direction,
        float timingDelta)
    {
        if (!hudEnabled && !judgementFeedbackOnlyEnabled)
            return;

        ResultType result;
        switch (rating)
        {
            case RhythmJudge.JudgeRating.Perfect:
                result = ResultType.Perfect;
                break;
            case RhythmJudge.JudgeRating.Good:
                result = ResultType.Good;
                break;
            default:
                result = ResultType.Miss;
                break;
        }

        ApplyResult(result);

        if (result != ResultType.Perfect)
            ShowTimingIndicator(direction, timingDelta);
    }

    private void ApplyResult(ResultType result)
    {
        switch (result)
        {
            case ResultType.Perfect:
                _perfectCount++;
                _combo++;
                _baseScore += perfectPoints;
                _score = _baseScore;
                ShowComboPulseIfEligible();
                ShowPerfectJudgement();
                break;

            case ResultType.Good:
                _goodCount++;
                _combo++;
                _baseScore += goodPoints;
                _score = _baseScore;
                ShowComboPulseIfEligible();
                ShowGoodJudgement();
                break;

            default:
                _missCount++;
                _combo = 0;
                _baseScore += missPoints;
                _score = _baseScore;
                HideComboImmediate();
                ShowMissJudgement();
                break;
        }

        if (_combo > _maxCombo)
            _maxCombo = _combo;

        RefreshDetailText();
    }

    private void ShowJudgement(string text, Color color)
    {
        if (_judgementText == null)
            return;

        _judgementBaseColor = color;
        _judgementVisual = JudgementVisual.Text;
        _judgementText.text = text;
        if (_perfectJudgementImage != null)
        {
            Color perfectImageColor = _perfectJudgementImage.color;
            perfectImageColor.a = 0f;
            _perfectJudgementImage.color = perfectImageColor;
        }
        if (_goodJudgementImage != null)
        {
            Color goodImageColor = _goodJudgementImage.color;
            goodImageColor.a = 0f;
            _goodJudgementImage.color = goodImageColor;
        }
        if (_missJudgementImage != null)
        {
            Color missImageColor = _missJudgementImage.color;
            missImageColor.a = 0f;
            _missJudgementImage.color = missImageColor;
        }
        _judgementAnimTime = 0f;
        ApplyJudgementAnimation(0f);
    }

    private void ShowGoodJudgement()
    {
        if (_judgementText == null)
            return;

        if (goodJudgementSprite == null)
        {
            ShowJudgement("GOOD", goodColor);
            return;
        }

        _judgementBaseColor = goodColor;
        _judgementVisual = JudgementVisual.GoodImage;
        _judgementText.text = string.Empty;

        if (_perfectJudgementImage != null)
        {
            Color perfectImageColor = _perfectJudgementImage.color;
            perfectImageColor.a = 0f;
            _perfectJudgementImage.color = perfectImageColor;
        }

        if (_goodJudgementImage != null)
        {
            _goodJudgementImage.sprite = goodJudgementSprite;
            _goodJudgementImage.SetNativeSize();
            _goodJudgementImage.rectTransform.sizeDelta = goodJudgementImageSize;
            Color goodImageColor = _goodJudgementImage.color;
            goodImageColor.a = 1f;
            _goodJudgementImage.color = goodImageColor;
        }

        if (_missJudgementImage != null)
        {
            Color missImageColor = _missJudgementImage.color;
            missImageColor.a = 0f;
            _missJudgementImage.color = missImageColor;
        }

        _judgementAnimTime = 0f;
        ApplyJudgementAnimation(0f);
    }

    private void ShowPerfectJudgement()
    {
        if (_judgementText == null)
            return;

        if (perfectJudgementSprite == null)
        {
            ShowJudgement("PERFECT", perfectColor);
            return;
        }

        _judgementBaseColor = perfectColor;
        _judgementVisual = JudgementVisual.PerfectImage;
        _judgementText.text = string.Empty;

        if (_perfectJudgementImage != null)
        {
            _perfectJudgementImage.sprite = perfectJudgementSprite;
            _perfectJudgementImage.SetNativeSize();
            _perfectJudgementImage.rectTransform.sizeDelta = perfectJudgementImageSize;
            Color perfectImageColor = _perfectJudgementImage.color;
            perfectImageColor.a = 1f;
            _perfectJudgementImage.color = perfectImageColor;
        }

        if (_missJudgementImage != null)
        {
            Color missImageColor = _missJudgementImage.color;
            missImageColor.a = 0f;
            _missJudgementImage.color = missImageColor;
        }
        if (_goodJudgementImage != null)
        {
            Color goodImageColor = _goodJudgementImage.color;
            goodImageColor.a = 0f;
            _goodJudgementImage.color = goodImageColor;
        }

        _judgementAnimTime = 0f;
        ApplyJudgementAnimation(0f);
    }

    private void ShowMissJudgement()
    {
        if (_judgementText == null)
            return;

        if (missJudgementSprite == null)
        {
            ShowJudgement("MISS", missColor);
            return;
        }

        _judgementBaseColor = missColor;
        _judgementVisual = JudgementVisual.MissImage;
        _judgementText.text = string.Empty;

        if (_perfectJudgementImage != null)
        {
            Color perfectImageColor = _perfectJudgementImage.color;
            perfectImageColor.a = 0f;
            _perfectJudgementImage.color = perfectImageColor;
        }
        if (_goodJudgementImage != null)
        {
            Color goodImageColor = _goodJudgementImage.color;
            goodImageColor.a = 0f;
            _goodJudgementImage.color = goodImageColor;
        }

        if (_missJudgementImage != null)
        {
            _missJudgementImage.sprite = missJudgementSprite;
            _missJudgementImage.SetNativeSize();
            _missJudgementImage.rectTransform.sizeDelta = missJudgementImageSize;
            Color missImageColor = _missJudgementImage.color;
            missImageColor.a = 1f;
            _missJudgementImage.color = missImageColor;
        }

        _judgementAnimTime = 0f;
        ApplyJudgementAnimation(0f);
    }

    private void ShowReadyJudgement()
    {
        if (_judgementText == null || _judgementRect == null)
            return;

        if (_missJudgementImage != null)
        {
            Color missImageColor = _missJudgementImage.color;
            missImageColor.a = 0f;
            _missJudgementImage.color = missImageColor;
        }
        if (_perfectJudgementImage != null)
        {
            Color perfectImageColor = _perfectJudgementImage.color;
            perfectImageColor.a = 0f;
            _perfectJudgementImage.color = perfectImageColor;
        }
        if (_goodJudgementImage != null)
        {
            Color goodImageColor = _goodJudgementImage.color;
            goodImageColor.a = 0f;
            _goodJudgementImage.color = goodImageColor;
        }

        _judgementText.text = "READY";
        _judgementBaseColor = Color.white;
        _judgementVisual = JudgementVisual.Text;
        _judgementAnimTime = -1f;

        _judgementRect.localScale = Vector3.one;
        _judgementRect.anchoredPosition = _judgementBasePosition;

        Color c = _judgementBaseColor;
        c.a = 1f;
        _judgementText.color = c;
    }

    private void ShowTimingIndicator(FlickDirection direction, float timingDelta)
    {
        if (!_timingIndicators.TryGetValue(direction, out TimingIndicatorState state))
            return;

        bool isEarly = timingDelta < 0f;
        if (state.Image != null && TryGetTimingIndicatorSpriteData(isEarly, out Texture texture, out Rect uvRect))
        {
            state.UsingImage = true;
            state.Image.texture = texture;
            state.Image.uvRect = uvRect;
            state.Image.rectTransform.sizeDelta = GetTimingIndicatorImageSize(uvRect);
            state.BaseColor = timingIndicatorImageColor;
            if (state.Text != null)
            {
                Color textColor = state.Text.color;
                textColor.a = 0f;
                state.Text.color = textColor;
            }
        }
        else
        {
            if (state.Text == null)
                return;

            state.UsingImage = false;
            state.Text.text = isEarly ? "Early!" : "Late!";
            state.BaseColor = isEarly ? earlyTimingIndicatorColor : lateTimingIndicatorColor;
        }

        state.AnimTime = 0f;
        RectTransform activeRect = GetTimingIndicatorRect(state);
        if (TryGetTimingIndicatorAnchoredPosition(direction, activeRect, out Vector2 anchoredPosition))
            state.BasePosition = ApplyTimingIndicatorImageInwardNudge(direction, state, anchoredPosition);

        ApplyTimingIndicatorAnimation(state, 0f);
    }

    private void ShowComboPulseIfEligible()
    {
        if (_comboText == null || _comboRect == null)
            return;

        if (!hudEnabled)
        {
            HideComboImmediate();
            return;
        }

        if (_combo < Mathf.Max(1, comboShowThreshold))
        {
            HideComboImmediate();
            return;
        }

        _comboVisible = true;
        _comboText.text = $"{_combo}{comboSuffix}";
        _comboCurrentBaseScale = CalculateComboBaseScale();
        _comboPulseTime = 0f;
        ApplyComboAnimation();
    }

    private float CalculateComboBaseScale()
    {
        int threshold = Mathf.Max(1, comboShowThreshold);
        int growthMaxCombo = Mathf.Max(threshold, comboGrowthMaxCombo);
        float t = growthMaxCombo == threshold
            ? 1f
            : Mathf.InverseLerp(threshold, growthMaxCombo, _combo);
        return Mathf.Lerp(comboBaseScaleMin, comboBaseScaleMax, t);
    }

    private void RefreshDetailText()
    {
        if (_detailText == null)
            return;

        if (!hudEnabled)
        {
            _detailText.text = string.Empty;
            return;
        }

        int totalJudged = _perfectCount + _goodCount + _missCount;
        float accuracy = totalJudged > 0
            ? ((_perfectCount * 1f) + (_goodCount * 0.7f)) / totalJudged * 100f
            : 0f;
        int scoreFontSize = detailFontSize * 2;

        _detailText.text =
            $"<size={scoreFontSize}>Score: {_score}</size>\n" +
            $"Combo: {_combo}\n" +
            $"Perfect: {_perfectCount}\n" +
            $"Good: {_goodCount}\n" +
            $"Miss: {_missCount}\n" +
            $"Accuracy: {accuracy:F1}%";
    }

    private void ResetRunStats()
    {
        _perfectCount = 0;
        _goodCount = 0;
        _missCount = 0;
        _combo = 0;
        _maxCombo = 0;
        _score = 0;
        _baseScore = 0;
        _lastFiredMultiplier = 1f;
        HideComboImmediate();
        HideAllTimingIndicatorsImmediate();
    }

    public void SetHudEnabled(bool enabled)
    {
        hudEnabled = enabled;
        ApplyHudVisibility();
    }

    public void SetJudgementFeedbackOnlyEnabled(bool enabled)
    {
        judgementFeedbackOnlyEnabled = enabled;
        ApplyHudVisibility();
    }

    private void ApplyHudVisibility()
    {
        if (_canvas != null)
            _canvas.enabled = hudEnabled || judgementFeedbackOnlyEnabled;

        if (_comboText != null)
            _comboText.enabled = hudEnabled;

        if (_detailText != null)
            _detailText.enabled = hudEnabled;

        bool feedbackVisible = hudEnabled || judgementFeedbackOnlyEnabled;
        foreach (TimingIndicatorState state in _timingIndicators.Values)
        {
            if (state.Text != null)
                state.Text.enabled = feedbackVisible && !state.UsingImage;
            if (state.Image != null)
                state.Image.enabled = feedbackVisible && state.UsingImage;
        }
    }

    private void TickJudgementAnimation()
    {
        if (_judgementText == null || _judgementRect == null || _judgementAnimTime < 0f)
            return;

        float totalDuration = Mathf.Max(0.01f, judgementVisibleDuration + judgementFadeDuration);
        _judgementAnimTime += Time.unscaledDeltaTime;
        float normalized = Mathf.Clamp01(_judgementAnimTime / totalDuration);
        ApplyJudgementAnimation(normalized);

        if (_judgementAnimTime >= totalDuration)
        {
            HideJudgementImmediate();
        }
    }

    private bool HasActiveTimingIndicatorAnimation()
    {
        foreach (TimingIndicatorState state in _timingIndicators.Values)
        {
            if (state.AnimTime >= 0f)
                return true;
        }

        return false;
    }

    private void TickTimingIndicatorAnimations()
    {
        float totalDuration = Mathf.Max(0.01f, timingIndicatorVisibleDuration + timingIndicatorFadeDuration);

        foreach (TimingIndicatorState state in _timingIndicators.Values)
        {
            if (GetTimingIndicatorRect(state) == null || state.AnimTime < 0f)
                continue;

            state.AnimTime += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(state.AnimTime / totalDuration);
            ApplyTimingIndicatorAnimation(state, normalized);

            if (state.AnimTime >= totalDuration)
                HideTimingIndicatorImmediate(state);
        }
    }

    private void TickComboPulseAnimation()
    {
        if (!_comboVisible || _comboRect == null || _comboText == null || _comboPulseTime < 0f)
            return;

        _comboPulseTime += Time.unscaledDeltaTime;
        ApplyComboAnimation();

        if (_comboPulseTime >= Mathf.Max(0.01f, comboPulseReturnDuration))
        {
            _comboPulseTime = -1f;
            ApplyComboAnimation();
        }
    }

    private void ApplyComboAnimation()
    {
        if (_comboRect == null || _comboText == null)
            return;

        float duration = Mathf.Max(0.01f, comboPulseReturnDuration);
        float pulseT = _comboPulseTime < 0f
            ? 1f
            : Mathf.Clamp01(_comboPulseTime / duration);
        float baseScale = Mathf.Max(0.05f, _comboCurrentBaseScale);
        float pulseScale = baseScale * Mathf.Max(1f, comboPulseScale);
        float scale = Mathf.Lerp(pulseScale, baseScale, pulseT);
        float rise = Mathf.Lerp(comboPulseRiseDistance, 0f, pulseT);

        _comboRect.localScale = Vector3.one * scale;
        _comboRect.anchoredPosition = _comboBasePosition + (Vector2.up * rise);

        Color c = comboColor;
        c.a = _comboVisible ? 1f : 0f;
        _comboText.color = c;
    }

    private void ApplyJudgementAnimation(float normalized)
    {
        if (_judgementText == null || _judgementRect == null)
            return;

        float returnDuration = Mathf.Max(0.01f, judgementPopReturnDuration);
        float popT = Mathf.Clamp01(_judgementAnimTime / returnDuration);
        float scale = Mathf.Lerp(judgementPopScale, 1f, popT);

        float alpha = 1f;
        if (_judgementAnimTime > judgementVisibleDuration)
        {
            float fadeT = (_judgementAnimTime - judgementVisibleDuration) / Mathf.Max(0.01f, judgementFadeDuration);
            alpha = 1f - Mathf.Clamp01(fadeT);
        }

        Vector2 riseOffset = Vector2.up * (judgementRiseDistance * normalized);

        _judgementRect.localScale = Vector3.one * scale;
        _judgementRect.anchoredPosition = _judgementBasePosition + riseOffset;
        if (_perfectJudgementRect != null)
        {
            _perfectJudgementRect.localScale = Vector3.one * scale;
            _perfectJudgementRect.anchoredPosition = _judgementBasePosition + perfectJudgementImageOffset + riseOffset;
        }
        if (_goodJudgementRect != null)
        {
            _goodJudgementRect.localScale = Vector3.one * scale;
            _goodJudgementRect.anchoredPosition = _judgementBasePosition + goodJudgementImageOffset + riseOffset;
        }
        if (_missJudgementRect != null)
        {
            _missJudgementRect.localScale = Vector3.one * scale;
            _missJudgementRect.anchoredPosition = _judgementBasePosition + missJudgementImageOffset + riseOffset;
        }

        Color c = _judgementBaseColor;
        c.a = alpha;
        _judgementText.color = c;

        if (_missJudgementImage != null)
        {
            Color missImageColor = _missJudgementImage.color;
            missImageColor.a = _judgementVisual == JudgementVisual.MissImage ? alpha : 0f;
            _missJudgementImage.color = missImageColor;
        }
        if (_perfectJudgementImage != null)
        {
            Color perfectImageColor = _perfectJudgementImage.color;
            perfectImageColor.a = _judgementVisual == JudgementVisual.PerfectImage ? alpha : 0f;
            _perfectJudgementImage.color = perfectImageColor;
        }
        if (_goodJudgementImage != null)
        {
            Color goodImageColor = _goodJudgementImage.color;
            goodImageColor.a = _judgementVisual == JudgementVisual.GoodImage ? alpha : 0f;
            _goodJudgementImage.color = goodImageColor;
        }
    }

    private void HideJudgementImmediate()
    {
        if (_judgementText == null || _judgementRect == null)
            return;

        _judgementAnimTime = -1f;
        _judgementRect.localScale = Vector3.one;
        _judgementRect.anchoredPosition = _judgementBasePosition;
        if (_perfectJudgementRect != null)
        {
            _perfectJudgementRect.localScale = Vector3.one;
            _perfectJudgementRect.anchoredPosition = _judgementBasePosition + perfectJudgementImageOffset;
        }
        if (_goodJudgementRect != null)
        {
            _goodJudgementRect.localScale = Vector3.one;
            _goodJudgementRect.anchoredPosition = _judgementBasePosition + goodJudgementImageOffset;
        }
        if (_missJudgementRect != null)
        {
            _missJudgementRect.localScale = Vector3.one;
            _missJudgementRect.anchoredPosition = _judgementBasePosition + missJudgementImageOffset;
        }

        Color c = _judgementText.color;
        c.a = 0f;
        _judgementText.color = c;
        _judgementVisual = JudgementVisual.Text;

        if (_missJudgementImage != null)
        {
            Color missImageColor = _missJudgementImage.color;
            missImageColor.a = 0f;
            _missJudgementImage.color = missImageColor;
        }
        if (_perfectJudgementImage != null)
        {
            Color perfectImageColor = _perfectJudgementImage.color;
            perfectImageColor.a = 0f;
            _perfectJudgementImage.color = perfectImageColor;
        }
        if (_goodJudgementImage != null)
        {
            Color goodImageColor = _goodJudgementImage.color;
            goodImageColor.a = 0f;
            _goodJudgementImage.color = goodImageColor;
        }
    }

    private void ApplyTimingIndicatorAnimation(TimingIndicatorState state, float normalized)
    {
        RectTransform activeRect = GetTimingIndicatorRect(state);
        if (activeRect == null)
            return;

        float returnDuration = Mathf.Max(0.01f, timingIndicatorPopReturnDuration);
        float popT = Mathf.Clamp01(state.AnimTime / returnDuration);
        float scale = Mathf.Lerp(timingIndicatorPopScale, 1f, popT);

        float alpha = 1f;
        if (state.AnimTime > timingIndicatorVisibleDuration)
        {
            float fadeT = (state.AnimTime - timingIndicatorVisibleDuration) / Mathf.Max(0.01f, timingIndicatorFadeDuration);
            alpha = 1f - Mathf.Clamp01(fadeT);
        }

        Vector2 riseOffset = Vector2.up * (timingIndicatorRiseDistance * normalized);
        ApplyTimingIndicatorScale(state, scale);
        ApplyTimingIndicatorPosition(state, state.BasePosition + riseOffset);

        Color c = state.BaseColor;
        c.a = alpha;
        if (state.Text != null)
        {
            Color textColor = state.Text.color;
            textColor.a = state.UsingImage ? 0f : alpha;
            state.Text.color = state.UsingImage ? textColor : c;
            state.Text.enabled = !state.UsingImage;
        }
        if (state.Image != null)
        {
            Color imageColor = state.UsingImage ? c : state.Image.color;
            imageColor.a = state.UsingImage ? alpha : 0f;
            state.Image.color = imageColor;
            state.Image.enabled = state.UsingImage;
        }
    }

    private void HideAllTimingIndicatorsImmediate()
    {
        foreach (TimingIndicatorState state in _timingIndicators.Values)
            HideTimingIndicatorImmediate(state);
    }

    private void HideTimingIndicatorImmediate(TimingIndicatorState state)
    {
        if (state == null)
            return;

        state.AnimTime = -1f;
        ApplyTimingIndicatorScale(state, 1f);
        ApplyTimingIndicatorPosition(state, state.BasePosition);

        if (state.Text != null)
        {
            Color textColor = state.Text.color;
            textColor.a = 0f;
            state.Text.color = textColor;
            state.Text.enabled = false;
        }
        if (state.Image != null)
        {
            Color imageColor = state.Image.color;
            imageColor.a = 0f;
            state.Image.color = imageColor;
            state.Image.enabled = false;
        }
        state.UsingImage = false;
    }

    private void HideComboImmediate()
    {
        if (_comboText == null || _comboRect == null)
            return;

        _comboVisible = false;
        _comboCurrentBaseScale = 1f;
        _comboPulseTime = -1f;
        _comboRect.localScale = Vector3.one;
        _comboRect.anchoredPosition = _comboBasePosition;

        Color c = _comboText.color;
        c.a = 0f;
        _comboText.color = c;
    }

    private void TickReelVisuals()
    {
        if (_conductor == null || _reelStatusImage == null) return;

        RhythmReelNote activeReel = _conductor.activeReel;
        bool isReeling = activeReel != null;
          
        if (isReeling)   
        {
            if (_reelArcObj == null)
            {
                _reelArcObj = Instantiate(timingRingPrefab, _conductor.transform);
                _reelArcObj.name = "World_Reel_Progress_Arc";
                
                _reelArcObj.transform.localPosition = new Vector3(0, 0, 0.02f);
                _reelArcObj.transform.localRotation = Quaternion.Euler(0, 0, 90f); // Start at top
                _reelArcObj.layer = _conductor.gameObject.layer;
                _currentReelArc = _reelArcObj.GetComponent<DynamicArc>();
                if (_currentReelArc != null) _currentReelArc.Setup(64);

                MeshRenderer ren = _reelArcObj.GetComponent<MeshRenderer>();
                if (ren != null) ren.material.color = reelArcColor;
            }

            if (_currentReelArc != null)
            {
                // 1. Move to the OUTSIDE
                // Radius = Hit Ring + half thickness + small gap for visibility
                float radius = _conductor.hitRingRadius + (reelArcThickness / 2f) + 0.15f;
                
                // 2. Calculate Clockwise Progress Angle
                float progressAngle = 360f * (Mathf.Clamp01(activeReel.Progress / 2f));
                

                _reelArcObj.transform.localRotation = Quaternion.Euler(0, 0, -progressAngle / 2f);
                
                _currentReelArc.Redraw(radius, reelArcThickness, progressAngle, 64);
            }

            Color c = Color.white;
            c.a = 1f;
            _reelStatusImage.color = c;

            float pulse = 1f + Mathf.Sin(Time.time * reelPulseSpeed) * reelPulseAmount;
            _reelStatusRect.localScale = Vector3.one * pulse;


            float currentMultiplier = 1f + (activeReel.Progress * 0.5f);
            currentMultiplier = Mathf.Min(currentMultiplier, 2f); 

            if(_lastFiredMultiplier < 2.0f)
            {
                if (currentMultiplier >= _lastFiredMultiplier + 0.099f || currentMultiplier >= 2.0f)
                {
                    _lastFiredMultiplier = (currentMultiplier >= 2.0f) ? 2.0f : Mathf.Floor(currentMultiplier * 10f) / 10f;

                    _score = Mathf.RoundToInt(_baseScore * _lastFiredMultiplier);
                    RefreshDetailText();

                    // Trigger the Judgement Popup (e.g., "2.0x")
                    string multText = string.Format(multiplierFormat, _lastFiredMultiplier);
                    
                    // Color shift to Red/Neon when Max is reached
                    Color displayColor = (_lastFiredMultiplier >= 2.0f) ? Color.red : multiplierColor;
                    ShowJudgement(multText, displayColor);
                }
            }
            else if (_lastFiredMultiplier >= 2.0f)
            {
                _maxPowerTimer += Time.deltaTime;
                if (_maxPowerTimer >= maxPowerFlashInterval)
                {
                    _maxPowerTimer = 0f;
                    TriggerMultiplierPop(2.0f);
                }
            }
        }
        else
        {
            Color c = _reelStatusImage.color;
            c.a = 0f;
            _reelStatusImage.color = c;
            _lastFiredMultiplier = 1f;
            _maxPowerTimer = 0f;

            if (_reelArcObj != null)
            {
                Destroy(_reelArcObj);
                _reelArcObj = null;
                _currentReelArc = null;
            }

        }
    }

    private void TriggerMultiplierPop(float multValue)
    {
        string multText = string.Format(multiplierFormat, multValue);
        Color displayColor = (multValue >= 2.0f) ? Color.red : multiplierColor;
        
        ShowJudgement(multText, displayColor);
    }

    private static class ListPool
    {
        private static readonly Stack<List<int>> Pool = new Stack<List<int>>();

        public static List<int> Get()
        {
            if (Pool.Count > 0)
                return Pool.Pop();

            return new List<int>(64);
        }

        public static void Release(List<int> list)
        {
            list.Clear();
            Pool.Push(list);
        }
    }
}
