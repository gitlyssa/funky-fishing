using UnityEngine;

public class RodMoveScript : MonoBehaviour
{
    public Camera mainCamera;
    public BobberSpawner bobberSpawner;

    void Update()
    {
        // Get mouse position in screen space
        Vector3 mousePos = Input.mousePosition;

        // Use the rod's current world Y and Z
        float yPos = transform.position.y;
        float zPos = transform.position.z;

        // Set z distance from camera to rod plane (distance along camera forward axis)
        mousePos.z = Mathf.Abs(mainCamera.transform.position.y - yPos);

        // Convert to world space
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePos);

        // Only move side-to-side (x axis), keep Y and Z the same as your current position
        if (bobberSpawner == null || !bobberSpawner.bobberSpawned)
        {
            transform.position = new Vector3(worldPos.x, yPos, zPos);
            return;
        }
    }
}