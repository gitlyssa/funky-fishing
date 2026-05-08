using UnityEngine;

public class CrabScuttle : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the parent Crab_Root here")]
    public CrabMovement rootMovement;

    [Header("Scuttle Settings")]
    public float scuttleSpeed = 15f;
    public float scuttleHeight = 0.15f;
    public float scuttleWobble = 10f;

    public float idleScuttleSpeed = 5f;
    public float idleScuttleHeight = 0.05f;
    public float idleScuttleWobble = 20f;

    private float cycle = 0f;

    void Update()
    {
        if (rootMovement != null && !rootMovement.isWaiting)
        {
            cycle += Time.deltaTime * scuttleSpeed;

            transform.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(cycle)) * scuttleHeight, 0);
            

            transform.localRotation = Quaternion.Euler(Mathf.Sin(cycle) * scuttleWobble, 0, 0);
        }
        else
        {

            cycle += Time.deltaTime * idleScuttleSpeed;

            transform.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(cycle)) * idleScuttleHeight, 0);
            transform.localRotation = Quaternion.Euler(Mathf.Sin(cycle) * idleScuttleWobble, 0, 0);
        }
    }
}