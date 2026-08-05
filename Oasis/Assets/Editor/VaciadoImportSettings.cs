using UnityEngine;
using UnityEditor;
using System.IO;

public static class VaciadoImportSettings
{
    private const string VaciadoPath         = "Assets/Animation/Animation_Vaciado";
    private const string MaterialsSubfolder  = "Materials";

    // ── Loop detection: filename contains any of these (case-insensitive) → loop ──
    private static readonly string[] LoopKeywords = { "Idle", "Walk", "Run" };

    [MenuItem("Oasis/Setup Vaciado Import Settings")]
    public static void Run()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { VaciadoPath });

        if (guids.Length == 0)
        {
            Debug.LogWarning($"[VaciadoImport] No FBX files found in {VaciadoPath}");
            EditorUtility.DisplayDialog("Oasis - Vaciado Import",
                $"No FBX files found in:\n{VaciadoPath}", "OK");
            return;
        }

        int processed = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            bool isMainModel = HasSkinnedMeshRenderer(path);
            ImportModel(importer, path, isMainModel);
            processed++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[VaciadoImport] Processed {processed} FBX files in {VaciadoPath}.");
        EditorUtility.DisplayDialog("Oasis - Vaciado Import",
            $"Import settings applied.\n{processed} FBX files processed.", "OK");
    }

    // ── Main model detection ──────────────────────────────────────────────────

    private static bool HasSkinnedMeshRenderer(string fbxPath)
    {
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        foreach (var asset in allAssets)
        {
            if (asset is SkinnedMeshRenderer)
                return true;
        }
        return false;
    }

    // ── Per-file import configuration ─────────────────────────────────────────

    private static void ImportModel(ModelImporter importer, string path, bool isMainModel)
    {
        // Humanoid → applies to all Vaciado FBXs
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;

        // ── Material extraction (main model only) ──
        if (isMainModel)
        {
            string materialsPath = Path.Combine(VaciadoPath, MaterialsSubfolder)
                                       .Replace("\\", "/");

            if (!AssetDatabase.IsValidFolder(materialsPath))
            {
                // Parent must exist (VaciadoPath is verified by FindAssets)
                AssetDatabase.CreateFolder(VaciadoPath, MaterialsSubfolder);
                Debug.Log($"[VaciadoImport] Created materials folder: {materialsPath}");
            }

            importer.materialLocation = ModelImporterMaterialLocation.External;
            importer.materialName     = ModelImporterMaterialName.BasedOnMaterialName;
        }

        // ── Animation clip loop configuration ──
        string filename = Path.GetFileNameWithoutExtension(path);
        bool isLoop = ShouldLoop(filename);

        var clips = importer.clipAnimations.Length > 0
            ? importer.clipAnimations
            : importer.defaultClipAnimations;

        for (int i = 0; i < clips.Length; i++)
        {
            clips[i].loopTime = isLoop;
            clips[i].loopPose = isLoop;
        }

        if (clips.Length > 0)
            importer.clipAnimations = clips;

        importer.SaveAndReimport();
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        Debug.Log($"[VaciadoImport] {filename} — Humanoid, Loop={isLoop}" +
                  (isMainModel ? ", Materials external" : ""));
    }

    // ── Loop detection by substring ───────────────────────────────────────────

    private static bool ShouldLoop(string filename)
    {
        string lower = filename.ToLowerInvariant();
        foreach (string keyword in LoopKeywords)
        {
            if (lower.Contains(keyword.ToLowerInvariant()))
                return true;
        }
        return false;
    }
}
