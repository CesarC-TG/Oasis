using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

public static class VaciadoPrefabSetup
{
    private const string VaciadoPath      = "Assets/Animation/Animation_Vaciado";
    private const string MainModelFbxName = "human+character+3d+model";
    private const string MainModelPath    = VaciadoPath + "/human+character+3d+model.fbx";
    private const string PrefabPath       = VaciadoPath + "/Vaciado.prefab";
    private const string ControllerPath   = VaciadoPath + "/VaciadoAnimator.controller";

    [MenuItem("Oasis/Setup Vaciado Prefab")]
    public static void Run()
    {
        // ── 1. Validate main model exists ──
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MainModelPath);
        if (modelAsset == null)
        {
            Debug.LogError($"[VaciadoPrefab] Main model not found at: {MainModelPath}");
            EditorUtility.DisplayDialog("Oasis - Vaciado Prefab",
                $"Main model not found at:\n{MainModelPath}", "OK");
            return;
        }

        // ── 2. Find Avatar in main model sub-assets ──
        Avatar avatar = FindAvatar(MainModelPath);
        if (avatar == null)
        {
            Debug.LogError($"[VaciadoPrefab] No Avatar found in: {MainModelPath}. " +
                           "Run 'Setup Vaciado Import Settings' first to import as Humanoid.");
            EditorUtility.DisplayDialog("Oasis - Vaciado Prefab",
                "No Avatar found in main model.\n\n" +
                "Run Oasis > Setup Vaciado Import Settings first.", "OK");
            return;
        }
        Debug.Log($"[VaciadoPrefab] Avatar found: {avatar.name}");

        // ── 3. Create or overwrite the prefab ──
        GameObject prefab = CreateOrUpdatePrefab(modelAsset, avatar);

        // ── 4. Build AnimatorController ──
        AnimatorController controller = BuildAnimatorController();

        // ── 5. Assign controller + avatar to prefab via SerializedObject ──
        AssignControllerToPrefab(prefab, controller, avatar);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Oasis - Vaciado Prefab",
            $"Vaciado.prefab ready.\n\n" +
            $"Prefab:     {PrefabPath}\n" +
            $"Controller: {ControllerPath}", "OK");
    }

    // ── Avatar discovery ──────────────────────────────────────────────────────

    private static Avatar FindAvatar(string fbxPath)
    {
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        foreach (var asset in allAssets)
        {
            if (asset is Avatar avatar)
                return avatar;
        }
        return null;
    }

    // ── Prefab creation ───────────────────────────────────────────────────────

    private static GameObject CreateOrUpdatePrefab(GameObject sourceModel, Avatar avatar)
    {
        // Instantiate in scene to finalise component setup before prefab save
        GameObject instance = (GameObject)Object.Instantiate(sourceModel);
        instance.name = "Vaciado";

        // Ensure Animator exists on the root
        Animator animator = instance.GetComponent<Animator>();
        if (animator == null)
            animator = instance.AddComponent<Animator>();
        animator.avatar = avatar;
        animator.applyRootMotion = false;

        // Save as prefab (overwrites if already present)
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);

        // Clean up temporary scene object
        Object.DestroyImmediate(instance);

        return prefab;
    }

    // ── Animator Controller construction ──────────────────────────────────────

    private static AnimatorController BuildAnimatorController()
    {
        // Delete old controller if it exists
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        // ── Parameters ──
        controller.AddParameter("Speed",        AnimatorControllerParameterType.Float);
        controller.AddParameter("IsAttacking",  AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsHit",        AnimatorControllerParameterType.Trigger);
        controller.AddParameter("IsDead",       AnimatorControllerParameterType.Bool);

        var root = controller.layers[0].stateMachine;

        // ── Load animation clips ──
        AnimationClip idleClip   = LoadClip("zombie idle");
        AnimationClip walkClip   = LoadClip("walking");
        AnimationClip runClip    = LoadClip("zombie running");
        AnimationClip attackClip = LoadClip("zombie attack");
        AnimationClip hitClip    = LoadClip("zombie reaction hit");
        AnimationClip deathClip  = LoadClip("zombie agonizing");

        // ── States ──
        var stIdle   = AddState(root, "Idle",   idleClip,   new Vector3(-250,    0));
        var stWalk   = AddState(root, "Walk",   walkClip,   new Vector3(   0,  -80));
        var stRun    = AddState(root, "Run",    runClip,    new Vector3(   0,   80));
        var stAttack = AddState(root, "Attack", attackClip, new Vector3( 200,  120));
        var stHit    = AddState(root, "Hit",    hitClip,    new Vector3( 200,    0));
        var stDeath  = AddState(root, "Death",  deathClip,  new Vector3( 400, -120));

        root.defaultState = stIdle;

        // ── Locomotion transitions ──
        Transition(stIdle, stWalk, "Speed", AnimatorConditionMode.Greater, 0.1f, 0.15f);
        Transition(stIdle, stRun,  "Speed", AnimatorConditionMode.Greater, 0.7f, 0.15f);
        Transition(stWalk, stIdle, "Speed", AnimatorConditionMode.Less,    0.1f, 0.20f);
        Transition(stWalk, stRun,  "Speed", AnimatorConditionMode.Greater, 0.7f, 0.15f);
        Transition(stRun,  stWalk, "Speed", AnimatorConditionMode.Less,    0.7f, 0.15f);
        Transition(stRun,  stIdle, "Speed", AnimatorConditionMode.Less,    0.1f, 0.20f);

        // ── Any → Attack (triggered by IsAttacking bool) ──
        var anyToAttack = root.AddAnyStateTransition(stAttack);
        anyToAttack.hasExitTime = false;
        anyToAttack.duration    = 0.1f;
        anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "IsAttacking");

        // Attack → Idle (exit at 90 %)
        var attackToIdle = stAttack.AddTransition(stIdle);
        attackToIdle.hasExitTime = true;
        attackToIdle.exitTime    = 0.9f;
        attackToIdle.duration    = 0.2f;

        // ── Any → Hit (triggered by IsHit trigger) ──
        var anyToHit = root.AddAnyStateTransition(stHit);
        anyToHit.hasExitTime = false;
        anyToHit.duration    = 0.05f;
        anyToHit.AddCondition(AnimatorConditionMode.If, 0, "IsHit");

        // Hit → Idle (exit at 85 %)
        var hitToIdle = stHit.AddTransition(stIdle);
        hitToIdle.hasExitTime = true;
        hitToIdle.exitTime    = 0.85f;
        hitToIdle.duration    = 0.2f;

        // ── Any → Death (triggered by IsDead bool) ──
        var anyToDeath = root.AddAnyStateTransition(stDeath);
        anyToDeath.hasExitTime = false;
        anyToDeath.duration    = 0.1f;
        anyToDeath.AddCondition(AnimatorConditionMode.If, 0, "IsDead");

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log("[VaciadoPrefab] AnimatorController built with 6 states: " +
                  "Idle, Walk, Run, Attack, Hit, Death");

        return controller;
    }

    // ── Assign controller + avatar to prefab ──────────────────────────────────

    private static void AssignControllerToPrefab(GameObject prefab,
                                                  AnimatorController controller,
                                                  Avatar avatar)
    {
        var animator = prefab.GetComponent<Animator>();
        if (animator == null)
            animator = prefab.AddComponent<Animator>();

        // Use SerializedObject for reliable assignment on prefab assets
        var so = new SerializedObject(animator);
        so.FindProperty("m_Controller").objectReferenceValue = controller;
        so.FindProperty("m_Avatar").objectReferenceValue     = avatar;
        so.FindProperty("m_ApplyRootMotion").boolValue       = false;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(prefab);
        EditorUtility.SetDirty(animator);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AnimatorState AddState(AnimatorStateMachine sm, string name,
                                          AnimationClip clip, Vector3 pos)
    {
        var state = sm.AddState(name, pos);
        if (clip != null)
        {
            state.motion = clip;
        }
        else
        {
            Debug.LogWarning($"[VaciadoPrefab] State '{name}' has no clip — " +
                             "assigned a placeholder empty clip.");
        }
        return state;
    }

    private static void Transition(AnimatorState from, AnimatorState to,
                                   string param, AnimatorConditionMode mode,
                                   float threshold, float duration)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration    = duration;
        t.AddCondition(mode, threshold, param);
    }

    /// <summary>
    /// Loads an AnimationClip by fuzzy-matching the FBX filename.
    /// First tries exact name match, then falls back to substring match.
    /// </summary>
    private static AnimationClip LoadClip(string fbxName)
    {
        string[] guids = AssetDatabase.FindAssets(fbxName, new[] { VaciadoPath });

        // ── Pass 1: exact filename match (case-insensitive) ──
        foreach (string guid in guids)
        {
            string path     = AssetDatabase.GUIDToAssetPath(guid);
            string filename = Path.GetFileNameWithoutExtension(path);
            if (filename.ToLowerInvariant() == fbxName.ToLowerInvariant())
                return ExtractFirstClip(path);
        }

        // ── Pass 2: fuzzy substring fallback ──
        foreach (string guid in guids)
        {
            string path     = AssetDatabase.GUIDToAssetPath(guid);
            string filename = Path.GetFileNameWithoutExtension(path);
            if (filename.ToLowerInvariant().Contains(fbxName.ToLowerInvariant()))
            {
                AnimationClip clip = ExtractFirstClip(path);
                if (clip != null)
                {
                    Debug.Log($"[VaciadoPrefab] Fuzzy match: '{fbxName}' → '{filename}' ({clip.name})");
                    return clip;
                }
            }
        }

        Debug.LogWarning($"[VaciadoPrefab] No clip found for: {fbxName}");
        return null;
    }

    private static AnimationClip ExtractFirstClip(string path)
    {
        Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var obj in all)
        {
            if (obj is AnimationClip clip && !clip.name.StartsWith("__"))
                return clip;
        }
        return null;
    }
}
