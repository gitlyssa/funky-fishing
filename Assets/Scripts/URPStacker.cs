using UnityEngine;
using UnityEngine.Rendering.Universal;

public class URPStacker : MonoBehaviour
{
    private Camera _overlayCam;
    private Camera _stackedBaseCam;

    private void Awake()
    {
        _overlayCam = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        EnsureStacked();
    }

    private void LateUpdate()
    {
        EnsureStacked();
    }

    private void OnDisable()
    {
        RemoveFromCurrentBase();
    }

    private void OnDestroy()
    {
        RemoveFromCurrentBase();
    }

    private void EnsureStacked()
    {
        if (_overlayCam == null)
            return;

        Camera baseCam = FindBaseCamera();
        if (baseCam == null || baseCam == _overlayCam)
            return;

        UniversalAdditionalCameraData baseData = baseCam.GetUniversalAdditionalCameraData();

        if (_stackedBaseCam != null && _stackedBaseCam != baseCam)
            RemoveFromCurrentBase();

        if (!baseData.cameraStack.Contains(_overlayCam))
            baseData.cameraStack.Add(_overlayCam);

        _stackedBaseCam = baseCam;
    }

    private Camera FindBaseCamera()
    {
        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera cam = cameras[i];
            if (cam == null)
                continue;

            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            if (data.renderType == CameraRenderType.Base)
                return cam;
        }

        return Camera.main;
    }

    private void RemoveFromCurrentBase()
    {
        if (_stackedBaseCam == null || _overlayCam == null)
            return;

        UniversalAdditionalCameraData data = _stackedBaseCam.GetUniversalAdditionalCameraData();
        if (data.cameraStack.Contains(_overlayCam))
            data.cameraStack.Remove(_overlayCam);

        _stackedBaseCam = null;
    }
}
