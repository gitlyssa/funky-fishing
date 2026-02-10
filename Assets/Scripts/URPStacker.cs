using UnityEngine;
using UnityEngine.Rendering.Universal;

public class URPStacker : MonoBehaviour
{
    void Start()
    {
        Camera baseCam = Camera.main; // Make sure your Fishing Cam is tagged 'MainCamera'
        
        if (baseCam == null) return;

        // 2. Get the URP-specific data for the base camera
        var cameraData = baseCam.GetUniversalAdditionalCameraData();

        // 3. Get this camera
        Camera overlayCam = GetComponent<Camera>();

        // 4. Add this camera to the 'Stack' so it renders on top
        if (!cameraData.cameraStack.Contains(overlayCam))
        {
            cameraData.cameraStack.Add(overlayCam);
        }
    }

    private void OnDestroy()
    {
        Camera baseCam = Camera.main;
        if (baseCam != null)
        {
            var cameraData = baseCam.GetUniversalAdditionalCameraData();
            Camera overlayCam = GetComponent<Camera>();
            
            // Remove this camera from the stack so the Fishing Cam stops looking for it
            if (cameraData.cameraStack.Contains(overlayCam))
            {
                cameraData.cameraStack.Remove(overlayCam);
            }
        }
    }
}
