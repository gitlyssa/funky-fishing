using System.Collections.Generic;
using FMOD.Studio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RhythmPerformanceHud : MonoBehaviour
{
    [Header("HUD Toggle")]
    [SerializeField] private bool hudEnabled = true;

    [Header("Points")]
    [SerializeField] private int perfectPoints = 100;
    [SerializeField] private int goodPoints = 70;
    [SerializeField] private int missPoints = 0;

    [Header("Judgement Text")]
    [SerializeField] private Vector2 judgementAnchoredPosition = new Vector2(0f, -120f);
    [SerializeField] private int judgementFontSize = 56;
    [SerializeField] private Sprite perfectJudgementSprite;
    [SerializeField] private Vector2 perfectJudgementImageSize = new Vector2(520f, 130f);
    [SerializeField] private Sprite goodJudgementSprite;
    [SerializeField] private Vector2 goodJudgementImageSize = new Vector2(420f, 120f);
    [SerializeField] private Sprite missJudgementSprite;
    [SerializeField] private Vector2 missJudgementImageSize = new Vector2(420f, 120f);
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
    [SerializeField] private int comboFontSize = 74;
    [SerializeField] private Color comboColor = new Color(0.35f, 0.95f, 0.45f, 1f);
    [SerializeField] private string comboSuffix = " COMBO";
    [SerializeField] private float comboBaseScaleMin = 0.55f;
    [SerializeField] private float comboBaseScaleMax = 1.38f;
    [SerializeField] private int comboGrowthMaxCombo = 18;
    [SerializeField] private float comboPulseReturnDuration = 0.14f;
    [SerializeField] private float comboPulseScale = 1.34f;
    [SerializeField] private float comboPulseRiseDistance = 10f;

    [Header("Detailed Text")]
    [SerializeField] private Vector2 detailAnchoredPosition = new Vector2(-28f, -28f);
    [SerializeField] private int detailFontSize = 26;

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

    public int CurrentScore => _score;
    public float CurrentAccuracy {
        get {
            int totalJudged = _perfectCount + _goodCount + _missCount;
            return totalJudged > 0 ? ((_perfectCount * 1f) + (_goodCount * 0.7f)) / totalJudged * 100f : 0f;
        }
    }

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

    private void Awake()
    {
        EnsureReferences();
        EnsureUi();
        ResetRunStats();
        RefreshDetailText();
        ApplyHudVisibility();
    }

    private void OnValidate()
    {
        ApplyHudVisibility();
    }

    private void Update()
    {
        EnsureReferences();
        EnsureUi();

        if (!hudEnabled)
        {
            _wasPlaybackActive = false;
            _trackedNotes.Clear();
            HideJudgementImmediate();
            HideComboImmediate();
            return;
        }

        if (_conductor == null || _judge == null)
            return;

        bool playbackActive = IsRhythmPlaybackActive();

        if (playbackActive && !_wasPlaybackActive)
        {
            ResetRunStats();
            SyncTrackedNotesToCurrent();
            ShowReadyJudgement();
            RefreshDetailText();
        }

        if (!playbackActive && _wasPlaybackActive)
        {
            _trackedNotes.Clear();
            HideComboImmediate();
        }

        _wasPlaybackActive = playbackActive;

        if (!playbackActive)
            return;

        CollectNewNotes();
        ResolveRemovedNotes();
        TickJudgementAnimation();
        TickComboPulseAnimation();
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
        if (_judgementText != null && _detailText != null && _comboText != null && _missJudgementImage != null && _perfectJudgementImage != null && _goodJudgementImage != null)
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
            new Vector2(450f, 320f),
            detailFontSize,
            TextAlignmentOptions.TopRight,
            string.Empty);

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

        Color perfectImageColor = _perfectJudgementImage.color;
        perfectImageColor.a = 0f;
        _perfectJudgementImage.color = perfectImageColor;

        Color goodImageColor = _goodJudgementImage.color;
        goodImageColor.a = 0f;
        _goodJudgementImage.color = goodImageColor;

        Color imageColor = _missJudgementImage.color;
        imageColor.a = 0f;
        _missJudgementImage.color = imageColor;

        HideComboImmediate();
        HideJudgementImmediate();

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

    private void ApplyResult(ResultType result)
    {
        switch (result)
        {
            case ResultType.Perfect:
                _perfectCount++;
                _combo++;
                _score += perfectPoints;
                ShowComboPulseIfEligible();
                ShowPerfectJudgement();
                break;

            case ResultType.Good:
                _goodCount++;
                _combo++;
                _score += goodPoints;
                ShowComboPulseIfEligible();
                ShowGoodJudgement();
                break;

            default:
                _missCount++;
                _combo = 0;
                _score += missPoints;
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

    private void ShowComboPulseIfEligible()
    {
        if (_comboText == null || _comboRect == null)
            return;

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

        int totalJudged = _perfectCount + _goodCount + _missCount;
        float accuracy = totalJudged > 0
            ? ((_perfectCount * 1f) + (_goodCount * 0.7f)) / totalJudged * 100f
            : 0f;

        _detailText.text =
            $"Score: {_score}\n" +
            $"Combo: {_combo}\n" +
            $"Max Combo: {_maxCombo}\n" +
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
        HideComboImmediate();
    }

    public void SetHudEnabled(bool enabled)
    {
        hudEnabled = enabled;
        ApplyHudVisibility();
    }

    private void ApplyHudVisibility()
    {
        if (_canvas != null)
            _canvas.enabled = hudEnabled;
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

        _judgementRect.localScale = Vector3.one * scale;
        _judgementRect.anchoredPosition = _judgementBasePosition + (Vector2.up * (judgementRiseDistance * normalized));
        if (_perfectJudgementRect != null)
        {
            _perfectJudgementRect.localScale = Vector3.one * scale;
            _perfectJudgementRect.anchoredPosition = _judgementBasePosition + (Vector2.up * (judgementRiseDistance * normalized));
        }
        if (_goodJudgementRect != null)
        {
            _goodJudgementRect.localScale = Vector3.one * scale;
            _goodJudgementRect.anchoredPosition = _judgementBasePosition + (Vector2.up * (judgementRiseDistance * normalized));
        }
        if (_missJudgementRect != null)
        {
            _missJudgementRect.localScale = Vector3.one * scale;
            _missJudgementRect.anchoredPosition = _judgementBasePosition + (Vector2.up * (judgementRiseDistance * normalized));
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
