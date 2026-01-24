using UnityEngine;

public class BobberSpawner : MonoBehaviour
{
    public Camera mainCamera;           
    public GameObject bobberPrefab;     
    public float pondY = 1f;            // Height where bobber should appear (Y axis)

    public bool bobberSpawned = false;

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

        // Instantiate the prefab at this position
        Instantiate(bobberPrefab, worldPos, Quaternion.identity);
    }
}