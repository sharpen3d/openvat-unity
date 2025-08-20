using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Diagnostics;
using System.Linq;

public class OpenVATEditor : EditorWindow
{
    private string folderPath = "Assets/OpenVATContent";

    [MenuItem("Tools/OpenVAT Editor")]
    public static void ShowWindow()
    {
        GetWindow<OpenVATEditor>("OpenVAT Editor");
    }

    private void OnGUI()
    {
        GUILayout.Label("OpenVAT Content Importer", EditorStyles.boldLabel);

        folderPath = EditorGUILayout.TextField("Folder Path", folderPath);

        if (GUILayout.Button("Process OpenVAT Content"))
        {
            ProcessOpenVATContent();
        }
    }

    private void ProcessOpenVATContent()
    {
        if (!Directory.Exists(folderPath))
        {
            UnityEngine.Debug.LogError("Folder does not exist: " + folderPath);
            return;
        }

        // Support FBX, GLB, and GLTF
        string[] fbxFiles = Directory.GetFiles(folderPath, "*.fbx");
        string[] glbFiles = Directory.GetFiles(folderPath, "*.glb");
        string[] gltfFiles = Directory.GetFiles(folderPath, "*.gltf");
        string[] modelFiles = fbxFiles.Concat(glbFiles).Concat(gltfFiles).ToArray();

        // Support PNG and EXR textures
        string[] pngTextures = Directory.GetFiles(folderPath, "*_vat.png");
        string[] exrTextures = Directory.GetFiles(folderPath, "*_vat.exr");
        string[] vatTextures = pngTextures.Concat(exrTextures).ToArray();

        string[] jsonFiles = Directory.GetFiles(folderPath, "*.json");

        if (modelFiles.Length == 0 || vatTextures.Length == 0 || jsonFiles.Length == 0)
        {
            UnityEngine.Debug.LogError("Required files are missing in the folder.");
            return;
        }

        string modelPath = modelFiles[0];
        string vatTexturePath = vatTextures[0];
        string jsonPath = jsonFiles[0];

        // Re-import VAT and optional textures with correct settings
        ImportTexture(vatTexturePath, false);
        string vnrmTexturePath = Directory.GetFiles(folderPath, "*_vnrm.png").Length > 0 ? Directory.GetFiles(folderPath, "*_vnrm.png")[0] : null;
        if (vnrmTexturePath != null)
        {
            ImportTexture(vnrmTexturePath, false);
        }

        // Re-import PBR textures with correct settings
        string basecolorPath = GetPBRTexturePath("_basecolor", "_albedo");
        string normalPath = GetPBRTexturePath("_nrml", "_normal");
        string roughnessPath = GetPBRTexturePath("_roug", "_roughness");
        string metallicPath = GetPBRTexturePath("_metl", "_metallic");
        string aoPath = GetPBRTexturePath("_ao", "_ambient", "_occlusion");
        string emissionPath = GetPBRTexturePath("_emis", "_emission");

        if (normalPath != null)
        {
            ImportTexture(normalPath, true);
        }

        // Create material
        string materialPath = Path.Combine(folderPath, Path.GetFileNameWithoutExtension(modelPath) + "_mat.mat");
        Material material = new Material(Shader.Find(basecolorPath != null ? "Shader Graphs/openVAT_decoder" : "Shader Graphs/openVAT_decoder_basic"));

        material.SetTexture("_openVAT_main", AssetDatabase.LoadAssetAtPath<Texture2D>(vatTexturePath));
        if (vnrmTexturePath != null)
        {
            material.SetTexture("_openVAT_nrml", AssetDatabase.LoadAssetAtPath<Texture2D>(vnrmTexturePath));
        }

        SetJSONParameters(material, jsonPath, vatTexturePath);

        if (basecolorPath != null)
        {
            material.SetTexture("_Basecolor", AssetDatabase.LoadAssetAtPath<Texture2D>(basecolorPath));
        }
        if (normalPath != null)
        {
            material.SetTexture("_Normal", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
        }
        if (roughnessPath != null)
        {
            material.SetTexture("_Roughness", AssetDatabase.LoadAssetAtPath<Texture2D>(roughnessPath));
        }
        if (metallicPath != null)
        {
            material.SetTexture("_Metallic", AssetDatabase.LoadAssetAtPath<Texture2D>(metallicPath));
        }
        if (emissionPath != null)
        {
            material.SetTexture("_Emission", AssetDatabase.LoadAssetAtPath<Texture2D>(emissionPath));
        }
        if (aoPath != null)
        {
            material.SetTexture("_AmbientOcclusion", AssetDatabase.LoadAssetAtPath<Texture2D>(aoPath));
        }

        AssetDatabase.CreateAsset(material, materialPath);
        AssetDatabase.SaveAssets();

        // Add FBX to scene and apply material
        GameObject fbxObject = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));

        // Ensure the object is at the origin
        fbxObject.transform.position = Vector3.zero;
        //fbxObject.transform.rotation = Quaternion.identity;

        fbxObject.GetComponent<Renderer>().sharedMaterial = material;

        // Save as prefab
        string prefabPath = Path.Combine(folderPath, Path.GetFileNameWithoutExtension(modelPath) + ".prefab");
        PrefabUtility.SaveAsPrefabAsset(fbxObject, prefabPath);
        DestroyImmediate(fbxObject);

        EditorSceneManager.MarkAllScenesDirty();
    }

    private void ImportTexture(string path, bool isNormalMap)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = isNormalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;

        // Set max texture size
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex != null)
            importer.maxTextureSize = Mathf.Max(tex.width, tex.height);

        // Set format based on source (8/16/32 bit)
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
        {
            overridden = true,
            maxTextureSize = importer.maxTextureSize,
            // format = GetTextureFormat(tex)
        });

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    private string GetPBRTexturePath(params string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            string[] paths = Directory.GetFiles(folderPath, "*" + keyword + ".png");
            if (paths.Length > 0)
            {
                return paths[0];
            }
        }
        return null;
    }

    private void SetJSONParameters(Material material, string jsonPath, string vatTexturePath)
    {
        string jsonString = File.ReadAllText(jsonPath);
        jsonString = jsonString.Replace("os-remap", "os_remap");  // Replace hyphen with underscore

        RemapData remapData = JsonUtility.FromJson<RemapData>(jsonString);

        Vector3 minValues = new Vector3(remapData.os_remap.Max[0], remapData.os_remap.Min[1], remapData.os_remap.Min[2]);
        Vector3 maxValues = new Vector3(remapData.os_remap.Min[0], remapData.os_remap.Max[1], remapData.os_remap.Max[2]);
        float frameCount = remapData.os_remap.Frames;

        material.SetVector("_minValues", minValues);
        material.SetVector("_maxValues", maxValues);
        material.SetFloat("_frames", frameCount);
        material.SetFloat("_resolutionY", AssetDatabase.LoadAssetAtPath<Texture2D>(vatTexturePath).height);
    }

    [System.Serializable]
    private class RemapData
    {
        public OsRemap os_remap;
    }

    [System.Serializable]
    private class OsRemap
    {
        public float[] Min;
        public float[] Max;
        public float Frames;
    }
}
