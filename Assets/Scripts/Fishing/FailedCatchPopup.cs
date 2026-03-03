using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FailedCatchPopup : MonoBehaviour
{
    [Header("Copy")]
    [SerializeField] private string defaultMessage = "FISH ESCAPED!";

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float popReturnDuration = 0.12f;
    [SerializeField, Min(0f)] private float visibleDuration = 0.2f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.34f;
    [SerializeField] private float popScale = 1.3f;
    [SerializeField] private float riseDistance = 28f;

    [Header("Style")]
    [SerializeField] private int fontSize = 64;
    [SerializeField] private Color textColor = new Color(1f, 0.34f, 0.34f, 1f);
    [SerializeField] private Vector2 anchoredPosition = new Vector2(0f, -120f);
    [SerializeField] private Vector2 size = new Vector2(960f, 180f);
    [SerializeField] private int sortingOrder = 220;

    private Canvas _canvas;
    private TextMeshProUGUI _text;
    private RectTransform _rect;
    private Vector2 _basePosition;
    private Coroutine _showRoutine;
    private Color _baseColor;

    private void Awake()
    {
        EnsureUi();
        HideImmediate();
    }

    public void Show(string message = null)
    {
        EnsureUi();

        string resolvedMessage = string.IsNullOrWhiteSpace(message)
            ? defaultMessage
            : message.Trim();

        if (_showRoutine != null)
            StopCoroutine(_showRoutine);

        _showRoutine = StartCoroutine(ShowRoutine(resolvedMessage));
    }

    private IEnumerator ShowRoutine(string message)
    {
        _text.text = message;

        float totalDuration = Mathf.Max(0.01f, visibleDuration + fadeDuration);
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float normalized = Mathf.Clamp01(elapsed / totalDuration);
            float popT = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, popReturnDuration));
            float scale = Mathf.Lerp(popScale, 1f, popT);

            float alpha = 1f;
            if (elapsed > visibleDuration)
            {
                float fadeT = (elapsed - visibleDuration) / Mathf.Max(0.01f, fadeDuration);
                alpha = 1f - Mathf.Clamp01(fadeT);
            }

            _rect.localScale = Vector3.one * scale;
            _rect.anchoredPosition = _basePosition + (Vector2.up * (riseDistance * normalized));

            Color c = _baseColor;
            c.a = alpha;
            _text.color = c;

            yield return null;
        }

        HideImmediate();
        _showRoutine = null;
    }

    private void HideImmediate()
    {
        if (_text == null || _rect == null)
            return;

        _rect.localScale = Vector3.one;
        _rect.anchoredPosition = _basePosition;

        Color c = _baseColor;
        c.a = 0f;
        _text.color = c;
    }

    private void EnsureUi()
    {
        if (_canvas != null && _text != null && _rect != null)
            return;

        Transform existingCanvas = transform.Find("FailedCatchPopupCanvas");
        if (existingCanvas != null)
            _canvas = existingCanvas.GetComponent<Canvas>();

        if (_canvas == null)
        {
            GameObject canvasGo = new GameObject(
                "FailedCatchPopupCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        _canvas.sortingOrder = sortingOrder;

        Transform existingText = _canvas.transform.Find("FailedCatchText");
        if (existingText != null)
            _text = existingText.GetComponent<TextMeshProUGUI>();

        if (_text == null)
        {
            GameObject textGo = new GameObject("FailedCatchText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(_canvas.transform, false);
            _text = textGo.GetComponent<TextMeshProUGUI>();
        }

        _rect = _text.rectTransform;
        _rect.anchorMin = new Vector2(0.5f, 0.5f);
        _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.pivot = new Vector2(0.5f, 0.5f);
        _rect.anchoredPosition = anchoredPosition;
        _rect.sizeDelta = size;
        _basePosition = anchoredPosition;

        if (TMP_Settings.defaultFontAsset != null)
            _text.font = TMP_Settings.defaultFontAsset;

        _text.fontSize = fontSize;
        _text.alignment = TextAlignmentOptions.Center;
        _text.raycastTarget = false;
        _text.text = defaultMessage;

        _baseColor = textColor;
        _text.color = _baseColor;
    }
}
