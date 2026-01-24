using UnityEngine;
using System.Collections.Generic;

public class BallVisualizer : MonoBehaviour
{
    public RhythmInputProcessor inputSource;

    [Header("Materials")]
    public Material idleMaterial;
    public Material activteMaterial;
    public Material flickMaterial;

    [Header("Settings")]
    public float flickFlashDuration = 0.1f;

    [Header("Spheres (R, UR, U, UL, L, DL, D, DR)")]
    public List<GameObject> directionSpheres;
    public GameObject reelSphere;

    public float flickSizeMultiplier = 0.8f;
    public float holdSizeMultiplier = 0.6f;
    public float idleSizeMultiplier = 0.5f;

    private Dictionary<FlickDirection, GameObject> directionToSphere;
    private Dictionary<FlickDirection, float> flickTimers = new Dictionary<FlickDirection, float>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // link directions to spheres
        directionToSphere = new Dictionary<FlickDirection, GameObject>
        {
            { FlickDirection.Right, directionSpheres[0] },
            { FlickDirection.UpRight, directionSpheres[1] },
            { FlickDirection.Up, directionSpheres[2] },
            { FlickDirection.UpLeft, directionSpheres[3] },
            { FlickDirection.Left, directionSpheres[4] },
            { FlickDirection.DownLeft, directionSpheres[5] },
            { FlickDirection.Down, directionSpheres[6] },
            { FlickDirection.DownRight, directionSpheres[7] },
        };
        
    }

    // Update is called once per frame
    void Update()
    {   
        // reel button should be active if reel is held
        if (inputSource.RawReelButton)
        {
            reelSphere.GetComponent<Renderer>().material = activteMaterial;
            reelSphere.transform.localScale = Vector3.one * holdSizeMultiplier; // Slight swell
        }
        else
        {
            reelSphere.GetComponent<Renderer>().material = idleMaterial;
            reelSphere.transform.localScale = Vector3.one * idleSizeMultiplier;
        }

        foreach (var pair in directionToSphere)
        {
            FlickDirection direction = pair.Key;
            GameObject ball = pair.Value;

            // Check for flick
            if (inputSource.GetFlick(direction))
            {   
                flickTimers[direction] = Time.time + flickFlashDuration;
            }



            if (flickTimers.ContainsKey(direction) && Time.time < flickTimers[direction])
            {
                //Flick Flash
                ball.GetComponent<Renderer>().material = flickMaterial;
                ball.transform.localScale = Vector3.one * flickSizeMultiplier; // Visual punch
            }   
            else if (inputSource.IsHolding(direction))
            {
                //Hold
                ball.GetComponent<Renderer>().material = activteMaterial;
                ball.transform.localScale = Vector3.one * holdSizeMultiplier; // Slight swell
            }
            else
            {
                // Idle
                ball.GetComponent<Renderer>().material = idleMaterial;
                ball.transform.localScale = Vector3.one * idleSizeMultiplier;
            }
        }
    }
}
