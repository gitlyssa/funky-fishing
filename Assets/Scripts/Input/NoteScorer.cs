using UnityEngine;

public class NoteScorer : MonoBehaviour
{
    [Header("References")]
    public NoteSpawner noteSpawner;
    public RhythmInputProcessor inputProcessor;

    [Header("Timing Windows (in seconds)")]
    public float perfectWindow = 0.1f;
    public float goodWindow = 0.25f;
    public float missWindow = 0.5f; //maximum time a note can be late before we can't hit anymore

    void Update()
    {
        HandleAutoMiss();

        // loop through 8 directions to check for hits
        foreach (FlickDirection dir in System.Enum.GetValues(typeof(FlickDirection)))
        {
            if (dir == FlickDirection.None)
                continue;

            if (inputProcessor.GetFlick(dir))
            {
                TryHitNote(dir);
            }
        }
    }

    private void HandleAutoMiss()
    {
        for (int i = noteSpawner.activeNotes.Count - 1; i >= 0; i--)
        {
            RhythmNote note = noteSpawner.activeNotes[i];
            float timeDiff = Time.time - note.targetHitTime;
            
            // if more than missWindow has passsed, remove its a miss
            if (timeDiff > missWindow)
            {
                Debug.Log("Missed Note!");
                RemoveNote(i);
            }
        }
    }

    private void TryHitNote(FlickDirection dir)
    {
        for (int i = 0; i < noteSpawner.activeNotes.Count; i++)
        {
            RhythmNote note = noteSpawner.activeNotes[i];

            if(!note.isFlickNote)
                continue; //skip if not flick note

            if (IsNoteInDirection(note, dir))
            {
                float timeDiff = Time.time - note.targetHitTime;
                float absDiff = Mathf.Abs(timeDiff);

                if (absDiff <= missWindow)
                {
                    EvaluateHit(i, absDiff);
                    return; // found hit, exit
                }
            }
        }
    }

    private void EvaluateHit(int index, float diff)
    {
        string rating = "Poor";
        if (diff <= perfectWindow)
        {
            rating = "Perfect";
        }
        else if (diff <= goodWindow)
        {
            rating = "Good";
        }

        Debug.Log($"{rating}! Timing Error: {diff * 1000:F0}ms");
        
        RemoveNote(index);
        // maybe spawn some particles?
    }

    private void RemoveNote(int index)
    {
        RhythmNote note = noteSpawner.activeNotes[index];
        noteSpawner.activeNotes.RemoveAt(index);
        Destroy(note.gameObject);
    }

   private bool IsNoteInDirection(RhythmNote note, FlickDirection dir)
    {
        float noteAngle = (note.startAngle % 360 + 360) % 360;
        float targetAngle = GetAngleFromFlag(dir);

        if (targetAngle < 0) return false; // Handled 'None'

        float diff = Mathf.Abs(Mathf.DeltaAngle(noteAngle, targetAngle));
        return diff <= 22.5f; 
    }

    private float GetAngleFromFlag(FlickDirection dir)
    {
        switch (dir)
        {
            case FlickDirection.Right:     return 0f;
            case FlickDirection.UpRight:   return 45f;
            case FlickDirection.Up:        return 90f;
            case FlickDirection.UpLeft:    return 135f;
            case FlickDirection.Left:      return 180f;
            case FlickDirection.DownLeft:  return 225f;
            case FlickDirection.Down:      return 270f;
            case FlickDirection.DownRight: return 315f;
            default: return -1f; // None
        }
    }

    
}
