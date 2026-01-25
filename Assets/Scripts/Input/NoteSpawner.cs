using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
public class NoteSpawner : MonoBehaviour
{

    public GameObject notePrefab;
    public float globalScrollSpeed = 10f;
    public float spawnZ = 30f;

    [Header("Materials")]
    public Material holdMaterial;
    public Material flickMaterial; 

    public List<RhythmNote> activeNotes = new List<RhythmNote>();

    private float spawnTimer = 0f;
    private float spawnInterval = 1f; // spawn a note every second
    
    // hit time is time when note should be hit
    // duration is how long the note lasts (0 for flick)
    // starting angle and ending angle
    public void SpawnNote(float hitTime, float duration, float sAngle, float eAngle)
    {
        float travelTime = spawnZ / globalScrollSpeed;
        
        GameObject go = Instantiate(notePrefab);
        RhythmNote note = go.GetComponent<RhythmNote>();
        
        note.Initialize(hitTime, duration, travelTime, sAngle, eAngle, holdMaterial, flickMaterial);
        activeNotes.Add(note);
    }

    public void RemoveNote(int index)
    {
        RhythmNote note = activeNotes[index];
        activeNotes.RemoveAt(index);
        Destroy(note.gameObject);
    }

    // Update is called once per frame
    void Update()
    {   

        // randomly spawn a note every second in one of the 8 directions

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval)
        {
            spawnTimer -= spawnInterval;

            float angle = Random.Range(0, 8) * 45f; // 0, 45, 90, ..., 315

            SpawnNote(Time.time + (spawnZ / globalScrollSpeed), 0f, angle, angle);
        }


        // activeNotes.RemoveAll(n => n == null);
        // // press z, x, c, v for a flick note in left, up, right, down
        // if (Keyboard.current.zKey.wasPressedThisFrame)
        // {
        //     SpawnNote(Time.time + 2f, 0f, 180f, 180f); // Left
        // }
        // if (Keyboard.current.xKey.wasPressedThisFrame)
        // {
        //     SpawnNote(Time.time + 2f, 0f, 90f, 90f); // Up
        // }
        // if (Keyboard.current.cKey.wasPressedThisFrame)
        // {
        //     SpawnNote(Time.time + 2f, 0f, 0f, 0f); // Right
        // }
        // if (Keyboard.current.vKey.wasPressedThisFrame)
        // {
        //     SpawnNote(Time.time + 2f, 0f, 270f, 270f); // Down
        // }

        // // b n m for hold notes in left, up, right
        // if (Keyboard.current.bKey.wasPressedThisFrame)
        // {
        //     SpawnNote(Time.time + 2f, 1f, 180f, 180f); // Left
        // }
        // if (Keyboard.current.nKey.wasPressedThisFrame)
        // {
        //     SpawnNote(Time.time + 2f, 1f, 90f, 90f); // Up
        // }
        // if (Keyboard.current.mKey.wasPressedThisFrame)
        // {
        //     SpawnNote(Time.time + 2f, 1f, 0f, 0f); // Right
        // }
    }
}
