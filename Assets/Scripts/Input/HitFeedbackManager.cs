using UnityEngine;

public class HitFeedbackManager : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject perfectPrefab;
    public GameObject goodPrefab;
    public GameObject missPrefab;

    public void TriggerFeedback(Vector3 position, string rating)
    {
        GameObject prefabToSpawn = null;

        switch (rating)
        {
            case "PERFECT": prefabToSpawn = perfectPrefab; break;
            case "GOOD":    prefabToSpawn = goodPrefab;    break;
            case "MISS":    prefabToSpawn = missPrefab;    break;
        }

        if (prefabToSpawn != null)
        {
            Instantiate(prefabToSpawn, position, Quaternion.identity);

        }
    }
}
