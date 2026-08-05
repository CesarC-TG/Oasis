using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

/// <summary>
/// Creates and assigns a full Animator Controller for Vaciado (zombie) enemies.
/// Covers: locomotion (Idle/Walk/Run), attacks (Punch/Kick/Headbutt),
/// hit reactions (2 types), death (agonizing), and stand-up stumble.
///
/// Run via: Oasis > Setup Vaciado Animator
///
/// Clips sourced from: Assets/Animation/Animation_Vaciado/
/// Output controller:  Assets/Animation/Animation_Vaciado/VaciadoAnimator.controller
/// </summary>
public static class VaciadoAnimatorSetup
{
    private const string AnimPath   = "Assets/Animation/Animation_Vaciado";
    private const string OutputPath = "Assets/Animation/Animation_Vaciado/VaciadoAnimator.controller";
    private const string ModelPath  = "Assets/Animation/Animation_Vaciado/Vaciado_Model.fbx";

    [MenuItem("Oasis/Setup Vaciado Animator")]
    public static void Run()
    {
        SetAllHumanoid();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var controller = BuildVaciadoAnimator();
        AssignControllerToSceneVaciados(controller);

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Oasis — Vaciado Animator",
            "Vaciado Animator built and assigned to all scene Vaciados.\n\n" +
            "Parameters:\n" +
            "  Speed (float), Attack (trigger), AttackIndex (int)\n" +
            "  HitReaction (trigger), HitType (int), Death (trigger)",
            "OK");
    }

    // ── Humanoid reimport ────────────────────────────────────────────────

    static void SetAllHumanoid()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { AnimPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;

            // Loop locomotion + idle; non-loop attacks/hits/death
            string lower = path.ToLower();
            bool shouldLoop = lower.Contains("walk") || lower.Contains("run") ||
                              lower.Contains("idle");
            var clips = importer.clipAnimations.Length > 0
                ? importer.clipAnimations : importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].loopTime = shouldLoop;
                clips[i].loopPose = shouldLoop;
            }
            importer.clipAnimations = clips;

            importer.SaveAndReimport();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[VaciadoSetup] Humanoid + Loop({shouldLoop}): {path}");
        }
    }

    // ── Animator Controller ──────────────────────────────────────────────

    static AnimatorController BuildVaciadoAnimator()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(OutputPath) != null)
            AssetDatabase.DeleteAsset(OutputPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(OutputPath);
        controller.AddParameter("Speed",        AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack",       AnimatorControllerParameterType.Trigger);
        controller.AddParameter("AttackIndex",  AnimatorControllerParameterType.Int);
        controller.AddParameter("HitReaction",  AnimatorControllerParameterType.Trigger);
        controller.AddParameter("HitType",      AnimatorControllerParameterType.Int);
        controller.AddParameter("Death",        AnimatorControllerParameterType.Trigger);

        var root = controller.layers[0].stateMachine;

        // ── Load clips ──

        AnimationClip vIdle    = GetFirstMatching("idle");            // zombie idle
        AnimationClip vWalk    = GetFirstMatching("walking");          // walking
        AnimationClip vRun     = GetFirstMatching("running");          // zombie running
        AnimationClip vPunch   = GetFirstMatching("punching");         // zombie punching
        AnimationClip vKick    = GetFirstMatching("kicking");          // zombie kicking
        AnimationClip vHeadbutt= GetFirstMatching("headbutt");         // zombie headbutt
        AnimationClip vHit1    = GetFirstMatching("reaction hit");     // zombie reaction hit
        AnimationClip vAgony   = GetFirstMatching("agonizing");        // zombie agonizing
        AnimationClip vStandUp = GetFirstMatching("stand up");         // zombie stand up
        AnimationClip vStumble = GetFirstMatching("stumbling");        // zombie stumbling

        Debug.Log($"[VaciadoSetup] Clips — Idle:{vIdle?.name} Walk:{vWalk?.name} Run:{vRun?.name} " +
                  $"Punch:{vPunch?.name} Kick:{vKick?.name} Headbutt:{vHeadbutt?.name} " +
                  $"Hit:{vHit1?.name} Death:{vAgony?.name}");

        // ── Locomotion blend tree ──

        var locoTree = new BlendTree
        {
            name           = "VaciadoLoco",
            blendType      = BlendTreeType.Simple1D,
            blendParameter  = "Speed",
            children = new[]
            {
                new ChildMotion { motion = vIdle, timeScale = 1f, threshold = 0f   },
                new ChildMotion { motion = vWalk, timeScale = 1f, threshold = 0.5f },
                new ChildMotion { motion = vRun,  timeScale = 1f, threshold = 1f   },
            },
            useAutomaticThresholds = false
        };

        var stLoco = root.AddState("Locomotion", new Vector3(0, 0));
        stLoco.motion = locoTree;
        root.defaultState = stLoco;

        // ── Attack sub-state machine ──

        var attackSM = root.AddStateMachine("Attack", new Vector3(200, -150));

        var stPunch = attackSM.AddState("Punch", new Vector3(-100, 0));
        if (vPunch != null) { stPunch.motion = vPunch; stPunch.speed = 1.2f; }
        var stKick  = attackSM.AddState("Kick",  new Vector3(0, 60));
        if (vKick != null)  { stKick.motion  = vKick;  stKick.speed  = 1.1f; }
        var stHead  = attackSM.AddState("Headbutt", new Vector3(100, 0));
        if (vHeadbutt != null) { stHead.motion = vHeadbutt; stHead.speed = 1.0f; }

        attackSM.defaultState = stPunch;

        var anyAttack = root.AddAnyStateTransition(attackSM);
        anyAttack.hasExitTime = false; anyAttack.duration = 0.05f;
        anyAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");

        var aEntry0 = attackSM.AddEntryTransition(stPunch);
        aEntry0.AddCondition(AnimatorConditionMode.Equals, 0, "AttackIndex");
        var aEntry1 = attackSM.AddEntryTransition(stKick);
        aEntry1.AddCondition(AnimatorConditionMode.Equals, 1, "AttackIndex");
        var aEntry2 = attackSM.AddEntryTransition(stHead);
        aEntry2.AddCondition(AnimatorConditionMode.Equals, 2, "AttackIndex");

        // Exit after clip
        var exPunch = stPunch.AddExitTransition();
        exPunch.hasExitTime = true; exPunch.exitTime = 0.85f; exPunch.duration = 0.15f;
        var exKick = stKick.AddExitTransition();
        exKick.hasExitTime = true; exKick.exitTime = 0.85f; exKick.duration = 0.15f;
        var exHead = stHead.AddExitTransition();
        exHead.hasExitTime = true; exHead.exitTime = 0.85f; exHead.duration = 0.15f;

        // ── Hit Reaction sub-state machine ──

        var hitSM = root.AddStateMachine("HitReaction", new Vector3(200, 150));

        var stHit1 = hitSM.AddState("Hit_Reaction", new Vector3(0, 40));
        if (vHit1   != null) stHit1.motion = vHit1;
        var stStumble = hitSM.AddState("Stumble", new Vector3(0, -40));
        if (vStumble != null) stStumble.motion = vStumble;

        hitSM.defaultState = stHit1;

        var anyHit = root.AddAnyStateTransition(hitSM);
        anyHit.hasExitTime = false; anyHit.duration = 0.05f;
        anyHit.AddCondition(AnimatorConditionMode.If, 0, "HitReaction");

        var hEntry0 = hitSM.AddEntryTransition(stHit1);
        hEntry0.AddCondition(AnimatorConditionMode.Equals, 0, "HitType");
        var hEntry1 = hitSM.AddEntryTransition(stStumble);
        hEntry1.AddCondition(AnimatorConditionMode.Equals, 1, "HitType");

        var exHit1 = stHit1.AddExitTransition();
        exHit1.hasExitTime = true; exHit1.exitTime = 0.85f; exHit1.duration = 0.15f;
        var exStumble = stStumble.AddExitTransition();
        exStumble.hasExitTime = true; exStumble.exitTime = 0.85f; exStumble.duration = 0.15f;

        // Stand-up after stumble (auto-enter when HitType=2 or speed after stumble)
        var stStandUp = AddState(root, "StandUp", vStandUp, new Vector3(400, 200));
        Transition(stStumble, stStandUp, "HitType", AnimatorConditionMode.Equals, 2, 0.1f);
        Transition(stStandUp, stLoco, "Speed", AnimatorConditionMode.Greater, 0.1f, 0.15f);

        // ── Death state (no exit per design) ──

        var stDeath = AddState(root, "Death", vAgony, new Vector3(500, 0));
        var anyDeath = root.AddAnyStateTransition(stDeath);
        anyDeath.hasExitTime = false; anyDeath.duration = 0.1f;
        anyDeath.AddCondition(AnimatorConditionMode.If, 0, "Death");
        // No exit — death is terminal

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    // ── Scene assignment — all Vaciado GameObjects ───────────────────────

    static void AssignControllerToSceneVaciados(AnimatorController controller)
    {
        // Find the Avatar from the model FBX
        Avatar vaciadoAvatar = null;
        if (!string.IsNullOrEmpty(ModelPath))
        {
            Object[] all = AssetDatabase.LoadAllAssetsAtPath(ModelPath);
            foreach (var obj in all)
                if (obj is Avatar a) { vaciadoAvatar = a; break; }
        }
        Debug.Log($"[VaciadoSetup] Avatar from {ModelPath}: {(vaciadoAvatar != null ? vaciadoAvatar.name : "NOT FOUND")}");

        // Find all GameObjects with "Vaciado" in name that have an Animator
        var allGOs = Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        int assigned = 0;
        foreach (var go in allGOs)
        {
            if (!go.name.ToLower().Contains("vaciado")) continue;
            var animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.GetComponentInChildren<Animator>();
            if (animator == null) continue;

            Undo.RecordObject(animator, "Assign Vaciado Animator");
            var so = new SerializedObject(animator);
            so.FindProperty("m_Controller").objectReferenceValue = controller;
            if (vaciadoAvatar != null)
                so.FindProperty("m_Avatar").objectReferenceValue = vaciadoAvatar;
            so.FindProperty("m_ApplyRootMotion").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(animator);
            assigned++;
            Debug.Log($"[VaciadoSetup] Assigned Controller to: {go.name}");
        }

        if (assigned == 0)
            Debug.LogWarning("[VaciadoSetup] No Vaciado GameObjects with Animator found in scene.");

        EditorSceneManager.MarkAllScenesDirty();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    static AnimatorState AddState(AnimatorStateMachine sm, string name, AnimationClip clip, Vector3 pos)
    {
        var state = sm.AddState(name, pos);
        if (clip != null) state.motion = clip;
        return state;
    }

    static void Transition(AnimatorState from, AnimatorState to, string param,
                           AnimatorConditionMode mode, float threshold, float duration)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration    = duration;
        t.AddCondition(mode, threshold, param);
    }

    /// <summary>
    /// Finds the first FBX whose filename contains <c>match</c> (case-insensitive)
    /// and returns its first AnimationClip.
    /// </summary>
    static AnimationClip GetFirstMatching(string match)
    {
        string lowerMatch = match.ToLower();
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { AnimPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string lowerPath = path.ToLower();
            if (!lowerPath.EndsWith(".fbx")) continue;
            if (!lowerPath.Contains(lowerMatch)) continue;

            Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var obj in all)
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__"))
                    return clip;
            }
        }
        Debug.LogWarning($"[VaciadoSetup] No FBX matching '{match}' in {AnimPath}");
        return null;
    }
}
