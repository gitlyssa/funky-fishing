
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(PauseManager))]
public class FishingPauseTuningPanel : MonoBehaviour
{
    private const string TuningFontResourcePath = "Fonts & Materials/LiberationSans SDF";
    private static TMP_FontAsset s_tuningFontAsset;

    private enum TuningField
    {
        RequireBumperOrTriggerHold,
        MinimumCatchGrade,
        CastForwardLinG,
        CastGyroDps,
        YankBackLinG,
        YankGyroDps,
        MinTimeBetweenCastAndYank,
        CooldownAfterTrigger
    }

    private sealed class FieldSpec
    {
        public TuningField Field;
        public string Label;
        public string Description;
        public float Min;
        public float Max;
        public int Decimals;
        public float DefaultValue;
        public bool WholeNumbers;
        public bool IsToggle;
        public bool IsGrade;
    }

    private sealed class SliderRow
    {
        public FieldSpec Spec;
        public Slider Slider;
        public TextMeshProUGUI ValueLabel;
    }

    [Serializable]
    private sealed class TuningSaveData
    {
        public bool requireBumperOrTriggerHold;
        public bool hasMinimumCatchGradeRank;
        public int minimumCatchGradeRank;
        public float castForwardLinG;
        public float castGyroDps;
        public float yankBackLinG;
        public float yankGyroDps;
        public float minTimeBetweenCastAndYank;
        public float cooldownAfterTrigger;
        public string savedUtc;
    }

    [Header("Auto Find")]
    [SerializeField] private bool autoFindTarget = true;
    [SerializeField] private JoyConGestureDetector gestureDetector;

    [Header("Pause Menu Button")]
    [SerializeField] private string openButtonName = "FishingTuningButton";
    [SerializeField] private Vector2 openButtonSize = new Vector2(180f, 34f);
    [SerializeField] private Vector2 openButtonOffset = new Vector2(20f, 20f);
    [SerializeField] private bool adaptToMirroredPausePanel = true;

    [Header("Panel Layout")]
    [SerializeField] private string panelObjectName = "FishingTuningPanel";
    [SerializeField] private Vector2 panelSize = new Vector2(760f, 500f);
    [SerializeField] private Vector2 panelOffset = Vector2.zero;
    [SerializeField] private Vector2 panelMaxViewportPercent = new Vector2(0.88f, 0.82f);

    [Header("Persistence")]
    [SerializeField] private bool autoLoadOnStart = true;
    [SerializeField] private bool autoSaveOnChange = true;
    [SerializeField] private string saveFileName = "fishing_joycon_tuning.json";
    [SerializeField] private string exportFolderName = "FishingTuningExports";

    private PauseManager _pauseManager;
    private RectTransform _panelRoot;
    private RectTransform _scrollRoot;
    private RectTransform _actionsRow;
    private Button _openButton;
    private Button _backButton;
    private Selectable _firstTuningSelectable;
    private TextMeshProUGUI _targetLabel;
    private TextMeshProUGUI _statusLabel;
    private TextMeshProUGUI _pathLabel;
    private RectTransform _infoPopupOverlay;
    private TextMeshProUGUI _infoPopupTitle;
    private TextMeshProUGUI _infoPopupBody;
    private Button _infoPopupCloseButton;
    private readonly List<SliderRow> _rows = new List<SliderRow>();

    private bool _built;
    private bool _wasPauseOpen;
    private bool _isTuningOpen;
    private bool _hasTarget;
    private bool _useExternalHostLayout;
    private bool _cachedValuesAreAuthoritative;
    private JoyConGestureDetector _lastResolvedGestureDetector;
    private TuningSaveData _cachedSaveData;

    private static readonly FieldSpec[] Specs =
    {
        new FieldSpec { Field = TuningField.RequireBumperOrTriggerHold, Label = "Require Trigger/Bumper Hold (Cast)", Description = "Requires the cast button to be held so casual swings do not fire a cast.", Min = 0f, Max = 1f, Decimals = 0, DefaultValue = 1f, WholeNumbers = true, IsToggle = true },
        new FieldSpec { Field = TuningField.MinimumCatchGrade, Label = "Minimum Catch Grade", Description = "Sets the lowest rhythm grade that still counts as a landed fish.", Min = 0f, Max = 4f, Decimals = 0, DefaultValue = 1f, WholeNumbers = true, IsGrade = true },
        new FieldSpec { Field = TuningField.CastForwardLinG, Label = "Cast Forward Lin (g)", Description = "How much forward acceleration is needed for a cast swing to register.", Min = 0.05f, Max = 2.00f, Decimals = 2, DefaultValue = 0.05000000074505806f },
        new FieldSpec { Field = TuningField.CastGyroDps, Label = "Cast Gyro (dps)", Description = "How much rotational speed is needed for a cast swing to register.", Min = 80f, Max = 520f, Decimals = 0, DefaultValue = 160.2053985595703f },
        new FieldSpec { Field = TuningField.YankBackLinG, Label = "Yank Back Lin (g)", Description = "How much backward acceleration is needed for a yank to register.", Min = 0.05f, Max = 2.00f, Decimals = 2, DefaultValue = 0.05000000074505806f },
        new FieldSpec { Field = TuningField.YankGyroDps, Label = "Yank Gyro (dps)", Description = "How much rotational speed is needed for a yank to register.", Min = 80f, Max = 520f, Decimals = 0, DefaultValue = 200.3485107421875f },
        new FieldSpec { Field = TuningField.MinTimeBetweenCastAndYank, Label = "Min Cast->Yank Time (s)", Description = "Minimum delay after a cast before a yank input can trigger.", Min = 0.05f, Max = 0.90f, Decimals = 2, DefaultValue = 0.05000000074505806f },
        new FieldSpec { Field = TuningField.CooldownAfterTrigger, Label = "Trigger Cooldown (s)", Description = "Short cooldown before another cast or yank can be detected.", Min = 0.05f, Max = 0.90f, Decimals = 2, DefaultValue = 0.05000000074505806f }
    };

    private void Awake()
    {
        _pauseManager = GetComponent<PauseManager>();
        _cachedSaveData = BuildDefaultSaveData();
    }

    private void Start()
    {
        BuildUiIfNeeded();
        if (!_useExternalHostLayout)
        {
            AdjustOpenButtonPlacement();
            AdjustPanelToViewport();
        }
        ResolveTarget();
        if (autoLoadOnStart)
            TryLoadPersistentFile(showStatusWhenMissing: false);
        RefreshUiFromTarget();
        CloseTuningPanelInternal(selectOpenButton: false);
    }

    private void OnDisable()
    {
        HideInfoPopup();
    }

    private void Update()
    {
        if (_pauseManager == null || _pauseManager.PausePanel == null)
            return;

        bool pauseOpen = _pauseManager.PausePanel.activeInHierarchy;
        if (!pauseOpen)
        {
            _wasPauseOpen = false;
            CloseTuningPanelInternal(selectOpenButton: false);
            SetOpenButtonActive(false);
            return;
        }

        ResolveTarget();
        if (!_useExternalHostLayout)
        {
            AdjustOpenButtonPlacement();
            AdjustPanelToViewport();
            SyncOpenButtonVisibility();
        }

        if (!_wasPauseOpen)
        {
            CloseTuningPanelInternal(selectOpenButton: false);
            RefreshUiFromTarget();
            if (!_useExternalHostLayout)
                SyncOpenButtonVisibility();
        }

        _wasPauseOpen = true;
    }

    public bool IsTuningPanelOpen() => _isTuningOpen;

    public void ApplyDefaultPreset(bool persist = true)
    {
        ResolveTarget();

        _cachedSaveData = BuildDefaultSaveData();
        _cachedSaveData.savedUtc = DateTime.UtcNow.ToString("o");
        _cachedValuesAreAuthoritative = true;

        if (_hasTarget)
            ApplySaveData(_cachedSaveData);

        RefreshUiFromTarget();
        if (persist)
            SaveCurrentToPersistentFile();
    }

    public void PrepareForUnifiedOptions(Transform hostParent, Action onBack)
    {
        BuildUiIfNeeded();
        if (_panelRoot == null || hostParent == null)
            return;

        _useExternalHostLayout = true;

        if (_openButton != null)
        {
            Destroy(_openButton.gameObject);
            _openButton = null;
        }

        _panelRoot.SetParent(hostParent, false);
        Stretch(_panelRoot);

        if (_panelRoot.TryGetComponent(out Image panelImage))
            panelImage.color = new Color(0.05f, 0.05f, 0.06f, 1f);

        if (_scrollRoot != null)
        {
            _scrollRoot.offsetMin = new Vector2(10f, 12f);
            _scrollRoot.offsetMax = new Vector2(-10f, -46f);
        }

        if (_actionsRow != null)
            _actionsRow.gameObject.SetActive(false);

        if (_targetLabel != null && _targetLabel.transform.parent is RectTransform targetBox)
            targetBox.gameObject.SetActive(false);

        if (_pathLabel != null && _pathLabel.transform.parent is RectTransform pathBox)
            pathBox.gameObject.SetActive(false);

        if (_statusLabel != null && _statusLabel.transform.parent is RectTransform statusBox)
            statusBox.gameObject.SetActive(false);

        if (_backButton != null)
        {
            _backButton.onClick.RemoveAllListeners();
            _backButton.onClick.AddListener(() =>
            {
                FunkyAudioSettings.PlayUiConfirm();
                onBack?.Invoke();
            });
        }

        CloseTuningPanelInternal(selectOpenButton: false);
    }

    public void CloseForUnifiedOptions()
    {
        CloseTuningPanelInternal(selectOpenButton: false);
    }

    public void OpenTuningPanel()
    {
        RhythmPauseTuningPanel rhythmPanel = GetComponent<RhythmPauseTuningPanel>();
        if (rhythmPanel != null && rhythmPanel.IsTuningPanelOpen())
            rhythmPanel.CloseTuningPanel();

        if (_panelRoot == null)
            return;

        AdjustPanelToViewport();
        _isTuningOpen = true;
        _panelRoot.gameObject.SetActive(true);
        SetOpenButtonActive(false);
        ResolveTarget();
        RefreshUiFromTarget();
        EnsureSelection();
        SyncOpenButtonVisibility();
        if (rhythmPanel != null)
            rhythmPanel.SyncOpenButtonVisibility();
    }

    public void CloseTuningPanel()
    {
        CloseTuningPanelInternal(selectOpenButton: true);
    }

    public void EnsureSelection()
    {
        if (!_isTuningOpen)
            return;

        EventSystem evt = EventSystem.current;
        if (evt == null)
            return;

        GameObject selected = evt.currentSelectedGameObject;
        if (selected != null && selected.activeInHierarchy && selected.transform.IsChildOf(_panelRoot))
            return;

        if (_firstTuningSelectable != null && _firstTuningSelectable.gameObject.activeInHierarchy)
            evt.SetSelectedGameObject(_firstTuningSelectable.gameObject);
    }

    private void CloseTuningPanelInternal(bool selectOpenButton)
    {
        _isTuningOpen = false;
        HideInfoPopup();

        if (_panelRoot != null)
            _panelRoot.gameObject.SetActive(false);

        SyncOpenButtonVisibility();

        RhythmPauseTuningPanel rhythmPanel = GetComponent<RhythmPauseTuningPanel>();
        if (rhythmPanel != null)
            rhythmPanel.SyncOpenButtonVisibility();

        if (!selectOpenButton)
            return;

        EventSystem evt = EventSystem.current;
        if (evt == null || _openButton == null || !_openButton.gameObject.activeInHierarchy)
            return;

        evt.SetSelectedGameObject(_openButton.gameObject);
    }

    private void SetOpenButtonActive(bool active)
    {
        if (_openButton != null)
            _openButton.gameObject.SetActive(active);
    }

    public void SyncOpenButtonVisibility()
    {
        if (_useExternalHostLayout)
        {
            SetOpenButtonActive(false);
            return;
        }

        bool pauseOpen = _pauseManager != null &&
            _pauseManager.PausePanel != null &&
            _pauseManager.PausePanel.activeInHierarchy;
        bool rhythmOpen = IsRhythmTuningPanelOpen();
        SetOpenButtonActive(pauseOpen && !_isTuningOpen && !rhythmOpen);
    }

    private bool IsRhythmTuningPanelOpen()
    {
        RhythmPauseTuningPanel rhythmPanel = GetComponent<RhythmPauseTuningPanel>();
        return rhythmPanel != null && rhythmPanel.IsTuningPanelOpen();
    }

    private void AdjustOpenButtonPlacement()
    {
        if (_useExternalHostLayout)
            return;

        if (_openButton == null)
            return;

        RectTransform rect = _openButton.transform as RectTransform;
        if (rect == null)
            return;

        bool flipX = false;
        bool flipY = false;
        if (adaptToMirroredPausePanel && _pauseManager != null && _pauseManager.PausePanel != null)
        {
            Vector3 s = _pauseManager.PausePanel.transform.lossyScale;
            flipX = s.x < 0f;
            flipY = s.y < 0f;
        }

        float anchorX = flipX ? 1f : 0f;
        float anchorY = flipY ? 1f : 0f;
        rect.anchorMin = new Vector2(anchorX, anchorY);
        rect.anchorMax = new Vector2(anchorX, anchorY);
        rect.pivot = new Vector2(anchorX, anchorY);
        rect.sizeDelta = openButtonSize;
        rect.anchoredPosition = new Vector2(
            flipX ? -openButtonOffset.x : openButtonOffset.x,
            flipY ? -openButtonOffset.y : openButtonOffset.y);
    }

    private void AdjustPanelToViewport()
    {
        if (_useExternalHostLayout)
            return;

        if (_panelRoot == null || _pauseManager == null || _pauseManager.PausePanel == null)
            return;

        RectTransform pauseRect = _pauseManager.PausePanel.transform as RectTransform;
        if (pauseRect == null)
            return;

        Vector2 size = pauseRect.rect.size;
        if (size.x <= 1f || size.y <= 1f)
            return;

        float maxW = size.x * Mathf.Clamp(panelMaxViewportPercent.x, 0.1f, 1f);
        float maxH = size.y * Mathf.Clamp(panelMaxViewportPercent.y, 0.1f, 1f);
        _panelRoot.sizeDelta = new Vector2(Mathf.Min(panelSize.x, maxW), Mathf.Min(panelSize.y, maxH));
    }

    private void ResolveTarget()
    {
        if (autoFindTarget && gestureDetector == null)
            gestureDetector = FindObjectOfType<JoyConGestureDetector>();

        if (_lastResolvedGestureDetector != gestureDetector)
        {
            _lastResolvedGestureDetector = gestureDetector;
            HandleTargetChanged();
        }

        _hasTarget = gestureDetector != null;
        if (_hasTarget)
            UpdateTargetLabel("Target: JoyConGestureDetector");
        else
            UpdateTargetLabel("Target: JoyConGestureDetector not found in scene");
    }

    private void BuildUiIfNeeded()
    {
        if (_built || _pauseManager == null || _pauseManager.PausePanel == null)
            return;

        Transform parent = _pauseManager.PausePanel.transform;
        CreateOpenButton(parent);
        CreatePanel(parent);
        _built = true;
    }

    private void CreateOpenButton(Transform parent)
    {
        Transform existing = parent.Find(openButtonName);
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject go = new GameObject(openButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.14f, 0.25f, 0.36f, 0.92f);

        _openButton = go.GetComponent<Button>();
        _openButton.targetGraphic = image;
        _openButton.onClick.AddListener(() =>
        {
            FunkyAudioSettings.PlayUiConfirm();
            OpenTuningPanel();
        });

        TextMeshProUGUI label = CreateText(go.transform, "Label", "Fishing Tuning", 13f, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);

        AdjustOpenButtonPlacement();
    }

    private void CreatePanel(Transform parent)
    {
        Transform existing = parent.Find(panelObjectName);
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject go = new GameObject(panelObjectName, typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        go.transform.SetParent(parent, false);

        _panelRoot = go.GetComponent<RectTransform>();
        _panelRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _panelRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _panelRoot.pivot = new Vector2(0.5f, 0.5f);
        _panelRoot.sizeDelta = panelSize;
        _panelRoot.anchoredPosition = panelOffset;

        Image bg = go.GetComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.06f, 1f);

        CreateHeader();
        CreateTargetLabel();
        CreateSliderScrollArea();
        CreateStatusLabel();
        CreateActionButtons();
        CreateInfoPopup();
    }

    private void CreateHeader()
    {
        RectTransform header = CreateRect(_panelRoot, "Header", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -38f), new Vector2(-10f, -8f));

        _backButton = CreateButton(header, "BackButton", "Back", CloseTuningPanel, new Color(0.35f, 0.20f, 0.20f, 1f), 12f);
        RectTransform backRect = _backButton.transform as RectTransform;
        backRect.anchorMin = new Vector2(0f, 0f);
        backRect.anchorMax = new Vector2(0f, 1f);
        backRect.pivot = new Vector2(0f, 0.5f);
        backRect.sizeDelta = new Vector2(92f, 0f);
        backRect.anchoredPosition = Vector2.zero;

        _firstTuningSelectable = _backButton;

        Button resetButton = CreateButton(
            header,
            "ResetDefaultsButton",
            "Reset Defaults",
            ResetToDefaults,
            new Color(0.3f, 0.16f, 0.16f, 1f),
            11f);
        RectTransform resetRect = resetButton.transform as RectTransform;
        resetRect.anchorMin = new Vector2(1f, 0f);
        resetRect.anchorMax = new Vector2(1f, 1f);
        resetRect.pivot = new Vector2(1f, 0.5f);
        resetRect.sizeDelta = new Vector2(128f, 0f);
        resetRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI title = CreateText(header, "Title", "Fishing Joy-Con Tuning", 16f, FontStyles.Bold, TextAlignmentOptions.Center);
        title.color = new Color(1f, 0.95f, 0.72f, 1f);
        Stretch(title.rectTransform);
        title.rectTransform.offsetMin = new Vector2(98f, 0f);
        title.rectTransform.offsetMax = new Vector2(-134f, 0f);
    }

    private void CreateTargetLabel()
    {
        RectTransform rect = CreateRect(_panelRoot, "TargetLabel", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -64f), new Vector2(-10f, -42f));
        _targetLabel = CreateText(rect, "Text", "Target: detecting...", 12f, FontStyles.Normal, TextAlignmentOptions.Left);
        _targetLabel.color = new Color(0.78f, 0.93f, 1f, 1f);
        Stretch(_targetLabel.rectTransform);
    }

    private void CreateSliderScrollArea()
    {
        RectTransform scrollRoot = CreateRect(_panelRoot, "SliderScrollRoot", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 90f), new Vector2(-10f, -70f));
        _scrollRoot = scrollRoot;
        Image scrollBg = scrollRoot.gameObject.AddComponent<Image>();
        scrollBg.color = new Color(0.08f, 0.08f, 0.1f, 1f);

        GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportGo.transform.SetParent(scrollRoot, false);
        RectTransform viewport = viewportGo.GetComponent<RectTransform>();
        Stretch(viewport);
        Image vimg = viewportGo.GetComponent<Image>();
        vimg.color = new Color(0f, 0f, 0f, 0.01f);
        viewportGo.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(viewport, false);
        RectTransform content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        HorizontalLayoutGroup h = contentGo.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 8f;
        h.padding = new RectOffset(8, 8, 8, 8);
        h.childAlignment = TextAnchor.UpperLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 22f;

        Transform left = CreateSliderColumn(content, "LeftColumn");
        Transform right = CreateSliderColumn(content, "RightColumn");

        for (int i = 0; i < Specs.Length; i++)
        {
            Transform target = (i % 2 == 0) ? left : right;
            CreateSliderRow(target, Specs[i]);
        }
    }

    private Transform CreateSliderColumn(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        LayoutElement le = go.GetComponent<LayoutElement>();
        le.flexibleWidth = 1f;

        VerticalLayoutGroup v = go.GetComponent<VerticalLayoutGroup>();
        v.spacing = 4f;
        v.padding = new RectOffset(0, 0, 0, 0);
        v.childAlignment = TextAnchor.UpperLeft;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        return go.transform;
    }

    private void CreateSliderRow(Transform parent, FieldSpec spec)
    {
        GameObject rowGo = new GameObject(spec.Field + "_Row", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        rowGo.transform.SetParent(parent, false);

        LayoutElement rowLayout = rowGo.GetComponent<LayoutElement>();
        rowLayout.preferredHeight = 54f;

        VerticalLayoutGroup rowV = rowGo.GetComponent<VerticalLayoutGroup>();
        rowV.spacing = 2f;
        rowV.padding = new RectOffset(0, 0, 0, 0);
        rowV.childAlignment = TextAnchor.UpperLeft;
        rowV.childControlWidth = true;
        rowV.childControlHeight = true;
        rowV.childForceExpandWidth = true;
        rowV.childForceExpandHeight = false;

        GameObject headerGo = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        headerGo.transform.SetParent(rowGo.transform, false);

        LayoutElement headerLayout = headerGo.GetComponent<LayoutElement>();
        headerLayout.preferredHeight = 20f;

        HorizontalLayoutGroup header = headerGo.GetComponent<HorizontalLayoutGroup>();
        header.spacing = 4f;
        header.padding = new RectOffset(2, 2, 0, 0);
        header.childAlignment = TextAnchor.MiddleLeft;
        header.childControlWidth = true;
        header.childControlHeight = true;
        header.childForceExpandWidth = false;
        header.childForceExpandHeight = false;

        GameObject labelGroupGo = new GameObject("LabelGroup", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        labelGroupGo.transform.SetParent(headerGo.transform, false);

        HorizontalLayoutGroup labelGroup = labelGroupGo.GetComponent<HorizontalLayoutGroup>();
        labelGroup.spacing = 3f;
        labelGroup.padding = new RectOffset(0, 0, 0, 0);
        labelGroup.childAlignment = TextAnchor.MiddleLeft;
        labelGroup.childControlWidth = true;
        labelGroup.childControlHeight = true;
        labelGroup.childForceExpandWidth = false;
        labelGroup.childForceExpandHeight = false;

        LayoutElement labelGroupLayout = labelGroupGo.GetComponent<LayoutElement>();
        labelGroupLayout.flexibleWidth = 1f;

        TextMeshProUGUI name = CreateText(labelGroupGo.transform, "Name", spec.Label, 13f, FontStyles.Normal, TextAlignmentOptions.Left);
        name.color = new Color(0.95f, 0.95f, 0.95f, 1f);

        CreateInfoButton(labelGroupGo.transform, spec);

        TextMeshProUGUI value = CreateText(headerGo.transform, "Value", "-", 13f, FontStyles.Bold, TextAlignmentOptions.Right);
        value.color = new Color(0.95f, 0.87f, 0.45f, 1f);
        LayoutElement valueLayout = value.gameObject.AddComponent<LayoutElement>();
        valueLayout.preferredWidth = 72f;

        Slider slider = CreateSlider(rowGo.transform);
        slider.minValue = spec.Min;
        slider.maxValue = spec.Max;
        slider.wholeNumbers = spec.WholeNumbers;

        SliderRow row = new SliderRow { Spec = spec, Slider = slider, ValueLabel = value };
        _rows.Add(row);

        if (_firstTuningSelectable == null)
            _firstTuningSelectable = slider;

        slider.onValueChanged.AddListener(v =>
        {
            ApplyFieldValue(spec, v);
            value.text = FormatValue(spec, v);
            if (autoSaveOnChange)
                SaveCurrentToPersistentFile();
        });
    }

    private void CreateInfoButton(Transform parent, FieldSpec spec)
    {
        GameObject go = new GameObject(spec.Field + "_InfoButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.preferredWidth = 12f;
        layout.preferredHeight = 12f;
        layout.minWidth = 12f;
        layout.minHeight = 12f;

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.18f, 0.26f, 0.38f, 0.95f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() =>
        {
            FunkyAudioSettings.PlayUiConfirm();
            ShowInfoPopup(spec.Label, spec.Description);
        });

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(12f, 12f);

        TextMeshProUGUI label = CreateText(go.transform, "Label", "i", 7f, FontStyles.Bold, TextAlignmentOptions.Center);
        label.color = new Color(0.92f, 0.96f, 1f, 1f);
        Stretch(label.rectTransform);
    }

    private void CreateInfoPopup()
    {
        _infoPopupOverlay = CreateRect(_panelRoot, "InfoPopupOverlay", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image overlayImage = _infoPopupOverlay.gameObject.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.68f);

        Button overlayButton = _infoPopupOverlay.gameObject.AddComponent<Button>();
        overlayButton.targetGraphic = overlayImage;
        overlayButton.onClick.AddListener(() =>
        {
            FunkyAudioSettings.PlayUiConfirm();
            HideInfoPopup();
        });

        RectTransform card = CreateRect(_infoPopupOverlay, "InfoPopupCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-170f, -96f), new Vector2(170f, 96f));
        Image cardImage = card.gameObject.AddComponent<Image>();
        cardImage.color = new Color(0.1f, 0.12f, 0.16f, 0.98f);

        VerticalLayoutGroup layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.padding = new RectOffset(14, 14, 14, 14);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        GameObject titleGo = new GameObject("TitleLayout", typeof(RectTransform), typeof(LayoutElement));
        titleGo.transform.SetParent(card, false);
        LayoutElement titleLayout = titleGo.GetComponent<LayoutElement>();
        titleLayout.preferredHeight = 24f;
        _infoPopupTitle = CreateText(titleGo.transform, "Title", "", 14f, FontStyles.Bold, TextAlignmentOptions.Left);
        _infoPopupTitle.color = new Color(1f, 0.95f, 0.72f, 1f);
        Stretch(_infoPopupTitle.rectTransform);

        GameObject bodyGo = new GameObject("BodyLayout", typeof(RectTransform), typeof(LayoutElement));
        bodyGo.transform.SetParent(card, false);
        LayoutElement bodyLayout = bodyGo.GetComponent<LayoutElement>();
        bodyLayout.flexibleHeight = 1f;
        bodyLayout.preferredHeight = 84f;
        _infoPopupBody = CreateText(bodyGo.transform, "Body", "", 12f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        _infoPopupBody.color = new Color(0.92f, 0.94f, 0.98f, 1f);
        _infoPopupBody.textWrappingMode = TextWrappingModes.Normal;
        Stretch(_infoPopupBody.rectTransform);

        GameObject buttonsGo = new GameObject("ButtonsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        buttonsGo.transform.SetParent(card, false);
        HorizontalLayoutGroup buttonsLayout = buttonsGo.GetComponent<HorizontalLayoutGroup>();
        buttonsLayout.childAlignment = TextAnchor.MiddleRight;
        buttonsLayout.childControlWidth = false;
        buttonsLayout.childControlHeight = true;
        buttonsLayout.childForceExpandWidth = false;
        buttonsLayout.childForceExpandHeight = false;
        LayoutElement buttonsRowLayout = buttonsGo.GetComponent<LayoutElement>();
        buttonsRowLayout.preferredHeight = 30f;

        _infoPopupCloseButton = CreateButton(buttonsGo.transform, "CloseButton", "Close", HideInfoPopup, new Color(0.24f, 0.18f, 0.18f, 1f), 11f);
        RectTransform closeRect = _infoPopupCloseButton.transform as RectTransform;
        if (closeRect != null)
            closeRect.sizeDelta = new Vector2(84f, 30f);

        _infoPopupOverlay.gameObject.SetActive(false);
    }

    private void ShowInfoPopup(string title, string description)
    {
        if (_infoPopupOverlay == null)
            return;

        _infoPopupTitle.text = title;
        _infoPopupBody.text = description;
        _infoPopupOverlay.gameObject.SetActive(true);
        _infoPopupOverlay.SetAsLastSibling();

        if (_infoPopupCloseButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_infoPopupCloseButton.gameObject);
    }

    private void HideInfoPopup()
    {
        if (_infoPopupOverlay != null)
            _infoPopupOverlay.gameObject.SetActive(false);
    }

    private Slider CreateSlider(Transform parent)
    {
        GameObject sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
        sliderGo.transform.SetParent(parent, false);

        LayoutElement layout = sliderGo.GetComponent<LayoutElement>();
        layout.preferredHeight = 16f;

        Slider slider = sliderGo.GetComponent<Slider>();

        GameObject bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(sliderGo.transform, false);
        RectTransform bg = bgGo.GetComponent<RectTransform>();
        bg.anchorMin = new Vector2(0f, 0.38f);
        bg.anchorMax = new Vector2(1f, 0.62f);
        bg.offsetMin = Vector2.zero;
        bg.offsetMax = Vector2.zero;
        bgGo.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

        GameObject fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaGo.transform.SetParent(sliderGo.transform, false);
        RectTransform fillArea = fillAreaGo.GetComponent<RectTransform>();
        fillArea.anchorMin = new Vector2(0f, 0.34f);
        fillArea.anchorMax = new Vector2(1f, 0.66f);
        fillArea.offsetMin = new Vector2(4f, 0f);
        fillArea.offsetMax = new Vector2(-4f, 0f);

        GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(fillAreaGo.transform, false);
        RectTransform fill = fillGo.GetComponent<RectTransform>();
        Stretch(fill);
        fillGo.GetComponent<Image>().color = new Color(0.20f, 0.72f, 1f, 1f);

        GameObject handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGo.transform.SetParent(sliderGo.transform, false);
        RectTransform handle = handleGo.GetComponent<RectTransform>();
        handle.sizeDelta = new Vector2(5f, 8f);
        Image handleImage = handleGo.GetComponent<Image>();
        handleImage.color = new Color(1f, 0.95f, 0.75f, 1f);

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;

        return slider;
    }

    private void CreateStatusLabel()
    {
        RectTransform statusBox = CreateRect(_panelRoot, "StatusBox", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 44f), new Vector2(250f, 66f));
        _statusLabel = CreateText(statusBox, "Text", "Status: Ready", 10f, FontStyles.Normal, TextAlignmentOptions.Left);
        _statusLabel.color = new Color(0.78f, 0.93f, 1f, 1f);
        _statusLabel.enableWordWrapping = false;
        _statusLabel.overflowMode = TextOverflowModes.Ellipsis;
        Stretch(_statusLabel.rectTransform);

        RectTransform pathBox = CreateRect(_panelRoot, "PathBox", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(258f, 44f), new Vector2(-10f, 66f));
        _pathLabel = CreateText(pathBox, "Text", "Path: " + GetCompactDisplayPath(), 9f, FontStyles.Normal, TextAlignmentOptions.Left);
        _pathLabel.color = new Color(0.70f, 0.83f, 0.95f, 1f);
        _pathLabel.enableWordWrapping = false;
        _pathLabel.overflowMode = TextOverflowModes.Ellipsis;
        Stretch(_pathLabel.rectTransform);
    }

    private void CreateActionButtons()
    {
        RectTransform row = CreateRect(_panelRoot, "ActionsRow", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 10f), new Vector2(-10f, 40f));
        _actionsRow = row;
        HorizontalLayoutGroup h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 4f;
        h.padding = new RectOffset(0, 0, 0, 0);
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = false;

        CreateButton(row, "SaveButton", "Save", SaveCurrentToPersistentFile, new Color(0.16f, 0.30f, 0.18f, 1f), 12f);
        CreateButton(row, "LoadButton", "Load", () => TryLoadPersistentFile(showStatusWhenMissing: true), new Color(0.15f, 0.20f, 0.32f, 1f), 12f);
        CreateButton(row, "ExportButton", "Export Copy", ExportSavedCopy, new Color(0.32f, 0.22f, 0.12f, 1f), 12f);
    }

    private Button CreateButton(Transform parent, string name, string text, UnityEngine.Events.UnityAction onClick, Color color, float fontSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.preferredHeight = 30f;

        Image image = go.GetComponent<Image>();
        image.color = color;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() =>
        {
            FunkyAudioSettings.PlayUiConfirm();
            onClick?.Invoke();
        });

        TextMeshProUGUI label = CreateText(go.transform, "Label", text, fontSize, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        return button;
    }

    private static RectTransform CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size, FontStyles style, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (s_tuningFontAsset == null)
            s_tuningFontAsset = Resources.Load<TMP_FontAsset>(TuningFontResourcePath);

        if (s_tuningFontAsset != null)
            tmp.font = s_tuningFontAsset;

        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.enableWordWrapping = false;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void RefreshUiFromTarget()
    {
        if (!_built)
            return;

        for (int i = 0; i < _rows.Count; i++)
        {
            SliderRow row = _rows[i];
            float value = GetFieldValue(row.Spec);
            row.Slider.SetValueWithoutNotify(value);
            row.ValueLabel.text = FormatValue(row.Spec, value);
        }
    }

    private void ResetToDefaults()
    {
        ApplyDefaultPreset(autoSaveOnChange);
    }

    private void SaveCurrentToPersistentFile()
    {
        TuningSaveData data = BuildSaveDataFromCurrentState();
        string path = GetSavePath();

        try
        {
            EnsureDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
            SetStatus("Status: Saved.");
        }
        catch (Exception ex)
        {
            SetStatus("Status: Save failed - " + ex.Message);
        }
    }

    private void TryLoadPersistentFile(bool showStatusWhenMissing)
    {
        string path = GetSavePath();
        if (!File.Exists(path))
        {
            if (showStatusWhenMissing)
                SetStatus("Status: No saved file found.");
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            TuningSaveData data = JsonUtility.FromJson<TuningSaveData>(json);
            if (data == null)
            {
                SetStatus("Status: Load failed (invalid JSON).");
                return;
            }

            _cachedSaveData = SanitizeSaveData(data);
            _cachedValuesAreAuthoritative = true;
            if (_hasTarget)
                ApplySaveData(_cachedSaveData);
            RefreshUiFromTarget();
            SetStatus("Status: Loaded.");
        }
        catch (Exception ex)
        {
            SetStatus("Status: Load failed - " + ex.Message);
        }
    }

    private void ExportSavedCopy()
    {
        string savePath = GetSavePath();
        if (!File.Exists(savePath))
            SaveCurrentToPersistentFile();

        if (!File.Exists(savePath))
        {
            SetStatus("Status: Export failed (no base save file).");
            return;
        }

        string exportDir = GetExportDirectory();
        string exportName = "fishing_joycon_tuning_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".json";
        string exportPath = Path.Combine(exportDir, exportName);

        try
        {
            EnsureDirectory(exportDir);
            File.Copy(savePath, exportPath, overwrite: false);
            SetStatus("Status: Exported copy.");
        }
        catch (Exception ex)
        {
            SetStatus("Status: Export failed - " + ex.Message);
        }
    }

    private TuningSaveData BuildSaveDataFromTarget()
    {
        return new TuningSaveData
        {
            requireBumperOrTriggerHold = ReadField(TuningField.RequireBumperOrTriggerHold) >= 0.5f,
            hasMinimumCatchGradeRank = true,
            minimumCatchGradeRank = Mathf.RoundToInt(ReadField(TuningField.MinimumCatchGrade)),
            castForwardLinG = ReadField(TuningField.CastForwardLinG),
            castGyroDps = ReadField(TuningField.CastGyroDps),
            yankBackLinG = ReadField(TuningField.YankBackLinG),
            yankGyroDps = ReadField(TuningField.YankGyroDps),
            minTimeBetweenCastAndYank = ReadField(TuningField.MinTimeBetweenCastAndYank),
            cooldownAfterTrigger = ReadField(TuningField.CooldownAfterTrigger),
            savedUtc = DateTime.UtcNow.ToString("o")
        };
    }

    private TuningSaveData BuildSaveDataFromCurrentState()
    {
        TuningSaveData data = _hasTarget ? BuildSaveDataFromTarget() : CopySaveData(_cachedSaveData);
        data.savedUtc = DateTime.UtcNow.ToString("o");
        return data;
    }

    private TuningSaveData BuildDefaultSaveData()
    {
        return new TuningSaveData
        {
            requireBumperOrTriggerHold = FindSpec(TuningField.RequireBumperOrTriggerHold).DefaultValue >= 0.5f,
            hasMinimumCatchGradeRank = true,
            minimumCatchGradeRank = Mathf.RoundToInt(FindSpec(TuningField.MinimumCatchGrade).DefaultValue),
            castForwardLinG = FindSpec(TuningField.CastForwardLinG).DefaultValue,
            castGyroDps = FindSpec(TuningField.CastGyroDps).DefaultValue,
            yankBackLinG = FindSpec(TuningField.YankBackLinG).DefaultValue,
            yankGyroDps = FindSpec(TuningField.YankGyroDps).DefaultValue,
            minTimeBetweenCastAndYank = FindSpec(TuningField.MinTimeBetweenCastAndYank).DefaultValue,
            cooldownAfterTrigger = FindSpec(TuningField.CooldownAfterTrigger).DefaultValue,
            savedUtc = string.Empty
        };
    }

    private void ApplySaveData(TuningSaveData data)
    {
        WriteField(TuningField.RequireBumperOrTriggerHold, data.requireBumperOrTriggerHold ? 1f : 0f);
        WriteField(
            TuningField.MinimumCatchGrade,
            data.hasMinimumCatchGradeRank ? data.minimumCatchGradeRank : (int)FishingSessionHud.CatchGradeRank.C);
        WriteField(TuningField.CastForwardLinG, data.castForwardLinG);
        WriteField(TuningField.CastGyroDps, data.castGyroDps);
        WriteField(TuningField.YankBackLinG, data.yankBackLinG);
        WriteField(TuningField.YankGyroDps, data.yankGyroDps);
        WriteField(TuningField.MinTimeBetweenCastAndYank, data.minTimeBetweenCastAndYank);
        WriteField(TuningField.CooldownAfterTrigger, data.cooldownAfterTrigger);
    }

    private float ReadField(TuningField field)
    {
        if (gestureDetector == null)
            return 0f;

        switch (field)
        {
            case TuningField.RequireBumperOrTriggerHold: return gestureDetector.requireBumperOrTriggerHold ? 1f : 0f;
            case TuningField.MinimumCatchGrade: return FishingSessionHud.MinimumSuccessfulCatchGradeRank;
            case TuningField.CastForwardLinG: return gestureDetector.castForwardLinG;
            case TuningField.CastGyroDps: return gestureDetector.castGyroDps;
            case TuningField.YankBackLinG: return gestureDetector.yankBackLinG;
            case TuningField.YankGyroDps: return gestureDetector.yankGyroDps;
            case TuningField.MinTimeBetweenCastAndYank: return gestureDetector.minTimeBetweenCastAndYank;
            case TuningField.CooldownAfterTrigger: return gestureDetector.cooldownAfterTrigger;
            default: return 0f;
        }
    }

    private void WriteField(TuningField field, float value)
    {
        if (gestureDetector == null)
            return;

        FieldSpec spec = FindSpec(field);
        float clamped = Mathf.Clamp(value, spec.Min, spec.Max);

        switch (field)
        {
            case TuningField.RequireBumperOrTriggerHold: gestureDetector.requireBumperOrTriggerHold = clamped >= 0.5f; break;
            case TuningField.MinimumCatchGrade: FishingSessionHud.MinimumSuccessfulCatchGradeRank = Mathf.RoundToInt(clamped); break;
            case TuningField.CastForwardLinG: gestureDetector.castForwardLinG = clamped; break;
            case TuningField.CastGyroDps: gestureDetector.castGyroDps = clamped; break;
            case TuningField.YankBackLinG: gestureDetector.yankBackLinG = clamped; break;
            case TuningField.YankGyroDps: gestureDetector.yankGyroDps = clamped; break;
            case TuningField.MinTimeBetweenCastAndYank: gestureDetector.minTimeBetweenCastAndYank = clamped; break;
            case TuningField.CooldownAfterTrigger: gestureDetector.cooldownAfterTrigger = clamped; break;
        }
    }

    private static FieldSpec FindSpec(TuningField field)
    {
        for (int i = 0; i < Specs.Length; i++)
        {
            if (Specs[i].Field == field)
                return Specs[i];
        }

        return Specs[0];
    }

    private static void EnsureDirectory(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    private string GetSavePath() => Path.Combine(Application.persistentDataPath, saveFileName);
    private string GetExportDirectory() => Path.Combine(Application.persistentDataPath, exportFolderName);

    private string GetCompactDisplayPath()
    {
        string normalized = GetSavePath().Replace('\\', '/');
        const int keepChars = 64;
        if (normalized.Length <= keepChars)
            return normalized;
        return "..." + normalized.Substring(normalized.Length - keepChars);
    }

    private void SetStatus(string message)
    {
        if (_statusLabel != null)
            _statusLabel.text = message;
    }

    private float GetFieldValue(FieldSpec spec)
    {
        if (gestureDetector == null)
            return ReadCachedField(spec.Field);

        return ReadField(spec.Field);
    }

    private void ApplyFieldValue(FieldSpec spec, float value)
    {
        WriteCachedField(spec.Field, value);
        _cachedValuesAreAuthoritative = true;

        if (_hasTarget)
            WriteField(spec.Field, value);
    }

    private static string FormatValue(FieldSpec spec, float value)
    {
        if (spec.IsToggle)
            return value >= 0.5f ? "On" : "Off";
        if (spec.IsGrade)
            return FishingSessionHud.GetGradeLetterForRank(Mathf.RoundToInt(value));

        return value.ToString("F" + Mathf.Max(0, spec.Decimals));
    }

    private void UpdateTargetLabel(string text)
    {
        if (_targetLabel != null)
            _targetLabel.text = text;
    }

    private void HandleTargetChanged()
    {
        _hasTarget = gestureDetector != null;
        if (!_hasTarget)
            return;

        if (_cachedValuesAreAuthoritative)
        {
            ApplySaveData(_cachedSaveData);
            return;
        }

        _cachedSaveData = BuildSaveDataFromTarget();
    }

    private float ReadCachedField(TuningField field)
    {
        switch (field)
        {
            case TuningField.RequireBumperOrTriggerHold: return _cachedSaveData.requireBumperOrTriggerHold ? 1f : 0f;
            case TuningField.MinimumCatchGrade: return _cachedSaveData.hasMinimumCatchGradeRank ? _cachedSaveData.minimumCatchGradeRank : FindSpec(field).DefaultValue;
            case TuningField.CastForwardLinG: return _cachedSaveData.castForwardLinG;
            case TuningField.CastGyroDps: return _cachedSaveData.castGyroDps;
            case TuningField.YankBackLinG: return _cachedSaveData.yankBackLinG;
            case TuningField.YankGyroDps: return _cachedSaveData.yankGyroDps;
            case TuningField.MinTimeBetweenCastAndYank: return _cachedSaveData.minTimeBetweenCastAndYank;
            case TuningField.CooldownAfterTrigger: return _cachedSaveData.cooldownAfterTrigger;
            default: return FindSpec(field).DefaultValue;
        }
    }

    private void WriteCachedField(TuningField field, float value)
    {
        FieldSpec spec = FindSpec(field);
        float clamped = Mathf.Clamp(value, spec.Min, spec.Max);

        switch (field)
        {
            case TuningField.RequireBumperOrTriggerHold:
                _cachedSaveData.requireBumperOrTriggerHold = clamped >= 0.5f;
                break;
            case TuningField.MinimumCatchGrade:
                _cachedSaveData.hasMinimumCatchGradeRank = true;
                _cachedSaveData.minimumCatchGradeRank = Mathf.RoundToInt(clamped);
                break;
            case TuningField.CastForwardLinG:
                _cachedSaveData.castForwardLinG = clamped;
                break;
            case TuningField.CastGyroDps:
                _cachedSaveData.castGyroDps = clamped;
                break;
            case TuningField.YankBackLinG:
                _cachedSaveData.yankBackLinG = clamped;
                break;
            case TuningField.YankGyroDps:
                _cachedSaveData.yankGyroDps = clamped;
                break;
            case TuningField.MinTimeBetweenCastAndYank:
                _cachedSaveData.minTimeBetweenCastAndYank = clamped;
                break;
            case TuningField.CooldownAfterTrigger:
                _cachedSaveData.cooldownAfterTrigger = clamped;
                break;
        }
    }

    private TuningSaveData SanitizeSaveData(TuningSaveData data)
    {
        TuningSaveData sanitized = BuildDefaultSaveData();
        if (data == null)
            return sanitized;

        sanitized.requireBumperOrTriggerHold = data.requireBumperOrTriggerHold;
        sanitized.hasMinimumCatchGradeRank = true;
        sanitized.minimumCatchGradeRank = Mathf.RoundToInt(Mathf.Clamp(
            data.hasMinimumCatchGradeRank ? data.minimumCatchGradeRank : FindSpec(TuningField.MinimumCatchGrade).DefaultValue,
            FindSpec(TuningField.MinimumCatchGrade).Min,
            FindSpec(TuningField.MinimumCatchGrade).Max));
        sanitized.castForwardLinG = Mathf.Clamp(data.castForwardLinG, FindSpec(TuningField.CastForwardLinG).Min, FindSpec(TuningField.CastForwardLinG).Max);
        sanitized.castGyroDps = Mathf.Clamp(data.castGyroDps, FindSpec(TuningField.CastGyroDps).Min, FindSpec(TuningField.CastGyroDps).Max);
        sanitized.yankBackLinG = Mathf.Clamp(data.yankBackLinG, FindSpec(TuningField.YankBackLinG).Min, FindSpec(TuningField.YankBackLinG).Max);
        sanitized.yankGyroDps = Mathf.Clamp(data.yankGyroDps, FindSpec(TuningField.YankGyroDps).Min, FindSpec(TuningField.YankGyroDps).Max);
        sanitized.minTimeBetweenCastAndYank = Mathf.Clamp(data.minTimeBetweenCastAndYank, FindSpec(TuningField.MinTimeBetweenCastAndYank).Min, FindSpec(TuningField.MinTimeBetweenCastAndYank).Max);
        sanitized.cooldownAfterTrigger = Mathf.Clamp(data.cooldownAfterTrigger, FindSpec(TuningField.CooldownAfterTrigger).Min, FindSpec(TuningField.CooldownAfterTrigger).Max);
        sanitized.savedUtc = data.savedUtc ?? string.Empty;
        return sanitized;
    }

    private static TuningSaveData CopySaveData(TuningSaveData source)
    {
        if (source == null)
            return null;

        return new TuningSaveData
        {
            requireBumperOrTriggerHold = source.requireBumperOrTriggerHold,
            hasMinimumCatchGradeRank = source.hasMinimumCatchGradeRank,
            minimumCatchGradeRank = source.minimumCatchGradeRank,
            castForwardLinG = source.castForwardLinG,
            castGyroDps = source.castGyroDps,
            yankBackLinG = source.yankBackLinG,
            yankGyroDps = source.yankGyroDps,
            minTimeBetweenCastAndYank = source.minTimeBetweenCastAndYank,
            cooldownAfterTrigger = source.cooldownAfterTrigger,
            savedUtc = source.savedUtc
        };
    }
}

public static class FishingPauseTuningPanelBootstrap
{
    private static bool _hookedSceneLoaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallBootstrap()
    {
        if (!_hookedSceneLoaded)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            _hookedSceneLoaded = true;
        }

        InstallForScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallForScene();
    }

    private static void InstallForScene()
    {
        bool hasFishingTarget = UnityEngine.Object.FindObjectOfType<JoyConGestureDetector>(true) != null;
        if (!hasFishingTarget)
            return;

        PauseManager[] managers = UnityEngine.Object.FindObjectsOfType<PauseManager>(true);
        for (int i = 0; i < managers.Length; i++)
        {
            PauseManager manager = managers[i];
            if (manager == null)
                continue;

            if (manager.GetComponent<FishingPauseTuningPanel>() == null)
                manager.gameObject.AddComponent<FishingPauseTuningPanel>();
        }
    }
}

