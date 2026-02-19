using UnityEngine;
using UnityEngine.Rendering.Universal;

public class URPStacker : MonoBehaviour
{
    void Start()
    {
        Camera baseCam = Camera.main; 
        
        if (baseCam == null) return;

        //get base camera
        var cameraData = baseCam.GetUniversalAdditionalCameraData();

        //this camera
        Camera overlayCam = GetComponent<Camera>();

        // add to stack
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
            
            // Remove this camera from the stack
            if (cameraData.cameraStack.Contains(overlayCam))
            {
                cameraData.cameraStack.Remove(overlayCam);
            }
        }
    }
}
