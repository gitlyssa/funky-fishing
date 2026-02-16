using UnityEngine;
using UnityEditor;
using System.IO;

public static class SolidTerrainTextureGenerator
{
    // Change these whenever you want, then re-run the menu command.
    // GrassGreen -> RGB(100, 140, 79), Hex #648C4F, A=255
    static readonly Color32 GrassGreen = new Color32(100, 140, 79, 255); 
    // DirtBrown: (0.45f, 0.32f, 0.22f, 1f) -> RGB(115, 82, 56), Hex #735238, A=255
    static readonly Color32 DirtBrown = new Color32(115, 82, 56, 255);
    // RockGray: (0.45f, 0.45f, 0.45f, 1f) -> RGB(115, 115, 115), Hex #737373, A=255
    static readonly Color32 RockGray  = new Color32(115, 115, 115, 255);


    const int Size = 8; // 4, 8, or 16. Tiny is fine.

    [MenuItem("Tools/Terrain/Generate Solid Terrain Textures")]
    public static void Generate()
    {
        string folder = "Assets/TerrainTextures";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "TerrainTextures");

        WriteSolidPng(Path.Combine(folder, "GrassGreen.png"), GrassGreen);
        WriteSolidPng(Path.Combine(folder, "DirtBrown.png"), DirtBrown);
        WriteSolidPng(Path.Combine(folder, "RockGray.png"), RockGray);

        AssetDatabase.Refresh();

        // Optional: auto-apply good import settings
        ApplyImportSettings(folder + "/GrassGreen.png");
        ApplyImportSettings(folder + "/DirtBrown.png");
        ApplyImportSettings(folder + "/RockGray.png");

        Debug.Log("Generated solid terrain textures in " + folder);
    }

    static void WriteSolidPng(string assetPath, Color c)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        var pixels = new Color[Size * Size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = c;
        tex.SetPixels(pixels);
        tex.Apply();

        byte[] png = tex.EncodeToPNG();
        File.WriteAllBytes(assetPath, png);

        Object.DestroyImmediate(tex);
    }

    static void ApplyImportSettings(string assetPath)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        if (importer == null) return;

        importer.textureType = TextureImporterType.Default;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear; // use Point for harsher pixel look
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.sRGBTexture = true;

        importer.SaveAndReimport();
    }
}
