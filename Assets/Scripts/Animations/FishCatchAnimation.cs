using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

public class FishCatchAnimation : MonoBehaviour
{
    [Header("UI & Effects")]
    public GameObject overlayPanel; //black image modify transparency
    public TextMeshProUGUI judgementText; // "Perfect Catch!"
    public TextMeshProUGUI fishNameText;  // "Redbelly caught!"
    public TextMeshProUGUI clickText;        // press any key to continue
    [SerializeField] private GameObject scoreboardPanel;
    [SerializeField] private TextMeshProUGUI rank1Text;
    [SerializeField] private TextMeshProUGUI rank2Text;
    [SerializeField] private TextMeshProUGUI rank3Text;
    [SerializeField] private TextMeshProUGUI rank4Text;
    [SerializeField] private TextMeshProUGUI rank5Text;

    public Image perfectJudgementImage;
    public Image greatJudgementImage;
    public Image goodJudgementImage;
    private Image _activeJudgementImage;
    private string _defaultClickText;

    private float _imageSize = 3.1f; // Base size for judgement images


    [Header("Positioning")]
    public float flyToCameraDuration = 2.6f;
    public float distanceFromCamera = 1.2f;
    public float verticalOffset = -0.1f;
    public float spinSpeed = 150f;
    private bool _continuePressed = false;
    private bool _continueInputReady = true;

    private void Awake()
    {
        ResolveScoreboardReferences();

        // Ensure everything is hidden at start
        if (overlayPanel != null) overlayPanel.SetActive(false);
        if (scoreboardPanel != null) scoreboardPanel.SetActive(false);
        if (clickText != null) clickText.gameObject.SetActive(false);
        if (clickText != null) _defaultClickText = clickText.text;
        if (judgementText != null) judgementText.gameObject.SetActive(false);
        if (fishNameText != null) fishNameText.gameObject.SetActive(false);
        HideAllJudgements();
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

    private TextMeshProUGUI GetRankText(int index)
    {
        switch (index)
        {
            case 0: return rank1Text;
            case 1: return rank2Text;
            case 2: return rank3Text;
            case 3: return rank4Text;
            case 4: return rank5Text;
            default: return null;
        }
    }

    private void ResolveScoreboardReferences()
    {
        Transform searchRoot = null;
        if (clickText != null)
            searchRoot = clickText.transform.parent;
        else if (fishNameText != null)
            searchRoot = fishNameText.transform.parent;
        else if (judgementText != null)
            searchRoot = judgementText.transform.parent;
        else
            searchRoot = transform;

        if (scoreboardPanel == null)
        {
            Transform scoreboardTransform = FindChildRecursive(searchRoot, "Scoreboard");
            if (scoreboardTransform == null)
                scoreboardTransform = FindSceneTransformByName("Scoreboard");
            if (scoreboardTransform != null)
                scoreboardPanel = scoreboardTransform.gameObject;
        }

        Transform scoreboardRoot = scoreboardPanel != null ? scoreboardPanel.transform : searchRoot;

        if (rank1Text == null) rank1Text = FindTextByName(scoreboardRoot, "Rank1Text");
        if (rank2Text == null) rank2Text = FindTextByName(scoreboardRoot, "Rank2Text");
        if (rank3Text == null) rank3Text = FindTextByName(scoreboardRoot, "Rank3Text");
        if (rank4Text == null) rank4Text = FindTextByName(scoreboardRoot, "Rank4Text");
        if (rank5Text == null) rank5Text = FindTextByName(scoreboardRoot, "Rank5Text");
    }

    private static TextMeshProUGUI FindTextByName(Transform root, string childName)
    {
        if (root == null)
            return null;

        Transform child = FindChildRecursive(root, childName);
        if (child == null)
            child = FindSceneTransformByName(childName);
        if (child == null)
            return null;

        return child.GetComponent<TextMeshProUGUI>();
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildRecursive(root.GetChild(i), childName);
            if (result != null)
                return result;
        }

        return null;
    }

    private static Transform FindSceneTransformByName(string targetName)
    {
        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform candidate = allTransforms[i];
            if (candidate == null)
                continue;
            if (candidate.name != targetName)
                continue;
            if (!candidate.gameObject.scene.IsValid())
                continue;

            return candidate;
        }

        return null;
    }

    private void ShowTopScores()
    {
        ResolveScoreboardReferences();

        var topScores = SessionTopScoresTracker.TopScores;
        for (int i = 0; i < SessionTopScoresTracker.MaxTrackedScores; i++)
        {
            TextMeshProUGUI rankText = GetRankText(i);
            if (rankText == null)
                continue;

            rankText.gameObject.SetActive(true);
            rankText.text = i < topScores.Count
                ? $"{i + 1}. {topScores[i]}"
                : $"{i + 1}. ---";
        }

        if (scoreboardPanel != null)
            scoreboardPanel.SetActive(true);
        else
            Debug.LogWarning("FishCatchAnimation could not resolve the Scoreboard panel.");

        if (clickText != null)
            clickText.gameObject.SetActive(false);
    }

    private void HideTopScores()
    {
        if (scoreboardPanel != null)
            scoreboardPanel.SetActive(false);
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
        HideTopScores();
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
        float accuracy = FishingSessionHud.LastCatchAccuracy;
        
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
        fishXform.gameObject.SetActive(false);
        HideAllJudgements();
        ShowTopScores();

        yield return WaitForContinuePressed();

        HideTopScores();
        if (overlayPanel != null) overlayPanel.SetActive(false);
        if (clickText != null) clickText.gameObject.SetActive(false);
        
        Destroy(fish);
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
}

