using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Cleanup + real-texture pass:
///  1. Reverts the OasisSolidity clutter (footings, curbs, rails, props) so the
///     city reads clean again.
///  2. Applies CC0 photorealistic textures (ambientCG) downloaded to
///     Assets/Textures/Web/ onto every generated Oasis material.
/// Idempotent.
/// </summary>
public static class OasisCleanup
{
    [MenuItem("Oasis/Clean Up Clutter")]
    public static void CleanClutter()
    {
        DestroyClutter();
        ApplyWebTextures();
        AssetDatabase.SaveAssets();
        EditorApplication.RepaintProjectWindow();
        Debug.Log("[Oasis] Cleanup + real textures applied.");
    }

    // ── 1. Remove solidity-pass clutter ─────────────────────────────────────

    static readonly string[] ClutterPrefixes =
    {
        "Footing", "PergolaCol_", "PergolaTop", "Planter_", "Hedge_", "Pool_",
        "FencePost_", "FenceRail_", "Hay_", "WaterTower", "Coil_", "PipeRack_",
        "Crane_Brace", "ForjaLamp", "WallW", "WallN", "GateFront", "Antenna",
        "AC_", "Chimney_", "Rail_", "Gatehouse_", "WallR_Glow",
    };

    static void DestroyClutter()
    {
        int n = 0;
        foreach (GameObject go in UnityEngine.Object.FindObjectsOfType<GameObject>(true))
        {
            bool match = Array.Exists(ClutterPrefixes, p => go.name.StartsWith(p));
            if (!match) continue;
            // Skip real zone walls (they belong to the zone layout, not clutter)
            if (go.name == "WallL" || go.name == "WallR" || go.name == "Wall_0") continue;
            UnityEngine.Object.DestroyImmediate(go);
            n++;
        }
        // Remove street lamps added by solidity pass (Spine/Bridge/Forja/*Lamp)
        foreach (GameObject go in UnityEngine.Object.FindObjectsOfType<GameObject>(true))
        {
            if (go.name.StartsWith("SpineLamp") || go.name.StartsWith("BridgeLamp"))
            {
                UnityEngine.Object.DestroyImmediate(go);
                n++;
            }
        }
        Debug.Log($"[Oasis] Removed {n} clutter objects.");
    }

    // ── 2. Apply downloaded textures to materials ───────────────────────────

    static void ApplyWebTextures()
    {
        ConfigureNormalImporters();
        // material path -> { Color, Normal(DX), Roughness, AO, Metal? }
        var map = new Dictionary<string, string[]>
        {
            ["Assets/Materials/Generated/Mat_Asphalt.mat"]    = new[] { "Asphalt031", "Asphalt031" },
            ["Assets/Materials/Generated/Mat_Brick.mat"]      = new[] { "Bricks102", "Bricks102" },
            ["Assets/Materials/Generated/Mat_Panel.mat"]      = new[] { "Concrete008", "Concrete008" },
            ["Assets/Materials/Generated/Mat_WhiteElite.mat"] = new[] { "Concrete008", "Concrete008" },
            ["Assets/Materials/Generated/Mat_Tiles.mat"]      = new[] { "PavingStones129", "PavingStones129" },
            ["Assets/Materials/Generated/Mat_Field.mat"]      = new[] { "Grass006", "Grass006" },
            ["Assets/Materials/Generated/Mat_Bridge.mat"]     = new[] { "Concrete008", "Concrete008" },
            ["Assets/Materials/Generated/Mat_Spire.mat"]      = new[] { "MetalPlates006", "MetalPlates006" },
            ["Assets/Materials/OasisMetal.mat"]               = new[] { "MetalPlates006", "MetalPlates006" },
            ["Assets/Materials/OasisRoad.mat"]                = new[] { "Asphalt031", "Asphalt031" },
            ["Assets/Materials/OasisPlaza.mat"]               = new[] { "PavingStones129", "PavingStones129" },
        };

        // Mat_TF_00..13 (procedural facade materials) also get concrete/paving
        for (int i = 0; i < 14; i++)
        {
            map[$"Assets/Materials/Generated/Mat_TF_{i:D2}.mat"] = new[] { "Concrete008", "Concrete008" };
        }

        // Glass: real texture too, made more opaque so it reads
        map["Assets/Materials/OasisGlass.mat"] = new[] { "MetalPlates006", "MetalPlates006" };

        // Oasis materials: bright base color so the albedo texture is visible
        Material oMetal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/OasisMetal.mat");
        if (oMetal != null) oMetal.SetColor("_BaseColor", new Color(0.82f, 0.85f, 0.88f, 1f));
        Material oRoad = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/OasisRoad.mat");
        if (oRoad != null) oRoad.SetColor("_BaseColor", new Color(1f, 1f, 1f, 1f));
        Material oPlaza = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/OasisPlaza.mat");
        if (oPlaza != null) oPlaza.SetColor("_BaseColor", new Color(0.95f, 0.97f, 1f, 1f));
        Material oGlass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/OasisGlass.mat");
        if (oGlass != null) oGlass.SetColor("_BaseColor", new Color(0.70f, 0.84f, 0.90f, 0.88f));

        foreach (var kv in map)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(kv.Key);
            if (m == null)
            {
                Debug.LogWarning($"[Oasis] missing material {kv.Key}");
                continue;
            }
            string id = kv.Value[0];
            string basePath = $"Assets/Textures/Web/{id}/{id}_1K-JPG";
            SetTex(m, "_BaseMap", $"{basePath}_Color.jpg");
            SetTex(m, "_BumpMap", $"{basePath}_NormalDX.jpg");
            m.SetTextureScale("_BaseMap", new Vector2(4f, 4f));
            m.SetTextureScale("_BumpMap", new Vector2(4f, 4f));
            m.SetFloat("_Smoothness", 0.35f);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
        }

        // Metal: metallic + rough + bump (base color already brightened above)
        Material metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/OasisMetal.mat");
        if (metal != null)
        {
            SetTex(metal, "_MetallicGlossMap", "Assets/Textures/Web/MetalPlates006/MetalPlates006_1K-JPG_Metalness.jpg");
            metal.SetFloat("_Metallic", 0.5f);
            metal.SetFloat("_Smoothness", 0.6f);
            metal.SetTextureScale("_MetallicGlossMap", new Vector2(4f, 4f));
        }

        // Towers: dim the emissive masks so the concrete texture reads
        for (int i = 0; i < 14; i++)
        {
            Material tf = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/Generated/Mat_TF_{i:D2}.mat");
            if (tf == null) continue;
            Color em = tf.GetColor("_EmissionColor");
            tf.SetColor("_EmissionColor", em * 0.16f);
            tf.SetFloat("_Smoothness", 0.5f);
            EditorUtility.SetDirty(tf);
        }
    }

    static void ConfigureNormalImporters()
    {
        foreach (string dir in System.IO.Directory.GetDirectories("Assets/Textures/Web"))
        {
            foreach (string file in System.IO.Directory.GetFiles(dir, "*NormalDX*.jpg"))
            {
                string rel = file.Replace('\\', '/');
                TextureImporter imp = AssetImporter.GetAtPath(rel) as TextureImporter;
                if (imp == null) continue;
                if (imp.textureType != TextureImporterType.NormalMap)
                {
                    imp.textureType = TextureImporterType.NormalMap;
                    imp.sRGBTexture = false;
                    imp.SaveAndReimport();
                }
            }
        }
    }

    static void SetTex(Material m, string prop, string path)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null)
        {
            AssetDatabase.ImportAsset(path);
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        if (tex != null)
        {
            tex.wrapMode = TextureWrapMode.Repeat;
            m.SetTexture(prop, tex);
            m.EnableKeyword("_NORMALMAP");
            if (prop == "_BaseMap") m.mainTexture = tex;
        }
        else
        {
            Debug.LogWarning($"[Oasis] texture not found: {path}");
        }
    }
}