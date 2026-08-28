using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class OasisZoneBuilder
{
    private static Material _amber, _white, _field, _metal, _glass, _panel, _asphalt, _brick;
    private static Material _cyan, _magenta, _gold;
    private static Texture2D _fieldTex;

    [MenuItem("Oasis/Build Outer Zones")]
    public static void BuildAll()
    {
        EnsureFolder("Assets/Materials/Generated");
        LoadMats();
        BuildBarrioBajo();
        BuildForja();
        BuildCampos();
        BuildCuspide();
        BuildElPaso();
        BuildRoadNetwork();
        BuildCityDetail();

        AssetDatabase.SaveAssets();
        EditorApplication.RepaintProjectWindow();
        Debug.Log("[Oasis] Outer zones built.");
    }

    // ── Materials & data ───────────────────────────────────────────────────

    static void LoadMats()
    {
        _metal  = LoadOr("Assets/Materials/OasisMetal.mat");
        _glass  = LoadOr("Assets/Materials/OasisGlass.mat");
        _cyan   = LoadOr("Assets/Materials/OasisEmissive_Cyan.mat");
        _magenta= LoadOr("Assets/Materials/OasisEmissive_Magenta.mat");
        _gold   = LoadOr("Assets/Materials/OasisEmissive_Gold.mat");
        _panel  = LoadOr("Assets/Materials/Generated/Mat_Panel.mat");
        _asphalt= LoadOr("Assets/Materials/Generated/Mat_Asphalt.mat");
        _brick  = LoadOr("Assets/Materials/Generated/Mat_Brick.mat");

        _amber = Make("Mat_Emissive_Amber",
            new Color(0.08f, 0.035f, 0.01f), 0.6f, 0.4f, new Color(1f, 0.42f, 0.07f));
        _white = Make("Mat_WhiteElite",
            new Color(0.92f, 0.9f, 0.86f), 0.25f, 0.7f, new Color(0f, 0f, 0f));

        _fieldTex = GetTexture("Tex_FieldRows", BuildFieldRows);
        _field = Make("Mat_Field", Color.white, 0.25f, 0.35f, Color.black);
        _field.SetTexture("_BaseMap", _fieldTex);
    }

    static Material LoadOr(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    static Material Make(string name, Color baseColor, float metallic, float smooth, Color emission)
    {
        string path = $"Assets/Materials/Generated/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(s) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = baseColor;
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smooth);
        bool em = EmissionOn(emission) ? true : false;
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", emission);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        EditorUtility.SetDirty(m);
        return m;
    }

    static bool EmissionOn(Color c) => c.maxColorComponent > 0.01f;

    static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;
        string parent = folderPath.Substring(0, folderPath.LastIndexOf('/'));
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderPath.Substring(folderPath.LastIndexOf('/') + 1));
    }

    static Texture2D GetTexture(string name, Func<Color[]> builder)
    {
        string path = $"Assets/Materials/Generated/{name}.asset";
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex != null) return tex;
        var rand = new System.Random(name.GetHashCode());
        Color[] px = builder();
        tex = new Texture2D(128, 256, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply(false, false);
        AssetDatabase.CreateAsset(tex, path);
        return tex;
    }

    static Color[] BuildFieldRows()
    {
        const int W = 128, H = 256;
        var px = new Color[W * H];
        var rand = new System.Random(7);
        for (int y = 0; y < H; y++)
        {
            bool row = (y / 10) % 2 == 0;
            Color baseC = row
                ? new Color(0.13f, 0.22f, 0.09f)
                : new Color(0.22f, 0.13f, 0.07f);
            for (int x = 0; x < W; x++)
            {
                float n = (float)(rand.NextDouble() - 0.5f) * 0.08f;
                Color c = new Color(
                    Mathf.Clamp01(baseC.r + n),
                    Mathf.Clamp01(baseC.g + n * 1.5f),
                    Mathf.Clamp01(baseC.b + n));
                if (x % 8 == 0) c = Color.Lerp(c, Color.black, 0.55f);
                px[y * W + x] = c;
            }
        }
        return px;
    }

    // ── Primitives ─────────────────────────────────────────────────────────

    static GameObject P(Transform parent, string name, PrimitiveType type,
                        Vector3 pos, Vector3 scl, Material m, float eulerY = 0f, float eulerX = 0f, float eulerZ = 0f)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scl;
        if (eulerX != 0f || eulerY != 0f || eulerZ != 0f)
        {
            go.transform.localRotation = Quaternion.Euler(eulerX, eulerY, eulerZ);
        }
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    static void Rebuild(string rootName, Action<Transform> build)
    {
        GameObject old = GameObject.Find(rootName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        GameObject root = new GameObject(rootName);
        build(root.transform);
    }

    const float CB = 9.5f;  // Barrio base
    const float FB = 11f;   // Forja base
    const float LB = 10f;   // Campos base
    const float PB = 14f;   // El Paso base

    // ── El Barrio Bajo (west bank, away from the central river) ────────────────

    static void BuildBarrioBajo()
    {
        Rebuild("Zona_BarrioBajo", (t) =>
        {
            P(t, "Pad", PrimitiveType.Cube, new Vector3(-95f, CB - 0.4f, 10f), new Vector3(56f, 0.8f, 74f), _panel);
            float[,] blk = { { -82f, 40f, 14f, 13f }, { -97f, 17f, 11f, 11f },
                             { -110f, 32f, 9f, 10f }, { -88f, -4f, 12f, 12f },
                             { -109f, 0f, 10f, 9f }, { -95f, -17f, 13f, 11f } };
            for (int i = 0; i < blk.GetLength(0); i++)
            {
                float x = blk[i, 0], z = blk[i, 1], w = blk[i, 2], h = blk[i, 3];
                P(t, $"Block_{i}", PrimitiveType.Cube,
                    new Vector3(x, CB + h / 2f, z), new Vector3(w, h, w * 0.85f),
                    i % 2 == 0 ? _brick : _panel);
                if (i % 2 == 0)
                {
                    P(t, $"Block_{i}_Roof", PrimitiveType.Cube,
                        new Vector3(x, CB + h + 1.2f, z), new Vector3(w + 1f, 1.6f, w * 0.85f + 1f),
                        _metal, 0f, 14f);
                }
            }
            // Shacks + clutter
            float[,] shack = { { -120f, 25f }, { -121f, -5f }, { -82f, 10f }, { -68f, 13f } };
            for (int i = 0; i < shack.GetLength(0); i++)
            {
                float x = shack[i, 0], z = shack[i, 1];
                P(t, $"Shack_{i}", PrimitiveType.Cube, new Vector3(x, CB + 1.8f, z), new Vector3(4.4f, 3.6f, 4.4f), _panel);
                P(t, $"Shack_{i}_R", PrimitiveType.Cube, new Vector3(x + 0.3f, CB + 4.2f, z), new Vector3(5.2f, 1.2f, 5.2f), _brick, 0f, 12f);
            }
            for (int i = 0; i < 5; i++)
            {
                float x = -115f + i * 10f;
                P(t, $"Crate_{i}", PrimitiveType.Cube, new Vector3(x, CB + 1.1f, 47f), new Vector3(2.2f, 2.2f, 2.2f), _brick);
            }
            for (int i = 0; i < 9; i++)
            {
                float x = -117f + i * 6.5f;
                float z = -17f + (i % 3) * 8f;
                P(t, $"WallLamp_{i}", PrimitiveType.Cube, new Vector3(x, CB + 3.4f, z), new Vector3(1.1f, 0.5f, 1.1f), _amber);
            }
            // Neon signs
            P(t, "Neon_Sign1", PrimitiveType.Cube, new Vector3(-82f, CB + 9f, 37f), new Vector3(6f, 1.4f, 0.4f), _magenta);
            P(t, "Neon_Sign2", PrimitiveType.Cube, new Vector3(-110f, CB + 6f, 31f), new Vector3(5f, 1.2f, 0.4f), _cyan);
            // Cables between buildings
            P(t, "Cable_1", PrimitiveType.Cube, new Vector3(-89f, CB + 13f, 29f), new Vector3(12f, 0.2f, 0.2f), ColorMat());
            P(t, "Cable_2", PrimitiveType.Cube, new Vector3(-98f, CB + 10f, 24f), new Vector3(0.2f, 0.2f, 14f), ColorMat());
        });
    }

    static Material ColorMat()
    {
        string path = "Assets/Materials/Generated/Mat_Cable.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(s) { name = "Mat_Cable" };
            m.color = new Color(0.03f, 0.03f, 0.04f);
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }

    // ── La Forja (north industrial) ──────────────────────────────────────────

    static void BuildForja()
    {
        Rebuild("Zona_LaForja", (t) =>
        {
            P(t, "Pad", PrimitiveType.Cube, new Vector3(0f, FB - 0.4f, -110f), new Vector3(84f, 0.8f, 64f), _panel);
            float[,] wh = { { -30f, -120f }, { -10f, -100f }, { 10f, -120f },
                            { 30f, -100f }, { 0f, -132f }, { -20f, -112f } };
            for (int i = 0; i < wh.GetLength(0); i++)
            {
                float x = wh[i, 0], z = wh[i, 1];
                P(t, $"Warehouse_{i}", PrimitiveType.Cube, new Vector3(x, FB + 3.4f, z), new Vector3(16f, 6.8f, 12f), _panel);
                P(t, $"Warehouse_{i}_Roof", PrimitiveType.Cube, new Vector3(x, FB + 7.6f, z), new Vector3(17f, 1.6f, 13f), _metal, 0f, 12f);
            }
            // Smokestacks with heat glow rims
            float[,] stacks = { { -24f, -84f, 24f }, { -6f, -86f, 28f }, { 22f, -86f, 22f }, { 14f, -136f, 26f } };
            for (int i = 0; i < stacks.GetLength(0); i++)
            {
                float x = stacks[i, 0], z = stacks[i, 1], h = stacks[i, 2];
                P(t, $"Stack_{i}", PrimitiveType.Cylinder, new Vector3(x, FB + h / 2f, z), new Vector3(3f, h / 2f, 3f), _panel);
                P(t, $"Stack_{i}_Glow", PrimitiveType.Cylinder, new Vector3(x, FB + h + 0.5f, z), new Vector3(3.6f, 0.5f, 3.6f), _amber);
            }
            // Tanks
            float[,] tanks = { { 34f, -128f, 5f, 9f }, { 44f, -116f, 7f, 12f }, { 38f, -96f, 4f, 8f } };
            for (int i = 0; i < tanks.GetLength(0); i++)
            {
                float x = tanks[i, 0], z = tanks[i, 1], r = tanks[i, 2], h = tanks[i, 3];
                P(t, $"Tank_{i}", PrimitiveType.Cylinder, new Vector3(x, FB + h / 2f, z), new Vector3(r * 2f, h / 2f, r * 2f), _metal);
                P(t, $"Tank_{i}_Cap", PrimitiveType.Cube, new Vector3(x + r * 0.4f, FB + h + 0.6f, z), new Vector3(r, 1.2f, r), _panel);
            }
            // Crane
            P(t, "Crane_Tower", PrimitiveType.Cube, new Vector3(-40f, FB + 7f, -100f), new Vector3(2f, 14f, 2f), _metal);
            P(t, "Crane_Arm", PrimitiveType.Cube, new Vector3(-30f, FB + 14.5f, -100f), new Vector3(0.8f, 1.4f, 22f), _metal);
            // Ground pipes
            for (int i = 0; i < 3; i++)
            {
                P(t, $"Pipe_{i}", PrimitiveType.Cube, new Vector3(-20f + i * 22f, FB + 0.4f, -94f), new Vector3(20f, 0.6f, 0.6f), _metal);
            }
            // Blast furnace glow
            P(t, "Furnace", PrimitiveType.Cube, new Vector3(25f, FB + 5f, -118f), new Vector3(9f, 10f, 7f), _glass);
            P(t, "Furnace_Glow", PrimitiveType.Cube, new Vector3(25f, FB + 7.2f, -114.4f), new Vector3(5.5f, 5f, 0.8f), _amber);
        });
    }

    // ── Los Campos (south farmlands) ─────────────────────────────────────────

    static void BuildCampos()
    {
        Rebuild("Zona_Campos", (t) =>
        {
            float[,] fd = { { -60f, 95f, 15f, 10f }, { 25f, 115f, 14f, 9f }, { -25f, 150f, 15f, 10f } };
            for (int i = 0; i < fd.GetLength(0); i++)
            {
                float x = fd[i, 0], z = fd[i, 1], w = fd[i, 2], d = fd[i, 3];
                P(t, $"Field_{i}", PrimitiveType.Plane, new Vector3(x, LB + 0.06f, z), new Vector3(w, 1f, d), _field);
                P(t, $"Field_{i}_Pad", PrimitiveType.Cube, new Vector3(x, LB - 0.3f, z), new Vector3(w * 10f, 0.5f, d * 10f), _panel);
            }
            float[,] silos = { { 55f, 90f, 3f, 11f }, { 62f, 100f, 4f, 13f }, { 70f, 88f, 3f, 10f }, { 66f, 112f, 5f, 15f } };
            for (int i = 0; i < silos.GetLength(0); i++)
            {
                float x = silos[i, 0], z = silos[i, 1], r = silos[i, 2], h = silos[i, 3];
                P(t, $"Silo_{i}", PrimitiveType.Cylinder, new Vector3(x, LB + h / 2f, z), new Vector3(r * 2f, h / 2f, r * 2f), _white);
                P(t, $"Silo_{i}_Ring", PrimitiveType.Cylinder, new Vector3(x, LB + 0.6f, z), new Vector3(r * 2f + 0.5f, 0.5f, r * 2f + 0.5f), _metal);
            }
            // Agri domes (glass)
            float[,] domes = { { -85f, 100f, 7f }, { -100f, 125f, 5f }, { 0f, 165f, 6f } };
            for (int i = 0; i < domes.GetLength(0); i++)
            {
                float x = domes[i, 0], z = domes[i, 1], r = domes[i, 2];
                P(t, $"Dome_{i}", PrimitiveType.Cylinder, new Vector3(x, LB + r * 0.8f, z), new Vector3(r * 2f, r * 0.8f, r * 2f), _glass);
            }
            // Wind turbines
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -120f : 95f, z = i == 0 ? 80f : 160f;
                P(t, $"Turbine_{i}_Tower", PrimitiveType.Cylinder, new Vector3(x, LB + 9f, z), new Vector3(1.4f, 9f, 1.4f), _white);
                P(t, $"Turbine_{i}_Nacelle", PrimitiveType.Cube, new Vector3(x, LB + 18.2f, z), new Vector3(1.8f, 1.2f, 4f), _white);
                P(t, $"Turbine_{i}_BladeA", PrimitiveType.Cube, new Vector3(x, LB + 18.2f, z - 7f), new Vector3(0.7f, 1f, 7f), _white);
                P(t, $"Turbine_{i}_BladeB", PrimitiveType.Cube, new Vector3(x, LB + 18.2f, z + 7f), new Vector3(0.7f, 1f, 7f), _white);
            }
            // Barns
            float[,] barns = { { 15f, 75f }, { -15f, 175f } };
            for (int i = 0; i < barns.GetLength(0); i++)
            {
                float x = barns[i, 0], z = barns[i, 1];
                P(t, $"Barn_{i}", PrimitiveType.Cube, new Vector3(x, LB + 3f, z), new Vector3(12f, 6f, 9f), _brick);
                P(t, $"Barn_{i}_Roof", PrimitiveType.Cube, new Vector3(x, LB + 6.6f, z), new Vector3(13f, 1.4f, 10f), _metal, 0f, 18f);
            }
            // Irrigation sprinklers
            for (int i = 0; i < 6; i++)
            {
                float x = -80f + i * 18f;
                float z = 120f + (i % 2) * 30f;
                P(t, $"Sprinkler_{i}", PrimitiveType.Cylinder, new Vector3(x, LB + 1.2f, z), new Vector3(0.8f, 1.2f, 0.8f), _metal);
                P(t, $"Sprinkler_{i}_Jet", PrimitiveType.Cube, new Vector3(x, LB + 2.6f, z), new Vector3(0.5f, 0.9f, 0.5f), _cyan);
            }
        });
    }

    // ── La Cúspide (NE slope, elite) ──────────────────────────────────────────

    static void BuildCuspide()
    {
        Rebuild("Zona_LaCuspide", (t) =>
        {
            float[,] ter = { { 88f, -148f, 24f, 18f, 22f }, { 102f, -160f, 22f, 18f, 30f }, { 114f, -172f, 24f, 18f, 38f } };
            var villa = new float[,] { { 88f, -148f, 26f }, { 102f, -160f, 34f }, { 114f, -172f, 42f } };
            for (int i = 0; i < ter.GetLength(0); i++)
            {
                float x = ter[i, 0], z = ter[i, 1], w = ter[i, 2], d = ter[i, 3], y = ter[i, 4];
                P(t, $"Terrace_{i}", PrimitiveType.Cube, new Vector3(x, y - 1.4f, z), new Vector3(w, 1.4f, d), _white);
                // elite villa on terrace
                float v = villa[i, 2];
                P(t, $"Villa_{i}", PrimitiveType.Cube, new Vector3(x, v + 2.5f, z), new Vector3(9f, 5f, 8f), _white);
                P(t, $"Villa_{i}_Glass", PrimitiveType.Cube, new Vector3(x, v + 4.6f, z), new Vector3(6f, 1.6f, 5f), _glass);
                P(t, $"Villa_{i}_Trim", PrimitiveType.Cube, new Vector3(x, v + 5.4f, z), new Vector3(10.2f, 0.5f, 9.2f), _amber);
                P(t, $"Villa_{i}_Roof", PrimitiveType.Cube, new Vector3(x, v + 6.8f, z), new Vector3(10.2f, 1f, 9.2f), _white, 0f, 16f);
                // terrace lamp
                P(t, $"TerLamp_{i}", PrimitiveType.Cube, new Vector3(x + w * 0.32f, y + 0.6f, z - d * 0.35f), new Vector3(0.5f, 1.2f, 0.5f), _white);
                P(t, $"TerLamp_{i}_Head", PrimitiveType.Cube, new Vector3(x + w * 0.32f, y + 1.5f, z - d * 0.35f), new Vector3(1f, 0.4f, 1f), _amber);
            }
            // Grand stairway between terrace 0 and 1
            for (int s = 0; s < 8; s++)
            {
                float x = 92f, z = -154.5f + s * 1.5f;
                float y = 23.6f + s * 0.55f;
                P(t, $"Step_{s}", PrimitiveType.Cube, new Vector3(x, y, z), new Vector3(12f, 0.4f, 1.4f), _white);
            }
            // Observatory dome
            P(t, "Obs_Shaft", PrimitiveType.Cylinder, new Vector3(95f, 32f, -196f), new Vector3(5f, 2f, 5f), _white);
            P(t, "Obs_Dome", PrimitiveType.Cylinder, new Vector3(95f, 35.6f, -196f), new Vector3(6f, 3f, 6f), _glass);
            P(t, "Obs_Ring", PrimitiveType.Cylinder, new Vector3(95f, 30.2f, -196f), new Vector3(6.4f, 0.4f, 6.4f), _amber);
            // Los Cimientos: dense mid-slope housing (map: La Cúspide, zona residencial alta)
            float[,] cim = { { 78f, -132f, 19f }, { 92f, -140f, 23f }, { 106f, -136f, 21f },
                             { 84f, -148f, 25f }, { 70f, -142f, 18f } };
            for (int i = 0; i < cim.GetLength(0); i++)
            {
                float x = cim[i, 0], z = cim[i, 1], y = cim[i, 2];
                float h = 7f + (i % 3) * 2.5f;
                float w = 7f + (i % 2) * 2f;
                P(t, $"Cimientos_Tower_{i}", PrimitiveType.Cube,
                    new Vector3(x, y + h / 2f, z), new Vector3(w, h, w * 0.85f), _panel);
                P(t, $"Cimientos_Tower_{i}_Trim", PrimitiveType.Cube,
                    new Vector3(x, y + h + 0.35f, z), new Vector3(w + 0.8f, 0.3f, w * 0.85f + 0.8f), _amber);
                // lit windows on the south face
                for (int v = 0; v < 3; v++)
                {
                    P(t, $"Cimientos_Win_{i}_{v}", PrimitiveType.Cube,
                        new Vector3(x - w * 0.35f, y + h * (0.3f + v * 0.25f), z + w * 0.85f / 2f + 0.1f),
                        new Vector3(1.1f, 1.2f, 0.15f), v == 1 ? _cyan : _amber);
                }
            }
        });
    }

    // ── El Paso (north gate) ─────────────────────────────────────────────────

    static void BuildElPaso()
    {
        Rebuild("Zona_ElPaso", (t) =>
        {
            float[] px = { -22f, 22f };
            for (int i = 0; i < 2; i++)
            {
                P(t, $"Pylon_{i}", PrimitiveType.Cube, new Vector3(px[i], PB + 15f, -244f), new Vector3(6f, 30f, 6f), _metal);
                P(t, $"Pylon_{i}_Tip", PrimitiveType.Cube, new Vector3(px[i], PB + 31f, -244f), new Vector3(3.4f, 2f, 3.4f), _cyan);
            }
            P(t, "Gate_Arch", PrimitiveType.Cube, new Vector3(0f, PB + 26f, -244f), new Vector3(48f, 1.6f, 3f), _cyan);
            for (int i = -1; i <= 1; i++)
            {
                P(t, $"Banner_{i + 1}", PrimitiveType.Cube, new Vector3(i * 12f, PB + 22.4f, -244.5f), new Vector3(1.5f, 5f, 0.3f), _gold);
            }
            // Warning strips at the gate base
            P(t, "Gate_Glow_L", PrimitiveType.Cube, new Vector3(-34f, PB + 0.3f, -243.5f), new Vector3(3f, 0.4f, 2.4f), _amber);
            P(t, "Gate_Glow_R", PrimitiveType.Cube, new Vector3(34f, PB + 0.3f, -243.5f), new Vector3(3f, 0.4f, 2.4f), _amber);
        });
    }

    // ── Road network + bridges ───────────────────────────────────────────────

    static void BuildRoadNetwork()
    {
        Rebuild("Red_Vial", (t) =>
        {
            // City centre -> Forja (north)
            P(t, "Road_N1", PrimitiveType.Cube, new Vector3(0f, 10.7f, -75f), new Vector3(8f, 0.5f, 48f), _asphalt);
            P(t, "Road_N2", PrimitiveType.Cube, new Vector3(0f, 10.8f, -122f), new Vector3(8f, 0.5f, 48f), _asphalt);
            // City -> Campos (south)
            P(t, "Road_S1", PrimitiveType.Cube, new Vector3(0f, 10.2f, 78f), new Vector3(8f, 0.5f, 58f), _asphalt);
            P(t, "Road_S2", PrimitiveType.Cube, new Vector3(0f, 10.2f, 140f), new Vector3(8f, 0.5f, 58f), _asphalt);
            // West bridge (city -> Barrio) over the central river
            P(t, "Bridge_Deck", PrimitiveType.Cube, new Vector3(-75f, 12.0f, -30f), new Vector3(110f, 0.9f, 8f), _asphalt);
            P(t, "Bridge_Edge_L", PrimitiveType.Cube, new Vector3(-75f, 12.9f, -26f), new Vector3(110f, 0.4f, 0.6f), _cyan);
            P(t, "Bridge_Edge_R", PrimitiveType.Cube, new Vector3(-75f, 12.9f, -34.2f), new Vector3(110f, 0.4f, 0.6f), _cyan);
            float[] pyl = { -48f, -85f, -108f };
            for (int i = 0; i < 2; i++)
            {
                P(t, $"BridgePylon_{i}", PrimitiveType.Cube, new Vector3(pyl[i], 11.4f + 8f, -30f), new Vector3(3f, 17f, 3f), _metal);
            }
            // Barrio connector
            P(t, "Road_W", PrimitiveType.Cube, new Vector3(-122f, 10.0f, -34f), new Vector3(12f, 0.5f, 20f), _asphalt);
            // Cúspide switchback ramps (north-east)
            System.ValueTuple<float, float, float, float, float>[] ramps =
            {
                (30f, -140f, 70f, -165f, 10.6f),
                (70f, -165f, 120f, -186f, 12f),
                (120f, -186f, 132f, -193f, 13.4f),
            };
            foreach (var r in ramps)
            {
                Vector3 a = new Vector3(r.Item1, r.Item5, r.Item2);
                Vector3 b = new Vector3(r.Item3, r.Item5 + 1.5f, r.Item4);
                Vector3 dir = (b - a);
                float len = dir.magnitude;
                Vector3 mid = (a + b) / 2f;
                float yaw = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg - 90f;
                P(t, "Ramp", PrimitiveType.Cube, mid, new Vector3(7f, 0.5f, len), _asphalt, yaw, -10f);
            }
        });
    }

    // ── Cinematic rig ────────────────────────────────────────────────────────

    [MenuItem("Oasis/Cinematic/Crear Cinemática")]
    public static void CreateCinematicRig()
    {
        GameObject old = GameObject.Find("CinematicRig");
        if (old != null) UnityEngine.Object.DestroyImmediate(old);

        GameObject rigGO = new GameObject("CinematicRig");
        OasisCinematic rig = rigGO.AddComponent<OasisCinematic>();

        GameObject camGO = new GameObject("CinematicCamera");
        camGO.transform.SetParent(rigGO.transform, false);
        Camera cam = camGO.AddComponent<Camera>();
        camGO.AddComponent<AudioListener>();
        cam.enabled = false;
        cam.nearClipPlane = 1f;
        cam.farClipPlane = 4000f;
        cam.fieldOfView = 50f;
        cam.clearFlags = CameraClearFlags.Skybox;
        rig.boundCamera = cam;

        rig.shotPositions = new Vector3[]
        {
            new Vector3(120f, 80f, 120f),
            new Vector3(78f, 26f, -75f),
            new Vector3(-55f, 22f, 55f),
            new Vector3(-70f, 16f, -28f),
            new Vector3(-155f, 30f, -50f),
            new Vector3(-70f, 24f, 165f),
            new Vector3(5f, 30f, -120f),
            new Vector3(150f, 60f, -160f),
            new Vector3(0f, 48f, -230f),
            new Vector3(265f, 130f, 20f),
        };
        rig.shotLookTargets = new string[]
        {
            "Spire", "Oasis_City", "Market_Plaza", "Oasis_Environment",
            "Zona_BarrioBajo", "Zona_Campos", "Zona_LaForja", "Zona_LaCuspide", "Zona_ElPaso", "",
        };
        rig.shotLookOffsets = new Vector3[]
        {
            new Vector3(0f, 20f, 0f), new Vector3(0f, 8f, 0f), new Vector3(0f, 6f, 0f), new Vector3(0f, 10f, 0f),
            new Vector3(0f, 8f, 0f), new Vector3(0f, 10f, 0f), new Vector3(0f, 8f, 0f), new Vector3(0f, 14f, 0f),
            new Vector3(0f, 8f, 0f), new Vector3(0f, 16f, 0f),
        };

        EditorSceneManager.MarkSceneDirty(rigGO.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Oasis] Cinematic rig created. Use 'Oasis/Cinematic/Reproducir Cinemática' to play it.");
    }

    [MenuItem("Oasis/Cinematic/Reproducir Cinemática")]
    public static void PlayCinematic()
    {
        CreateCinematicRig();
        EditorApplication.EnterPlaymode();
    }

    // ── City detail polish ───────────────────────────────────────────────────

    static void BuildCityDetail()
    {
        const float baseY = 11.2f; // top of city center (matches OasisTerrainGenerator CityLevel 0.14)
        GameObject root = GameObject.Find("Oasis_City");
        if (root == null) return;

        Transform t = root.transform;
        if (root.transform.Find("Fountain") == null)
        {
            P(t, "Fountain_Basin", PrimitiveType.Cylinder, new Vector3(0f, baseY + 0.8f, 0f), new Vector3(14f, 1.6f, 14f), _white);
            P(t, "Fountain_Water", PrimitiveType.Cylinder, new Vector3(0f, baseY + 1.5f, 0f), new Vector3(11f, 0.7f, 11f), _cyan);
            P(t, "Fountain_Ring", PrimitiveType.Cylinder, new Vector3(0f, baseY + 1.2f, 0f), new Vector3(12.5f, 0.5f, 12.5f), _glass);
            P(t, "Fountain_Jet", PrimitiveType.Cylinder, new Vector3(0f, baseY + 4.2f, 0f), new Vector3(1.8f, 3.4f, 1.8f), _glass);
            P(t, "Fountain_Spray", PrimitiveType.Cylinder, new Vector3(0f, baseY + 7.4f, 0f), new Vector3(0.8f, 0.5f, 0.8f), _cyan);
        }
        float[] lampZ = { -36f, -12f, 12f, 36f };
        foreach (float z in lampZ)
        {
            string tag = $"LampNS_{Mathf.RoundToInt(z)}";
            if (root.transform.Find(tag) == null)
            {
                P(t, tag, PrimitiveType.Cube, new Vector3(5f, baseY + 2.6f, z), new Vector3(0.5f, 5.2f, 0.5f), _metal);
                P(t, tag + "_Head", PrimitiveType.Cube, new Vector3(5f, baseY + 5.4f, z), new Vector3(1.6f, 0.5f, 1.6f), _amber);
            }
        }
        float[] lampX = { -36f, 36f };
        foreach (float x in lampX)
        {
            string tag = $"LampEW_{Mathf.RoundToInt(x)}";
            if (root.transform.Find(tag) == null)
            {
                P(t, tag, PrimitiveType.Cube, new Vector3(x, baseY + 2.6f, 5f), new Vector3(0.5f, 5.2f, 0.5f), _metal);
                P(t, tag + "_Head", PrimitiveType.Cube, new Vector3(x, baseY + 5.4f, 5f), new Vector3(1.6f, 0.5f, 1.6f), _amber);
            }
        }
    }
}