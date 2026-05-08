using System.Collections;
using UnityEngine;

public class CrabMovement : MonoBehaviour
{
    public enum CrabState { IdleAtA, MovingToB, IdleAtB, MovingToA }
    
    [Header("Current State")]
    public CrabState currentState = CrabState.IdleAtA;

    [Header("Waypoints (Assign in Inspector)")]
    public Transform posA;
    public Transform logBottom;
    public Transform logTop;
    public Transform posB;

    [Header("Movement Settings")]
    public float moveSpeed = 2f;
    public float rotationSpeed = 5f;

    [Header("Idle Settings")]
    public float pacingDistanceA = 2f; 
    public float waitTimeBetweenMoves = 1.5f;

    [Header("Autonomous Behavior")]
    public float chanceToGoToB = 15f;
    public float chanceToReturnToA = 20f;

    // Exposed so the child mesh knows when to play the scuttle animation
    public bool isExecutingAction { get; private set; } = false;
    public bool isWaiting { get; private set; } = false;
    
    private Vector3 startPosA;
    private Vector3 pacingDirection; 

    void Start()
    {
        if (posA != null)
        {
            startPosA = posA.position;
            pacingDirection = Quaternion.Euler(0, 45f, 0) * Vector3.forward;
        }
    }

    void Update()
    {
        if (isExecutingAction) return;

        switch (currentState)
        {
            case CrabState.IdleAtA:
                StartCoroutine(PaceAtA());
                break;
            case CrabState.IdleAtB:
                StartCoroutine(SpinAtB());
                break;
        }
    }

    // --- TRIGGER METHODS ---

    public void TriggerMoveToB()
    {
        if (currentState == CrabState.IdleAtA && !isExecutingAction)
        {
            StartCoroutine(SequenceMoveToB());
        }
    }

    public void TriggerMoveToA()
    {
        if (currentState == CrabState.IdleAtB && !isExecutingAction)
        {
            StartCoroutine(SequenceMoveToA());
        }
    }

    // --- IDLE COROUTINES ---

    private IEnumerator PaceAtA()
    {
        isExecutingAction = true;
        isWaiting = false;

        if (Random.Range(0f, 100f) < chanceToGoToB)
        {
            isExecutingAction = false; 
            StartCoroutine(SequenceMoveToB());
            yield break; 
        }

        Vector3 targetPacePoint = startPosA + (pacingDirection * Random.Range(-pacingDistanceA, pacingDistanceA));
        Vector3 movementVector = targetPacePoint - transform.position;
        bool walkBackwards = Vector3.Dot(movementVector, pacingDirection) < 0;
        
        yield return StartCoroutine(MoveToSpecificPoint(targetPacePoint, true, walkBackwards));
        
        isWaiting = true;
        yield return new WaitForSeconds(waitTimeBetweenMoves);

        isExecutingAction = false;
    }

    private IEnumerator SpinAtB()
    {
        isExecutingAction = true;
        isWaiting = false;

        if (Random.Range(0f, 100f) < chanceToReturnToA)
        {
            isExecutingAction = false;
            StartCoroutine(SequenceMoveToA());
            yield break;
        }

        int spins = Random.Range(0, 3); 
        
        if (spins > 0)
        {
            int direction = Random.Range(0, 2) == 0 ? -1 : 1; 
            float totalDegrees = 360f * spins;
            
            // Convert our standard rotationSpeed into a fast "degrees per second" rate
            float degreesPerSecond = rotationSpeed * 40f; 
            
            // Time = Distance / Speed
            float duration = totalDegrees / degreesPerSecond;
            float timeElapsed = 0f;

            while (timeElapsed < duration)
            {
                transform.Rotate(0, degreesPerSecond * direction * Time.deltaTime, 0);
                timeElapsed += Time.deltaTime;
                yield return null;
            }
        }

        isWaiting = true;
        yield return new WaitForSeconds(waitTimeBetweenMoves);

        isExecutingAction = false;
    }

    // --- MOVEMENT SEQUENCES ---

    private IEnumerator SequenceMoveToB()
    {
        isExecutingAction = true;
        isWaiting = false;
        currentState = CrabState.MovingToB;

        yield return StartCoroutine(MoveToSpecificPoint(logBottom.position, true, true));
        yield return StartCoroutine(MoveToSpecificPoint(logTop.position, true, true));
        yield return StartCoroutine(MoveToSpecificPoint(posB.position, true, true));

        currentState = CrabState.IdleAtB;
        isWaiting = true;
        yield return new WaitForSeconds(waitTimeBetweenMoves);
        isExecutingAction = false;
    }

    private IEnumerator SequenceMoveToA()
    {
        isExecutingAction = true;
        isWaiting = false;
        currentState = CrabState.MovingToA;

        yield return StartCoroutine(MoveToSpecificPoint(logTop.position, true, false));
        yield return StartCoroutine(MoveToSpecificPoint(logBottom.position, true, false));
        yield return StartCoroutine(MoveToSpecificPoint(posA.position, true, false));
        
        currentState = CrabState.IdleAtA;
        isWaiting = true;
        yield return new WaitForSeconds(waitTimeBetweenMoves);
        isExecutingAction = false;
    }

    // --- CORE MOVEMENT LOGIC ---

    private IEnumerator MoveToSpecificPoint(Vector3 targetPos, bool rotateToFace, bool walkBackwards = false)
    {   
        float flip = walkBackwards ? -1.0f : 1.0f;
        
        while (Vector3.Distance(transform.position, targetPos) > 0.05f)
        {
            if (rotateToFace)
            {   
                Vector3 direction = (targetPos - transform.position).normalized;
                if (direction != Vector3.zero)
                {
                    Quaternion lookRotation = Quaternion.LookRotation(direction * flip);
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
                }
            }

            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
            yield return null; 
        }
    }
}