using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class BeatSpawner : MonoBehaviour
{
    [SerializeField] private GameObject beat;
    [SerializeField] private float beatSpeed = 6f;
    [SerializeField] private float hitLineOffsetX = -5f;

    private float songTime = 0f;
    public AudioSource beatMapSong;
    public AudioResource songOne;

    private List<float> beatTimes = new List<float>()
    {
        2f, 4f, 6f, 8f, 10f, 12f, 14f, 16f
    };

    private int nextBeatIndex = 0;
    private float travelTime;
    private float spawnX;
    private float hitLineX;
    private float laneY;
    private float laneZ;

    private List<GameObject> activeBeats = new List<GameObject>();

    public GameManager gameManager;

    void Start()
    {
        gameManager = FindObjectOfType<GameManager>();
        Camera cam = Camera.main;

        laneY = transform.position.y;
        laneZ = transform.position.z;
        hitLineX = transform.position.x + hitLineOffsetX;

        // Distance from camera to lane plane
        float depth = Mathf.Abs(cam.transform.position.z - laneZ);

        // Right edge of screen in world space
        Vector3 rightEdge = cam.ViewportToWorldPoint(
            new Vector3(1f, 0.5f, depth)
        );

        spawnX = rightEdge.x;

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

        // if ()
    }

    void SpawnBeat()
    {
        GameObject newBeat = Instantiate(beat);
        newBeat.transform.position = new Vector3(spawnX, laneY, laneZ);

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