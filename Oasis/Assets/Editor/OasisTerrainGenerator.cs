using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class OasisTerrainGenerator
{
    // ── Tuning (normalized 0..1 on the terrain) ─────────────────────────────
    private const float TerrainHeight   = 80f;
    private const float TerrainSizeMeters = 500f;

    // Járnviðr mountains: circular ring around the valley (0 = valle abierto)
    private const float MountainHeight  = 0.82f;   // peak ≈ 66 m
    // Ring in normalized radius terms (center 0.5,0.5; r = sqrt(dx²+dz²))
    private const float RingRStart      = 0.34f;   // edge of the open valley (~170 m)
    private const float RingRPeak       = 0.40f;   // ring nominal radius (~200 m)
    private const float RingRWidth      = 0.09f;   // wall thickness band (~45 m)
    private const float RingJagged      = 0.06f;   // angular jaggedness amplitude (±30 m)

    // El Paso: the only opening (north wall is lowered inside this wedge)
    private const float PasoWidthMeters = 80f;     // half-width of the north gap
    private const float PasoHeightScale = 0.12f;

    // River passes through the wall where it reaches the ring (x≈-42)
    private const float RiverGapX       = -42f;
    private const float RiverGapHalf    = 25f;

    // Valley floor
    private const float ValleyBase      = 0.12f;
    private const float ValleyNoiseAmp  = 0.015f;

    // Futuristic city center: gentle flat in the middle (no floating slab)
    private const float CityHalf        = 0.11f;
    private const float CityLevel       = 0.14f;   // 0.14 * 80 = 11.2 = BaseY of city

    // River meandering north->south along the central-west edge of the city
    // (map: river runs N-S through the centre; the central mesa keeps it to x≈-42)
    private const float RiverMeanX      = 0.415f;
    private const float RiverAmp        = 0.020f;
    private const float RiverWaves      = 1.3f;
    private const float RiverHalfWidth  = 0.025f;
    private const float RiverDepth      = 0.035f;

    private const float North = 0f;    // nz == 0 is the north edge (El Paso)

    [MenuItem("Oasis/Generate Base Terrain")]
    public static void Generate()
    {
        Terrain terrain = FindTerrain();
        if (terrain == null)
        {
            Debug.LogError("[Oasis] Terrain not found in the active scene.");
            return;
        }

        TerrainData td = terrain.terrainData;
        td.size = new Vector3(500f, TerrainHeight, 500f);

        int res = td.heightmapResolution;
        float[,] heights = new float[res, res];
        for (int z = 0; z < res; z++)
        {
            float nz = z / (float)(res - 1);
            for (int x = 0; x < res; x++)
            {
                float nx = x / (float)(res - 1);
                heights[z, x] = HeightAt(nx, nz);
            }
        }

        td.SetHeights(0, 0, heights);
        terrain.Flush();
        EditorUtility.SetDirty(terrain);
        AssetDatabase.SaveAssets();

        RebuildEnvironment(terrain);

        // Player starts standing on the city platform.
        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            Transform t = player.transform;
            t.position = new Vector3(0f, CityLevel * TerrainHeight + 1f, 0f);
            EditorUtility.SetDirty(player);
        }

        EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Oasis] Base terrain generated. Heights: {res}x{res}");
    }

    // ── Height function (normalized terrain coords) ─────────────────────────

    static float HeightAt(float nx, float nz)
    {
        // Rolling valley floor
        float n = Mathf.PerlinNoise(nx * 3.1f + 11f, nz * 3.1f + 5f);
        float h = ValleyBase + (n - 0.5f) * ValleyNoiseAmp * 2f;

        // Futuristic city platform at the center
        float dx = Mathf.Abs(nx - 0.5f);
        float dz = Mathf.Abs(nz - 0.5f);
        if (dx < CityHalf && dz < CityHalf)
        {
            float rim = Mathf.Min((CityHalf - dx) / 0.02f, (CityHalf - dz) / 0.02f);
            h = Mathf.Lerp(h, CityLevel, Mathf.Clamp01(rim));
        }

        // River channel (carved through the valley, beside the platform)
        float cx = RiverMeanX + Mathf.Sin(nz * Mathf.PI * 2f * RiverWaves) * RiverAmp;
        float d = Mathf.Abs(nx - cx);
        if (d < RiverHalfWidth)
        {
            float t = Mathf.Clamp01((RiverHalfWidth - d) / (RiverHalfWidth * 0.4f));
            h = Mathf.Min(h, ValleyBase - RiverDepth + t * 0.015f);
        }

        // Járnviðr mountain ring with El Paso gap to the north
        float wall = MountainWall(nx, nz);
        h = Mathf.Lerp(h, MountainHeight, wall);

        return Mathf.Clamp(h, 0.005f, 0.95f);
    }

    static float MountainWall(float nx, float nz)
    {
        // Circular (not square) ring: euclidean distance from the center.
        float dx = nx - 0.5f;
        float dz = nz - 0.5f;
        float r = Mathf.Sqrt(dx * dx + dz * dz);

        // Jagged silhouette: the ring radius wobbles with the polar angle.
        float ang = Mathf.Atan2(dz, dx);
        float wobble = Mathf.PerlinNoise(ang * 1.7f + 0.37f, 0.53f) - 0.5f;
        float rr = RingRPeak + wobble * RingJagged * 2f;

        // Wall profile: soft foothills (r < rr) then steep face (r > rr).
        float t = (r - rr + RingRWidth * 0.5f) / RingRWidth;
        float wall = Mathf.Clamp01(t);
        wall = wall * wall * (3f - 2f * wall); // smoothstep: soft base, steep crest

        // Peak height varies around the ring (mountain range, not a wall)
        float crest = Mathf.PerlinNoise(nx * 6f + 1.3f, nz * 6f + 4.1f);
        wall *= 0.55f + 0.45f * crest;

        // El Paso: keep the northern central wedge as the only opening
        float px = dx * TerrainSizeMeters;
        float pz = dz * TerrainSizeMeters;
        if (pz < 0f && Mathf.Abs(px) < PasoWidthMeters && t > 0f)
        {
            // Only lower the notch; tall ring shoulders remain
            wall *= PasoHeightScale;
        }

        // River tears a pass through the ring where it exits the valley
        if (Mathf.Abs(px - RiverGapX) < RiverGapHalf && t > 0f && t < 0.9f)
            wall *= 0.55f;

        return Mathf.Clamp01(wall);
    }

    // ── Environment: river water + city platform marker ─────────────────────

    static void RebuildEnvironment(Terrain terrain)
    {
        GameObject env = GameObject.Find("Oasis_Environment");
        if (env != null) Object.DestroyImmediate(env);
        env = new GameObject("Oasis_Environment");
        Undo.RegisterCreatedObjectUndo(env, "Oasis Environment");
        EditorUtility.SetDirty(env);

        float waterY = (ValleyBase - RiverDepth + 0.02f) * TerrainHeight;
        CreateRiver(env.transform, terrain, waterY);
    }

    static void CreateRiver(Transform parent, Terrain terrain, float waterY)
    {
        GameObject river = new GameObject("River");
        river.transform.SetParent(parent, false);

        int segs = 9;
        float xc0 = RiverCenterX(0.10f), zc0 = 0.10f;
        for (int k = 1; k <= segs; k++)
        {
            float nz1 = 0.10f + (0.80f / segs) * k;
            float xc1 = RiverCenterX(nz1);
            float zc1 = nz1;

            float dx = (xc1 - xc0) * 500f;
            float dz = (zc1 - zc0) * 500f;
            float len = Mathf.Sqrt(dx * dx + dz * dz);
            float yaw = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;

            GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Plane);
            seg.name = $"RiverSegment_{k}";
            seg.transform.SetParent(river.transform, false);
            seg.transform.position = new Vector3(xc0 * 500f - 250f, waterY, zc0 * 500f - 250f);
            seg.transform.localEulerAngles = new Vector3(0f, yaw, 0f);
            seg.transform.localScale = new Vector3(
                RiverHalfWidth * 2f * 500f * 1.3f / 10f, 1f, len * 1.08f / 10f);

            var sr = seg.GetComponent<Renderer>();
            if (sr != null) sr.sharedMaterial = WaterMaterial();
            Object.DestroyImmediate(seg.GetComponent<Collider>());

            xc0 = xc1; zc0 = zc1;
        }
        EditorUtility.SetDirty(river);
    }

    static float RiverCenterX(float nz)
    {
        return RiverMeanX + Mathf.Sin(nz * Mathf.PI * 2f * RiverWaves) * RiverAmp;
    }

    // ── Materials ───────────────────────────────────────────────────────────

    static string EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = path.Substring(0, path.LastIndexOf('/'));
            string name   = path.Substring(path.LastIndexOf('/') + 1);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
        return path;
    }

    static Material LoadOrCreateMaterial(string path, System.Func<Material> factory)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;

        EnsureFolder("Assets/Materials");
        m = factory();
        AssetDatabase.CreateAsset(m, path);
        AssetDatabase.SaveAssets();
        return m;
    }

    static Material _water;
    static Material WaterMaterial()
    {
        if (_water != null) return _water;
        _water = LoadOrCreateMaterial("Assets/Materials/OasisWater.mat", () =>
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = "OasisWater";
            m.SetFloat("_Surface", 1f);            // transparent
            m.SetFloat("_Blend", 0f);
            m.SetColor("_BaseColor", new Color(0.04f, 0.45f, 0.62f, 0.72f));
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 0.92f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return m;
        });
        return _water;
    }

    static Material _cityPad;
    static Material CityMarkerMaterial()
    {
        if (_cityPad != null) return _cityPad;
        _cityPad = LoadOrCreateMaterial("Assets/Materials/OasisCityPad.mat", () =>
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = "OasisCityPad";
            m.SetFloat("_Surface", 1f);
            m.SetColor("_BaseColor", new Color(1f, 0.72f, 0.1f, 0.35f));
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return m;
        });
        return _cityPad;
    }

    // ── Terrain texturing (splatmap, URP Terrain Lit) ───────────────────────

    [MenuItem("Oasis/Texture Terrain")]
    public static void TextureTerrain()
    {
        Terrain terrain = FindTerrain();
        if (terrain == null)
        {
            Debug.LogError("[Oasis] Terrain not found in the active scene.");
            return;
        }
        TerrainData td = terrain.terrainData;

        TerrainLayer grass = LoadOrCreateTerrainLayer(
            "Assets/Materials/Generated/TL_ValleyGrass.asset", "Grass006");
        TerrainLayer dirt = LoadOrCreateTerrainLayer(
            "Assets/Materials/Generated/TL_FoothillDirt.asset", "Ground110");
        TerrainLayer rock = LoadOrCreateTerrainLayer(
            "Assets/Materials/Generated/TL_MountainRock.asset", "Rock064");

        td.terrainLayers = new[] { grass, dirt, rock };
        terrain.materialTemplate = TerrainLitMaterial();

        int res = td.alphamapResolution;
        float[,,] splat = new float[res, res, 3];
        for (int z = 0; z < res; z++)
        {
            float nz = z / (float)(res - 1);
            for (int x = 0; x < res; x++)
            {
                float nx = x / (float)(res - 1);
                float h = HeightAt(nx, nz);

                // Slope from neighbor samples (normalized meters/meter)
                float ep = 1f / (res - 1);
                float hx = HeightAt(nx + ep, nz);
                float hz = HeightAt(nx, nz + ep);
                float gx = Mathf.Abs(hx - h) * TerrainHeight / (ep * TerrainSizeMeters);
                float gz = Mathf.Abs(hz - h) * TerrainHeight / (ep * TerrainSizeMeters);
                float slope = Mathf.Sqrt(gx * gx + gz * gz);

                // Distances to the city pad and the mountains
                float dxw = (nx - 0.5f) * TerrainSizeMeters;
                float dzw = (nz - 0.5f) * TerrainSizeMeters;
                float cityD = Mathf.Sqrt(dxw * dxw + dzw * dzw);
                float wall = MountainWall(nx, nz);

                float wRock, wDirt, wGrass;
                if (cityD < 120f)
                {
                    // Urban core: hard rock/concrete plate under the city pad
                    wRock  = 0.75f; wDirt = 0.20f; wGrass = 0.05f;
                }
                else
                {
                    // Mountain faces + rocky peaks
                    float rockMix = Mathf.Clamp01((slope - 0.18f) / 0.35f);
                    float wallMix = Mathf.Clamp01(wall * 1.6f - 0.35f);
                    wRock = Mathf.Max(rockMix, wallMix);
                    // Dirt in the transition band (foothills, dry flats)
                    wDirt = Mathf.Clamp01(0.85f - wRock * 0.9f) *
                            Mathf.Clamp01(1 - Mathf.Abs(slope - 0.12f) / 0.18f);
                    if (cityD > 430f) wDirt *= 0.5f; // drier far from the ring
                    // Grass covers the rest, softer away from the ring
                    wGrass = Mathf.Clamp01(1 - wRock - wDirt);
                }

                // Riverbed: muddy channel along the meander (keeps the pass visible)
                float cxr = RiverMeanX + Mathf.Sin(nz * Mathf.PI * 2f * RiverWaves) * RiverAmp;
                if (Mathf.Abs(nx - cxr) < RiverHalfWidth)
                {
                    wRock = 0.05f; wDirt = 0.85f; wGrass = 0.10f;
                }

                // Soft blend: normalize + slight blur via neighbor-free noise
                float total = wRock + wDirt + wGrass;
                float wob = 0.9f + Mathf.PerlinNoise(nx * 9f + 3f, nz * 9f + 7f) * 0.2f;
                splat[z, x, 0] = wRock  / total * wob;
                splat[z, x, 1] = wDirt  / total * (2f - wob);
                splat[z, x, 2] = wGrass / total;
            }
        }
        td.SetAlphamaps(0, 0, splat);
        terrain.Flush();
        EditorUtility.SetDirty(td);
        EditorUtility.SetDirty(terrain);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        Debug.Log($"[Oasis] Terrain textured: {res}x{res} splat, 3 layers (URP Terrain Lit).");
    }

    static TerrainLayer LoadOrCreateTerrainLayer(string path, string texId)
    {
        TerrainLayer tl = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (tl != null) return tl;

        EnsureFolder("Assets/Materials/Generated");
        tl = new TerrainLayer { name = System.IO.Path.GetFileNameWithoutExtension(path) };
        string basePath = $"Assets/Textures/Web/{texId}/{texId}_1K-JPG";
        tl.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{basePath}_Color.jpg");
        tl.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{basePath}_NormalDX.jpg");
        tl.tileSize = new Vector2(12f, 12f);
        AssetDatabase.CreateAsset(tl, path);
        return tl;
    }

    static Material _terrainLit;
    static Material TerrainLitMaterial()
    {
        string path = "Assets/Materials/Generated/TerrainLit.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        EnsureFolder("Assets/Materials/Generated");
        m = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"))
        { name = "TerrainLit" };
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    static Terrain FindTerrain()
    {
        var t = Object.FindObjectOfType<Terrain>();
        if (t != null) return t;

        GameObject go = GameObject.Find("Terrain");
        return go != null ? go.GetComponent<Terrain>() : null;
    }
}