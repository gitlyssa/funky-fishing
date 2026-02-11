using UnityEngine;
using System.Collections;
using TMPro;

public class RhythmIntroAnimation : MonoBehaviour
{
    [Header("Background Fade")]
    [SerializeField] private MeshRenderer backgroundCubeRenderer; 
    public float fadeDuration = 2.0f;
    [Range(0, 1)] public float targetAlpha = 0.8f;

    [Header("3D Movement")]
    [SerializeField] private GameObject rhythmContainer; 
    public float moveDuration = 2.0f;
    public Vector3 startPositionOffset = new Vector3(0, -15, 0); 
    public Vector3 startRotationOffset = new Vector3(0, 0, -720);
    public Vector3 startScale = Vector3.one * 0.3f;

    [Header("Fish Animation")]
    [SerializeField] private GameObject fishModel;
    public Vector3 fishWindUpRotation = new Vector3(0, 1080, 0); 
    public float fishFadeOutSpeed = 2f;

    [Header("UI Text")]
    [SerializeField] private TextMeshPro readyText;
    [SerializeField] private TextMeshPro goText;
    [SerializeField] private GameObject musicContainer;

    private Material _bgMaterial;
    private Color _initialColor;


    

    private void Awake()
    {
        _bgMaterial = backgroundCubeRenderer.material;
        _initialColor = _bgMaterial.color;
        _bgMaterial.color = new Color(_initialColor.r, _initialColor.g, _initialColor.b, 0);

        // Init Wheel State
        rhythmContainer.transform.localPosition = startPositionOffset;
        rhythmContainer.transform.localRotation = Quaternion.Euler(startRotationOffset);
        rhythmContainer.transform.localScale = startScale;

        // Init Fish State (Spinning at center)
        fishModel.transform.localRotation = Quaternion.Euler(fishWindUpRotation);
        fishModel.transform.localScale = Vector3.zero;

        // Init Text
        readyText.gameObject.SetActive(false);
        goText.gameObject.SetActive(false);
        musicContainer.SetActive(false); 
    }

    private void Start()
    {
        StartCoroutine(FullIntroSequence());
    }

    private IEnumerator FullIntroSequence()
    {
        // BACKGROUND AND FISH
        float fadeElapsed = 0f;
        while (fadeElapsed < fadeDuration)
        {
            fadeElapsed += Time.deltaTime;
            float t = fadeElapsed / fadeDuration;
            float easedT = t * t * (3f - 2f * t); // Smoothstep function

            float fishSpin = Mathf.Lerp(1f, 0f, easedT) * fishWindUpRotation.y; 
            fishModel.transform.localRotation = Quaternion.Euler(0, fishSpin + 270, 0);
            fishModel.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one * 0.4f, easedT);

            _bgMaterial.color = new Color(_initialColor.r, _initialColor.g, _initialColor.b, Mathf.Lerp(0, targetAlpha, easedT));
            yield return null;
        }
        _bgMaterial.color = new Color(_initialColor.r, _initialColor.g, _initialColor.b, targetAlpha);

        readyText.gameObject.SetActive(true);
        StartCoroutine(AnimatePopIn(readyText.transform));


        yield return new WaitForSeconds(1.2f);

        // SCENE OBJECTS
        float moveElapsed = 0f;

        while (moveElapsed < moveDuration)
        {
            moveElapsed += Time.deltaTime;
            float t = moveElapsed / moveDuration;

            float easedT = 1f - Mathf.Pow(1f - t, 5);

            rhythmContainer.transform.localPosition = Vector3.Lerp(startPositionOffset, Vector3.zero, easedT);
            float currentSpinZ = Mathf.Lerp(startRotationOffset.z, 0, easedT);
            rhythmContainer.transform.localRotation = Quaternion.Euler(0, 0, currentSpinZ);
            rhythmContainer.transform.localScale = Vector3.Lerp(startScale, Vector3.one, easedT);

            yield return null;
        }

        rhythmContainer.transform.localPosition = Vector3.zero;
        rhythmContainer.transform.localRotation = Quaternion.identity;
        rhythmContainer.transform.localScale = Vector3.one;

        // go text
        goText.gameObject.SetActive(true);
        StartCoroutine(AnimatePopIn(goText.transform));

        // Wait for a moment before starting gameplay theyre all hard coded times dunno
        yield return new WaitForSeconds(2.0f);

        StartGameplay();
    }

    private void StartGameplay()
    {
        musicContainer.SetActive(true); 
    }

    private IEnumerator AnimatePopIn(Transform target)
    {
        float duration = 0.4f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // sin overbounce
            float s = Mathf.Sin(t * Mathf.PI * 0.5f + (Mathf.PI * 0.1f)) * 1.2f; 
            target.localScale = Vector3.one * s;
            yield return null;
        }
        target.localScale = Vector3.one;

        // hold for hlaf a second
        yield return new WaitForSeconds(0.5f);
        StartCoroutine(AnimatePopOut(target));
    }

    private IEnumerator AnimatePopOut(Transform target)
    {
        float duration = 0.3f;
        float elapsed = 0f;
        Vector3 initialScale = target.localScale;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            target.localScale = Vector3.Lerp(initialScale, Vector3.zero, t * t);
            yield return null;
        }
        target.gameObject.SetActive(false);
    }

    private IEnumerator AnimateScaleToZero(Transform target, float duration)
    {
        float elapsed = 0f;
        Vector3 startScl = target.localScale;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            target.localScale = Vector3.Lerp(startScl, Vector3.zero, elapsed / duration);
            yield return null;
        }
        target.gameObject.SetActive(false);
    }
}

