using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ArcGenerator : MonoBehaviour
{

    /*
    This script generates a mesh for an arc, you can specify the inner and outer radius and how many degrees of a circle it is
    I didn't end up using this script because i wanted to deform the arc as it moved along, but the dynamic arc script is pretty much copy paste
    in a loop.

    We should be able to attach a texture or anything onto the arc mesh generated.

    I don't think this is very useful anymore, but you can generate and save a mesh with this if you want a static one
     */
    public float innerRadius = 4f;
    public float outerRadius = 5f;
    public float arcAngle = 90f;
    public int segments = 24;

    public Mesh GenerateArc()
    {
        Mesh mesh = new Mesh();
        mesh.name = "ProceduralArc";

        int vertexCount = (segments + 1) * 2;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount]; // Required for textures
        int[] triangles = new int[segments * 6];

        float angleStep = arcAngle / segments;
        float startAngle = -arcAngle / 2f;

        for (int i = 0; i <= segments; i++)
        {
            float currentAngle = (startAngle + (i * angleStep)) * Mathf.Deg2Rad;
            float x = Mathf.Sin(currentAngle);
            float y = Mathf.Cos(currentAngle);

            // Vertices
            vertices[i * 2] = new Vector3(x * innerRadius, y * innerRadius, 0);
            vertices[i * 2 + 1] = new Vector3(x * outerRadius, y * outerRadius, 0);

            // UVs 
            float u = (float)i / segments;
            uvs[i * 2] = new Vector2(u, 0);
            uvs[i * 2 + 1] = new Vector2(u, 1);

            if (i < segments)
            {
                int baseIdx = i * 2;
                int triIdx = i * 6;
                triangles[triIdx] = baseIdx;
                triangles[triIdx + 1] = baseIdx + 1;
                triangles[triIdx + 2] = baseIdx + 2;
                triangles[triIdx + 3] = baseIdx + 1;
                triangles[triIdx + 4] = baseIdx + 3;
                triangles[triIdx + 5] = baseIdx + 2;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}