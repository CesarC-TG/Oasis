using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Final visual pass. Idempotent.
///  1. New CC0 materials: Mat_Wood (stalls/decks/boats/mills) + Mat_Roof (tiles) and
///     reassigns scene objects by name so the city stops reading as a gray maqueta.
///  2. Warm tints on all construction materials so each district reads warm stone.
///  3. Sunset lighting: single warm Sun, URP fog, HDRI skybox + ambient.
///  4. Post FX on the scene's global Volume (ACES, bloom, vignette).
/// Run at the END of the generation sequence, after Oasis/Clean Up Clutter.
/// </summary>
public static class OasisLighting
{
    private const string WebDir = "Assets/Textures/Web";
    private const string GenMats = "Assets/Materials/Generated";

    [MenuItem("Oasis/Lighting/Final Touch")]
    public static void FinalTouch()
    {
        ConfigureImporters();
        Material wood = TexturedMat("Mat_Wood", "WoodFloor001", "WoodFloor001_1K-JPG", 1.2f);
        Material roof = TexturedMat("Mat_Roof", "RoofingTiles013A", "RoofingTiles013A_1K-JPG", 1.6f);

        ApplyByMaterial(wood, roof);
        TintMaterials();
        SetupSun();
        SetupSkyAndFog();
        SetupPostFX();

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        SceneView.RepaintAll();
        Debug.Log("[Oasis] Final Touch: materiales, tintes, sol, niebla, skybox y post-FX aplicados.");
    }

    // ── Import settings (idempotent) ────────────────────────────────────────

    static void ConfigureImporters()
    {
        foreach (string dir in Directory.GetDirectories(WebDir))
        {
            // Normal maps (DX)
            foreach (string f in Directory.GetFiles(dir, "*NormalDX*.jpg"))
            {
                string rel = f.Replace('\\', '/');
                TextureImporter imp = AssetImporter.GetAtPath(rel) as TextureImporter;
                if (imp == null || imp.textureType == TextureImporterType.NormalMap) continue;
                imp.textureType = TextureImporterType.NormalMap;
                imp.sRGBTexture = false;
                imp.SaveAndReimport();
            }
            // HDR skybox (equirectangular .exr)
            foreach (string f in Directory.GetFiles(dir, "*_HDR.exr"))
            {
                string rel = f.Replace('\\', '/');
                TextureImporter imp = AssetImporter.GetAtPath(rel) as TextureImporter;
                if (imp == null) continue;
                imp.textureType = TextureImporterType.Default;
                imp.sRGBTexture = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.maxTextureSize = 2048;
                imp.SaveAndReimport();
            }
        }
    }

    // ── New CC0 materials ──────────────────────────────────────────────────

    static Material TexturedMat(string name, string folder, string file, float tiling)
    {
        string path = $"{GenMats}/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        string basePath = $"{WebDir}/{folder}/{file}";
        SetTex(m, "_BaseMap", $"{basePath}_Color.jpg");
        SetTex(m, "_BumpMap", $"{basePath}_NormalDX.jpg");
        m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
        m.SetTextureScale("_BumpMap", new Vector2(tiling, tiling));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", 0.4f);
        m.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(m);
        return m;
    }

    static void SetTex(Material m, string prop, string path)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null)
        {
            AssetDatabase.ImportAsset(path);
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        if (tex == null) return;
        tex.wrapMode = TextureWrapMode.Repeat;
        m.SetTexture(prop, tex);
        if (prop == "_BaseMap") m.mainTexture = tex;
    }

    // ── Reassign by object name ────────────────────────────────────────────

    static void ApplyByMaterial(Material wood, Material roof)
    {
        int nW = 0, nR = 0;
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string n = r.gameObject.name;

            bool isRoof = n.EndsWith("_Roof");
            bool isWood = n.StartsWith("CauceDeck_") || n == "CauceDeck" ||
                          n.StartsWith("Dock_Deck") || n.StartsWith("Dock_Pole") ||
                          n.StartsWith("Boat_") ||
                          n.StartsWith("Mill_Hut") || n.StartsWith("Mill_Wheel") ||
                          n.StartsWith("Mill_Axle") || n.StartsWith("Mill_Paddle");

            if (isRoof) { r.sharedMaterial = roof; nR++; }
            else if (isWood) { r.sharedMaterial = wood; nW++; }
        }
        Debug.Log($"[Oasis] Madera: {nW} objetos · Teja: {nR} objetos.");
    }

    // ── Warm tints per construction material ───────────────────────────────

    static void TintMaterials()
    {
        Tint("Assets/Materials/Generated/Mat_Panel.mat", new Color(0.96f, 0.93f, 0.88f));
        Tint("Assets/Materials/Generated/Mat_WhiteElite.mat", new Color(0.99f, 0.97f, 0.91f));
        Tint("Assets/Materials/Generated/Mat_Brick.mat", new Color(0.98f, 0.90f, 0.82f));
        Tint("Assets/Materials/Generated/Mat_Tiles.mat", new Color(0.97f, 0.95f, 0.90f));
        Tint("Assets/Materials/Generated/Mat_Field.mat", new Color(0.93f, 1.00f, 0.86f));
        Tint("Assets/Materials/Generated/Mat_Asphalt.mat", new Color(1f, 1f, 1f));
        Tint("Assets/Materials/OasisMetal.mat", new Color(0.85f, 0.88f, 0.90f));
    }

    static void Tint(string path, Color c)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) return;
        m.SetColor("_BaseColor", c);
        EditorUtility.SetDirty(m);
    }

    // ── Sunset sun ─────────────────────────────────────────────────────────

    static void SetupSun()
    {
        // Remove any leftover directional lights (SampleScene defaults incl. colored ones)
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Light l in lights)
        {
            if (l.type == LightType.Directional && l.name != "Sun")
                Object.DestroyImmediate(l.gameObject);
        }

        GameObject sunGO = GameObject.Find("Sun");
        if (sunGO == null) sunGO = new GameObject("Sun");
        Light sun = sunGO.GetComponent<Light>();
        if (sun == null) sun = sunGO.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.86f, 0.70f);
        sun.intensity = 1.3f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(28f, -32f, 0f);

        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1f;
    }

    // ── Skybox HDRI + fog ──────────────────────────────────────────────────

    static void SetupSkyAndFog()
    {
        // Skybox: Skybox/Panoramic with the equirectangular evening HDRI
        Material sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/OasisSky.mat");
        if (sky == null)
        {
            Shader pan = Shader.Find("Skybox/Panoramic");
            if (pan == null) { Debug.LogWarning("[Oasis] shader Skybox/Panoramic no encontrado."); return; }
            sky = new Material(pan) { name = "OasisSky" };
            AssetDatabase.CreateAsset(sky, "Assets/Materials/OasisSky.mat");
        }
        Texture2D hdr = AssetDatabase.LoadAssetAtPath<Texture2D>(
            $"{WebDir}/EveningSkyHDRI010A/EveningSkyHDRI010A_1K_HDR.exr");
        if (hdr != null)
        {
            sky.SetTexture("_MainTex", hdr);
            sky.SetFloat("_Mapping", 1f);   // Latitude-Longitude
            sky.SetFloat("_ImageType", 0f); // 360 degrees
            sky.SetFloat("_Layout", 0f);
            sky.SetFloat("_Exposure", 1.05f);
        }
        RenderSettings.skybox = sky;

        // Atmospheric dusk fog
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.62f, 0.55f, 0.58f);
        RenderSettings.fogStartDistance = 260f;
        RenderSettings.fogEndDistance = 1150f;
    }

    // ── Post FX on the scene's global volume ───────────────────────────────

    static void SetupPostFX()
    {
        Volume vol = Object.FindFirstObjectByType<Volume>();

        string path = "Assets/Materials/Generated/OasisFXProfile.asset";
        VolumeProfile p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (p == null)
        {
            p = new VolumeProfile();
            AssetDatabase.CreateAsset(p, path);
        }

        if (vol == null)
        {
            GameObject g = new GameObject("Global Volume");
            vol = g.AddComponent<Volume>();
        }
        vol.isGlobal = true;
        vol.sharedProfile = p;

        var color = GetOrAdd<ColorAdjustments>(p);
        color.postExposure.value = 0.15f;
        color.contrast.value = 16f;
        color.saturation.value = 14f;
        color.colorFilter.value = new Color(1f, 0.97f, 0.93f, 1f);

        var tonemap = GetOrAdd<Tonemapping>(p);
        tonemap.mode.value = TonemappingMode.ACES;

        var bloom = GetOrAdd<Bloom>(p);
        bloom.intensity.value = 1.4f;
        bloom.threshold.value = 0.75f;

        var vignette = GetOrAdd<Vignette>(p);
        vignette.intensity.value = 0.22f;
        vignette.color.value = new Color(0.08f, 0.05f, 0.08f, 1f);

        EditorUtility.SetDirty(p);
        foreach (var comp in p.components)
            if (!AssetDatabase.Contains(comp))
                AssetDatabase.AddObjectToAsset(comp, p);
        EditorUtility.SetDirty(vol);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(path);
        Debug.Log($"[Oasis] SetupPostFX OK, profile={path}");
    }

    static T GetOrAdd<T>(VolumeProfile p) where T : VolumeComponent
    {
        if (p.TryGet<T>(out T found))
            return found;
        return p.Add<T>(true);
    }
}