using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Solidify pass: removes leftover marker planes, grounds every building with
/// a visible footing pedestal and adds fine detail props across all zones so
/// nothing reads as a floating primitive. Idempotent — safe to run repeatedly.
/// </summary>
public static class OasisSolidity
{
    static Material _white, _metal, _glass, _panel, _brick, _asphalt, _amber, _cyan, _gold, _field, _cable, _magenta;

    const float CB = 9.5f, FB = 11f, LB = 10f, PB = 14f;

    [MenuItem("Oasis/Add Solidity Detail")]
    public static void BuildAll()
    {
        LoadMats();
        EnsureFolder("Assets/Materials/Generated");

        RemovePlatformMarker();
        FootingForAll();
        RoadCurbsAndLamps();
        BarrioDetail();
        ForjaDetail();
        CamposDetail();
        CuspideDetail();
        ElPasoDetail();
        BridgeRails();

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkAllScenesDirty();
        EditorApplication.RepaintProjectWindow();
        Debug.Log("[Oasis] Solidity detail pass complete.");
    }

    [MenuItem("Oasis/Remove Platform Marker")]
    public static void RemovePlatformMarker()
    {
        GameObject marker = GameObject.Find("City Platform (marker)");
        if (marker == null)
        {
            Debug.Log("[Oasis] No platform marker found.");
            return;
        }
        UnityEngine.Object.DestroyImmediate(marker);
        Debug.Log("[Oasis] Removed 'City Platform (marker)'.");
    }

    // ── Materials ───────────────────────────────────────────────────────────

    static void LoadMats()
    {
        _white  = Material("Mat_Cable", new Color(0.92f, 0.90f, 0.86f), 0.25f, 0.7f);
        _metal  = Material("Mat_Cable", new Color(0.62f, 0.62f, 0.66f), 0.9f, 0.6f);
        _glass  = Material("Mat_Cable", new Color(0.15f, 0.22f, 0.28f), 0.1f, 0.9f);
        _panel  = Material("Mat_Cable", new Color(0.46f, 0.5f, 0.55f), 0.5f, 0.55f);
        _brick  = Material("Mat_Cable", new Color(0.52f, 0.33f, 0.2f), 0.2f, 0.35f);
        _asphalt= Material("Mat_Cable", new Color(0.13f, 0.13f, 0.14f), 0.2f, 0.3f);
        _amber  = Material("Mat_Cable", new Color(0.35f, 0.14f, 0.03f), 0f, 0.5f);
        _cyan   = Material("Mat_Cable", new Color(0f, 0.85f, 1f), 0f, 0.6f);
        _gold   = Material("Mat_Cable", new Color(1f, 0.78f, 0.15f), 0f, 0.5f);
        _magenta = Material("Mat_Cable", new Color(1f, 0.1f, 0.7f), 0f, 0.6f);
        _field  = Material("Mat_Cable", new Color(0.18f, 0.3f, 0.13f), 0.1f, 0.3f);
        _cable  = Material("Mat_Cable", new Color(0.03f, 0.03f, 0.04f), 0.8f, 0.4f);

        // Prefer the real generated assets when they exist.
        Assign(ref _white,  "Assets/Materials/Generated/Mat_WhiteElite.mat");
        Assign(ref _metal,  "Assets/Materials/OasisMetal.mat");
        Assign(ref _glass,  "Assets/Materials/OasisGlass.mat");
        Assign(ref _panel,  "Assets/Materials/Generated/Mat_Panel.mat");
        Assign(ref _brick,  "Assets/Materials/Generated/Mat_Brick.mat");
        Assign(ref _asphalt,"Assets/Materials/Generated/Mat_Asphalt.mat");
        Assign(ref _amber,  "Assets/Materials/Generated/Mat_Emissive_Amber.mat");
        Assign(ref _cyan,   "Assets/Materials/OasisEmissive_Cyan.mat");
        Assign(ref _gold,   "Assets/Materials/OasisEmissive_Gold.mat");
        Assign(ref _magenta,"Assets/Materials/OasisEmissive_Magenta.mat");
    }

    static void Assign(ref Material m, string path)
    {
        Material asset = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (asset != null) m = asset;
    }

    static Material Material(string key, Color col, float metallic, float smooth)
    {
        if (key == "Mat_Cable")
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Generated/Mat_Cable.mat");
            if (m != null) return m;
        }
        string path = $"Assets/Materials/Generated/{key}.mat";
        Material n = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (n == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            n = new Material(s) { name = key };
            AssetDatabase.CreateAsset(n, path);
        }
        n.color = col;
        n.SetFloat("_Metallic", metallic);
        n.SetFloat("_Smoothness", smooth);
        return n;
    }

    static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;
        string parent = folderPath.Substring(0, folderPath.LastIndexOf('/'));
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderPath.Substring(folderPath.LastIndexOf('/') + 1));
    }

    // ── Primitives ──────────────────────────────────────────────────────────

    static GameObject P(Transform parent, string name, PrimitiveType type,
                        Vector3 pos, Vector3 scl, Material m, float eulerY = 0f, float eulerX = 0f)
    {
        if (parent.Find(name) != null) return null;
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scl;
        if (eulerY != 0f || eulerX != 0f)
            go.transform.localRotation = Quaternion.Euler(eulerX, eulerY, 0f);
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    // ── 1. Footing pedestals under every large structure ─────────────────────

    static readonly string[] FootingOurs =
    {
        "Tower_", "Emissary_", "Warehouse_", "Barn_", "Villa_", "Block_", "Spire",
        "Silo_", "Stack_", "Tank_", "Furnace", "Obs_Shaft", "Pylon_",
    };

    static void FootingForAll()
    {
        foreach (GameObject target in UnityEngine.Object.FindObjectsOfType<GameObject>(true))
        {
            if (target.transform.parent == null) continue;
            string n = target.name;
            bool match = Array.Exists(FootingOurs, p => n.StartsWith(p) || n == p);
            if (!match) continue;

            // Buildings were built centred on (base + h/2).
            Vector3 s = target.transform.localScale;
            float h = Mathf.Abs(s.y);
            Vector3 bottom = target.transform.position - Vector3.up * (h / 2f);

            // A cylinder primitive has a round-ish collider → circular footing.
            bool isCyl = n.StartsWith("Silo") || n.StartsWith("Stack") || n.StartsWith("Tank")
                      || n == "Spire";

            Material baseMat = n.StartsWith("Villa") || n.StartsWith("Silo") ? _white : _panel;
            if (target.transform.Find("Footing") != null) continue;

            float w = Mathf.Abs(s.x) * 1.16f;
            float d = isCyl ? w : Mathf.Abs(s.z) * 1.12f;
            float fh = Mathf.Clamp(h * 0.08f, 0.6f, 1.6f);
            GameObject foot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            foot.name = "Footing";
            foot.transform.SetParent(target.transform, false);
            foot.transform.position = bottom - Vector3.up * (fh / 2f) + Vector3.up * 0.02f;
            foot.transform.localScale = new Vector3(w, fh, d);
            foot.GetComponent<Renderer>().sharedMaterial = baseMat;
        }
    }

    // ── 2. Road curbs, markings and street lamps ─────────────────────────────

    static void RoadCurbsAndLamps()
    {
        Transform red = GameObject.Find("Red_Vial")?.transform;
        if (red != null)
        {
            // Curbs along each road segment
            string[] roads = { "Road_N1", "Road_N2", "Road_S1", "Road_S2", "Road_W" };
            foreach (string rn in roads)
            {
                Transform rd = red.Find(rn);
                if (rd == null) continue;
                Vector3 pos = rd.position;
                Vector3 scl = rd.localScale;
                // Road length runs along Z (except Road_W which runs along X)
                bool alongX = rn == "Road_W";
                float half = (alongX ? scl.x : scl.z) / 2f;
                Vector3 c1 = pos + (alongX ? Vector3.forward : Vector3.right) * Mathf.Abs((alongX ? scl.z : scl.x)) * 0.55f;
                Vector3 c2 = pos - (alongX ? Vector3.forward : Vector3.right) * Mathf.Abs((alongX ? scl.z : scl.x)) * 0.55f;
                P(red, rn + "_CurbL", PrimitiveType.Cube, c1 + Vector3.up * 0.25f,
                    alongX ? new Vector3(half * 2f, 0.45f, 0.5f) : new Vector3(0.5f, 0.45f, half * 2f), _panel);
                P(red, rn + "_CurbR", PrimitiveType.Cube, c2 + Vector3.up * 0.25f,
                    alongX ? new Vector3(half * 2f, 0.45f, 0.5f) : new Vector3(0.5f, 0.45f, half * 2f), _panel);
                // Centre dashes
                for (float t = -half + 3f; t < half - 2f; t += 6f)
                {
                    Vector3 dp = pos + (alongX ? Vector3.right : Vector3.forward) * t + Vector3.up * 0.28f;
                    P(red, rn + "_Dash", PrimitiveType.Cube, dp,
                        alongX ? new Vector3(2.4f, 0.08f, 0.55f) : new Vector3(0.55f, 0.08f, 2.4f), _amber);
                }
            }
        }

        // Street lamps along the N–S spine and the west bridge
        Transform city = GameObject.Find("Oasis_City")?.transform;
        if (city != null)
        {
            float[] nz = { -99f, -123f };
            float[] sz = { 50f, 96f, 142f };
            foreach (float z in nz)
                StreetLamp(city, "SpineLampN_" + Mathf.RoundToInt(z), new Vector3(5.4f, 10.4f, z));
            foreach (float z in sz)
                StreetLamp(city, "SpineLampS_" + Mathf.RoundToInt(z), new Vector3(5.4f, 10.0f, z));
        }
        if (red != null)
        {
            StreetLamp(red, "BridgeLamp_L", new Vector3(-48f, 11.6f, -23.6f));
            StreetLamp(red, "BridgeLamp_R", new Vector3(-48f, 11.6f, -36.4f));
        }
    }

    static void StreetLamp(Transform parent, string name, Vector3 basePos)
    {
        if (P(parent, name, PrimitiveType.Cube, basePos, new Vector3(0.35f, 6.2f, 0.35f), _metal) == null) return;
        P(parent, name + "_Arm", PrimitiveType.Cube, basePos + new Vector3(1.4f, 6.0f, 0f), new Vector3(2.8f, 0.3f, 0.3f), _metal);
        P(parent, name + "_Head", PrimitiveType.Cube, basePos + new Vector3(3.0f, 6.0f, 0f), new Vector3(1.1f, 0.4f, 0.4f), _amber);
        P(parent, name + "_Brace", PrimitiveType.Cube, basePos + new Vector3(1.4f, 3.2f, 0f), new Vector3(1.5f, 0.22f, 0.22f), _metal);
    }

    // ── 3. Bridge guard rails ────────────────────────────────────────────────

    static void BridgeRails()
    {
        Transform red = GameObject.Find("Red_Vial")?.transform;
        if (red == null) return;
        // Deck runs along X at y 11.4 on top of Road_W/Bridge
        float[] xs = { -70f, -62f, -90f, -98f };
        foreach (float x in xs)
        {
            if (P(red, "Rail_" + x, PrimitiveType.Cube, new Vector3(x, 12.9f, -25.6f), new Vector3(0.4f, 1.2f, 0.4f), _metal) != null)
            {
                P(red, "RailCableL_" + Mathf.RoundToInt(x), PrimitiveType.Cube, new Vector3(x, 13.3f, -26.3f), new Vector3(0.4f, 0.12f, 8f), _cable);
            }
        }
    }

    // ── 4. Barrio props: roof AC + antennas + perimeter shanty walls ─────────

    static void BarrioDetail()
    {
        Transform z = GameObject.Find("Zona_BarrioBajo")?.transform;
        if (z == null) return;
        // Rooftop units on the brick blocks
        Transform[] roofs = { z.Find("Block_0_Roof"), z.Find("Block_2_Roof"), z.Find("Block_4_Roof") };
        for (int i = 0; i < roofs.Length && roofs[i] != null; i++)
        {
            Vector3 rp = roofs[i].position;
            Vector3 rs = roofs[i].localScale;
            P(z, $"AC_{i}_A", PrimitiveType.Cube, rp + new Vector3(-rs.x * 0.26f, rs.y * 0.55f, -rs.z * 0.22f),
                new Vector3(2.2f, 1.6f, 1.8f), _metal);
            P(z, $"AC_{i}_B", PrimitiveType.Cube, rp + new Vector3(rs.x * 0.26f, rs.y * 0.55f, -rs.z * 0.22f),
                new Vector3(2.2f, 1.6f, 1.8f), _panel);
            P(z, $"Chimney_{i}", PrimitiveType.Cylinder, rp + new Vector3(rs.x * 0.2f, rs.y * 0.85f, rs.z * 0.2f),
                new Vector3(0.9f, 2.4f, 0.9f), _brick);
        }
        // Antenna mast on the last block
        Transform lastRoof = z.Find("Block_4_Roof");
        if (lastRoof != null)
        {
            Vector3 rp = lastRoof.position;
            P(z, "Antenna", PrimitiveType.Cylinder, rp + Vector3.up * 2.4f, new Vector3(0.25f, 4.5f, 0.25f), _metal);
            P(z, "Antenna_Glow", PrimitiveType.Cube, rp + Vector3.up * 5.1f, new Vector3(0.6f, 0.25f, 0.6f), _magenta);
        }
        // Perimeter wall around the pad west/south edge
        P(z, "Wall_W", PrimitiveType.Cube, new Vector3(-153.5f, CB + 1.1f, -45f), new Vector3(0.7f, 2.2f, 74f), _brick);
        P(z, "Wall_N", PrimitiveType.Cube, new Vector3(-125f, CB + 1.1f, -8f), new Vector3(56f, 2.2f, 0.7f), _brick);
        P(z, "GateFront", PrimitiveType.Cube, new Vector3(-125f, CB + 1.7f, -10.4f), new Vector3(3.6f, 3.4f, 0.6f), _panel);
    }

    // ── 5. Forja props: coil piles, spare pipe racks, crane brace ────────────

    static void ForjaDetail()
    {
        Transform z = GameObject.Find("Zona_LaForja")?.transform;
        if (z == null) return;
        // Steel coil piles beside warehouse 0 and 5
        P(z, "Coil_0", PrimitiveType.Cylinder, new Vector3(-39f, FB + 1.0f, -132f), new Vector3(3.2f, 2.0f, 3.2f), _glass);
        P(z, "Coil_1", PrimitiveType.Cylinder, new Vector3(-39f, FB + 2.2f, -132f), new Vector3(2.4f, 1.4f, 2.4f), _glass);
        P(z, "Coil_2", PrimitiveType.Cylinder, new Vector3(-30f, FB + 1.0f, -134f), new Vector3(2.6f, 2.0f, 2.6f), _glass);
        // Pipe rack near the furnace
        P(z, "PipeRack_Leg_A", PrimitiveType.Cube, new Vector3(20f, FB + 0.5f, -124f), new Vector3(0.4f, 1.0f, 0.4f), _metal);
        P(z, "PipeRack_Leg_B", PrimitiveType.Cube, new Vector3(29f, FB + 0.5f, -124f), new Vector3(0.4f, 1.0f, 0.4f), _metal);
        P(z, "PipeRack_Bar_1", PrimitiveType.Cube, new Vector3(24.5f, FB + 1.4f, -124f), new Vector3(10f, 0.6f, 0.6f), _metal);
        P(z, "PipeRack_Bar_2", PrimitiveType.Cube, new Vector3(24.5f, FB + 2.3f, -124f), new Vector3(9f, 0.5f, 0.5f), _metal);
        P(z, "PipeRack_Bar_3", PrimitiveType.Cube, new Vector3(24.5f, FB + 3.0f, -124f), new Vector3(8f, 0.5f, 0.5f), _metal);
        // Diagonal brace under the crane arm (solidity!)
        Transform arm = z.Find("Crane_Arm");
        if (arm != null)
        {
            Vector3 ap = arm.position;
            P(z, "Crane_Brace", PrimitiveType.Cube, new Vector3(ap.x + 9f, FB + 9.5f, ap.z),
                new Vector3(0.35f, 11f, 0.35f), _metal, 0f, 26f);
        }
        // Lamp post at the pad entrance
        StreetLamp(z, "ForjaLamp", new Vector3(-36f, FB + 0.2f, -78f));
    }

    // ── 6. Campos: fence posts, hay bales, water tower ───────────────────────

    static void CamposDetail()
    {
        Transform z = GameObject.Find("Zona_Campos")?.transform;
        if (z == null) return;
        float[,] fd = { { -60f, 95f, 15f, 10f }, { 25f, 115f, 14f, 9f }, { -25f, 150f, 15f, 10f } };
        for (int i = 0; i < fd.GetLength(0); i++)
        {
            float x = fd[i, 0], zC = fd[i, 1], w = fd[i, 2], d = fd[i, 3];
            FenceL(z, $"FencePost_{i}_A", new Vector3(x - w / 2f - 1.5f, LB + 0.7f, zC - d / 2f - 1.5f));
            FenceL(z, $"FencePost_{i}_B", new Vector3(x + w / 2f + 1.5f, LB + 0.7f, zC - d / 2f - 1.5f));
            FenceL(z, $"FencePost_{i}_C", new Vector3(x - w / 2f - 1.5f, LB + 0.7f, zC + d / 2f + 1.5f));
            FenceL(z, $"FencePost_{i}_D", new Vector3(x + w / 2f + 1.5f, LB + 0.7f, zC + d / 2f + 1.5f));
            FenceRail(z, $"FenceRail_{i}_A", new Vector3(x, LB + 0.9f, zC - d / 2f - 1.5f), new Vector3(w + 3f, 0.16f, 0.16f));
            FenceRail(z, $"FenceRail_{i}_B", new Vector3(x, LB + 0.9f, zC + d / 2f + 1.5f), new Vector3(w + 3f, 0.16f, 0.16f));
        }
        // Hay bales beside the barns
        float[,] barns = { { 15f, 75f }, { -15f, 175f } };
        for (int i = 0; i < barns.GetLength(0); i++)
        {
            float bx = barns[i, 0], bz = barns[i, 1];
            for (int k = 0; k < 3; k++)
            {
                P(z, $"Hay_{i}_{k}", PrimitiveType.Cylinder, new Vector3(bx + 9f, LB + 0.8f, bz - 3f + k * 2.2f),
                    new Vector3(1.8f, 1.1f, 1.8f), _gold, 0f, 90f);
            }
        }
        // Water tower near silos
        Transform silo0 = z.Find("Silo_0");
        if (silo0 != null)
        {
            P(z, "WaterTower_Leg_A", PrimitiveType.Cube, new Vector3(50f, LB + 0.6f, 90f), new Vector3(0.5f, 1.2f, 0.5f), _metal);
            P(z, "WaterTower_Leg_B", PrimitiveType.Cube, new Vector3(56f, LB + 0.6f, 90f), new Vector3(0.5f, 1.2f, 0.5f), _metal);
            P(z, "WaterTower_Shaft", PrimitiveType.Cylinder, new Vector3(53f, LB + 3.2f, 90f), new Vector3(2.2f, 4f, 2.2f), _panel);
            P(z, "WaterTower_Tank", PrimitiveType.Cylinder, new Vector3(53f, LB + 7f, 90f), new Vector3(5.5f, 2.6f, 5.5f), _metal);
            P(z, "WaterTower_TankRing", PrimitiveType.Cylinder, new Vector3(53f, LB + 8.6f, 90f), new Vector3(5.8f, 0.5f, 5.8f), _amber);
        }
    }

    static void FenceL(Transform parent, string name, Vector3 pos)
    {
        P(parent, name, PrimitiveType.Cube, pos, new Vector3(0.18f, 1.1f, 0.18f), _white);
    }
    static void FenceRail(Transform parent, string name, Vector3 pos, Vector3 scl)
    {
        P(parent, name, PrimitiveType.Cube, pos, scl, _metal);
    }

    // ── 7. Cúspide: hedges, pool slab, pergola columns ──────────────────────

    static void CuspideDetail()
    {
        Transform z = GameObject.Find("Zona_LaCuspide")?.transform;
        if (z == null) return;
        float[,] ter = { { 115f, -195f, 26f, 20f, 30f }, { 150f, -212f, 24f, 18f, 36f }, { 165f, -178f, 28f, 20f, 42f } };
        for (int i = 0; i < ter.GetLength(0); i++)
        {
            float x = ter[i, 0], zC = ter[i, 1], w = ter[i, 2], d = ter[i, 3], y = ter[i, 4];
            // Hedges along the front edge of each terrace
            Hedgerow(z, $"Hedge_{i}", new Vector3(x, y + 0.45f, zC - d / 2f + 1.2f), w - 6f);
            // Pool slab on terrace 0
            if (i == 0)
            {
                P(z, "Pool_Slab", PrimitiveType.Cube, new Vector3(x - 4f, y + 0.28f, zC + d * 0.18f), new Vector3(9f, 0.25f, 6f), _white);
                P(z, "Pool_Water", PrimitiveType.Cube, new Vector3(x - 4f, y + 0.55f, zC + d * 0.18f), new Vector3(8f, 0.3f, 5f), _cyan);
                P(z, "Pool_Rim", PrimitiveType.Cube, new Vector3(x - 4f, y + 0.5f, zC + d * 0.18f), new Vector3(9.6f, 0.2f, 6.6f), _glass);
            }
            // Pergola columns around the terrace 2 villa
            if (i == 2)
            {
                for (int k = 0; k < 4; k++)
                {
                    float offset = (k % 2) * 10f - 5f;
                    P(z, $"PergolaCol_{k}", PrimitiveType.Cube, new Vector3(x + offset, y + 2.6f, zC - d / 2f + 1.2f),
                        new Vector3(0.4f, 5.2f, 0.4f), _white);
                }
                P(z, "PergolaTop", PrimitiveType.Cube, new Vector3(x, y + 5.4f, zC - d / 2f + 1.2f), new Vector3(12f, 0.35f, 0.4f), _amber);
            }
        }
        // Planter boxes flanking the observatory
        P(z, "Planter_A", PrimitiveType.Cube, new Vector3(131f, 33.9f, -238f), new Vector3(4f, 1.2f, 2f), _white);
        P(z, "Planter_A_Soil", PrimitiveType.Cube, new Vector3(131f, 34.7f, -238f), new Vector3(3.6f, 0.5f, 1.6f), _field);
        P(z, "Planter_B", PrimitiveType.Cube, new Vector3(149f, 33.9f, -238f), new Vector3(4f, 1.2f, 2f), _white);
        P(z, "Planter_B_Soil", PrimitiveType.Cube, new Vector3(149f, 34.7f, -238f), new Vector3(3.6f, 0.5f, 1.6f), _field);
    }

    static void Hedgerow(Transform parent, string name, Vector3 pos, float len)
    {
        P(parent, name, PrimitiveType.Cube, pos, new Vector3(len, 0.9f, 0.6f), _field);
    }

    // ── 8. El Paso: retaining walls + gatehouses ────────────────────────────

    static void ElPasoDetail()
    {
        Transform z = GameObject.Find("Zona_ElPaso")?.transform;
        if (z == null) return;
        // Low retaining walls running west/east from each pylon
        P(z, "WallL", PrimitiveType.Cube, new Vector3(-30f, PB + 1.3f, -241.5f), new Vector3(24f, 2.6f, 1.4f), _panel);
        P(z, "WallR", PrimitiveType.Cube, new Vector3(30f, PB + 1.3f, -241.5f), new Vector3(24f, 2.6f, 1.4f), _panel);
        P(z, "WallL_Glow", PrimitiveType.Cube, new Vector3(-30f, PB + 2.7f, -241.7f), new Vector3(24f, 0.25f, 0.3f), _cyan);
        P(z, "WallR_Glow", PrimitiveType.Cube, new Vector3(30f, PB + 2.7f, -241.7f), new Vector3(24f, 0.25f, 0.3f), _cyan);
        // Small gatehouses flanking the arch
        P(z, "Gatehouse_L", PrimitiveType.Cube, new Vector3(-46f, PB + 1.8f, -243f), new Vector3(4f, 3.6f, 3f), _panel);
        P(z, "Gatehouse_R", PrimitiveType.Cube, new Vector3(46f, PB + 1.8f, -243f), new Vector3(4f, 3.6f, 3f), _panel);
        P(z, "Gatehouse_L_Light", PrimitiveType.Cube, new Vector3(-46f, PB + 3.4f, -243.6f), new Vector3(1.6f, 1.1f, 0.3f), _amber);
        P(z, "Gatehouse_R_Light", PrimitiveType.Cube, new Vector3(46f, PB + 3.4f, -243.6f), new Vector3(1.6f, 1.1f, 0.3f), _amber);
    }
}