using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class BeatSpawner : MonoBehaviour
{
    [SerializeField] private GameObject beat;
    [SerializeField] private float beatSpeed = 6f;
    [SerializeField] private float hitLineOffsetX = -5f;
    [SerializeField] private bool useCameraRelativeLane = true;
    [SerializeField] private float cameraDepthOffset = 10f;
    [SerializeField] private float cameraVerticalOffset = 0f;

    private float songTime = 0f;
    public AudioSource beatMapSong;
    public AudioResource songOne;

    private List<float> beatTimes = new List<float>()
    {
        2f, 4f, 6f, 8f, 10f, 12f, 14f, 16f
    };

    private int nextBeatIndex = 0;
    private float travelTime;
    private Camera cam;
    private float spawnX;
    private float hitLineX;
    private float laneY;
    private float laneZ;
    private List<GameObject> activeBeats = new List<GameObject>();
    private GameManager gameManager;

    void Start()
    {
        gameManager = FindObjectOfType<GameManager>();
        cam = Camera.main;

        if (useCameraRelativeLane && cam != null)
        {
            Vector3 lanePoint = cam.transform.position + cam.transform.forward * cameraDepthOffset
                + cam.transform.up * cameraVerticalOffset;
            laneY = lanePoint.y;
            laneZ = lanePoint.z;
            hitLineX = lanePoint.x + hitLineOffsetX;
        }
        else
        {
            laneY = transform.position.y;
            laneZ = transform.position.z;
            hitLineX = transform.position.x + hitLineOffsetX;
        }

        // Distance from camera to lane plane
        float depth = cam != null ? Mathf.Abs(cam.transform.position.z - laneZ) : Mathf.Abs(laneZ);
        if (useCameraRelativeLane)
        {
            depth = cameraDepthOffset;
        }

        // Right edge of screen in world space
        Vector3 rightEdge;
        if (cam != null)
        {
            rightEdge = cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, depth));
            if (useCameraRelativeLane)
            {
                rightEdge += cam.transform.up * cameraVerticalOffset;
            }
        }
        else
        {
            rightEdge = new Vector3(transform.position.x, laneY, laneZ);
        }

        spawnX = rightEdge.x;
        laneY = rightEdge.y;
        laneZ = rightEdge.z;

        float distance = Mathf.Abs(spawnX - hitLineX);
        travelTime = distance / beatSpeed;

        // Play song
        beatMapSong.resource = songOne;
        beatMapSong.Play();
    }

    void Update()
    {
        songTime = beatMapSong.time;

        if (nextBeatIndex >= beatTimes.Count)
            return;

        if (songTime >= beatTimes[nextBeatIndex] - travelTime)
        {
            SpawnBeat();
            nextBeatIndex++;
        }

        // after 7 seconds reset the game
        if (songTime >= 7f)
        {
            reset();
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            reset();
        }
        
    }

    void SpawnBeat()
    {
        GameObject newBeat = Instantiate(beat);
        newBeat.transform.position = new Vector3(spawnX, laneY, laneZ);
        if (cam != null)
        {
            newBeat.transform.rotation = cam.transform.rotation;
        }

        BeatMovement bm = newBeat.GetComponent<BeatMovement>();
        if (bm != null)
            bm.speed = beatSpeed;
        activeBeats.Add(newBeat);
    }
    
    void reset()
    {
        nextBeatIndex = 0;
        songTime = 0f;
        beatMapSong.Stop();
        beatMapSong.Play();
        foreach (GameObject beat in activeBeats)
        {
            Destroy(beat);
        }
        activeBeats.Clear();
        gameManager.FinishRhythmGame(true);
    }
}
