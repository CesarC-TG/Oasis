using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class OasisCityTextures
{
    private const float BaseY = 11.2f;

    private static readonly Color Cyan    = new Color(0f, 0.9f, 1f);
    private static readonly Color Magenta = new Color(1f, 0.18f, 0.97f);
    private static readonly Color WallBase = new Color(0.09f, 0.11f, 0.15f);

    // Shared texture assets
    private static Texture2D _facA, _facB, _facC, _facD, _facE;
    private static Texture2D _mskA, _mskB, _mskC, _mskD, _mskE;
    private static Texture2D _panel, _asphalt, _tiles, _brick, _glassBand;

    [MenuItem("Oasis/Texture City & Detail")]
    public static void TextureCity()
    {
        GameObject root = GameObject.Find("Oasis_City");
        if (root == null)
        {
            Debug.LogError("[Oasis] Oasis_City not found. Run 'Oasis/Build City Core' first.");
            return;
        }

        EnsureFolder("Assets/Materials/Generated");
        GenerateSharedAssets();
        Material[] towerMats = CreateTowerMaterials();

        ApplyMaterials(root.transform, towerMats);
        AddRooftopDetails(root.transform, towerMats);
        TiltStallLegs(root.transform);
        AddSkybridges(root.transform);
        AddSpireAntenna(root.transform);

        EditorSceneManager.MarkSceneDirty(root.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Oasis] City textured and detailed.");
    }

    // ── Procedural texture generation ─────────────────────────────────────────

    static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;
        string parent = folderPath.Substring(0, folderPath.LastIndexOf('/'));
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        string name = folderPath.Substring(folderPath.LastIndexOf('/') + 1);
        AssetDatabase.CreateFolder(parent, name);
    }

    static void GenerateSharedAssets()
    {
        // Tower facades: 5 variants + matching emissive masks (white where lit)
        _facA = GetTexture("Tex_Facade_A", () => BuildFacade(0, 1));
        _facB = GetTexture("Tex_Facade_B", () => BuildFacade(1, 2));
        _facC = GetTexture("Tex_Facade_C", () => BuildFacade(2, 3));
        _facD = GetTexture("Tex_Facade_D", () => BuildFacade(3, 4));
        _facE = GetTexture("Tex_Facade_E", () => BuildFacade(4, 5));

        _mskA = GetTexture("Tex_Mask_A", () => BuildMask(0));
        _mskB = GetTexture("Tex_Mask_B", () => BuildMask(1));
        _mskC = GetTexture("Tex_Mask_C", () => BuildMask(2));
        _mskD = GetTexture("Tex_Mask_D", () => BuildMask(3));
        _mskE = GetTexture("Tex_Mask_E", () => BuildMask(4));

        _panel    = GetTexture("Tex_Panel",    BuildPanel);
        _asphalt  = GetTexture("Tex_Asphalt",  BuildAsphalt);
        _tiles    = GetTexture("Tex_Tiles",    BuildTiles);
        _brick    = GetTexture("Tex_Brick",    BuildBrick);
        _glassBand= GetTexture("Tex_GlassBand",BuildSpireBands);
    }

    static Texture2D GetTexture(string name, Func<Color[]> builder)
    {
        string path = $"Assets/Materials/Generated/{name}.asset";
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex != null) return tex;

        Color[] px = builder();
        tex = new Texture2D(128, 256, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply(false, false);
        AssetDatabase.CreateAsset(tex, path);
        return tex;
    }

    static (Color[] px, Color[] mask) FacadeVariant(int v, System.Random rng)
    {
        const int W = 128, H = 256;
        var px = new Color[W * H];
        var mask = new Color[W * H];
        Array.Fill(px, new Color(0f, 0f, 0f, 1f));

        // Wall color with slight per-variant warmth/coolness
        Color wall = v switch
        {
            1 => new Color(0.10f, 0.12f, 0.17f),
            2 => new Color(0.08f, 0.13f, 0.14f),
            3 => new Color(0.12f, 0.10f, 0.15f),
            4 => new Color(0.13f, 0.09f, 0.12f),
            _ => WallBase,
        };
        Color darkWin = Color.Lerp(wall, Color.black, 0.55f);
        Color litWin = new Color(0.75f, 0.85f, 1f);

        int floors = 7;
        int floorH = H / floors;
        switch (v)
        {
            case 0: // A: dense window grid
            {
                int cols = 5;
                for (int y = 0; y < H; y++)
                {
                    int f = y / floorH;
                    int fy = y - f * floorH;
                    for (int x = 0; x < W; x++)
                    {
                        int c = x * cols / W;
                        int cx = x - c * (W / cols);
                        bool ledge = fy < 3 || fy > floorH - 3;
                        bool inWin = !ledge && cx > 3 && cx < W / cols - 3;
                        bool lit = inWin && (rng.NextDouble() < 0.4);
                        bool off = inWin && !lit && (rng.NextDouble() < 0.6);
                        px[y * W + x] = inWin ? ((lit ? litWin : (off ? darkWin : litWin))) : wall;
                        mask[y * W + x] = inWin && lit ? Color.white : Color.black;
                    }
                }
                break;
            }
            case 1: // B: big glass bands (all floors lit)
            {
                for (int y = 0; y < H; y++)
                {
                    int f = y / floorH;
                    int fy = y - f * floorH;
                    bool band = fy > 4 && fy < floorH - 4;
                    float grad = fy / (float)floorH;
                    var glass = Color.Lerp(new Color(0.45f, 0.62f, 0.72f), new Color(0.15f, 0.35f, 0.45f), grad);
                    for (int x = 0; x < W; x++)
                    {
                        bool pillar = (x % (W / 6)) < 3;
                        px[y * W + x] = band ? (pillar ? wall : glass) : wall;
                        mask[y * W + x] = band && !pillar ? Color.white : Color.black;
                    }
                }
                break;
            }
            case 2: // C: horizontal stripes + sparse boxes
            {
                for (int y = 0; y < H; y++)
                {
                    int f = y / floorH;
                    int fy = y - f * floorH;
                    bool stripe = fy % 2 == 0;
                    for (int x = 0; x < W; x++)
                    {
                        bool box = stripe && (rng.NextDouble() < 0.35);
                        bool lit = box && (rng.NextDouble() < 0.5);
                        px[y * W + x] = box ? (lit ? litWin : darkWin) : stripe ? wall : Color.Lerp(wall, Color.white, 0.06f);
                        mask[y * W + x] = box && lit ? Color.white : Color.black;
                    }
                }
                break;
            }
            case 3: // D: industrial panels, sparse openings
            {
                for (int y = 0; y < H; y++)
                {
                    int fy = y % floorH / 3;
                    bool rib = (y / 4) % 8 < 2;
                    for (int x = 0; x < W; x++)
                    {
                        bool vent = (x / 32) % 2 == 0 && rib && rng.NextDouble() < 0.3;
                        bool lit = vent && rng.NextDouble() < 0.5;
                        px[y * W + x] = vent ? (lit ? litWin : darkWin) : rib ? Color.Lerp(wall, Color.white, 0.09f) : wall;
                        mask[y * W + x] = vent && lit ? Color.white : Color.black;
                    }
                }
                break;
            }
            default: // E: crazy neon zig-zag
            {
                for (int y = 0; y < H; y++)
                {
                    int f = y / floorH;
                    int off = (f * 7) % W;
                    int zx = (y % 21) < 3 ? off : -1; // horizontal neon stripe per floor block
                    for (int x = 0; x < W; x++)
                    {
                        bool on = zx >= 0 && x == zx;
                        bool dot = !on && Math.Abs(x - ((off + y) % W)) < 2 && y % 47 < 4;
                        px[y * W + x] = on || dot ? litWin : WallBase;
                        mask[y * W + x] = (on || dot) ? Color.white : Color.black;
                    }
                }
                break;
            }
        }
        return (px, mask);
    }

    static Color[] BuildFacade(int v, int seed)
    {
        var (px, _) = FacadeVariant(v, new System.Random(seed));
        return px;
    }

    static Color[] BuildMask(int v)
    {
        var (_, mask) = FacadeVariant(v, new System.Random(v + 11));
        return mask;
    }

    static readonly System.Random _noise = new System.Random(42);

    static Color[] BuildPanel()
    {
        const int W = 128, H = 256;
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                int gx = x / 32, gy = y / 32;
                bool seam = (x % 32 == 0 || y % 32 == 0);
                float rust = (_noise.NextDouble() > 0.97) ? 0.18f : 0f;
                var baseC = Color.Lerp(new Color(0.12f, 0.14f, 0.17f), new Color(0.30f, 0.20f, 0.12f), rust);
                px[y * W + x] = seam ? Color.black : baseC;
            }
        }
        return px;
    }

    static Color[] BuildAsphalt()
    {
        const int W = 128, H = 256;
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float n = ((float)_noise.NextDouble() * 2f - 1f) * 0.10f;
                bool crack = _noise.NextDouble() < 0.015;
                var c = Color.Lerp(new Color(0.055f, 0.06f, 0.068f), Color.black, n * 2f);
                if (crack) c = new Color(0.20f, 0.20f, 0.22f);
                px[y * W + x] = c;
            }
        }
        return px;
    }

    static Color[] BuildTiles()
    {
        const int W = 128, H = 256;
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                int gx = x / 32, gy = y / 32;
                bool grout = (x % 32 < 2 || y % 32 < 2);
                bool alt = (gx + gy) % 2 == 0;
                var c = alt ? new Color(0.66f, 0.70f, 0.75f) : new Color(0.53f, 0.57f, 0.62f);
                px[y * W + x] = grout ? new Color(0.28f, 0.30f, 0.34f) : c;
            }
        }
        return px;
    }

    static Color[] BuildBrick()
    {
        const int W = 128, H = 256;
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                int row = y / 14;
                int brickH = (y % 14) == 13 ? 1 : 0;
                int off = (row % 2) * 32;
                bool mortar = brickH == 1 || (x + off) % 64 < 2;
                var c = Color.Lerp(new Color(0.36f, 0.21f, 0.16f), new Color(0.20f, 0.12f, 0.10f), (_noise.NextDouble() > 0.85 ? 1 : 0));
                px[y * W + x] = mortar ? new Color(0.30f, 0.26f, 0.24f) : c;
            }
        }
        return px;
    }

    static Color[] BuildSpireBands()
    {
        const int W = 128, H = 256;
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
        {
            bool band = (y / 8) % 4 == 0;
            var c = band ? Color.white : Color.black;
            for (int x = 0; x < W; x++) px[y * W + x] = c;
        }
        return px;
    }

    // ── Materials ─────────────────────────────────────────────────────────────

    static Material GetMat(string name)
    {
        string path = $"Assets/Materials/Generated/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        m = new Material(s) { name = name };
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Material MakeLit(string name, Texture2D baseMap, Texture2D mask, Color tint, Color emission,
                            float metallic = 0.55f, float smooth = 0.4f)
    {
        Material m = GetMat(name);
        if (baseMap != null) m.SetTexture("_BaseMap", baseMap);
        m.SetColor("_BaseColor", tint);
        if (mask != null) m.SetTexture("_EmissionMap", mask);
        m.SetColor("_EmissionColor", emission);
        m.EnableKeyword("_EMISSION");
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smooth);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material[] CreateTowerMaterials()
    {
        var mats = new Material[14];
        for (int i = 0; i < 14; i++)
        {
            int v = i % 5;
            var (fac, msk) = (new[] { _facA, _facB, _facC, _facD, _facE }[v],
                              new[] { _mskA, _mskB, _mskC, _mskD, _mskE }[v]);
            float light = 0.9f + (i % 3) * 0.05f;
            Color tint = new Color(light, light, light * (1f + (i % 2) * 0.04f));
            Color em = (i % 2 == 0) ? Cyan : Magenta;
            mats[i] = MakeLit($"Mat_TF_{i:D2}", fac, msk, tint, em);
        }
        return mats;
    }

    static Material _panelM, _asphaltM, _tilesM, _brickM, _bridgeM, _spireM, _emitM, _beaconM;

    static void EnsureSharedMats()
    {
        _panelM   = MakeLit("Mat_Panel",        _panel,     null, Color.white, Color.black);
        _asphaltM = MakeLit("Mat_Asphalt",      _asphalt,   null, Color.white, Color.black, 0.05f, 0.25f);
        _tilesM   = MakeLit("Mat_Tiles",        _tiles,     null, Color.white, Color.black, 0.1f, 0.5f);
        _brickM   = MakeLit("Mat_Brick",        _brick,     null, Color.white, Color.black, 0.15f, 0.3f);
        _bridgeM  = MakeLit("Mat_Bridge",       null,       null, new Color(0.55f, 0.7f, 0.78f, 0.55f), Color.white, 0.1f, 0.92f);
        _spireM   = MakeLit("Mat_Spire",        null,       _glassBand, new Color(0.5f, 0.68f, 0.78f), Cyan, 0.1f, 0.9f);
        _emitM    = MakeLit("Mat_Emissary",     _facD,      _mskD, Color.white, Magenta);
        _beaconM  = MakeLit("Mat_Beacon",       null,       null, Color.white, Cyan, 0.3f, 0.6f);
        SetTransparent(_bridgeM);
    }

    static void SetTransparent(Material m)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_AlphaClip", 0f);
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    // ── Apply to existing objects ─────────────────────────────────────────────

    static void ApplyMaterials(Transform root, Material[] towerMats)
    {
        EnsureSharedMats();
        foreach (Transform child in root)
        {
            string n = child.name;
            if (n.Contains("_Crown") || n.Contains("_Light") || n.Contains("_Head")) continue;

            Renderer r = child.GetComponent<Renderer>();
            if (r == null) continue;

            if (n.StartsWith("Tower_"))
            {
                int i = int.Parse(n.Substring(6, 2));
                r.sharedMaterial = towerMats[i];
            }
            else if (n.StartsWith("Emissary_")) r.sharedMaterial = _emitM;
            else if (n == "Spire") r.sharedMaterial = _spireM;
            else if (n == "Plinth" || n.StartsWith("RimAccent_")
                 || n.StartsWith("MerLamp_") || n.StartsWith("CauceLamp_")) r.sharedMaterial = _panelM;
            else if (n == "Market_Plaza") r.sharedMaterial = _tilesM;
            else if (n.EndsWith("_Roof")) r.sharedMaterial = _brickM;
            else if (n.EndsWith("_Leg")) r.sharedMaterial = _panelM;
            else if (n == "Road_NS" || n == "Road_EW" || n.StartsWith("CauceDeck")) r.sharedMaterial = _asphaltM;
        }
    }

    // ── Shape detail ───────────────────────────────────────────────────────────

    static void AddRooftopDetails(Transform root, Material[] towerMats)
    {
        for (int i = 0; i < 14; i++)
        {
            Transform tower = root.Find($"Tower_{i:D2}");
            if (tower == null) continue;
            Vector3 p = tower.localPosition;
            Vector3 s = tower.localScale;

            string b = $"RooftopUnit_{i:D2}";
            if (root.Find(b) == null)
            {
                var unit = GameObject.CreatePrimitive(PrimitiveType.Cube);
                unit.name = b;
                unit.transform.SetParent(root, false);
                unit.transform.localPosition = new Vector3(p.x, p.y + s.y / 2f + 1.1f, p.z);
                unit.transform.localScale = new Vector3(s.x * 0.26f, 2.2f, s.z * 0.26f);
                unit.GetComponent<Renderer>().sharedMaterial = _panelM;
            }

            string b2 = $"RooftopVent_{i:D2}";
            if (root.Find(b2) == null)
            {
                var vent = GameObject.CreatePrimitive(PrimitiveType.Cube);
                vent.name = b2;
                vent.transform.SetParent(root, false);
                vent.transform.localPosition = new Vector3(p.x + s.x * 0.16f, p.y + s.y / 2f + 2.6f, p.z + s.z * 0.12f);
                vent.transform.localScale = new Vector3(s.x * 0.15f, 1.6f, s.z * 0.15f);
                vent.GetComponent<Renderer>().sharedMaterial = _panelM;
            }
        }
    }

    static void TiltStallLegs(Transform root)
    {
        int n = 0;
        foreach (Transform child in root)
        {
            if (!child.name.EndsWith("_Leg")) continue;
            float x = Mathf.Sign(child.localPosition.x - -32f);
            float z = Mathf.Sign(child.localPosition.z - 28f);
            child.localRotation = Quaternion.Euler(0f, 0f, 0f);
            child.localRotation = Quaternion.Euler(x * 5f, 0f, z * 5f);
            n++;
        }
        Debug.Log($"[Oasis] Tilted {n} market legs.");
    }

    static void AddSkybridges(Transform root)
    {
        int[,] pairs = { { 0, 1 }, { 5, 6 }, { 9, 10 } };
        for (int b = 0; b < pairs.GetLength(0); b++)
        {
            int a0 = pairs[b, 0], a1 = pairs[b, 1];
            Transform t0 = root.Find($"Tower_{a0:D2}");
            Transform t1 = root.Find($"Tower_{a1:D2}");
            if (t0 == null || t1 == null) continue;

            Vector3 p0 = t0.localPosition, p1 = t1.localPosition;
            Vector3 dir = (p1 - p0).normalized;
            float len = (p1 - p0).magnitude;
            Vector3 mid = (p0 + p1) / 2f;
            float bridgeY = BaseY + 18f;

            string bn = $"SkyBridge_{b}";
            if (root.Find(bn) != null) continue;

            var bridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bridge.name = bn;
            bridge.transform.SetParent(root, false);
            bridge.transform.localPosition = new Vector3(mid.x, bridgeY, mid.z);
            bridge.transform.localRotation = Quaternion.LookRotation(dir);
            bridge.transform.localScale = new Vector3(4f, 1.4f, len);
            bridge.GetComponent<Renderer>().sharedMaterial = _bridgeM;
        }
    }

    static void AddSpireAntenna(Transform root)
    {
        Transform spire = root.Find("Spire");
        if (spire == null) return;
        float tipY = spire.localPosition.y + 45f;

        if (root.Find("SpireAntenna") == null)
        {
            var ant = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ant.name = "SpireAntenna";
            ant.transform.SetParent(root, false);
            ant.transform.localPosition = new Vector3(0, tipY + 5f, 0);
            ant.transform.localScale = new Vector3(1.2f, 5f, 1.2f);
            ant.GetComponent<Renderer>().sharedMaterial = _panelM;
        }
        if (root.Find("SpireBeacon") == null)
        {
            var bea = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bea.name = "SpireBeacon";
            bea.transform.SetParent(root, false);
            bea.transform.localPosition = new Vector3(0, tipY + 12f, 0);
            bea.transform.localScale = new Vector3(1.8f, 1.8f, 1.8f);
            bea.GetComponent<Renderer>().sharedMaterial = _beaconM;
        }
    }

    // ── Camera framing ────────────────────────────────────────────────────────

    static SceneView View()
    {
        if (SceneView.lastActiveSceneView != null) return SceneView.lastActiveSceneView;
        return SceneView.sceneViews.Count > 0 ? (SceneView)SceneView.sceneViews[0] : null;
    }

    static void Frame(Vector3 target, Vector3 camPos, float size)
    {
        SceneView sv = View();
        if (sv == null)
        {
            Debug.LogWarning("[Oasis] No Scene view open.");
            return;
        }
        sv.Focus();
        sv.in2DMode = false;
        sv.pivot = target;
        sv.rotation = Quaternion.LookRotation(target - camPos);
        sv.size = size;
        sv.Repaint();
        Debug.Log($"[Oasis] Camera framed: {target} from {camPos} (size {size}).");
    }

    [MenuItem("Oasis/Camera/Ver Ciudad")]
    public static void FrameCity()
    {
        Frame(new Vector3(0f, 12f, 0f), new Vector3(85f, 95f, 110f), 190f);
    }

    [MenuItem("Oasis/Camera/Ver El Corazón")]
    public static void FrameCorazon()
    {
        Frame(new Vector3(0f, 26f, 0f), new Vector3(52f, 42f, 52f), 68f);
    }

    [MenuItem("Oasis/Camera/Ver Valle")]
    public static void FrameValley()
    {
        Frame(new Vector3(0f, 8f, 0f), new Vector3(220f, 170f, 220f), 430f);
    }
}