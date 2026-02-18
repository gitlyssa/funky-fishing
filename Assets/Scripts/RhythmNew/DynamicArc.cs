using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class DynamicArc : MonoBehaviour
{
    /*
    Just a version of the arc generator that updates the mesh every frame
    
     */
    private Mesh _mesh;
    private Vector3[] _vertices;
    private int[] _triangles;
    private Vector2[] _uvs;

    public void Setup(int segments)
    {
        _mesh = new Mesh();
        _mesh.MarkDynamic(); // Optimization for frequent updates
        GetComponent<MeshFilter>().mesh = _mesh;

        _vertices = new Vector3[(segments + 1) * 2];
        _uvs = new Vector2[_vertices.Length];
        _triangles = new int[segments * 6];
    }

    public void SetMaterial(Material mat)
    {
        // sets the material of the mesh
        GetComponent<MeshRenderer>().material = mat;
    }

    public void Redraw(float centerRadius, float thickness, float arcAngle, int segments)
    {
        float inner = centerRadius - (thickness / 2f);
        float outer = centerRadius + (thickness / 2f);
        
        float angleStep = arcAngle / segments;
        float startAngle = -arcAngle / 2f;

        for (int i = 0; i <= segments; i++)
        {
            float currAngle = (startAngle + (i * angleStep)) * Mathf.Deg2Rad;
            float x = Mathf.Sin(currAngle);
            float y = Mathf.Cos(currAngle);

            _vertices[i * 2] = new Vector3(x * inner, y * inner, 0);
            _vertices[i * 2 + 1] = new Vector3(x * outer, y * outer, 0);
            
            _uvs[i * 2] = new Vector2((float)i / segments, 0);
            _uvs[i * 2 + 1] = new Vector2((float)i / segments, 1);

            if (i < segments)
            {
                int b = i * 2;
                int t = i * 6;
                _triangles[t] = b; _triangles[t+1] = b+1; _triangles[t+2] = b+2;
                _triangles[t+3] = b+1; _triangles[t+4] = b+3; _triangles[t+5] = b+2;
            }
        }

        _mesh.vertices = _vertices;
        _mesh.triangles = _triangles;
        _mesh.uv = _uvs;
        _mesh.RecalculateBounds(); 
        _mesh.RecalculateNormals();
    }
}