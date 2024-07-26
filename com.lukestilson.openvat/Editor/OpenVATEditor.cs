using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Diagnostics;

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

        string[] fbxFiles = Directory.GetFiles(folderPath, "*.fbx");
        string[] vatTextures = Directory.GetFiles(folderPath, "*_vat.png");
        string[] jsonFiles = Directory.GetFiles(folderPath, "*.json");

        if (fbxFiles.Length == 0 || vatTextures.Length == 0 || jsonFiles.Length == 0)
        {
            UnityEngine.Debug.LogError("Required files are missing in the folder.");
            return;
        }

        string fbxPath = fbxFiles[0];
        string vatTexturePath = vatTextures[0];
        string jsonPath = jsonFiles[0];

        // Re-import VAT and optional textures with correct settings
        ImportTexture(vatTexturePath, false, false);
        string vnrmTexturePath = Directory.GetFiles(folderPath, "*_vnrm.png").Length > 0 ? Directory.GetFiles(folderPath, "*_vnrm.png")[0] : null;
        if (vnrmTexturePath != null)
        {
            ImportTexture(vnrmTexturePath, false, false);
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
            ImportTexture(normalPath, true, false);
        }

        // Create material
        string materialPath = Path.Combine(folderPath, Path.GetFileNameWithoutExtension(fbxPath) + "_mat.mat");
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
        GameObject fbxObject = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath));

        // Ensure the object is at the origin
        fbxObject.transform.position = Vector3.zero;
        //fbxObject.transform.rotation = Quaternion.identity;

        fbxObject.GetComponent<Renderer>().sharedMaterial = material;

        // Save as prefab
        string prefabPath = Path.Combine(folderPath, Path.GetFileNameWithoutExtension(fbxPath) + ".prefab");
        PrefabUtility.SaveAsPrefabAsset(fbxObject, prefabPath);
        DestroyImmediate(fbxObject);

        EditorSceneManager.MarkAllScenesDirty();
    }

    private void ImportTexture(string path, bool isNormalMap, bool sRGB)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.sRGBTexture = sRGB;
        if (isNormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
        }
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
