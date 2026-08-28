using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine.SceneManagement;

/// <summary>
/// Rebuilds the full player AnimatorController deterministically.
///
/// Integrates two sources of truth:
///  - betoloco:   blend-tree locomotion (Idle/Walk/Run on Speed), Crouch, Jump + landing,
///                velocity-driven Speed, and correct animation loop settings.
///  - gameplay:   combat layer — 3-hit combo (Attack), 4-way dodge, hit reactions, death.
///
/// Run from the menu: Oasis > Player Animator > Build Full (v3 Locomotion + Combat)
/// This supersedes the old PlayerAnimationUpgrade (v2) script.
/// </summary>
public static class BuildPlayerAnimator
{
    private const string LocoDir   = "Assets/Characters/Animations";          // locomotion clips (PlayerCharacter@*)
    private const string CombatDir = "Assets/Animation/Animation_Player";      // combat clips (Mixamo)
    private const string OutputPath = LocoDir + "/PlayerAnimator.controller";

    [MenuItem("Oasis/Player Animator/Build Full (v3 Locomotion + Combat)")]
    public static void Build()
    {
        SetClipLoopSettings();
        AnimatorController controller = BuildController();
        AssignToScenePlayer(controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Oasis",
            "Animator v3 aplicado:\n" +
            "- Locomotion: BlendTree Idle/Walk/Run (Speed)\n" +
            "- Crouch + Jump con aterrizaje\n" +
            "- Combate: combo x3, dodge 4 direcciones, hit reaction, muerte",
            "OK");
    }

    // ───────────────────────────────────────────────────────────────
    // 1. Loop settings (betoloco): Idle/Walk/Run loop, Jump does not.
    // ───────────────────────────────────────────────────────────────
    static void SetClipLoopSettings()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { LocoDir });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;

            bool isJump = path.Contains("Jumping");
            var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].loopTime               = !isJump;
                clips[i].loopPose               = !isJump;
                clips[i].loopBlend              = !isJump;
                clips[i].loopBlendOrientation   = !isJump;
                clips[i].loopBlendPositionXZ    = !isJump;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
    }

    // ───────────────────────────────────────────────────────────────
    // 2. Build the controller
    // ───────────────────────────────────────────────────────────────
    static AnimatorController BuildController()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(OutputPath) != null)
            AssetDatabase.DeleteAsset(OutputPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(OutputPath);

        // Parameters — superset of everything the code actually sets.
        controller.AddParameter("Speed",         AnimatorControllerParameterType.Float);
        controller.AddParameter("IsCrouching",   AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsGrounded",    AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsRunning",     AnimatorControllerParameterType.Bool);
        controller.AddParameter("Jump",          AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack",        AnimatorControllerParameterType.Trigger);
        controller.AddParameter("AttackIndex",   AnimatorControllerParameterType.Int);
        controller.AddParameter("ComboIndex",    AnimatorControllerParameterType.Int);
        controller.AddParameter("Dodge",         AnimatorControllerParameterType.Trigger);
        controller.AddParameter("DodgeDirection",AnimatorControllerParameterType.Int);
        controller.AddParameter("HeavyHit",      AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hurt",          AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Death",         AnimatorControllerParameterType.Trigger);

        var root = controller.layers[0].stateMachine;

        // ── Locomotion blend tree ────────────────────────────────
        AnimationClip idle = LoadClip(LocoDir, "PlayerCharacter@Idle.fbx");
        AnimationClip walk = LoadClip(LocoDir, "PlayerCharacter@Walking.fbx");
        AnimationClip run  = LoadClip(LocoDir, "PlayerCharacter@Running.fbx");
        AnimationClip jump = LoadClip(LocoDir, "PlayerCharacter@Jumping.fbx");

        BlendTree locomotion = BuildLocomotionBlendTree(idle, walk, run);

        // ── Combat clips ─────────────────────────────────────────
        AnimationClip atkPunch = LoadClip(CombatDir, "Punching.fbx");
        AnimationClip atkKick  = LoadClip(CombatDir, "Kick.fbx");
        AnimationClip atkHook  = LoadClip(CombatDir, "Hook.fbx");
        AnimationClip dodgeFwd = LoadClip(CombatDir, "Dodge.fbx");
        AnimationClip dodgeBk  = LoadClip(CombatDir, "Dodge_backward.fbx");
        AnimationClip dodgeLf  = LoadClip(CombatDir, "Dodge_left.fbx");
        AnimationClip dodgeRt  = LoadClip(CombatDir, "Dodge_right.fbx");
        AnimationClip hitTorso = LoadClip(CombatDir, "Hit_torsoReaction.fbx");
        AnimationClip stunned  = LoadClip(CombatDir, "Stunned.fbx");
        AnimationClip die      = LoadClip(CombatDir, "Die.fbx");

        // ── States ───────────────────────────────────────────────
        var stMove   = root.AddState("Locomotion",    new Vector3(50,  50,  0));
        var stCrouch = root.AddState("Crouch",        new Vector3(250, 120, 0));
        var stJump   = root.AddState("Jump",          new Vector3(250, -40, 0));
        var stPunch  = root.AddState("Attack_Punch",  new Vector3(400, 200, 0));
        var stKick   = root.AddState("Attack_Kick",   new Vector3(400, 100, 0));
        var stHook   = root.AddState("Attack_Hook",   new Vector3(400, 0,   0));
        var stDgFwd  = root.AddState("Dodge_Fwd",     new Vector3(400, -120, 0));
        var stDgBk   = root.AddState("Dodge_Back",    new Vector3(400, -220, 0));
        var stDgLf   = root.AddState("Dodge_Left",    new Vector3(400, -320, 0));
        var stDgRt   = root.AddState("Dodge_Right",   new Vector3(400, -420, 0));
        var stHit    = root.AddState("Hit_Torso",     new Vector3(600, 200, 0));
        var stStun   = root.AddState("Stunned",       new Vector3(600, 100, 0));
        var stDeath  = root.AddState("Death",         new Vector3(600, 0,   0));

        stMove.motion   = locomotion;
        stCrouch.motion = walk;
        stJump.motion   = jump;
        stPunch.motion  = atkPunch;
        stKick.motion   = atkKick;
        stHook.motion   = atkHook;
        stDgFwd.motion  = dodgeFwd;
        stDgBk.motion   = dodgeBk;
        stDgLf.motion   = dodgeLf;
        stDgRt.motion   = dodgeRt;
        stHit.motion    = hitTorso;
        stStun.motion   = stunned;
        stDeath.motion  = die;

        root.defaultState = stMove;

        // ── Crouch (Locomotion <-> Crouch) ───────────────────────
        var toCrouch = stMove.AddTransition(stCrouch);
        toCrouch.hasExitTime = false;
        toCrouch.duration    = 0.15f;
        toCrouch.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");

        var toMove = stCrouch.AddTransition(stMove);
        toMove.hasExitTime = false;
        toMove.duration    = 0.15f;
        toMove.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");

        // ── Jump (AnyState -> Jump; Jump -> Locomotion on landing) ──
        var anyJump = root.AddAnyStateTransition(stJump);
        anyJump.hasExitTime = false;
        anyJump.duration    = 0.1f;
        anyJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");

        var land = stJump.AddTransition(stMove);
        land.hasExitTime = true;
        land.exitTime    = 0.6f;
        land.duration    = 0.2f;
        land.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");

        // ── Attack combo (AnyState -> Attack_X by ComboIndex) ────
        AddAttackTransition(root, stPunch, 0);
        AddAttackTransition(root, stKick,  1);
        AddAttackTransition(root, stHook,  2);
        AddExitTransition(stPunch, stMove);
        AddExitTransition(stKick,  stMove);
        AddExitTransition(stHook,  stMove);

        // ── Dodge (AnyState -> Dodge_X by DodgeDirection) ────────
        AddDodgeTransition(root, stDgFwd, 0);
        AddDodgeTransition(root, stDgBk,  1);
        AddDodgeTransition(root, stDgLf,  2);
        AddDodgeTransition(root, stDgRt,  3);
        AddExitTransition(stDgFwd, stMove);
        AddExitTransition(stDgBk,  stMove);
        AddExitTransition(stDgLf,  stMove);
        AddExitTransition(stDgRt,  stMove);

        // ── Hit reaction (Hurt -> torso, HeavyHit -> stunned) ────
        var toHit = root.AddAnyStateTransition(stHit);
        toHit.hasExitTime = false;
        toHit.duration    = 0.05f;
        toHit.AddCondition(AnimatorConditionMode.If, 0, "Hurt");
        AddExitTransition(stHit, stMove);

        var toStun = root.AddAnyStateTransition(stStun);
        toStun.hasExitTime = false;
        toStun.duration    = 0.05f;
        toStun.AddCondition(AnimatorConditionMode.If, 0, "HeavyHit");
        AddExitTransition(stStun, stMove);

        // ── Death (AnyState -> Death, terminal) ──────────────────
        var toDeath = root.AddAnyStateTransition(stDeath);
        toDeath.hasExitTime = false;
        toDeath.duration    = 0.1f;
        toDeath.AddCondition(AnimatorConditionMode.If, 0, "Death");

        EditorUtility.SetDirty(controller);
        return controller;
    }

    static void AddAttackTransition(AnimatorStateMachine root, AnimatorState state, int comboIndex)
    {
        var t = root.AddAnyStateTransition(state);
        t.hasExitTime = false;
        t.duration    = 0.05f;
        t.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        t.AddCondition(AnimatorConditionMode.Equals, comboIndex, "ComboIndex");
    }

    static void AddDodgeTransition(AnimatorStateMachine root, AnimatorState state, int direction)
    {
        var t = root.AddAnyStateTransition(state);
        t.hasExitTime = false;
        t.duration    = 0.05f;
        t.AddCondition(AnimatorConditionMode.If, 0, "Dodge");
        t.AddCondition(AnimatorConditionMode.Equals, direction, "DodgeDirection");
    }

    static void AddExitTransition(AnimatorState from, AnimatorState to)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime    = 0.8f;
        t.duration    = 0.15f;
    }

    static BlendTree BuildLocomotionBlendTree(AnimationClip idle, AnimationClip walk, AnimationClip run)
    {
        var blendTree = new BlendTree
        {
            name               = "Locomotion",
            blendType          = BlendTreeType.Simple1D,
            blendParameter     = "Speed",
            useAutomaticThresholds = false
        };

        blendTree.AddChild(idle, 0f);
        blendTree.AddChild(walk, 0.5f);
        blendTree.AddChild(run,  1f);

        return blendTree;
    }

    static void AssignToScenePlayer(AnimatorController controller)
    {
        GameObject playerGO = GameObject.Find("Player");
        if (playerGO == null)
        {
            Debug.LogWarning("[BuildPlayerAnimator] 'Player' not found in scene. Controller created but not assigned.");
            return;
        }

        Transform character = playerGO.transform.Find("PlayerCharacter");
        var animator = character != null ? character.GetComponent<Animator>() : null;
        if (animator == null)
        {
            Debug.LogWarning("[BuildPlayerAnimator] PlayerCharacter/Animator not found. Assign manually.");
            return;
        }

        var so = new SerializedObject(animator);
        so.FindProperty("m_Controller").objectReferenceValue = controller;
        so.FindProperty("m_ApplyRootMotion").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(animator);
        EditorSceneManager.MarkSceneDirty(playerGO.scene);
    }

    static AnimationClip LoadClip(string directory, string fbxName)
    {
        string path = $"{directory}/{fbxName}";
        Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var obj in all)
            if (obj is AnimationClip clip && !clip.name.StartsWith("__"))
                return clip;
        Debug.LogWarning($"[BuildPlayerAnimator] No clip found in: {path}");
        return null;
    }
}
