using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PondLevelControlHints : MonoBehaviour
{
    private const string DefaultPondSceneName = "Pond_Level_1";
    private const string PrefabResourcePath = "FishingControlHints";
    private const string ControlHintsEnabledPrefKey = "FunkyFishing.Fishing.ControlHintsEnabled";

    private enum HintState
    {
        Hidden,
        MoveAndCast,
        Yank,
        DirectionIntro,
        Reel
    }

    [Header("Scene")]
    [SerializeField] private string pondLevelSceneName = DefaultPondSceneName;

    [Header("Switch Hint Art")]
    [SerializeField] private Texture2D moveHintTexture;
    [SerializeField] private Texture2D castHintTexture;
    [SerializeField] private Texture2D yankHintTexture;
    [SerializeField] private Sprite directionLeftHintSprite;
    [SerializeField] private Sprite directionUpHintSprite;
    [SerializeField] private Sprite directionRightHintSprite;
    [SerializeField] private Sprite reelHintSprite;

    [Header("Layout")]
    [SerializeField] private int sortingOrder = 95;
    [SerializeField] private Vector2 panelOffset = new Vector2(44f, 44f);
    [SerializeField] private float panelPadding = 14f;
    [SerializeField] private float imageSpacing = 14f;
    [SerializeField] private Vector2 pondHintMaxSize = new Vector2(210f, 168f);
    [SerializeField] private Vector2 directionHintMaxSize = new Vector2(138f, 104f);
    [SerializeField] private Vector2 reelHintMaxSize = new Vector2(164f, 164f);
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.3f);

    [Header("Pulse")]
    [SerializeField, Min(0.1f)] private float pulseSpeed = 2.0f;
    [SerializeField, Range(0f, 0.5f)] private float pulseScaleAmount = 0.08f;
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0.72f;
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 1f;
    [SerializeField, Min(0.1f)] private float fadeSpeed = 7f;

    [Header("Rhythm Intro")]
    [SerializeField, Min(0.1f)] private float rhythmDirectionHintDuration = 4.5f;

    private static bool s_sceneHookRegistered;
    private static bool s_loggedMissingPrefab;
    private static PondLevelControlHints s_activeInstance;
    private static bool s_controlHintsSettingLoaded;
    private static bool s_controlHintsEnabled = true;

    private Canvas _canvas;
    private RectTransform _panelRect;
    private CanvasGroup _panelCanvasGroup;
    private HorizontalLayoutGroup _layoutGroup;
    private readonly Image[] _hintSlots = new Image[3];

    private BobberArcCaster _bobberArcCaster;
    private RhythmConductor _rhythmConductor;

    private Sprite _moveHintSprite;
    private Sprite _castHintSprite;
    private Sprite _yankHintSprite;

    private HintState _currentState = HintState.Hidden;
    private float _visibleBlend;
    private float _tensionStartedAt = float.NegativeInfinity;
    private bool _hasBobberStateSample;
    private BobberArcCaster.State _lastBobberState = BobberArcCaster.State.Idle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_sceneHookRegistered = false;
        s_loggedMissingPrefab = false;
        s_activeInstance = null;
        s_controlHintsSettingLoaded = false;
        s_controlHintsEnabled = true;
    }

    public static bool GetControlHintsEnabled()
    {
        EnsureControlHintsSettingLoaded();
        return s_controlHintsEnabled;
    }

    public static void SetControlHintsEnabled(bool enabled)
    {
        EnsureControlHintsSettingLoaded();
        if (s_controlHintsEnabled == enabled)
            return;

        s_controlHintsEnabled = enabled;
        PlayerPrefs.SetInt(ControlHintsEnabledPrefKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        if (s_sceneHookRegistered)
            return;

        s_sceneHookRegistered = true;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TrySpawnForPondScene();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void TrySpawnAfterInitialSceneLoad()
    {
        TrySpawnForPondScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != DefaultPondSceneName)
            return;

        TrySpawnForPondScene();
    }

    private static void TrySpawnForPondScene()
    {
        Scene pondScene = SceneManager.GetSceneByName(DefaultPondSceneName);
        if (!pondScene.IsValid() || !pondScene.isLoaded)
            return;

        PondLevelControlHints existing = FindFirstObjectByType<PondLevelControlHints>(FindObjectsInactive.Include);
        if (existing != null)
            return;

        GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            if (!s_loggedMissingPrefab)
            {
                Debug.LogWarning(
                    "PondLevelControlHints could not load its prefab from Resources/" +
                    PrefabResourcePath + ".prefab");
                s_loggedMissingPrefab = true;
            }
            return;
        }

        GameObject instance = Instantiate(prefab);
        SceneManager.MoveGameObjectToScene(instance, pondScene);
    }

    private void Awake()
    {
        if (s_activeInstance != null && s_activeInstance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_activeInstance = this;
    }

    private void Start()
    {
        BuildUi();
        RefreshDisplayedHints(HintState.Hidden);
        TickVisibility();
    }

    private void OnDestroy()
    {
        if (s_activeInstance == this)
            s_activeInstance = null;

        DestroyRuntimeSprite(ref _moveHintSprite);
        DestroyRuntimeSprite(ref _castHintSprite);
        DestroyRuntimeSprite(ref _yankHintSprite);
    }

    private void Update()
    {
        if (!IsBoundToLoadedPondScene())
            return;

        ResolveReferences();
        TrackTensionStartTime();

        HintState nextState = EvaluateHintState();
        if (nextState != _currentState)
            RefreshDisplayedHints(nextState);

        _currentState = nextState;
        TickVisibility();
    }

    private void BuildUi()
    {
        if (_canvas != null)
            return;

        GameObject canvasGo = new GameObject(
            "PondLevelControlHintsCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster = canvasGo.GetComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        GameObject panelGo = new GameObject(
            "HintPanel",
            typeof(RectTransform),
            typeof(Image),
            typeof(CanvasGroup),
            typeof(HorizontalLayoutGroup),
            typeof(ContentSizeFitter));
        panelGo.transform.SetParent(canvasGo.transform, false);

        _panelRect = panelGo.GetComponent<RectTransform>();
        _panelRect.anchorMin = Vector2.zero;
        _panelRect.anchorMax = Vector2.zero;
        _panelRect.pivot = new Vector2(0.5f, 0.5f);
        _panelRect.anchoredPosition = panelOffset;

        Image panelImage = panelGo.GetComponent<Image>();
        panelImage.color = panelColor;
        panelImage.raycastTarget = false;

        _panelCanvasGroup = panelGo.GetComponent<CanvasGroup>();
        _panelCanvasGroup.interactable = false;
        _panelCanvasGroup.blocksRaycasts = false;

        _layoutGroup = panelGo.GetComponent<HorizontalLayoutGroup>();
        _layoutGroup.childAlignment = TextAnchor.MiddleLeft;
        _layoutGroup.childControlWidth = false;
        _layoutGroup.childControlHeight = false;
        _layoutGroup.childForceExpandWidth = false;
        _layoutGroup.childForceExpandHeight = false;
        _layoutGroup.spacing = imageSpacing;
        int padding = Mathf.RoundToInt(panelPadding);
        _layoutGroup.padding = new RectOffset(padding, padding, padding, padding);

        ContentSizeFitter fitter = panelGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        for (int i = 0; i < _hintSlots.Length; i++)
        {
            GameObject imageGo = new GameObject($"Hint_{i}", typeof(RectTransform), typeof(Image));
            imageGo.transform.SetParent(panelGo.transform, false);

            Image image = imageGo.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.enabled = false;
            imageGo.SetActive(false);
            _hintSlots[i] = image;
        }
    }

    private void ResolveReferences()
    {
        if (_bobberArcCaster == null)
            _bobberArcCaster = FindFirstObjectByType<BobberArcCaster>();

        if (_rhythmConductor == null)
            _rhythmConductor = RhythmConductor.Instance != null
                ? RhythmConductor.Instance
                : FindFirstObjectByType<RhythmConductor>(FindObjectsInactive.Include);
    }

    private void TrackTensionStartTime()
    {
        if (_bobberArcCaster == null)
        {
            _hasBobberStateSample = false;
            _tensionStartedAt = float.NegativeInfinity;
            return;
        }

        BobberArcCaster.State currentState = _bobberArcCaster.CurrentState;
        if (!_hasBobberStateSample)
        {
            _lastBobberState = currentState;
            _hasBobberStateSample = true;

            if (currentState == BobberArcCaster.State.Tension)
                _tensionStartedAt = Time.unscaledTime;

            return;
        }

        if (currentState == BobberArcCaster.State.Tension &&
            _lastBobberState != BobberArcCaster.State.Tension)
        {
            _tensionStartedAt = Time.unscaledTime;
        }
        else if (currentState != BobberArcCaster.State.Tension &&
                 _lastBobberState == BobberArcCaster.State.Tension)
        {
            _tensionStartedAt = float.NegativeInfinity;
        }

        _lastBobberState = currentState;
    }

    private HintState EvaluateHintState()
    {
        if (_bobberArcCaster == null || ShouldHideAllHints())
            return HintState.Hidden;

        if (IsReelHintActive())
            return HintState.Reel;

        if (IsDirectionIntroHintActive())
            return HintState.DirectionIntro;

        if (IsYankHintActive())
            return HintState.Yank;

        if (_bobberArcCaster.CurrentState == BobberArcCaster.State.Idle)
            return HintState.MoveAndCast;

        return HintState.Hidden;
    }

    private bool ShouldHideAllHints()
    {
        if (!GetControlHintsEnabled())
            return true;

        if (Time.timeScale <= 0f)
            return true;

        if (PauseManager.IsAnyPauseUiOpen())
            return true;

        if (FishCatchAnimation.IsAnyCatchScreenActive)
            return true;

        return false;
    }

    private bool IsReelHintActive()
    {
        if (_rhythmConductor == null || _rhythmConductor.activeReel == null)
            return false;

        return _rhythmConductor.activeReel.CurrentPhase != ReelPhase.Resolved;
    }

    private bool IsDirectionIntroHintActive()
    {
        if (_bobberArcCaster == null || _bobberArcCaster.CurrentState != BobberArcCaster.State.Tension)
            return false;

        if (float.IsNegativeInfinity(_tensionStartedAt))
            return false;

        return Time.unscaledTime - _tensionStartedAt <= rhythmDirectionHintDuration;
    }

    private bool IsYankHintActive()
    {
        if (_bobberArcCaster == null ||
            _bobberArcCaster.CurrentState != BobberArcCaster.State.Landed)
        {
            return false;
        }

        return true;
    }

    private void RefreshDisplayedHints(HintState state)
    {
        switch (state)
        {
            case HintState.MoveAndCast:
                ApplySprites(
                    pondHintMaxSize,
                    GetOrCreateRuntimeSprite(ref _moveHintSprite, moveHintTexture, "MoveHintSprite"),
                    GetOrCreateRuntimeSprite(ref _castHintSprite, castHintTexture, "CastHintSprite"));
                break;

            case HintState.Yank:
                ApplySprites(
                    pondHintMaxSize,
                    GetOrCreateRuntimeSprite(ref _yankHintSprite, yankHintTexture, "YankHintSprite"));
                break;

            case HintState.DirectionIntro:
                ApplySprites(
                    directionHintMaxSize,
                    directionLeftHintSprite,
                    directionUpHintSprite,
                    directionRightHintSprite);
                break;

            case HintState.Reel:
                ApplySprites(reelHintMaxSize, reelHintSprite);
                break;

            default:
                ApplySprites(Vector2.zero);
                break;
        }
        if (_panelRect != null)
        {
            
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);

            _panelRect.anchoredPosition = new Vector2(
                panelOffset.x + (_panelRect.rect.width * 0.5f),
                panelOffset.y + (_panelRect.rect.height * 0.5f)
            );
        }
    }

    private void ApplySprites(Vector2 maxSize, params Sprite[] sprites)
    {
        int visibleCount = 0;

        for (int i = 0; i < _hintSlots.Length; i++)
        {
            Image slot = _hintSlots[i];
            Sprite sprite = i < sprites.Length ? sprites[i] : null;
            bool show = sprite != null;

            slot.gameObject.SetActive(show);
            slot.enabled = show;
            slot.sprite = sprite;

            if (!show)
                continue;

            RectTransform slotRect = slot.rectTransform;
            slotRect.sizeDelta = GetScaledSize(sprite, maxSize);
            visibleCount++;
        }

        _layoutGroup.spacing = visibleCount >= 3 ? imageSpacing * 0.8f : imageSpacing;
    }

    private void TickVisibility()
    {
        if (_panelRect == null || _panelCanvasGroup == null || _canvas == null)
            return;

        float target = _currentState == HintState.Hidden ? 0f : 1f;
        _visibleBlend = Mathf.MoveTowards(_visibleBlend, target, Time.unscaledDeltaTime * fadeSpeed);

        if (_visibleBlend <= 0.001f)
        {
            _canvas.enabled = false;
            _panelCanvasGroup.alpha = 0f;
            _panelRect.localScale = Vector3.one;
            return;
        }

        _canvas.enabled = true;

        float pulse = 0.5f + (Mathf.Sin(Time.unscaledTime * pulseSpeed) * 0.5f);
        float pulseAlpha = Mathf.Lerp(minAlpha, maxAlpha, pulse);
        float pulseScale = 1f + (pulse * pulseScaleAmount);

        _panelCanvasGroup.alpha = pulseAlpha * _visibleBlend;
        _panelRect.localScale = Vector3.one * Mathf.Lerp(1f, pulseScale, _visibleBlend);
    }

    private bool IsBoundToLoadedPondScene()
    {
        Scene pondScene = SceneManager.GetSceneByName(pondLevelSceneName);
        return pondScene.IsValid() &&
               pondScene.isLoaded &&
               gameObject.scene == pondScene;
    }

    private static Vector2 GetScaledSize(Sprite sprite, Vector2 maxSize)
    {
        if (sprite == null || maxSize.x <= 0f || maxSize.y <= 0f)
            return maxSize;

        Rect spriteRect = sprite.rect;
        if (spriteRect.width <= 0f || spriteRect.height <= 0f)
            return maxSize;

        float scaleX = maxSize.x / spriteRect.width;
        float scaleY = maxSize.y / spriteRect.height;
        float scale = Mathf.Min(scaleX, scaleY);
        return new Vector2(spriteRect.width * scale, spriteRect.height * scale);
    }

    private static Sprite GetOrCreateRuntimeSprite(ref Sprite runtimeSprite, Texture2D texture, string spriteName)
    {
        if (runtimeSprite != null)
            return runtimeSprite;

        if (texture == null)
            return null;

        runtimeSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        runtimeSprite.name = spriteName;
        return runtimeSprite;
    }

    private static void DestroyRuntimeSprite(ref Sprite sprite)
    {
        if (sprite == null)
            return;

        Destroy(sprite);
        sprite = null;
    }

    private static void EnsureControlHintsSettingLoaded()
    {
        if (s_controlHintsSettingLoaded)
            return;

        s_controlHintsEnabled = PlayerPrefs.GetInt(ControlHintsEnabledPrefKey, 1) != 0;
        s_controlHintsSettingLoaded = true;
    }
}
