using UnityEngine;

public class BobberSpawner : MonoBehaviour
{
    public Camera mainCamera;           
    public GameObject bobberPrefab;     
    public float pondY = 1f;            // Height where bobber should appear (Y axis)
    public bool bobberSpawned = false;
    [Header("Cast Tuning")]
    public float minMouseY = 200f; // bottom of pond in screen pixels
    public float maxMouseY = 800f; // top of pond in screen pixels
    public float maxCastDistance = 10f;

    public GameObject rod;              // Reference to the fishing rod

    void Update()
    {
        // Left click to spawn bobber
        if (Input.GetMouseButtonDown(0) && !bobberSpawned)  // 0 = left click
        {
            SpawnBobberAtMouse();
            bobberSpawned = true; 
        }

        // Right click to reset bobber spawn state
        if (Input.GetMouseButtonDown(1) && bobberSpawned)
        {
            bobberSpawned = false;
            // Destroy existing bobber if needed
            GameObject existingBobber = GameObject.FindWithTag("Bobber");
            if (existingBobber != null)
            {                
                Destroy(existingBobber);
            }
        }
    }

    void SpawnBobberAtMouse()
    {
        Vector3 mousePos = Input.mousePosition;

        // Set Z distance from camera to the plane where you want the bobber
        mousePos.z = Mathf.Abs(mainCamera.transform.position.y - pondY);

        // Convert to world space
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePos);

        // Keep the bobber at a fixed Y level (height of the water)
        worldPos.y = pondY;

        float verticalRatio = Mathf.InverseLerp(minMouseY, maxMouseY, mousePos.y);
        verticalRatio = Mathf.Clamp01(verticalRatio);

        // Scale cast distance based on vertical mouse position
        float castDistance = verticalRatio * maxCastDistance;

        // Direction from rod to mouse in XZ plane
        Vector3 rodPosXZ = new Vector3(rod.transform.position.x, 0, rod.transform.position.z);
        Vector3 mousePosXZ = new Vector3(worldPos.x, 0, worldPos.z);
        Vector3 direction = (mousePosXZ - rodPosXZ).normalized;

        // Spawn position = rod + direction * castDistance
        Vector3 spawnPos = rod.transform.position + direction * castDistance;

        // Instantiate the prefab at this position
        Instantiate(bobberPrefab, spawnPos, Quaternion.identity);
    }
}