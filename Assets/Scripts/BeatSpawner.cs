using System.Collections.Generic;
using UnityEngine;

public class BeatSpawner : MonoBehaviour
{
    [SerializeField] private GameObject beat;

    [Header("Timing")]
    [SerializeField] private float beatSpeed = 6f;
    [SerializeField] private float hitLineX = -5f;

    [Header("Spawn Position")]
    [SerializeField] private float spawnPadding = 0.5f;

    private float spawnX;
    private float laneY = 0f;

    // Song timer
    private float songTime = 0f;

    private List<float> beatTimes = new List<float>()
    {
        1.0f,
        1.5f,
        2.0f,
        2.5f,
        3.0f,
        4.0f,
        7.0f
    };

    private int nextBeatIndex = 0;
    private float travelTime;

    void Start()
    {
        Camera cam = Camera.main;
        float halfWidth = cam.orthographicSize * cam.aspect;
        spawnX = halfWidth - spawnPadding;

        float distance = Mathf.Abs(spawnX - hitLineX);
        travelTime = distance / beatSpeed;
    }

    void Update()
    {
        songTime += Time.deltaTime;

        if (nextBeatIndex >= beatTimes.Count)
            return;

        // Spawn early so it reaches hit line at the beat time
        if (songTime >= beatTimes[nextBeatIndex] - travelTime)
        {
            SpawnBeat();
            nextBeatIndex++;
        }
    }

    void SpawnBeat()
    {
        GameObject newBeat = Instantiate(beat);
        newBeat.transform.position = new Vector3(spawnX, laneY, 0f);

        BeatMovement movement = newBeat.GetComponent<BeatMovement>();
        if (movement != null)
        {
            movement.speed = beatSpeed;
        }
    }
}
