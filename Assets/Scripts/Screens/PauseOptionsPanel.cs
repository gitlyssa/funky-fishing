using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(PauseManager))]
public class PauseOptionsPanel : MonoBehaviour
{
    private enum OptionsTab
    {
        GeneralSettings,
        Controllers,
        RhythmAdvanced,
        FishingAdvanced
    }

    private sealed class PlaceholderPage
    {
        public RectTransform Root;
        public Button BackButton;
        public Button ResetButton;
        public TextMeshProUGUI MessageLabel;
    }

    private sealed class GeneralSettingsPage
    {
        public RectTransform Root;
        public Button BackButton;
        public Button ResetButton;
        public GeneralSliderRow RhythmSensitivityRow;
        public readonly List<VolumeSliderRow> VolumeRows = new List<VolumeSliderRow>();
        public ToggleSettingRow FullscreenToggle;
        public ToggleSettingRow RhythmAdvancedToggle;
        public ToggleSettingRow FishingAdvancedToggle;
    }

    private sealed class GeneralSliderRow
    {
        public RectTransform Root;
        public Image Background;
        public TextMeshProUGUI TitleLabel;
        public TextMeshProUGUI DescriptionLabel;
        public Slider Slider;
        public TextMeshProUGUI ValueLabel;
        public Func<float> Getter;
        public Action<float> Setter;
        public Func<float, string> Formatter;
    }

    private sealed class VolumeSliderRow
    {
        public Slider Slider;
        public TextMeshProUGUI ValueLabel;
        public Func<float> Getter;
        public Action<float> Setter;
    }

    private sealed class ToggleSettingRow
    {
        public Button Button;
        public TextMeshProUGUI Label;
    }

    private sealed class ControllerStatusPage
    {
        public RectTransform Root;
        public Button BackButton;
        public ControllerManagerScript Manager;
    }

    [Header("Pause Menu Button")]
    [SerializeField] private string openButtonName = "OptionsButton";
    [SerializeField] private Vector2 openButtonSize = new Vector2(180f, 34f);
    [SerializeField] private Vector2 openButtonOffset = new Vector2(-20f, 20f);
    [SerializeField] private bool adaptToMirroredPausePanel = true;

    [Header("Panel Layout")]
    [SerializeField] private string panelObjectName = "OptionsPanel";
    [SerializeField] private Vector2 panelSize = new Vector2(980f, 620f);
    [SerializeField] private Vector2 panelOffset = Vector2.zero;
    [SerializeField] private Vector2 panelMaxViewportPercent = new Vector2(0.94f, 0.88f);

    private PauseManager _pauseManager;
    private FishingPauseTuningPanel _fishingPanel;
    private RhythmPauseTuningPanel _rhythmPanel;

    private Button _openButton;
    private RectTransform _panelRoot;
    private RectTransform _contentRoot;
    private RectTransform _fishingHostRoot;
    private RectTransform _rhythmHostRoot;
    private GeneralSettingsPage _generalPage;
    private ControllerStatusPage _controllersPage;
    private PlaceholderPage _rhythmLockedPage;
    private PlaceholderPage _fishingLockedPage;
    private Button _generalTabButton;
    private Button _controllersTabButton;
    private Button _rhythmTabButton;
    private Button _fishingTabButton;

    private bool _built;
    private bool _wasPauseOpen;
    private bool _isOptionsOpen;
    private bool _standaloneSceneMode;
    private string _standaloneBackSceneName = "MainMenu";
    private bool _fullscreenEnabled;
    private float _generalRhythmSensitivity;
    private bool _rhythmAdvancedEnabled;
    private bool _fishingAdvancedEnabled;
    private OptionsTab _activeTab = OptionsTab.GeneralSettings;

    private const string FullscreenPrefKey = "FunkyFishing.Options.FullscreenEnabled";
    private const string GeneralRhythmSensitivityPrefKey = "FunkyFishing.Options.GeneralRhythmSensitivity";
    private const string RhythmAdvancedPrefKey = "FunkyFishing.Options.RhythmAdvancedEnabled";
    private const string LegacyRhythmAdvancedPrefKey = "FunkyFishing.Options.RythmAdvancedEnabled";
    private const string FishingAdvancedPrefKey = "FunkyFishing.Options.FishingAdvancedEnabled";

    private void Awake()
    {
        _fullscreenEnabled = PlayerPrefs.GetInt(FullscreenPrefKey, 1) != 0;
        _generalRhythmSensitivity = Mathf.Clamp01(PlayerPrefs.GetFloat(GeneralRhythmSensitivityPrefKey, RhythmPauseTuningPanel.GeneralSensitivityDefault));
        _rhythmAdvancedEnabled =
            PlayerPrefs.GetInt(RhythmAdvancedPrefKey, PlayerPrefs.GetInt(LegacyRhythmAdvancedPrefKey, 0)) != 0;
        _fishingAdvancedEnabled = PlayerPrefs.GetInt(FishingAdvancedPrefKey, 0) != 0;

        _pauseManager = GetComponent<PauseManager>();
        _fishingPanel = GetComponent<FishingPauseTuningPanel>();
        if (_fishingPanel == null)
            _fishingPanel = gameObject.AddComponent<FishingPauseTuningPanel>();

        _rhythmPanel = GetComponent<RhythmPauseTuningPanel>();
        if (_rhythmPanel == null)
            _rhythmPanel = gameObject.AddComponent<RhythmPauseTuningPanel>();
    }

    private void Start()
    {
        ApplyFullscreenSetting(_fullscreenEnabled);
        BuildUiIfNeeded();
        PrepareHostedPanels();
        ApplyGeneralRhythmSensitivityIfNeeded(persist: true);
        ApplyFishingDefaultsIfNeeded(persist: true);
        AdjustOpenButtonPlacement();
        AdjustPanelToViewport();

        if (_standaloneSceneMode)
        {
            OpenOptionsPanel();
            return;
        }

        CloseOptionsPanelInternal(selectOpenButton: false);
    }

    private void Update()
    {
        if (_standaloneSceneMode)
        {
            AdjustPanelToViewport();
            SetOpenButtonActive(false);

            if (!_isOptionsOpen)
                OpenOptionsPanel();

            return;
        }

        if (_pauseManager == null || _pauseManager.PausePanel == null)
            return;

        bool pauseOpen = _pauseManager.PausePanel.activeInHierarchy;
        if (!pauseOpen)
        {
            _wasPauseOpen = false;
            CloseOptionsPanelInternal(selectOpenButton: false);
            SetOpenButtonActive(false);
            return;
        }

        AdjustOpenButtonPlacement();
        AdjustPanelToViewport();
        SyncOpenButtonVisibility();

        if (!_wasPauseOpen)
            CloseOptionsPanelInternal(selectOpenButton: false);

        _wasPauseOpen = true;
    }

    public bool IsOptionsPanelOpen() => _isOptionsOpen;

    public void ConfigureStandaloneScene(string backSceneName = "MainMenu")
    {
        _standaloneSceneMode = true;
        if (!string.IsNullOrWhiteSpace(backSceneName))
            _standaloneBackSceneName = backSceneName;

        _activeTab = OptionsTab.GeneralSettings;
    }

    public void OpenOptionsPanel()
    {
        if (_panelRoot == null)
            return;

        PrepareHostedPanels();
        AdjustPanelToViewport();
        _isOptionsOpen = true;
        _panelRoot.gameObject.SetActive(true);
        SetOpenButtonActive(false);
        ShowTab(OptionsTab.GeneralSettings);
    }

    public void CloseOptionsPanel()
    {
        if (_standaloneSceneMode)
        {
            SceneManager.LoadScene(_standaloneBackSceneName);
            return;
        }

        CloseOptionsPanelInternal(selectOpenButton: true);
    }

    public void EnsureSelection()
    {
        if (!_isOptionsOpen || _panelRoot == null)
            return;

        EventSystem evt = EventSystem.current;
        if (evt == null)
            return;

        GameObject selected = evt.currentSelectedGameObject;
        if (selected != null && selected.activeInHierarchy && selected.transform.IsChildOf(_panelRoot))
            return;

        SelectDefaultForActiveTab();
    }

    private void CloseOptionsPanelInternal(bool selectOpenButton)
    {
        _isOptionsOpen = false;
        HideAllTabContent();

        if (_panelRoot != null)
            _panelRoot.gameObject.SetActive(false);

        SyncOpenButtonVisibility();

        if (!selectOpenButton)
            return;

        EventSystem evt = EventSystem.current;
        if (evt == null || _openButton == null || !_openButton.gameObject.activeInHierarchy)
            return;

        evt.SetSelectedGameObject(_openButton.gameObject);
    }

    private void BuildUiIfNeeded()
    {
        if (_built || _pauseManager == null || _pauseManager.PausePanel == null)
            return;

        Transform parent = _pauseManager.PausePanel.transform;
        if (!_standaloneSceneMode)
            CreateOpenButton(parent);
        CreatePanel(parent);
        _built = true;
    }

    private void PrepareHostedPanels()
    {
        if (!_built)
            return;

        if (_fishingPanel != null && _fishingHostRoot != null)
            _fishingPanel.PrepareForUnifiedOptions(_fishingHostRoot, CloseOptionsPanel);

        if (_rhythmPanel != null && _rhythmHostRoot != null)
            _rhythmPanel.PrepareForUnifiedOptions(_rhythmHostRoot, CloseOptionsPanel);
    }

    private void CreateOpenButton(Transform parent)
    {
        Transform existing = parent.Find(openButtonName);
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject go = new GameObject(openButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.17f, 0.24f, 0.29f, 0.95f);

        _openButton = go.GetComponent<Button>();
        _openButton.targetGraphic = image;
        _openButton.onClick.AddListener(() =>
        {
            FunkyAudioSettings.PlayUiConfirm();
            OpenOptionsPanel();
        });

        TextMeshProUGUI label = CreateText(go.transform, "Label", "Options", 13f, FontStyles.Bold, TextAlignmentOptions.Center);
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

        RectTransform tabRow = CreateRect(_panelRoot, "TabRow", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -46f), new Vector2(-12f, -10f));
        HorizontalLayoutGroup tabLayout = tabRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 6f;
        tabLayout.padding = new RectOffset(0, 0, 0, 0);
        tabLayout.childAlignment = TextAnchor.MiddleCenter;
        tabLayout.childControlWidth = true;
        tabLayout.childControlHeight = true;
        tabLayout.childForceExpandWidth = true;
        tabLayout.childForceExpandHeight = false;

        _generalTabButton = CreateTabButton(tabRow, "GeneralSettingsTabButton", "General Settings", OptionsTab.GeneralSettings);
        _controllersTabButton = CreateTabButton(tabRow, "ControllersTabButton", "Controllers", OptionsTab.Controllers);
        _rhythmTabButton = CreateTabButton(tabRow, "RhythmAdvancedTabButton", "Rhythm Advanced", OptionsTab.RhythmAdvanced);
        _fishingTabButton = CreateTabButton(tabRow, "FishingAdvancedTabButton", "Fishing Advanced", OptionsTab.FishingAdvanced);

        _contentRoot = CreateRect(_panelRoot, "ContentRoot", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 12f), new Vector2(-12f, -54f));

        _generalPage = CreateGeneralSettingsPage(_contentRoot, "GeneralSettingsPage");
        _controllersPage = CreateControllerStatusPage(_contentRoot, "ControllersPage");
        _rhythmLockedPage = CreateAdvancedSettingsLockedPage(
            _contentRoot,
            "RhythmAdvancedLockedPage",
            "Rhythm Advanced",
            "Rhythm advanced settings are currently off.\n\nTurn on Rhythm Advanced in the General Settings tab to change this tuning.");
        _fishingLockedPage = CreateAdvancedSettingsLockedPage(
            _contentRoot,
            "FishingAdvancedLockedPage",
            "Fishing Advanced",
            "Fishing advanced settings are currently off.\n\nTurn on Fishing Advanced in the General Settings tab to change this tuning.");
        _rhythmHostRoot = CreatePageRoot(_contentRoot, "RhythmAdvancedPage");
        _fishingHostRoot = CreatePageRoot(_contentRoot, "FishingAdvancedPage");
        RefreshGeneralSettingsUi();
    }

    private Button CreateTabButton(Transform parent, string name, string label, OptionsTab tab)
    {
        Button button = CreateButton(parent, name, label, () => ShowTab(tab), new Color(0.16f, 0.18f, 0.22f, 1f), 12f);
        if (button.TryGetComponent(out LayoutElement layout))
            layout.preferredHeight = 32f;
        return button;
    }

    private PlaceholderPage CreatePlaceholderPage(Transform parent, string name, string title, string message)
    {
        PlaceholderPage page = new PlaceholderPage();
        page.Root = CreatePageRoot(parent, name);

        RectTransform header = CreateRect(page.Root, "Header", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -38f), new Vector2(-10f, -8f));

        page.BackButton = CreateButton(header, "BackButton", "Back", CloseOptionsPanel, new Color(0.35f, 0.20f, 0.20f, 1f), 12f);
        RectTransform backRect = page.BackButton.transform as RectTransform;
        backRect.anchorMin = new Vector2(0f, 0f);
        backRect.anchorMax = new Vector2(0f, 1f);
        backRect.pivot = new Vector2(0f, 0.5f);
        backRect.sizeDelta = new Vector2(92f, 0f);
        backRect.anchoredPosition = Vector2.zero;

        page.ResetButton = CreateButton(
            header,
            "ResetDefaultsButton",
            "Reset Defaults",
            () => page.MessageLabel.text = "No settings are available to reset on this tab yet.",
            new Color(0.3f, 0.16f, 0.16f, 1f),
            11f);
        RectTransform resetRect = page.ResetButton.transform as RectTransform;
        resetRect.anchorMin = new Vector2(1f, 0f);
        resetRect.anchorMax = new Vector2(1f, 1f);
        resetRect.pivot = new Vector2(1f, 0.5f);
        resetRect.sizeDelta = new Vector2(128f, 0f);
        resetRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI titleLabel = CreateText(header, "Title", title, 16f, FontStyles.Bold, TextAlignmentOptions.Center);
        titleLabel.color = new Color(1f, 0.95f, 0.72f, 1f);
        Stretch(titleLabel.rectTransform);
        titleLabel.rectTransform.offsetMin = new Vector2(98f, 0f);
        titleLabel.rectTransform.offsetMax = new Vector2(-134f, 0f);

        RectTransform body = CreateRect(page.Root, "Body", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 24f), new Vector2(-24f, -54f));
        Image bodyBg = body.gameObject.AddComponent<Image>();
        bodyBg.color = new Color(0.05f, 0.05f, 0.06f, 0.90f);

        page.MessageLabel = CreateText(body, "Message", message, 18f, FontStyles.Normal, TextAlignmentOptions.Center);
        page.MessageLabel.textWrappingMode = TextWrappingModes.Normal;
        page.MessageLabel.color = new Color(0.82f, 0.88f, 0.94f, 1f);
        Stretch(page.MessageLabel.rectTransform);
        page.Root.gameObject.SetActive(false);
        return page;
    }

    private GeneralSettingsPage CreateGeneralSettingsPage(Transform parent, string name)
    {
        GeneralSettingsPage page = new GeneralSettingsPage();
        page.Root = CreatePageRoot(parent, name);

        RectTransform header = CreateRect(page.Root, "Header", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -38f), new Vector2(-10f, -8f));

        page.BackButton = CreateButton(header, "BackButton", "Back", CloseOptionsPanel, new Color(0.35f, 0.20f, 0.20f, 1f), 12f);
        RectTransform backRect = page.BackButton.transform as RectTransform;
        backRect.anchorMin = new Vector2(0f, 0f);
        backRect.anchorMax = new Vector2(0f, 1f);
        backRect.pivot = new Vector2(0f, 0.5f);
        backRect.sizeDelta = new Vector2(92f, 0f);
        backRect.anchoredPosition = Vector2.zero;

        page.ResetButton = CreateButton(
            header,
            "ResetDefaultsButton",
            "Reset Defaults",
            ResetGeneralSettingsToDefaults,
            new Color(0.3f, 0.16f, 0.16f, 1f),
            11f);
        RectTransform resetRect = page.ResetButton.transform as RectTransform;
        resetRect.anchorMin = new Vector2(1f, 0f);
        resetRect.anchorMax = new Vector2(1f, 1f);
        resetRect.pivot = new Vector2(1f, 0.5f);
        resetRect.sizeDelta = new Vector2(128f, 0f);
        resetRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI titleLabel = CreateText(header, "Title", "General Settings", 16f, FontStyles.Bold, TextAlignmentOptions.Center);
        titleLabel.color = new Color(1f, 0.95f, 0.72f, 1f);
        Stretch(titleLabel.rectTransform);
        titleLabel.rectTransform.offsetMin = new Vector2(98f, 0f);
        titleLabel.rectTransform.offsetMax = new Vector2(-134f, 0f);

        RectTransform body = CreateRect(page.Root, "Body", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 24f), new Vector2(-24f, -54f));
        Image bodyBg = body.gameObject.AddComponent<Image>();
        bodyBg.color = new Color(0.05f, 0.05f, 0.06f, 0.90f);

        GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportGo.transform.SetParent(body, false);
        RectTransform viewport = viewportGo.GetComponent<RectTransform>();
        Stretch(viewport);
        viewport.offsetMax = new Vector2(-14f, 0f);
        Image viewportImage = viewportGo.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
        viewportGo.GetComponent<Mask>().showMaskGraphic = false;

        GameObject columnGo = new GameObject("SettingsColumn", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        columnGo.transform.SetParent(viewport, false);
        RectTransform column = columnGo.GetComponent<RectTransform>();
        column.anchorMin = new Vector2(0f, 1f);
        column.anchorMax = new Vector2(1f, 1f);
        column.pivot = new Vector2(0.5f, 1f);
        column.anchoredPosition = Vector2.zero;
        column.offsetMin = new Vector2(18f, 0f);
        column.offsetMax = new Vector2(-18f, 0f);

        VerticalLayoutGroup columnLayout = columnGo.GetComponent<VerticalLayoutGroup>();
        columnLayout.spacing = 6f;
        columnLayout.padding = new RectOffset(0, 0, 8, 8);
        columnLayout.childAlignment = TextAnchor.UpperLeft;
        columnLayout.childControlWidth = true;
        columnLayout.childControlHeight = true;
        columnLayout.childForceExpandWidth = true;
        columnLayout.childForceExpandHeight = false;

        ContentSizeFitter contentFitter = columnGo.GetComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scrollRect = body.gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.content = column;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;
        scrollRect.verticalScrollbar = CreateGeneralSettingsScrollbar(body);
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.verticalScrollbarSpacing = 8f;

        page.RhythmSensitivityRow = CreateGeneralSliderCard(
            column,
            "RhythmSensitivityCard",
            "Joy-Con Rhythm Sensitivity",
            "Higher values make rhythm input stricter and less prone to accidental triggers. Lower values make it looser and easier to trigger by mistake.",
            () => _generalRhythmSensitivity,
            SetGeneralRhythmSensitivity,
            FormatGeneralRhythmSensitivityValue);
        page.VolumeRows.Add(CreateVolumeSliderCard(
            column,
            "MasterVolumeCard",
            "Master Volume",
            "Controls the overall level of sound in the game.",
            () => FunkyAudioSettings.MasterVolume,
            FunkyAudioSettings.SetMasterVolume));
        page.VolumeRows.Add(CreateVolumeSliderCard(
            column,
            "MusicVolumeCard",
            "Music Volume",
            "Controls the rhythm encounter and fish track volume.",
            () => FunkyAudioSettings.MusicVolume,
            FunkyAudioSettings.SetMusicVolume));
        page.VolumeRows.Add(CreateVolumeSliderCard(
            column,
            "AmbientVolumeCard",
            "Ambient / BG Volume",
            "Controls pond ambience and future menu/background music volume.",
            () => FunkyAudioSettings.AmbientVolume,
            FunkyAudioSettings.SetAmbientVolume));
        page.VolumeRows.Add(CreateVolumeSliderCard(
            column,
            "SfxVolumeCard",
            "Sound Effects",
            "Controls sound effects like casts, bites, hooks, and note hit sounds.",
            () => FunkyAudioSettings.SfxVolume,
            FunkyAudioSettings.SetSfxVolume));
        page.FullscreenToggle = CreateGeneralToggleCard(
            column,
            "DisplayModeCard",
            "Display Mode",
            "Switches between fullscreen and windowed mode.",
            () => SetFullscreenEnabled(!_fullscreenEnabled));

        page.RhythmAdvancedToggle = CreateGeneralToggleCard(
            column,
            "RhythmAdvancedCard",
            "Rhythm Advanced",
            "Turn this on to edit the Rhythm Advanced tuning tab.",
            () => SetRhythmAdvancedEnabled(!_rhythmAdvancedEnabled));
        page.FishingAdvancedToggle = CreateGeneralToggleCard(
            column,
            "FishingAdvancedCard",
            "Fishing Advanced",
            "Turn this on to edit the Fishing Advanced tuning tab.",
            () => SetFishingAdvancedEnabled(!_fishingAdvancedEnabled));

        page.Root.gameObject.SetActive(false);
        return page;
    }

    private static VolumeSliderRow CreateVolumeSliderCard(
        Transform parent,
        string name,
        string title,
        string description,
        Func<float> getter,
        Action<float> setter)
    {
        GameObject cardGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        cardGo.transform.SetParent(parent, false);

        LayoutElement cardLayout = cardGo.GetComponent<LayoutElement>();
        cardLayout.preferredHeight = 72f;

        Image cardBg = cardGo.GetComponent<Image>();
        cardBg.color = new Color(0.10f, 0.11f, 0.14f, 0.92f);

        RectTransform card = cardGo.GetComponent<RectTransform>();

        TextMeshProUGUI titleLabel = CreateText(card, "Title", title, 14f, FontStyles.Bold, TextAlignmentOptions.Left);
        titleLabel.color = new Color(1f, 0.95f, 0.72f, 1f);
        titleLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleLabel.rectTransform.offsetMin = new Vector2(16f, -28f);
        titleLabel.rectTransform.offsetMax = new Vector2(-120f, -8f);

        TextMeshProUGUI descriptionLabel = CreateText(card, "Description", description, 9f, FontStyles.Normal, TextAlignmentOptions.Left);
        descriptionLabel.textWrappingMode = TextWrappingModes.Normal;
        descriptionLabel.color = new Color(0.82f, 0.88f, 0.94f, 1f);
        descriptionLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        descriptionLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        descriptionLabel.rectTransform.offsetMin = new Vector2(16f, -48f);
        descriptionLabel.rectTransform.offsetMax = new Vector2(-112f, -22f);

        TextMeshProUGUI valueLabel = CreateText(card, "Value", "100%", 13f, FontStyles.Bold, TextAlignmentOptions.Right);
        valueLabel.color = new Color(0.95f, 0.87f, 0.45f, 1f);
        valueLabel.rectTransform.anchorMin = new Vector2(1f, 1f);
        valueLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        valueLabel.rectTransform.pivot = new Vector2(1f, 1f);
        valueLabel.rectTransform.sizeDelta = new Vector2(92f, 20f);
        valueLabel.rectTransform.anchoredPosition = new Vector2(-16f, -11f);

        Slider slider = CreateStandardSlider(card, "Slider");
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;

        RectTransform sliderRect = slider.transform as RectTransform;
        sliderRect.anchorMin = new Vector2(0f, 0f);
        sliderRect.anchorMax = new Vector2(1f, 0f);
        sliderRect.offsetMin = new Vector2(16f, 10f);
        sliderRect.offsetMax = new Vector2(-16f, 20f);

        VolumeSliderRow row = new VolumeSliderRow
        {
            Slider = slider,
            ValueLabel = valueLabel,
            Getter = getter,
            Setter = setter
        };

        slider.onValueChanged.AddListener(v =>
        {
            row.Setter?.Invoke(v);
            row.ValueLabel.text = FormatVolumePercent(v);
        });

        return row;
    }

    private static GeneralSliderRow CreateGeneralSliderCard(
        Transform parent,
        string name,
        string title,
        string description,
        Func<float> getter,
        Action<float> setter,
        Func<float, string> formatter)
    {
        GameObject cardGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        cardGo.transform.SetParent(parent, false);

        LayoutElement cardLayout = cardGo.GetComponent<LayoutElement>();
        cardLayout.preferredHeight = 72f;

        Image cardBg = cardGo.GetComponent<Image>();
        cardBg.color = new Color(0.10f, 0.11f, 0.14f, 0.92f);

        RectTransform card = cardGo.GetComponent<RectTransform>();

        TextMeshProUGUI titleLabel = CreateText(card, "Title", title, 14f, FontStyles.Bold, TextAlignmentOptions.Left);
        titleLabel.color = new Color(1f, 0.95f, 0.72f, 1f);
        titleLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleLabel.rectTransform.offsetMin = new Vector2(16f, -28f);
        titleLabel.rectTransform.offsetMax = new Vector2(-120f, -8f);

        TextMeshProUGUI descriptionLabel = CreateText(card, "Description", description, 9f, FontStyles.Normal, TextAlignmentOptions.Left);
        descriptionLabel.textWrappingMode = TextWrappingModes.Normal;
        descriptionLabel.color = new Color(0.82f, 0.88f, 0.94f, 1f);
        descriptionLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        descriptionLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        descriptionLabel.rectTransform.offsetMin = new Vector2(16f, -48f);
        descriptionLabel.rectTransform.offsetMax = new Vector2(-112f, -22f);

        TextMeshProUGUI valueLabel = CreateText(card, "Value", formatter != null ? formatter(getter()) : string.Empty, 13f, FontStyles.Bold, TextAlignmentOptions.Right);
        valueLabel.color = new Color(0.95f, 0.87f, 0.45f, 1f);
        valueLabel.rectTransform.anchorMin = new Vector2(1f, 1f);
        valueLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        valueLabel.rectTransform.pivot = new Vector2(1f, 1f);
        valueLabel.rectTransform.sizeDelta = new Vector2(100f, 20f);
        valueLabel.rectTransform.anchoredPosition = new Vector2(-16f, -11f);

        Slider slider = CreateStandardSlider(card, "Slider");
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;

        RectTransform sliderRect = slider.transform as RectTransform;
        sliderRect.anchorMin = new Vector2(0f, 0f);
        sliderRect.anchorMax = new Vector2(1f, 0f);
        sliderRect.offsetMin = new Vector2(16f, 10f);
        sliderRect.offsetMax = new Vector2(-16f, 20f);

        GeneralSliderRow row = new GeneralSliderRow
        {
            Root = card,
            Background = cardBg,
            TitleLabel = titleLabel,
            DescriptionLabel = descriptionLabel,
            Slider = slider,
            ValueLabel = valueLabel,
            Getter = getter,
            Setter = setter,
            Formatter = formatter
        };

        slider.onValueChanged.AddListener(v =>
        {
            row.Setter?.Invoke(v);
            if (row.ValueLabel != null && row.Formatter != null)
                row.ValueLabel.text = row.Formatter(v);
        });

        return row;
    }

    private static ToggleSettingRow CreateGeneralToggleCard(
        Transform parent,
        string name,
        string title,
        string description,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject cardGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        cardGo.transform.SetParent(parent, false);

        LayoutElement cardLayout = cardGo.GetComponent<LayoutElement>();
        cardLayout.preferredHeight = 62f;

        Image cardBg = cardGo.GetComponent<Image>();
        cardBg.color = new Color(0.10f, 0.11f, 0.14f, 0.92f);

        RectTransform card = cardGo.GetComponent<RectTransform>();

        TextMeshProUGUI titleLabel = CreateText(card, "Title", title, 14f, FontStyles.Bold, TextAlignmentOptions.Left);
        titleLabel.color = new Color(1f, 0.95f, 0.72f, 1f);
        titleLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleLabel.rectTransform.offsetMin = new Vector2(16f, -26f);
        titleLabel.rectTransform.offsetMax = new Vector2(-112f, -8f);

        TextMeshProUGUI descriptionLabel = CreateText(card, "Description", description, 8f, FontStyles.Normal, TextAlignmentOptions.Left);
        descriptionLabel.textWrappingMode = TextWrappingModes.Normal;
        descriptionLabel.color = new Color(0.82f, 0.88f, 0.94f, 1f);
        descriptionLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        descriptionLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        descriptionLabel.rectTransform.offsetMin = new Vector2(16f, -42f);
        descriptionLabel.rectTransform.offsetMax = new Vector2(-108f, -18f);

        Button toggleButton = CreateButton(card, name + "ToggleButton", "Off", onClick, new Color(0.28f, 0.18f, 0.18f, 1f), 12f);
        RectTransform toggleRect = toggleButton.transform as RectTransform;
        toggleRect.anchorMin = new Vector2(1f, 0.5f);
        toggleRect.anchorMax = new Vector2(1f, 0.5f);
        toggleRect.pivot = new Vector2(1f, 0.5f);
        toggleRect.sizeDelta = new Vector2(82f, 28f);
        toggleRect.anchoredPosition = new Vector2(-14f, -1f);

        return new ToggleSettingRow
        {
            Button = toggleButton,
            Label = toggleButton.GetComponentInChildren<TextMeshProUGUI>(true)
        };
    }

    private PlaceholderPage CreateAdvancedSettingsLockedPage(Transform parent, string name, string title, string message)
    {
        PlaceholderPage page = CreatePlaceholderPage(parent, name, title, message);
        if (page.MessageLabel != null)
            page.MessageLabel.fontSize = 16f;

        if (page.ResetButton != null)
        {
            page.ResetButton.onClick.RemoveAllListeners();
            page.ResetButton.onClick.AddListener(() =>
            {
                FunkyAudioSettings.PlayUiConfirm();
                if (name.Contains("Rhythm"))
                    SetRhythmAdvancedEnabled(false);
                else if (name.Contains("Fishing"))
                    SetFishingAdvancedEnabled(false);
                if (page.MessageLabel != null)
                    page.MessageLabel.text = message;
            });
        }

        return page;
    }

    private ControllerStatusPage CreateControllerStatusPage(Transform parent, string name)
    {
        ControllerStatusPage page = new ControllerStatusPage();
        page.Root = CreatePageRoot(parent, name);

        RectTransform header = CreateRect(page.Root, "Header", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -38f), new Vector2(-10f, -8f));

        page.BackButton = CreateButton(header, "BackButton", "Back", CloseOptionsPanel, new Color(0.35f, 0.20f, 0.20f, 1f), 12f);
        RectTransform backRect = page.BackButton.transform as RectTransform;
        backRect.anchorMin = new Vector2(0f, 0f);
        backRect.anchorMax = new Vector2(0f, 1f);
        backRect.pivot = new Vector2(0f, 0.5f);
        backRect.sizeDelta = new Vector2(92f, 0f);
        backRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI titleLabel = CreateText(header, "Title", "Controllers", 16f, FontStyles.Bold, TextAlignmentOptions.Center);
        titleLabel.color = new Color(1f, 0.95f, 0.72f, 1f);
        Stretch(titleLabel.rectTransform);
        titleLabel.rectTransform.offsetMin = new Vector2(98f, 0f);
        titleLabel.rectTransform.offsetMax = new Vector2(-12f, 0f);

        RectTransform body = CreateRect(page.Root, "Body", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 24f), new Vector2(-24f, -54f));
        Image bodyBg = body.gameObject.AddComponent<Image>();
        bodyBg.color = new Color(0.05f, 0.05f, 0.06f, 0.90f);

        TextMeshProUGUI subtitle = CreateText(
            body,
            "Subtitle",
            "Live controller detection updates while this menu is open.",
            12f,
            FontStyles.Normal,
            TextAlignmentOptions.Center);
        subtitle.color = new Color(0.82f, 0.88f, 0.94f, 1f);
        subtitle.rectTransform.anchorMin = new Vector2(0f, 1f);
        subtitle.rectTransform.anchorMax = new Vector2(1f, 1f);
        subtitle.rectTransform.pivot = new Vector2(0.5f, 1f);
        subtitle.rectTransform.offsetMin = new Vector2(18f, -42f);
        subtitle.rectTransform.offsetMax = new Vector2(-18f, -14f);

        RectTransform cardsRow = CreateRect(body, "CardsRow", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(26f, 18f), new Vector2(-26f, -58f));
        HorizontalLayoutGroup cardsLayout = cardsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        cardsLayout.spacing = 22f;
        cardsLayout.padding = new RectOffset(0, 0, 0, 0);
        cardsLayout.childAlignment = TextAnchor.MiddleCenter;
        cardsLayout.childControlWidth = true;
        cardsLayout.childControlHeight = true;
        cardsLayout.childForceExpandWidth = true;
        cardsLayout.childForceExpandHeight = true;

        Sprite joyConSprite = LoadOptionsSprite("OptionsUI/joycon");
        Sprite xboxSprite = LoadOptionsSprite("OptionsUI/xbox");

        CreateControllerCard(cardsRow, "JoyConCard", "Joy-Con", joyConSprite, "JoyConConnected");
        CreateControllerCard(cardsRow, "XboxCard", "Xbox", xboxSprite, "XboxConnected");

        page.Manager = page.Root.gameObject.AddComponent<ControllerManagerScript>();
        page.Root.gameObject.SetActive(false);
        return page;
    }

    private static RectTransform CreateControllerCard(Transform parent, string name, string title, Sprite sprite, string statusName)
    {
        RectTransform card = CreateRect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        LayoutElement layout = card.gameObject.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.flexibleHeight = 1f;
        layout.minWidth = 260f;

        Image bg = card.gameObject.AddComponent<Image>();
        bg.color = new Color(0.10f, 0.11f, 0.14f, 0.92f);

        TextMeshProUGUI titleLabel = CreateText(card, "Title", title, 17f, FontStyles.Bold, TextAlignmentOptions.Center);
        titleLabel.color = new Color(1f, 0.95f, 0.72f, 1f);
        titleLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
        titleLabel.rectTransform.offsetMin = new Vector2(16f, -44f);
        titleLabel.rectTransform.offsetMax = new Vector2(-16f, -10f);

        Image art = CreateImage(card, "Art", sprite, Color.white);
        art.rectTransform.anchorMin = new Vector2(0.12f, 0.30f);
        art.rectTransform.anchorMax = new Vector2(0.88f, 0.78f);
        art.rectTransform.offsetMin = Vector2.zero;
        art.rectTransform.offsetMax = Vector2.zero;
        art.preserveAspect = true;

        if (sprite == null)
            art.gameObject.SetActive(false);

        TextMeshProUGUI statusLabel = CreateText(card, statusName, "Checking...", 24f, FontStyles.Bold, TextAlignmentOptions.Center);
        statusLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
        statusLabel.rectTransform.anchorMax = new Vector2(1f, 0f);
        statusLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
        statusLabel.rectTransform.offsetMin = new Vector2(16f, 18f);
        statusLabel.rectTransform.offsetMax = new Vector2(-16f, 66f);
        statusLabel.color = new Color(0.82f, 0.88f, 0.94f, 1f);

        return card;
    }

    private RectTransform CreatePageRoot(Transform parent, string name)
    {
        RectTransform root = CreateRect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        root.gameObject.SetActive(false);
        return root;
    }

    private void ShowTab(OptionsTab tab)
    {
        HideAllTabContent();
        _activeTab = tab;

        switch (tab)
        {
            case OptionsTab.GeneralSettings:
                RefreshGeneralSettingsUi();
                if (_generalPage?.Root != null)
                    _generalPage.Root.gameObject.SetActive(true);
                break;

            case OptionsTab.Controllers:
                if (_controllersPage?.Root != null)
                    _controllersPage.Root.gameObject.SetActive(true);
                break;

            case OptionsTab.RhythmAdvanced:
                if (!IsAdvancedTabEnabled(OptionsTab.RhythmAdvanced))
                {
                    if (_rhythmLockedPage?.Root != null)
                        _rhythmLockedPage.Root.gameObject.SetActive(true);
                    break;
                }

                if (_rhythmHostRoot != null)
                    _rhythmHostRoot.gameObject.SetActive(true);
                if (_rhythmPanel != null)
                    _rhythmPanel.OpenTuningPanel();
                break;

            case OptionsTab.FishingAdvanced:
                if (!IsAdvancedTabEnabled(OptionsTab.FishingAdvanced))
                {
                    if (_fishingLockedPage?.Root != null)
                        _fishingLockedPage.Root.gameObject.SetActive(true);
                    break;
                }

                if (_fishingHostRoot != null)
                    _fishingHostRoot.gameObject.SetActive(true);
                if (_fishingPanel != null)
                    _fishingPanel.OpenTuningPanel();
                break;
        }

        UpdateTabVisuals();
        SelectDefaultForActiveTab();
    }

    private void HideAllTabContent()
    {
        if (_generalPage?.Root != null)
            _generalPage.Root.gameObject.SetActive(false);
        if (_controllersPage?.Root != null)
            _controllersPage.Root.gameObject.SetActive(false);
        if (_rhythmLockedPage?.Root != null)
            _rhythmLockedPage.Root.gameObject.SetActive(false);
        if (_fishingLockedPage?.Root != null)
            _fishingLockedPage.Root.gameObject.SetActive(false);
        if (_rhythmHostRoot != null)
            _rhythmHostRoot.gameObject.SetActive(false);
        if (_fishingHostRoot != null)
            _fishingHostRoot.gameObject.SetActive(false);

        if (_rhythmPanel != null)
            _rhythmPanel.CloseForUnifiedOptions();
        if (_fishingPanel != null)
            _fishingPanel.CloseForUnifiedOptions();
    }

    private void UpdateTabVisuals()
    {
        UpdateTabVisual(_generalTabButton, _activeTab == OptionsTab.GeneralSettings, false);
        UpdateTabVisual(_controllersTabButton, _activeTab == OptionsTab.Controllers, false);
        UpdateTabVisual(_rhythmTabButton, _activeTab == OptionsTab.RhythmAdvanced, !IsAdvancedTabEnabled(OptionsTab.RhythmAdvanced));
        UpdateTabVisual(_fishingTabButton, _activeTab == OptionsTab.FishingAdvanced, !IsAdvancedTabEnabled(OptionsTab.FishingAdvanced));
    }

    private static void UpdateTabVisual(Button button, bool active, bool locked)
    {
        if (button == null || !button.TryGetComponent(out Image image))
            return;

        if (locked)
        {
            image.color = active
                ? new Color(0.24f, 0.25f, 0.28f, 1f)
                : new Color(0.12f, 0.13f, 0.15f, 1f);
        }
        else
        {
            image.color = active
                ? new Color(0.33f, 0.49f, 0.63f, 1f)
                : new Color(0.16f, 0.18f, 0.22f, 1f);
        }

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.color = locked
                ? (active ? new Color(0.80f, 0.82f, 0.86f, 1f) : new Color(0.58f, 0.60f, 0.64f, 1f))
                : Color.white;
        }
    }

    private void SelectDefaultForActiveTab()
    {
        EventSystem evt = EventSystem.current;
        if (evt == null)
            return;

        switch (_activeTab)
        {
            case OptionsTab.RhythmAdvanced:
                if (!IsAdvancedTabEnabled(OptionsTab.RhythmAdvanced))
                {
                    if (_rhythmLockedPage?.BackButton != null)
                    {
                        evt.SetSelectedGameObject(_rhythmLockedPage.BackButton.gameObject);
                        return;
                    }
                    break;
                }

                _rhythmPanel?.EnsureSelection();
                return;

            case OptionsTab.FishingAdvanced:
                if (!IsAdvancedTabEnabled(OptionsTab.FishingAdvanced))
                {
                    if (_fishingLockedPage?.BackButton != null)
                    {
                        evt.SetSelectedGameObject(_fishingLockedPage.BackButton.gameObject);
                        return;
                    }
                    break;
                }

                _fishingPanel?.EnsureSelection();
                return;

            case OptionsTab.GeneralSettings:
                if (_generalPage?.BackButton != null)
                {
                    evt.SetSelectedGameObject(_generalPage.BackButton.gameObject);
                    return;
                }
                break;

            case OptionsTab.Controllers:
                if (_controllersPage?.BackButton != null)
                {
                    evt.SetSelectedGameObject(_controllersPage.BackButton.gameObject);
                    return;
                }
                break;

        }

        if (_generalTabButton != null)
            evt.SetSelectedGameObject(_generalTabButton.gameObject);
    }

    private void SyncOpenButtonVisibility()
    {
        bool pauseOpen = _pauseManager != null &&
            _pauseManager.PausePanel != null &&
            _pauseManager.PausePanel.activeInHierarchy;
        SetOpenButtonActive(pauseOpen && !_isOptionsOpen);
    }

    private void SetOpenButtonActive(bool active)
    {
        if (_openButton != null)
            _openButton.gameObject.SetActive(active);
    }

    private void AdjustOpenButtonPlacement()
    {
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

        float anchorX = flipX ? 0f : 1f;
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

    private static Button CreateButton(Transform parent, string name, string text, UnityEngine.Events.UnityAction onClick, Color color, float fontSize)
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

    private static Slider CreateStandardSlider(Transform parent, string name)
    {
        GameObject sliderGo = new GameObject(name, typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
        sliderGo.transform.SetParent(parent, false);

        LayoutElement layout = sliderGo.GetComponent<LayoutElement>();
        layout.preferredHeight = 11f;

        Slider slider = sliderGo.GetComponent<Slider>();

        GameObject bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(sliderGo.transform, false);
        RectTransform bg = bgGo.GetComponent<RectTransform>();
        bg.anchorMin = new Vector2(0f, 0.42f);
        bg.anchorMax = new Vector2(1f, 0.58f);
        bg.offsetMin = Vector2.zero;
        bg.offsetMax = Vector2.zero;
        bgGo.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

        GameObject fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaGo.transform.SetParent(sliderGo.transform, false);
        RectTransform fillArea = fillAreaGo.GetComponent<RectTransform>();
        fillArea.anchorMin = new Vector2(0f, 0.36f);
        fillArea.anchorMax = new Vector2(1f, 0.64f);
        fillArea.offsetMin = new Vector2(4f, 0f);
        fillArea.offsetMax = new Vector2(-4f, 0f);

        GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(fillAreaGo.transform, false);
        RectTransform fill = fillGo.GetComponent<RectTransform>();
        Stretch(fill);
        fillGo.GetComponent<Image>().color = new Color(0.33f, 0.67f, 0.86f, 1f);

        GameObject handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGo.transform.SetParent(sliderGo.transform, false);
        RectTransform handle = handleGo.GetComponent<RectTransform>();
        handle.sizeDelta = new Vector2(6f, 8f);
        Image handleImage = handleGo.GetComponent<Image>();
        handleImage.color = new Color(1f, 0.95f, 0.75f, 1f);

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private static Scrollbar CreateGeneralSettingsScrollbar(Transform parent)
    {
        GameObject scrollbarGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbarGo.transform.SetParent(parent, false);

        RectTransform scrollbarRect = scrollbarGo.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.sizeDelta = new Vector2(12f, 0f);
        scrollbarRect.anchoredPosition = new Vector2(-6f, 0f);

        Image background = scrollbarGo.GetComponent<Image>();
        background.color = new Color(0.12f, 0.13f, 0.16f, 0.9f);

        GameObject slidingAreaGo = new GameObject("Sliding Area", typeof(RectTransform));
        slidingAreaGo.transform.SetParent(scrollbarGo.transform, false);
        RectTransform slidingArea = slidingAreaGo.GetComponent<RectTransform>();
        Stretch(slidingArea);
        slidingArea.offsetMin = new Vector2(1f, 6f);
        slidingArea.offsetMax = new Vector2(-1f, -6f);

        GameObject handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGo.transform.SetParent(slidingAreaGo.transform, false);
        RectTransform handle = handleGo.GetComponent<RectTransform>();
        Stretch(handle);

        Image handleImage = handleGo.GetComponent<Image>();
        handleImage.color = new Color(0.76f, 0.82f, 0.9f, 0.95f);

        Scrollbar scrollbar = scrollbarGo.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scrollbar.size = 0.25f;
        return scrollbar;
    }

    private static Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size, FontStyles style, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Sprite LoadOptionsSprite(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return null;

        return Resources.Load<Sprite>(resourcePath);
    }

    private bool IsAdvancedTabEnabled(OptionsTab tab)
    {
        return tab switch
        {
            OptionsTab.RhythmAdvanced => _rhythmAdvancedEnabled,
            OptionsTab.FishingAdvanced => _fishingAdvancedEnabled,
            _ => true
        };
    }

    private void SetGeneralRhythmSensitivity(float value)
    {
        if (_rhythmAdvancedEnabled)
        {
            RefreshGeneralSettingsUi();
            return;
        }

        float clamped = Mathf.Clamp01(value);
        if (Mathf.Approximately(_generalRhythmSensitivity, clamped))
        {
            RefreshGeneralSettingsUi();
            return;
        }

        _generalRhythmSensitivity = clamped;
        PlayerPrefs.SetFloat(GeneralRhythmSensitivityPrefKey, _generalRhythmSensitivity);
        PlayerPrefs.Save();
        ApplyGeneralRhythmSensitivityIfNeeded(persist: true);
        RefreshGeneralSettingsUi();
    }

    private void ApplyGeneralRhythmSensitivityIfNeeded(bool persist)
    {
        if (_rhythmAdvancedEnabled || _rhythmPanel == null)
            return;

        _rhythmPanel.ApplyGeneralSensitivityPreset(_generalRhythmSensitivity, persist);
    }

    private void SetFullscreenEnabled(bool enabled)
    {
        if (_fullscreenEnabled == enabled)
        {
            RefreshGeneralSettingsUi();
            return;
        }

        _fullscreenEnabled = enabled;
        PlayerPrefs.SetInt(FullscreenPrefKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        ApplyFullscreenSetting(enabled);
        RefreshGeneralSettingsUi();
    }

    private static void ApplyFullscreenSetting(bool enabled)
    {
        if (enabled)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
#else
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
#endif
            Screen.fullScreen = true;
            return;
        }

        Screen.fullScreenMode = FullScreenMode.Windowed;
        Screen.fullScreen = false;
    }

    private void SetRhythmAdvancedEnabled(bool enabled)
    {
        if (_rhythmAdvancedEnabled == enabled)
        {
            RefreshGeneralSettingsUi();
            UpdateTabVisuals();
            return;
        }

        _rhythmAdvancedEnabled = enabled;
        PlayerPrefs.SetInt(RhythmAdvancedPrefKey, enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (!enabled)
            ApplyGeneralRhythmSensitivityIfNeeded(persist: true);

        RefreshGeneralSettingsUi();
        UpdateTabVisuals();

        if (_activeTab == OptionsTab.RhythmAdvanced)
            ShowTab(_activeTab);
    }

    private void SetFishingAdvancedEnabled(bool enabled)
    {
        if (_fishingAdvancedEnabled == enabled)
        {
            RefreshGeneralSettingsUi();
            UpdateTabVisuals();
            return;
        }

        _fishingAdvancedEnabled = enabled;
        PlayerPrefs.SetInt(FishingAdvancedPrefKey, enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (!enabled)
            ApplyFishingDefaultsIfNeeded(persist: true);

        RefreshGeneralSettingsUi();
        UpdateTabVisuals();

        if (_activeTab == OptionsTab.FishingAdvanced)
            ShowTab(_activeTab);
    }

    private void ResetGeneralSettingsToDefaults()
    {
        FunkyAudioSettings.ResetToDefaults();
        SetFullscreenEnabled(true);
        _generalRhythmSensitivity = RhythmPauseTuningPanel.GeneralSensitivityDefault;
        PlayerPrefs.SetFloat(GeneralRhythmSensitivityPrefKey, _generalRhythmSensitivity);
        PlayerPrefs.Save();
        SetRhythmAdvancedEnabled(false);
        SetFishingAdvancedEnabled(false);
        RefreshGeneralSettingsUi();
    }

    private void ApplyFishingDefaultsIfNeeded(bool persist)
    {
        if (_fishingAdvancedEnabled || _fishingPanel == null)
            return;

        _fishingPanel.ApplyDefaultPreset(persist);
    }

    private void RefreshGeneralSettingsUi()
    {
        if (_generalPage == null)
            return;

        for (int i = 0; i < _generalPage.VolumeRows.Count; i++)
        {
            VolumeSliderRow row = _generalPage.VolumeRows[i];
            if (row == null || row.Getter == null || row.Slider == null || row.ValueLabel == null)
                continue;

            float value = Mathf.Clamp01(row.Getter());
            row.Slider.SetValueWithoutNotify(value);
            row.ValueLabel.text = FormatVolumePercent(value);
        }

        if (_generalPage.RhythmSensitivityRow != null)
        {
            if (_generalPage.RhythmSensitivityRow.Getter != null && _generalPage.RhythmSensitivityRow.Slider != null)
            {
                float sensitivityValue = Mathf.Clamp01(_generalPage.RhythmSensitivityRow.Getter());
                _generalPage.RhythmSensitivityRow.Slider.SetValueWithoutNotify(sensitivityValue);
                if (_generalPage.RhythmSensitivityRow.ValueLabel != null && _generalPage.RhythmSensitivityRow.Formatter != null)
                    _generalPage.RhythmSensitivityRow.ValueLabel.text = _generalPage.RhythmSensitivityRow.Formatter(sensitivityValue);
            }

            SetGeneralSliderEnabledState(_generalPage.RhythmSensitivityRow, !_rhythmAdvancedEnabled);
        }

        if (_generalPage.FullscreenToggle?.Label != null)
            _generalPage.FullscreenToggle.Label.text = _fullscreenEnabled ? "Fullscreen" : "Windowed";

        if (_generalPage.FullscreenToggle?.Button != null &&
            _generalPage.FullscreenToggle.Button.TryGetComponent(out Image fullscreenToggleImage))
        {
            fullscreenToggleImage.color = _fullscreenEnabled
                ? new Color(0.18f, 0.42f, 0.26f, 1f)
                : new Color(0.28f, 0.18f, 0.18f, 1f);
        }

        if (_generalPage.RhythmAdvancedToggle?.Label != null)
            _generalPage.RhythmAdvancedToggle.Label.text = _rhythmAdvancedEnabled ? "On" : "Off";

        if (_generalPage.RhythmAdvancedToggle?.Button != null &&
            _generalPage.RhythmAdvancedToggle.Button.TryGetComponent(out Image rhythmToggleImage))
        {
            rhythmToggleImage.color = _rhythmAdvancedEnabled
                ? new Color(0.18f, 0.42f, 0.26f, 1f)
                : new Color(0.28f, 0.18f, 0.18f, 1f);
        }

        if (_generalPage.FishingAdvancedToggle?.Label != null)
            _generalPage.FishingAdvancedToggle.Label.text = _fishingAdvancedEnabled ? "On" : "Off";

        if (_generalPage.FishingAdvancedToggle?.Button != null &&
            _generalPage.FishingAdvancedToggle.Button.TryGetComponent(out Image fishingToggleImage))
        {
            fishingToggleImage.color = _fishingAdvancedEnabled
                ? new Color(0.18f, 0.42f, 0.26f, 1f)
                : new Color(0.28f, 0.18f, 0.18f, 1f);
        }
    }

    private static string FormatVolumePercent(float value)
    {
        return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
    }

    private static string FormatGeneralRhythmSensitivityValue(float value)
    {
        int enter = Mathf.RoundToInt(RhythmPauseTuningPanel.EvaluateGeneralSensitivityEnterDps(value));
        int exit = Mathf.RoundToInt(RhythmPauseTuningPanel.EvaluateGeneralSensitivityExitDps(value));
        return enter + " / " + exit;
    }

    private static void SetGeneralSliderEnabledState(GeneralSliderRow row, bool enabled)
    {
        if (row == null)
            return;

        if (row.Slider != null)
            row.Slider.interactable = enabled;

        if (row.Background != null)
            row.Background.color = enabled
                ? new Color(0.10f, 0.11f, 0.14f, 0.92f)
                : new Color(0.08f, 0.08f, 0.10f, 0.92f);

        if (row.TitleLabel != null)
            row.TitleLabel.color = enabled
                ? new Color(1f, 0.95f, 0.72f, 1f)
                : new Color(0.62f, 0.62f, 0.66f, 1f);

        if (row.DescriptionLabel != null)
            row.DescriptionLabel.color = enabled
                ? new Color(0.82f, 0.88f, 0.94f, 1f)
                : new Color(0.48f, 0.50f, 0.55f, 1f);

        if (row.ValueLabel != null)
            row.ValueLabel.color = enabled
                ? new Color(0.95f, 0.87f, 0.45f, 1f)
                : new Color(0.58f, 0.58f, 0.60f, 1f);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}

public static class PauseOptionsPanelBootstrap
{
    private const string ControllerMenuSceneName = "ControllerMenu";
    private const string StandaloneOptionsHostName = "StandaloneOptionsHost";
    private const string StandalonePausePanelName = "StandaloneOptionsPausePanel";
    private const string LegacyControllerBackdropName = "Backdrop";
    private static bool _hookedSceneLoaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (!_hookedSceneLoaded)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            _hookedSceneLoaded = true;
        }

        AttachToPauseManagers();
        AttachStandaloneControllerMenu();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AttachToPauseManagers();
        AttachStandaloneControllerMenu();
    }

    private static void AttachToPauseManagers()
    {
        PauseManager[] managers = UnityEngine.Object.FindObjectsByType<PauseManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length; i++)
        {
            PauseManager manager = managers[i];
            if (manager == null)
                continue;

            if (manager.GetComponent<PauseOptionsPanel>() == null)
                manager.gameObject.AddComponent<PauseOptionsPanel>();
        }
    }

    private static void AttachStandaloneControllerMenu()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name != ControllerMenuSceneName)
            return;

        Canvas canvas = FindSceneCanvas(activeScene);
        if (canvas == null)
            return;

        RectTransform hostRoot = EnsureStandaloneHost(canvas.transform);
        HideLegacyControllerSceneUi(canvas.transform, hostRoot);
        DisableLegacyControllerManager(activeScene);

        PauseManager manager = hostRoot.GetComponent<PauseManager>();
        if (manager == null)
            manager = hostRoot.gameObject.AddComponent<PauseManager>();

        RectTransform pausePanel = EnsureStandalonePausePanel(hostRoot);
        manager.PausePanel = pausePanel.gameObject;
        manager.ControlsPanel = null;
        manager.EnterStandaloneMenuMode();

        PauseOptionsPanel optionsPanel = hostRoot.GetComponent<PauseOptionsPanel>();
        if (optionsPanel == null)
            optionsPanel = hostRoot.gameObject.AddComponent<PauseOptionsPanel>();

        optionsPanel.ConfigureStandaloneScene("MainMenu");
    }

    private static Canvas FindSceneCanvas(Scene scene)
    {
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas.gameObject.scene == scene)
                return canvas;
        }

        return null;
    }

    private static RectTransform EnsureStandaloneHost(Transform canvasRoot)
    {
        Transform existing = canvasRoot.Find(StandaloneOptionsHostName);
        if (existing != null && existing is RectTransform existingRect)
            return existingRect;

        GameObject go = new GameObject(StandaloneOptionsHostName, typeof(RectTransform));
        go.transform.SetParent(canvasRoot, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static RectTransform EnsureStandalonePausePanel(Transform hostRoot)
    {
        Transform existing = hostRoot.Find(StandalonePausePanelName);
        if (existing != null && existing is RectTransform existingRect)
            return existingRect;

        GameObject go = new GameObject(StandalonePausePanelName, typeof(RectTransform));
        go.transform.SetParent(hostRoot, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static void HideLegacyControllerSceneUi(Transform canvasRoot, Transform preserveRoot)
    {
        for (int i = 0; i < canvasRoot.childCount; i++)
        {
            Transform child = canvasRoot.GetChild(i);
            if (child == preserveRoot || child.name == LegacyControllerBackdropName)
                continue;

            child.gameObject.SetActive(false);
        }
    }

    private static void DisableLegacyControllerManager(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root != null && root.name == "ControllerManager")
                root.SetActive(false);
        }
    }
}
