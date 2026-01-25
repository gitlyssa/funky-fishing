using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RhythmNote : MonoBehaviour
{
    [Header("Note Properties")]
    public float targetHitTime; 
    public float noteDuration;  
    public float travelTime;    
    public float startAngle;
    public float endAngle;

    public bool isFlickNote;

    private Renderer _renderer;


    private float ringRadius = 5f;
    private float spawnZ = 30f;
    // private float scoringZ = 0f;

    public void Initialize(float hitTime, float duration, float travelT, float sAng, float eAng, Material holdMat, Material flickMat)
    {
        targetHitTime = hitTime;
        noteDuration = duration;
        travelTime = travelT;
        startAngle = sAng;
        endAngle = eAng;
        isFlickNote = duration <= 0.01f;

        _renderer = GetComponent<Renderer>();
        _renderer.material = isFlickNote ? flickMat : holdMat;

        GenerateRibbonMesh();
    }

    void Update()
    {
        float currentTime = Time.time; 

        //progress of head
        float spawnTime = targetHitTime - travelTime;
        float tHead = (currentTime - spawnTime) / travelTime;

        //progress of tail
        float tailHitTime = targetHitTime + noteDuration;
        float tailSpawnTime = tailHitTime - travelTime;
        float tTail = (currentTime - tailSpawnTime) / travelTime;

        //if tail passes camera location and some buffer, destroy
        if (tTail > 1.3f) 
        {   
            // find parent list
            Destroy(gameObject);
            return;
        }

        //calculate current Z position based on head progress
        float curZ = spawnZ - (tHead * spawnZ);
        
        transform.position = new Vector3(0, 0, curZ);
    }

    void GenerateRibbonMesh()
    {
        Mesh mesh = new Mesh();
        int segments = isFlick ? 1 : Mathf.Max(1, Mathf.CeilToInt(noteDuration * 10));

        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        int[] triangles = new int[segments * 6];

        //physical length of notes, flick notes have fixed length
        float noteLengthZ = (noteDuration / travelTime) * spawnZ;
        float visualDepth = isFlick ? 0.8f : noteLengthZ;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments; 
            float angleRad = Mathf.Lerp(startAngle, endAngle, t) * Mathf.Deg2Rad;

            //tail is behind head in Z
            Vector3 centerPos = new Vector3(Mathf.Cos(angleRad) * ringRadius, Mathf.Sin(angleRad) * ringRadius, t * visualDepth);
            
            Vector3 dirFromCenter = new Vector3(centerPos.x, centerPos.y, 0).normalized;
            Vector3 sideVector = Vector3.Cross(dirFromCenter, Vector3.forward);

            float width = 1.2f; 
            vertices[i * 2] = centerPos + (sideVector * (width * 0.5f));
            vertices[i * 2 + 1] = centerPos - (sideVector * (width * 0.5f));
        }

        for (int i = 0; i < segments; i++)
        {
            int baseV = i * 2;
            //winding order for two triangles to form square
            triangles[i * 6] = baseV;
            triangles[i * 6 + 1] = baseV + 2; 
            triangles[i * 6 + 2] = baseV + 1;
            
            triangles[i * 6 + 3] = baseV + 1;
            triangles[i * 6 + 4] = baseV + 2;
            triangles[i * 6 + 5] = baseV + 3;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds(); 
        GetComponent<MeshFilter>().mesh = mesh;
    }

    private bool isFlick => noteDuration <= 0.01f;
}