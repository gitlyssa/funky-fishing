using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ArcGenerator))]
public class ArcGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        ArcGenerator gen = (ArcGenerator)target;

        if (GUILayout.Button("Bake and Save Mesh Asset"))
        {
            Mesh mesh = gen.GenerateArc();
            
            // This saves the mesh into your project folder so it's not "temporary"
            string path = "Assets/ProceduralArcMesh.asset";
            AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssets();
            
            // Assign it to the current object so you can see it
            gen.GetComponent<MeshFilter>().mesh = mesh;
            
            Debug.Log("Mesh saved to: " + path);
        }
    }
}