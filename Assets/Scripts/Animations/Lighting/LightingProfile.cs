using UnityEngine;
using UnityEngine.Rendering; 

[CreateAssetMenu(fileName = "NewLightingProfile", menuName = "Visuals/Lighting Profile")]
public class LightingProfile : ScriptableObject
{
    [Header("Ambient Light")]
    public bool ambientEnabled = true;
    public Color ambientColor = Color.gray;
    public float ambientIntensity = 1.0f;

    [Header("Fog Settings")]
    public bool fogEnabled = true;
    public Color fogColor = Color.black;
    public float fogDensity = 0.01f;

    [Header("Skybox Settings")]
    public Material skyboxMaterial;
    public float skyboxExposure = 1.0f;
    public Color skyboxTint = Color.white;

    [Header("Directional Light")]
    public Color directionalLightColor = Color.white;
    public float directionalLightIntensity = 1.0f;
    public Vector3 directionalLightDirection = new Vector3(50f, -240f, 0f);
    public float directionalLightShadowStrength = 1.0f;

    [Header("Local Environment Lights")]
    public Color localLightColor = Color.yellow;
    public float localLightIntensity = 1.5f;
    public float localLightRange = 10.0f;
    [Range(0, 1)]
    public float localLightShadowStrength = 0.5f;

    [Header("Firefly Behavior")]
    public float fireflyBPM = 60f;
    public float fireflyMoveSpeed = 1.5f;
    public float fireflyMoveRange = 0.2f;

    [Header("Post-Processing")]
    public VolumeProfile volumeProfile;
}