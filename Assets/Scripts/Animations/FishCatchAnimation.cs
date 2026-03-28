using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class FishCatchAnimation : MonoBehaviour
{
    private static int _activeCatchScreenCount;

    [Header("UI & Effects")]
    public GameObject overlayPanel; //black image modify transparency
    public TextMeshProUGUI judgementText; // "Perfect Catch!"
    public TextMeshProUGUI fishNameText;  // "Redbelly caught!"
    public TextMeshProUGUI clickText;        // press any key to continue
    public Image perfectJudgementImage;
    public Image greatJudgementImage;
    public Image goodJudgementImage;
    private Image _activeJudgementImage;
    private RectTransform _letterGradeContainer;
    private Image _letterGradeImage;
    private TextMeshProUGUI _letterGradeLabel;
    private string _defaultClickText;
    private readonly Dictionary<string, Sprite> _letterGradeSprites = new Dictionary<string, Sprite>();

    private float _imageSize = 3.1f; // Base size for judgement images
    private const float LetterGradeImageSize = 180f;
    private const string LetterGradeResourceFolder = "letter_grades";
    private const float LetterGradeContainerWidth = 240f;
    private const float LetterGradeContainerHeight = 280f;
    private const float LetterGradeLabelWidth = 240f;
    private const float LetterGradeLabelFontSize = 42f;
    private static readonly Vector2 LetterGradeAnchor = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 LetterGradeAnchoredPosition = new Vector2(-260f, -20f);
    private static readonly Vector2 LetterGradeLabelPosition = new Vector2(0f, 92f);
    private static readonly Vector2 LetterGradeImagePosition = new Vector2(0f, -8f);


    [Header("Positioning")]
    public float flyToCameraDuration = 2.6f;
    public float distanceFromCamera = 1.2f;
    public float verticalOffset = -0.1f;
    public float spinSpeed = 150f;
    private bool _continuePressed = false;
    private bool _continueInputReady = true;
    private bool _isCatchScreenActive = false;

    public static bool IsAnyCatchScreenActive => _activeCatchScreenCount > 0;

    private void Awake()
    {
        // Ensure everything is hidden at start
        if (overlayPanel != null) overlayPanel.SetActive(false);
        if (clickText != null) clickText.gameObject.SetActive(false);
        if (clickText != null) _defaultClickText = clickText.text;
        if (judgementText != null) judgementText.gameObject.SetActive(false);
        if (fishNameText != null) fishNameText.gameObject.SetActive(false);
        EnsureLetterGradeImage();
        HideAllJudgements();
    }

    private void OnDisable()
    {
        SetCatchScreenActive(false);
    }

    private void OnDestroy()
    {
        SetCatchScreenActive(false);
    }

    private void Update()
    {
        bool continueInputHeld = IsContinueInputHeld();
        if (!continueInputHeld)
        {
            _continueInputReady = true;
            return;
        }

        if (_continueInputReady)
        {
            SetContinue();
            _continueInputReady = false;
        }
    }
    private void HideAllJudgements()
    {
        if (perfectJudgementImage != null) perfectJudgementImage.gameObject.SetActive(false);
        if (greatJudgementImage != null) greatJudgementImage.gameObject.SetActive(false);
        if (goodJudgementImage != null) goodJudgementImage.gameObject.SetActive(false);
        if (_letterGradeContainer != null) _letterGradeContainer.gameObject.SetActive(false);
    }

    private void SetContinue()
    {
        _continuePressed = true;
    }

    private bool IsContinueInputHeld()
    {
        if (Input.anyKey)
            return true;

        for (int i = 0; i < 4; i++)
        {
            if (!JSL.JslStillConnected(i))
                continue;

            JSL.JOY_SHOCK_STATE state = JSL.JslGetSimpleState(i);
            if (state.buttons != 0)
                return true;
        }

        return false;
    }

    private IEnumerator WaitForContinuePressed()
    {
        _continuePressed = false;
        while (!_continuePressed)
            yield return null;
    }
    public IEnumerator TrophyRoutine(GameObject fish)
    {
        Camera cam = Camera.main;
        Transform fishXform = fish.transform;
        _continuePressed = false;
        _continueInputReady = !IsContinueInputHeld();
        SetCatchScreenActive(true);
        if (clickText != null)
            clickText.text = _defaultClickText;

        // disable all scripts on fish
        MonoBehaviour[] scripts = fish.GetComponents<MonoBehaviour>();

        foreach (var script in scripts)        {
            if (script != this) script.enabled = false;
        }
        if (fish.TryGetComponent(out Rigidbody rb)) 
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        if (fish.TryGetComponent(out Collider col)) col.enabled = false;

        // DARKEN BACKGROUND
        if (overlayPanel != null) overlayPanel.SetActive(true);

        // FLY TO CAMERA
        Vector3 startPos = fishXform.position;
        Vector3 targetPos = cam.transform.position + (cam.transform.forward * distanceFromCamera) + (cam.transform.up * verticalOffset);
        float elapsed = 0;
        while (elapsed < flyToCameraDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flyToCameraDuration;

            fishXform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            
            fishXform.position = Vector3.Lerp(startPos, targetPos, t);
            
            yield return null;
        }

        // SHOW TEXT
        float accuracy = FishingSessionHud.GetCurrentRunAccuracyOrLast();
        
        if (judgementText != null)
        {
            judgementText.text = GetJudgementString(accuracy);
            
            // judgementText.gameObject.SetActive(true);
            judgementText.transform.localScale = Vector3.zero; 
        }

        _activeJudgementImage = GetJudgementImage(accuracy);
        if (_activeJudgementImage != null) {
            _activeJudgementImage.gameObject.SetActive(true);
            _activeJudgementImage.rectTransform.localScale = Vector3.zero;
        }

        EnsureLetterGradeImage();
        if (_letterGradeContainer != null && _letterGradeImage != null && _letterGradeLabel != null)
        {
            _letterGradeImage.sprite = GetLetterGradeSprite(accuracy);
            bool hasGradeSprite = _letterGradeImage.sprite != null;
            _letterGradeLabel.text = "Grade:";
            _letterGradeContainer.gameObject.SetActive(hasGradeSprite);
            _letterGradeContainer.localScale = Vector3.zero;
        }

        
        if (fishNameText != null)
        {
            fishNameText.text = $"{fish.name.Replace("(Clone)", "")} caught!";
            fishNameText.gameObject.SetActive(true);
            fishNameText.transform.localScale = Vector3.zero;
        }

        if (clickText != null) clickText.gameObject.SetActive(true);

        float textElapsed = 0f;
        float textGrowDuration = 0.5f;
        while (textElapsed < textGrowDuration)
        {
            textElapsed += Time.deltaTime;
            float t = textElapsed / textGrowDuration;
            

            float bounceScale = Mathf.Lerp(0f, 1f, t); 

            if (_activeJudgementImage != null) _activeJudgementImage.rectTransform.localScale = Vector3.one * _imageSize * bounceScale;
            if (_letterGradeContainer != null && _letterGradeContainer.gameObject.activeSelf) _letterGradeContainer.localScale = Vector3.one * bounceScale;
            if (judgementText != null) judgementText.transform.localScale = Vector3.one * bounceScale;
            if (fishNameText != null) fishNameText.transform.localScale = Vector3.one * bounceScale;

            fishXform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            fishXform.position = targetPos;
            
            yield return null;
        }

        while (!_continuePressed)
        {
            fishXform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            fishXform.position = targetPos;
            yield return null;
        }

        if (judgementText != null) judgementText.gameObject.SetActive(false);
        if (fishNameText != null) fishNameText.gameObject.SetActive(false);
        HideAllJudgements();
        if (overlayPanel != null) overlayPanel.SetActive(false);
        if (clickText != null) clickText.gameObject.SetActive(false);
        SetCatchScreenActive(false);
        
        Destroy(fish);
    }

    private void SetCatchScreenActive(bool active)
    {
        if (_isCatchScreenActive == active)
            return;

        _isCatchScreenActive = active;
        _activeCatchScreenCount += active ? 1 : -1;
        if (_activeCatchScreenCount < 0)
            _activeCatchScreenCount = 0;
    }

    private string GetJudgementString(float acc)
    {
        if (acc >= 95f) {
        judgementText.color = new Color(1f, 0.84f, 0f); // Gold
            return "PERFECT CATCH!";
        }
        if (acc >= 80f) {
            judgementText.color = Color.cyan;
            return "GREAT CATCH!";
        }
        judgementText.color = Color.white;
        return "GOOD CATCH!";
    }

    private Image GetJudgementImage(float acc)
    {
        if (acc >= 95f) return perfectJudgementImage;
        if (acc >= 80f) return greatJudgementImage;
        return goodJudgementImage;
    }

    private void EnsureLetterGradeImage()
    {
        if (_letterGradeContainer != null && _letterGradeImage != null && _letterGradeLabel != null)
            return;

        Transform parent = null;
        if (judgementText != null)
            parent = judgementText.transform.parent;
        else if (overlayPanel != null)
            parent = overlayPanel.transform;

        if (parent == null)
            return;

        GameObject containerGo = new GameObject("LetterGradeContainer", typeof(RectTransform));
        containerGo.transform.SetParent(parent, false);
        containerGo.transform.SetSiblingIndex(judgementText != null ? judgementText.transform.GetSiblingIndex() + 1 : containerGo.transform.GetSiblingIndex());
        _letterGradeContainer = containerGo.GetComponent<RectTransform>();
        _letterGradeContainer.anchorMin = LetterGradeAnchor;
        _letterGradeContainer.anchorMax = LetterGradeAnchor;
        _letterGradeContainer.pivot = new Vector2(0.5f, 0.5f);
        _letterGradeContainer.anchoredPosition = LetterGradeAnchoredPosition;
        _letterGradeContainer.sizeDelta = new Vector2(LetterGradeContainerWidth, LetterGradeContainerHeight);

        GameObject labelGo = new GameObject("LetterGradeLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(_letterGradeContainer, false);
        _letterGradeLabel = labelGo.GetComponent<TextMeshProUGUI>();
        _letterGradeLabel.raycastTarget = false;
        _letterGradeLabel.text = "Grade:";
        _letterGradeLabel.fontSize = LetterGradeLabelFontSize;
        _letterGradeLabel.alignment = TextAlignmentOptions.Center;
        _letterGradeLabel.color = Color.white;
        _letterGradeLabel.enableAutoSizing = false;

        if (judgementText != null)
        {
            _letterGradeLabel.font = judgementText.font;
            _letterGradeLabel.fontSharedMaterial = judgementText.fontSharedMaterial;
        }
        else if (fishNameText != null)
        {
            _letterGradeLabel.font = fishNameText.font;
            _letterGradeLabel.fontSharedMaterial = fishNameText.fontSharedMaterial;
        }

        RectTransform labelRect = _letterGradeLabel.rectTransform;
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = LetterGradeLabelPosition;
        labelRect.sizeDelta = new Vector2(LetterGradeLabelWidth, 64f);

        GameObject gradeGo = new GameObject("LetterGradeImage", typeof(RectTransform), typeof(Image));
        gradeGo.transform.SetParent(_letterGradeContainer, false);
        _letterGradeImage = gradeGo.GetComponent<Image>();
        _letterGradeImage.raycastTarget = false;
        _letterGradeImage.preserveAspect = true;

        RectTransform gradeRect = _letterGradeImage.rectTransform;
        gradeRect.anchorMin = new Vector2(0.5f, 0.5f);
        gradeRect.anchorMax = new Vector2(0.5f, 0.5f);
        gradeRect.pivot = new Vector2(0.5f, 0.5f);
        gradeRect.anchoredPosition = LetterGradeImagePosition;
        gradeRect.sizeDelta = new Vector2(LetterGradeImageSize, LetterGradeImageSize);

        _letterGradeContainer.gameObject.SetActive(false);
    }

    private Sprite GetLetterGradeSprite(float accuracy)
    {
        string gradeKey = GetLetterGradeKey(accuracy);
        if (string.IsNullOrEmpty(gradeKey))
            return null;

        if (_letterGradeSprites.TryGetValue(gradeKey, out Sprite cachedSprite))
            return cachedSprite;

        Sprite sprite = LoadLetterGradeSprite(gradeKey);
        _letterGradeSprites[gradeKey] = sprite;
        return sprite;
    }

    private static string GetLetterGradeKey(float accuracy)
    {
        float clampedAccuracy = Mathf.Clamp(accuracy, 0f, 100f);
        if (clampedAccuracy >= 99.95f) return "s";
        if (clampedAccuracy >= 80f) return "a";
        if (clampedAccuracy >= 70f) return "b";
        if (clampedAccuracy >= 60f) return "c";
        if (clampedAccuracy >= 50f) return "d";
        return "f";
    }

    private static Sprite LoadLetterGradeSprite(string gradeKey)
    {
        string resourcePath = $"{LetterGradeResourceFolder}/grade_{gradeKey}";
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
            return sprite;

#if UNITY_EDITOR
        string editorAssetPath = $"Assets/Images/letter_grades/grade_{gradeKey}.png";
        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(editorAssetPath);
        if (sprite != null)
            return sprite;
#endif

        string absolutePath = Path.Combine(Application.dataPath, "Images", "letter_grades", $"grade_{gradeKey}.png");
        if (!File.Exists(absolutePath))
            return null;

        byte[] fileBytes = File.ReadAllBytes(absolutePath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
        if (!texture.LoadImage(fileBytes))
            return null;

        texture.name = $"grade_{gradeKey}_texture";
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
    }
}

