using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class OasisCityGenerator
{
    private const float BaseY = 11.2f;      // top of the city center (0.14 * 80)
    private const float CityRadiusHalf = 50f;

    // ── Materials ─────────────────────────────────────────────────────────────
    private static Material _metal;
    private static Material _glass;
    private static Material _cyan;
    private static Material _magenta;
    private static Material _gold;
    private static Material _road;
    private static Material _plaza;

    [MenuItem("Oasis/Build City Core")]
    public static void Build()
    {
        CleanupRoot();

        LoadMaterials();
        GameObject root = new GameObject("Oasis_City");

        BuildElCorazon(root.transform);
        BuildLaCamaraDeJano(root.transform);
        BuildDenseRing(root.transform);
        BuildLosArchivos(root.transform);
        BuildElMercado(root.transform);
        BuildElCauce(root.transform);

        EditorSceneManager.MarkSceneDirty(root.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Oasis] City core built under 'Oasis_City'.");
    }

    static void CleanupRoot()
    {
        GameObject existing = GameObject.Find("Oasis_City");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }
    }

    static void LoadMaterials()
    {
        _metal   = GetOrCreateLit("OasisMetal",       new Color(0.12f, 0.15f, 0.19f), 0.85f, 0.55f, Color.black);
        _glass   = GetOrCreateLit("OasisGlass",       new Color(0.56f, 0.72f, 0.79f), 0.10f, 0.92f, Color.black, transparent: true, alpha: 0.45f);
        _cyan    = GetOrCreateLit("OasisEmissive_Cyan",    new Color(0.02f, 0.06f, 0.08f), 0.6f, 0.6f, new Color(0f, 0.9f, 1f));
        _magenta = GetOrCreateLit("OasisEmissive_Magenta", new Color(0.06f, 0.01f, 0.06f), 0.6f, 0.6f, new Color(1f, 0.18f, 0.97f));
        _gold    = GetOrCreateLit("OasisEmissive_Gold",    new Color(0.07f, 0.06f, 0.01f), 0.7f, 0.5f, new Color(1f, 0.77f, 0.02f));
        _road    = GetOrCreateLit("OasisRoad",        new Color(0.07f, 0.08f, 0.10f), 0.2f, 0.2f, Color.black);
        _plaza   = GetOrCreateLit("OasisPlaza",       new Color(0.78f, 0.82f, 0.86f), 0.3f, 0.6f, Color.black);
    }

    // ── El Corazón: central spire + emissary ring + angular base ─────────────
    static void BuildElCorazon(Transform parent)
    {
        Prim(parent, "Spire", PrimitiveType.Cylinder, v3(0, BaseY + 45f, 0), v3(12, 45f, 12), _glass, _cyan, 0.0f);

        const float baseY = BaseY + 2f;
        Prim(parent, "Plinth", PrimitiveType.Cylinder, v3(0, baseY, 0), v3(28f, 4f, 28f), _metal, null, 0f);

        // Emissary towers in a ring around the spire
        const float ringRadius = 15f;
        for (int i = 0; i < 6; i++)
        {
            float ang = i * Mathf.PI * 2f / 6f;
            Vector3 pos = new Vector3(Mathf.Cos(ang) * ringRadius, BaseY + 27.5f, Mathf.Sin(ang) * ringRadius);
            Prim(parent, $"Emissary_{i}", PrimitiveType.Cylinder, pos - new Vector3(0, 0.1f, 0), v3(5f, 55f, 5f), _metal, (i % 2 == 0) ? _cyan : _magenta, 0f);
        }

        // Angular base rim (8 cubes touching the plinth outer edge)
        const float rimRadius = 15.2f;
        for (int i = 0; i < 8; i++)
        {
            float ang = i * Mathf.PI * 2f / 8f + 0.175f;
            Vector3 pos = new Vector3(Mathf.Cos(ang) * rimRadius, BaseY + 2f, Mathf.Sin(ang) * rimRadius);
            GameObject rim = Prim(parent, $"RimAccent_{i}", PrimitiveType.Cube, pos, v3(3.2f, 3.4f, 3.2f), (i % 2 == 0) ? _cyan : _gold, null, 0f);
            rim.transform.rotation = Quaternion.Euler(0f, -ang * Mathf.Rad2Deg + 90f, 0f);
        }
    }

    // ── La Cámara de Jano: hidden revival crypt under El Corazón (per map) ──
    static void BuildLaCamaraDeJano(Transform parent)
    {
        // Crypt sunk at the SW foot of the central mesa, on the Spire axis,
        // floor below valley level while the rim stays visible.
        const float cX = -24f, cZ = 34f, floorY = 7.6f;

        // Rebirth circle on the crypt floor
        Prim(parent, "JANO_Floor", PrimitiveType.Cylinder, v3(cX, floorY + 0.5f, cZ), v3(9f, 1f, 9f), _metal, null, 0f);
        Prim(parent, "JANO_Ring", PrimitiveType.Cylinder, v3(cX, floorY + 1.05f, cZ), v3(7.2f, 0.16f, 7.2f), _gold, null, 0f);
        Prim(parent, "JANO_Circle", PrimitiveType.Cylinder, v3(cX, floorY + 1.25f, cZ), v3(3.4f, 0.1f, 3.4f), _gold, null, 0f);

        // Low crypt wall (sunk, only the rim clears the valley floor)
        Prim(parent, "JANO_Wall", PrimitiveType.Cylinder, v3(cX, floorY + 0.9f, cZ), v3(11f, 1.8f, 11f), _metal, null, 0f);
        Prim(parent, "JANO_Rim", PrimitiveType.Cylinder, v3(cX, floorY + 2.05f, cZ), v3(11.6f, 0.32f, 11.6f), _gold, null, 0f);

        // Guardian columns inside the ring
        for (int i = 0; i < 4; i++)
        {
            float ang = i * Mathf.PI / 2f + Mathf.PI / 4f;
            float px = cX + Mathf.Cos(ang) * 3.6f;
            float pz = cZ + Mathf.Sin(ang) * 3.6f;
            Prim(parent, $"JANO_Column_{i}", PrimitiveType.Cylinder, v3(px, floorY + 2.1f, pz), v3(0.9f, 2.2f, 0.9f), _gold, null, 0f);
        }

        // Steps down into the crypt from the south approach
        for (int s = 0; s < 8; s++)
        {
            float z = 47f - s * 1.1f;
            float y = 9.75f - s * 0.26f;
            Prim(parent, $"JANO_Step_{s}", PrimitiveType.Cube, v3(cX, y, z), v3(6f, 0.35f, 1.5f), _plaza, null, 0f);
        }
    }

    // ── Dense vertical ring of towers + connecting roads ──────────────────────
    static void BuildDenseRing(Transform parent)
    {
        float[] heights = { 28f, 38f, 44f, 32f, 50f, 40f, 33f, 46f, 36f, 42f, 30f, 47f, 34f, 45f };
        float[] widths  = { 9f, 8f, 11f, 7f, 9f, 10f, 8f, 11f, 8f, 10f, 7f, 11f, 8f, 9f };
        for (int i = 0; i < heights.Length; i++)
        {
            float ang = i * (137.507764f * Mathf.Deg2Rad);          // golden angle spread
            float radius = 24f + (i % 3) * 6f + (i / 3) * 1.5f;
            float w = widths[i];
            float h = heights[i];
            Vector3 pos = new Vector3(Mathf.Cos(ang) * radius, BaseY + h / 2f, Mathf.Sin(ang) * radius);
            Material skin = (i % 3 == 0) ? _metal : _glass;
            GameObject tower = Prim(parent, $"Tower_{i:D2}", PrimitiveType.Cube, pos, v3(w, h, w), skin, null, 0f);
            tower.transform.rotation = Quaternion.Euler(0f, (i * 37f) % 360f, 0f);

            // Emissive crown strip
            Prim(parent, $"Tower_{i:D2}_Crown", PrimitiveType.Cube,
                 pos + new Vector3(0, h / 2f + 0.7f, 0),
                 v3(w - 1f, 1.4f, w - 1f),
                 (i % 3 == 0) ? _cyan : _magenta, null, 0f);
        }

        // Roads: N-S and E-W, with emissive center lines
        Prim(parent, "Road_NS", PrimitiveType.Cube, v3(0f, BaseY + 0.15f, 0f), v3(8f, 0.3f, 96f), _road, null, 0f);
        Prim(parent, "Road_EW", PrimitiveType.Cube, v3(0f, BaseY + 0.15f, 0f), v3(96f, 0.3f, 8f), _road, null, 0f);
        Prim(parent, "Road_NS_Line", PrimitiveType.Cube, v3(0f, BaseY + 0.32f, 0f), v3(0.5f, 0.1f, 96f), _cyan, null, 0f);
        Prim(parent, "Road_EW_Line", PrimitiveType.Cube, v3(0f, BaseY + 0.32f, 0f), v3(96f, 0.1f, 0.5f), _magenta, null, 0f);
    }

    // ── Los Archivos: NE data archive + vaulted hall (per interior map) ──────
    static void BuildLosArchivos(Transform parent)
    {
        const float ax = 31f, az = -16f;      // NE of El Corazón

        // Data hall: low slab
        Prim(parent, "ARCH_Hall", PrimitiveType.Cube, v3(ax, BaseY + 2.5f, az), v3(26f, 5f, 18f), _metal, null, 0f);
        // Vaulted glass roof
        Prim(parent, "ARCH_Vault", PrimitiveType.Cube, v3(ax, BaseY + 8.2f, az), v3(20f, 2.6f, 14f), _glass, null, 0f);
        // Corner pillars (4)
        Prim(parent, "ARCH_Pillar_NW", PrimitiveType.Cube, v3(ax - 11f, BaseY + 10f, az - 7f), v3(2.2f, 16f, 2.2f), _metal, null, 0f);
        Prim(parent, "ARCH_Pillar_NE", PrimitiveType.Cube, v3(ax + 11f, BaseY + 10f, az - 7f), v3(2.2f, 16f, 2.2f), _metal, null, 0f);
        Prim(parent, "ARCH_Pillar_SW", PrimitiveType.Cube, v3(ax - 11f, BaseY + 10f, az + 7f), v3(2.2f, 16f, 2.2f), _metal, null, 0f);
        Prim(parent, "ARCH_Pillar_SE", PrimitiveType.Cube, v3(ax + 11f, BaseY + 10f, az + 7f), v3(2.2f, 16f, 2.2f), _metal, null, 0f);
        // Gold louver band across the entrance facade
        Prim(parent, "ARCH_Louvers", PrimitiveType.Cube, v3(ax, BaseY + 5.1f, az + 9.2f), v3(24f, 3f, 0.8f), _gold, null, 0f);
        // Two data towers flanking the hall
        Prim(parent, "ARCH_Tower_E", PrimitiveType.Cylinder, v3(ax + 14f, BaseY + 18f, az), v3(2.6f, 34f, 2.6f), _glass, _cyan, 0f);
        Prim(parent, "ARCH_Tower_W", PrimitiveType.Cylinder, v3(ax - 14f, BaseY + 18f, az), v3(2.6f, 34f, 2.6f), _glass, _magenta, 0f);
        // Emissive data strips along the hall
        Prim(parent, "ARCH_DataLine_0", PrimitiveType.Cube, v3(ax, BaseY + 12.5f, az), v3(26.6f, 0.6f, 18.6f), _cyan, null, 0f);
        // Hourglass entrance marker (glowing, at the map's southwest approach)
        Prim(parent, "ARCH_Entry", PrimitiveType.Cylinder, v3(ax - 20f, BaseY + 4.5f, az + 2f), v3(3f, 8f, 3f), _glass, _gold, 0f);
    }

    // ── El Mercado: plaza + canopied stalls on the riverbank, south of the core ──
    static void BuildElMercado(Transform parent)
    {
        Vector3 plazaC = new Vector3(-12f, BaseY + 0.05f, 58f);
        // Elevated pad so the plaza sits level above the valley slope
        Prim(parent, "Market_Pad", PrimitiveType.Cube, new Vector3(-12f, 10.2f, 58f), new Vector3(34f, 1.8f, 34f), _road, null, 0f);
        Prim(parent, "Market_Plaza", PrimitiveType.Plane, plazaC, v3(3.4f, 1f, 3.4f), _plaza, null, 0f);
        Prim(parent, "Market_Plaza_Lines", PrimitiveType.Cube, plazaC + new Vector3(0, 0.06f, 14.5f), v3(32f, 0.08f, 0.6f), _magenta, null, 0f);

        // 4 canopied stalls in a 2x2 grid
        for (int gx = 0; gx < 2; gx++)
        {
            for (int gz = 0; gz < 2; gz++)
            {
                Vector3 center = new Vector3(-12f - 9f + gx * 18f, BaseY, 58f - 8f + gz * 16f);
                Material roofMat = (gz == 0) ? _metal : _glass;

                Prim(parent, $"Stall_{gx}{gz}_Roof", PrimitiveType.Cube, center + new Vector3(0, 3.4f, 0), v3(14f, 0.7f, 11f), roofMat, null, 0f);

                float[] lx = { -6f, 6f };
                float[] lz = { -4.5f, 4.5f };
                foreach (float x in lx)
                {
                    foreach (float z in lz)
                    {
                        Prim(parent, $"Stall_{gx}{gz}_Leg", PrimitiveType.Cube, center + new Vector3(x, 1.5f, z), v3(0.8f, 3f, 0.8f), _metal, null, 0f);
                    }
                }

                // Emissive stall light under the canopy
                Prim(parent, $"Stall_{gx}{gz}_Light", PrimitiveType.Cube, center + new Vector3(0, 2.9f, 0), v3(7f, 0.25f, 4.5f), _gold, null, 0f);
            }
        }

        // Scattered light posts around the plaza
        for (int i = 0; i < 8; i++)
        {
            float ang = i * Mathf.PI * 2f / 8f + 0.3f;
            float r = 16f + (i % 2) * 3f;
            Vector3 pos = new Vector3(-12f + Mathf.Cos(ang) * r, BaseY, 58f + Mathf.Sin(ang) * r);
            Prim(parent, $"MerLamp_{i}", PrimitiveType.Cube, pos + new Vector3(0, 3.2f, 0), v3(0.5f, 3.4f, 0.5f), _metal, null, 0f);
            Prim(parent, $"MerLamp_{i}_Head", PrimitiveType.Cube, pos + new Vector3(0, 5.1f, 0), v3(1.4f, 0.5f, 1.4f), (i % 2 == 0) ? _magenta : _gold, null, 0f);
        }
    }

    // ── El Cauce: river promenade hugging the west/north edge between core and river ──
    static void BuildElCauce(Transform parent)
    {
        // Walkway: north, central and south segments at the platform edge facing the river
        Vector3[] decks = { new Vector3(-26f, BaseY + 0.1f, -55f), new Vector3(4.5f, 0.2f, 30f),
                            new Vector3(-30f, BaseY + 0.1f, -25f), new Vector3(4.5f, 0.2f, 42f),
                            new Vector3(-33f, BaseY + 0.1f,  20f), new Vector3(4.5f, 0.2f, 36f) };
        for (int i = 0; i < 3; i++)
        {
            Prim(parent, $"CauceDeck_{i}", PrimitiveType.Cube, decks[i * 2], decks[i * 2 + 1], _road, null, 0f);
        }
        // Emissive railing strips facing the river
        Prim(parent, "Cauce_Rail_N", PrimitiveType.Cube, new Vector3(-28f, BaseY + 0.9f, -55f), new Vector3(0.25f, 1.6f, 30f), _cyan, null, 0f);
        Prim(parent, "Cauce_Rail_C", PrimitiveType.Cube, new Vector3(-32f, BaseY + 0.9f, -25f), new Vector3(0.25f, 1.6f, 42f), _cyan, null, 0f);
        Prim(parent, "Cauce_Rail_S", PrimitiveType.Cube, new Vector3(-35f, BaseY + 0.9f, 20f), new Vector3(0.25f, 1.6f, 36f), _cyan, null, 0f);

        // Lamp posts along the promenade
        float[] lampZ = { -68f, -60f, -52f, -44f, -36f, -24f, -12f, 0f, 8f, 20f, 31f, 37f };
        for (int i = 0; i < lampZ.Length; i++)
        {
            float z = lampZ[i];
            float lx = z < -38f ? -26f : (z < 2f ? -30f : -33f);
            Prim(parent, $"CauceLamp_{i}", PrimitiveType.Cube, new Vector3(lx, BaseY + 1.6f, z), new Vector3(0.45f, 3.2f, 0.45f), _metal, null, 0f);
            Prim(parent, $"CauceLamp_{i}_Head", PrimitiveType.Cube, new Vector3(lx - 1.1f, BaseY + 3.6f, z), new Vector3(1.6f, 0.4f, 0.4f), _gold, null, 0f);
        }

        // ── Water mills on the river (per map: mills, fishing, internal transport) ──
        float[] millZ = { 8f, -16f };
        for (int m = 0; m < millZ.Length; m++)
        {
            float mz = millZ[m];
            Vector3 wheelC = new Vector3(-53f, 10.6f, mz);
            // Mill hut on the riverside promenade edge
            Prim(parent, $"Mill_{m}_Hut", PrimitiveType.Cube, new Vector3(-46f, BaseY + 2.75f, mz), new Vector3(5.5f, 5.5f, 5f), _metal, null, 0f);
            Prim(parent, $"Mill_{m}_Roof", PrimitiveType.Cube, new Vector3(-46f, BaseY + 5.8f, mz), new Vector3(6.2f, 1.1f, 5.6f), _metal, null, 14f);
            Prim(parent, $"Mill_{m}_Lamp", PrimitiveType.Cube, new Vector3(-46f, BaseY + 6.7f, mz), new Vector3(1.2f, 0.5f, 1.2f), _gold, null, 0f);
            // Axle from the hut to the wheel hub
            Prim(parent, $"Mill_{m}_Axle", PrimitiveType.Cube, new Vector3(-49.5f, 10.6f, mz), new Vector3(3.2f, 0.5f, 0.5f), _metal, null, 0f);
            // Vertical paddle wheel (disk + 6 paddles) dipping into the water
            GameObject disc = Prim(parent, $"Mill_{m}_Wheel", PrimitiveType.Cylinder, wheelC, v3(6.2f, 0.45f, 6.2f), _metal, null, 0f);
            disc.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            for (int p = 0; p < 6; p++)
            {
                float a = p * 60f + 30f;
                float rad = a * Mathf.Deg2Rad;
                GameObject paddle = Prim(parent, $"Mill_{m}_Paddle_{p}", PrimitiveType.Cube,
                    wheelC + new Vector3(0f, Mathf.Cos(rad) * 2.9f, Mathf.Sin(rad) * 2.9f),
                    v3(0.5f, 1.3f, 0.35f), _metal, null, 0f);
                paddle.transform.rotation = Quaternion.Euler(0f, 0f, a);
            }
        }

        // ── Fishing dock + boats (map: pesca, transporte interno) ──────────────
        Prim(parent, "Dock_Deck", PrimitiveType.Cube, new Vector3(-40f, 8.9f, -48f), new Vector3(6f, 0.45f, 11f), _plaza, null, 0f);
        float[] dockPole = { -1f, 1f };
        foreach (float off in dockPole)
        {
            Prim(parent, "Dock_Pole", PrimitiveType.Cube,
                 new Vector3(-42f, 7.4f, -48f + off * 4.5f), new Vector3(0.35f, 2f, 0.35f), _metal, null, 0f);
        }
        float[,] boat = { { -44f, -43.5f, -14f }, { -43.5f, -53f, 20f } };
        for (int b = 0; b < boat.GetLength(0); b++)
        {
            float bx = boat[b, 0], bz = boat[b, 1], yaw = boat[b, 2];
            GameObject hull = Prim(parent, $"Boat_{b}_Hull", PrimitiveType.Cube,
                new Vector3(bx, 8.55f, bz), new Vector3(2.2f, 0.9f, 5.6f), _metal, null, 0f);
            hull.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            GameObject cabin = Prim(parent, $"Boat_{b}_Cabin", PrimitiveType.Cube,
                new Vector3(bx, 9.4f, bz), new Vector3(1.5f, 0.9f, 2.1f), _glass, null, 0f);
            cabin.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
        Prim(parent, "Dock_Mooring", PrimitiveType.Cube, new Vector3(-45.5f, 9.4f, -48f), new Vector3(0.4f, 1.4f, 0.4f), _metal, null, 0f);
        Prim(parent, "Dock_Mooring_Light", PrimitiveType.Cube, new Vector3(-45.5f, 10.15f, -48f), new Vector3(1.1f, 0.35f, 1.1f), _gold, null, 0f);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    static Vector3 v3(float x, float y, float z)
    {
        return new Vector3(x, y, z);
    }

    static GameObject Prim(Transform parent, string name, PrimitiveType type,
                           Vector3 pos, Vector3 scale, Material mat, Material crown, float _ignore)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        Renderer r = go.GetComponent<Renderer>();
        if (mat != null)
        {
            r.sharedMaterial = mat;
        }
        return go;
    }

    static Material GetOrCreateLit(string name, Color baseColor, float metallic,
                                   float smoothness, Color emission, bool transparent = false, float alpha = 1f)
    {
        string path = $"Assets/Materials/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[Oasis] URP Lit shader not found.");
                return null;
            }
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);

        if (transparent)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_AlphaClip", 0f);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        bool emissive = emission.maxColorComponent > 0.01f;
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        if (emissive)
        {
            mat.SetColor("_EmissionColor", emission);
            mat.EnableKeyword("_EMISSION");
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }
}