using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Detail pass: adds props that sell each district (safe prefixes so the
/// Cleanup pass never removes them). Idempotent: rebuilds under "Oasis_Detail".
/// </summary>
public static class OasisDetail
{
    // District base levels (must match OasisZoneBuilder / OasisCityGenerator)
    const float BaseY = 11.2f;   // city centre
    const float CB = 9.5f;       // El Barrio Bajo
    const float FB = 11f;        // La Forja
    const float LB = 10f;        // Los Campos
    const float PB = 14f;        // El Paso

    static Material _metal;
    static Material _glass;
    static Material _cyan;
    static Material _magenta;
    static Material _gold;

    [MenuItem("Oasis/Detail Structures")]
    public static void Build()
    {
        GameObject existing = GameObject.Find("Oasis_Detail");
        if (existing != null) Object.DestroyImmediate(existing);
        GameObject root = new GameObject("Oasis_Detail");

        LoadMaterials();

        WindowGlows(root.transform);
        Sig_Corazon(root.transform);
        Vent_StackSteam(root.transform);
        Door_ForgeGate(root.transform);
        Arch_Ring(root.transform);
        Awnings(root.transform);
        Sig_NeonBarrio(root.transform);
        Lamp_Field(root.transform);
        Lamp_Villa(root.transform);
        Stat_Observatory(root.transform);
        Banner_Gate(root.transform);
        Lamp_Gate(root.transform);
        Lamp_Deck(root.transform);
        Sig_Quay(root.transform);

        EditorSceneManager.MarkSceneDirty(root.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Oasis] Detail structures added under 'Oasis_Detail'.");
    }

    static void LoadMaterials()
    {
        _metal   = Load("Assets/Materials/OasisMetal.mat");
        _glass   = Load("Assets/Materials/OasisGlass.mat");
        _cyan    = Load("Assets/Materials/OasisEmissive_Cyan.mat");
        _magenta = Load("Assets/Materials/OasisEmissive_Magenta.mat");
        _gold    = Load("Assets/Materials/OasisEmissive_Gold.mat");
    }

    static Material Load(string path)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) Debug.LogWarning($"[Oasis] Missing material: {path}");
        return m;
    }

    // Lit window bands on the landmark towers of the dense ring
    static void WindowGlows(Transform parent)
    {
        string[] towers = { "Tower_02", "Tower_05", "Tower_08", "Tower_11" };
        for (int i = 0; i < towers.Length; i++)
        {
            GameObject tower = GameObject.Find("Oasis_City/" + towers[i]);
            if (tower == null) continue;
            Transform t = tower.transform;
            Vector3 s = t.localScale;
            float bandY = -s.y * 0.22f;          // below the crown
            float bandW = Mathf.Max(1.5f, s.x * 0.6f);
            Prim(parent, $"Win_{towers[i]}" + "_BandA", PrimitiveType.Cube,
                 t.position + t.up * s.y * 0.22f + t.forward * (s.z * 0.5f + 0.05f),
                 v3(bandW, s.y * 0.28f, 0.2f), i % 2 == 0 ? _cyan : _magenta);
            Prim(parent, $"Win_{towers[i]}" + "_BandB", PrimitiveType.Cube,
                 t.position - t.up * s.y * 0.22f + t.forward * (s.z * 0.5f + 0.05f),
                 v3(bandW, s.y * 0.28f, 0.2f), i % 2 == 0 ? _cyan : _magenta);
        }
    }

    // Hanging golden marker over the market plaza
    static void Sig_Corazon(Transform parent)
    {
        Prim(parent, "Sig_Corazon", PrimitiveType.Cube,
             new Vector3(-12f, BaseY + 9f, 50f), v3(6f, 1.4f, 0.3f), _gold);
    }

    // Steam plume ring over the northern smokestack
    static void Vent_StackSteam(Transform parent)
    {
        string name = "Zona_LaForja/Stack_2";
        Transform stack = GameObject.Find(name)?.transform;
        if (stack == null) return;
        Vector3 p = stack.position;                 // (22, FB + 11, -86), h = 22
        Prim(parent, "Vent_StackSteam", PrimitiveType.Cylinder,
             p + Vector3.up * 12.8f, v3(3.4f, 0.7f, 3.4f), _glass);
    }

    // Golden forge gate in front of the blast furnace
    static void Door_ForgeGate(Transform parent)
    {
        Prim(parent, "Door_ForgeGate", PrimitiveType.Cube,
             new Vector3(25f, FB + 5f, -115.3f), v3(3f, 5f, 0.6f), _gold);
        Prim(parent, "Door_ForgeGate_Arch", PrimitiveType.Cube,
             new Vector3(25f, FB + 8f, -115.3f), v3(4.4f, 1f, 0.7f), _gold);
    }

    // Golden ring crowning the biggest grain silo
    static void Arch_Ring(Transform parent)
    {
        string name = "Zona_Campos/Silo_3";
        Transform silo = GameObject.Find(name)?.transform;
        if (silo == null) return;
        Vector3 p = silo.position;                  // (66, LB + 7.5, 112), h = 15
        Prim(parent, "Arch_Ring", PrimitiveType.Cylinder,
             p + Vector3.up * 8f, v3(11.4f, 0.6f, 11.4f), _gold);
    }

    // Trade awnings on the Barrio Bajo facades
    static void Awnings(Transform parent)
    {
        float[,] blk = { { -82f, 40f, 14f, 13f }, { -97f, 17f, 11f, 11f }, { -88f, -4f, 12f, 12f } };
        Material[] mats = { _magenta, _cyan, _gold };
        for (int i = 0; i < blk.GetLength(0); i++)
        {
            float x = blk[i, 0], z = blk[i, 1], w = blk[i, 2], h = blk[i, 3];
            float zf = z + w * 0.85f / 2f + 0.35f;
            Prim(parent, $"Awning_Strip_{i}", PrimitiveType.Cube,
                 new Vector3(x, CB + h * 0.65f, zf), v3(w, 0.18f, 1.6f), mats[i]);
        }
    }

    // Vertical neon totem of the Barrio
    static void Sig_NeonBarrio(Transform parent)
    {
        Prim(parent, "Sig_NeonBarrio", PrimitiveType.Cube,
             new Vector3(-119f, CB + 4.5f, 24f), v3(0.6f, 4.5f, 0.6f), _magenta);
    }

    // Field lamp beside the south barn
    static void Lamp_Field(Transform parent)
    {
        Prim(parent, "Lamp_Field", PrimitiveType.Cube,
             new Vector3(24f, LB + 2.2f, 70f), v3(0.45f, 4.4f, 0.45f), _metal);
        Prim(parent, "Lamp_Field_Head", PrimitiveType.Cube,
             new Vector3(24f, LB + 4.8f, 70f), v3(1.5f, 0.5f, 1.5f), _gold);
    }

    // Terrace lamp of the elite Cúspide villa
    static void Lamp_Villa(Transform parent)
    {
        Prim(parent, "Lamp_Villa", PrimitiveType.Cube,
             new Vector3(120f, 42.5f, -168f), v3(0.5f, 4.5f, 0.5f), _metal);
        Prim(parent, "Lamp_Villa_Head", PrimitiveType.Cube,
             new Vector3(120f, 45.2f, -168f), v3(1.6f, 0.6f, 1.6f), _gold);
    }

    // Antenna spire over the observatory dome
    static void Stat_Observatory(Transform parent)
    {
        Prim(parent, "Stat_Observatory", PrimitiveType.Cube,
             new Vector3(95f, 38.4f, -196f), v3(0.7f, 3.6f, 0.7f), _metal);
        Prim(parent, "Stat_Observatory_Orb", PrimitiveType.Cube,
             new Vector3(95f, 40.4f, -196f), v3(1.4f, 0.8f, 1.4f), _gold);
    }

    // Banner string over the gate arch
    static void Banner_Gate(Transform parent)
    {
        for (int i = 0; i < 5; i++)
        {
            float x = -24f + i * 12f;
            Prim(parent, $"Banner_Over_{i}", PrimitiveType.Cube,
                 new Vector3(x, PB + 33.8f, -244f), v3(1.1f, 3.4f, 0.25f),
                 i % 2 == 0 ? _gold : _cyan);
        }
    }

    // Gate lamps at the passage entrance
    static void Lamp_Gate(Transform parent)
    {
        float[] px = { -26f, 26f };
        for (int i = 0; i < px.Length; i++)
        {
            Prim(parent, $"Lamp_Gate_{i}", PrimitiveType.Cube,
                 new Vector3(px[i], PB + 19.5f, -243.6f), v3(0.6f, 3.6f, 0.6f), _metal);
            Prim(parent, $"Lamp_Gate_{i}_Head", PrimitiveType.Cube,
                 new Vector3(px[i], PB + 21.8f, -243.8f), v3(1.6f, 0.8f, 1.2f), _magenta);
        }
    }

    // Southern lamppost of the central promenade
    static void Lamp_Deck(Transform parent)
    {
        Prim(parent, "Lamp_Deck", PrimitiveType.Cube,
             new Vector3(-30f, BaseY + 1.7f, -42f), v3(0.45f, 3.4f, 0.45f), _metal);
        Prim(parent, "Lamp_Deck_Head", PrimitiveType.Cube,
             new Vector3(-31.1f, BaseY + 3.7f, -42f), v3(1.6f, 0.4f, 0.4f), _gold);
    }

    // Waterside sign on the fishing dock
    static void Sig_Quay(Transform parent)
    {
        Prim(parent, "Sig_Quay", PrimitiveType.Cube,
             new Vector3(-40f, 10.3f, -53.5f), v3(5f, 1.4f, 0.25f), _gold);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    static Vector3 v3(float x, float y, float z) => new Vector3(x, y, z);

    static GameObject Prim(Transform parent, string name, PrimitiveType type,
                           Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.position = pos;
        go.transform.localScale = scale;
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }
}