using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

/// <summary>
/// Full-combat upgrade for the Player Animator Controller.
///
/// Builds a complete state machine covering:
///   Locomotion (Idle / Walk / Run blend tree) + RunTired
///   Sequential Attack combo (Punch → Kick → Hook via ComboIndex 0→1→2→0)
///   4-directional Dodge (Fwd / Back / Left / Right)
///   4-type Hit Reactions (LeftHead / RightHead / Torso / Stunned)
///   Crouch sub-SM (Stand_toCrouch → CrouchBlend → Crouched_toStand)
///   Jump → Idle (no Fall transition — Jump exits directly to Idle)
///   Fall_Reaction via HeavyHit trigger
///   Death (terminal)
///
/// Run via: Oasis > Upgrade Player Animator (Full Combat)
///
/// Clips sourced from:  Assets/Animation/Animation_Player/
/// Output controller:   Assets/Characters/Animations/PlayerAnimator.controller
/// </summary>
public static class PlayerAnimatorUpgrade
{
    // ── Paths ───────────────────────────────────────────────────────────────

    private const string AnimPath   = "Assets/Animation/Animation_Player";
    private const string OutputPath = "Assets/Characters/Animations/PlayerAnimator.controller";

    // ── Blend / exit-time constants ─────────────────────────────────────────

    private const float ExitTimeAttack = 0.85f;
    private const float ExitTimeDodge  = 0.85f;
    private const float ExitTimeJump   = 1.0f;       // Jump freezes last frame — exits via IsGrounded only
    private const float ExitTimeFall   = 0.90f;
    private const float ExitTimeCrouch = 0.90f;
    private const float ExitTimeHit    = 0.85f;
    private const float BlendDuration  = 0.15f;

    // ── Menu entry point ────────────────────────────────────────────────────

    [MenuItem("Oasis/Upgrade Player Animator (Full Combat)")]
    public static void Run()
    {
        SetAllHumanoid();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        AnimatorController controller = BuildFullAnimatorController();
        AssignControllerToScenePlayer(controller);

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog(
            "Oasis — Player Animator Upgrade",
            "Player Animator upgraded with full combat state machine.\n\n" +
            "Parameters:\n" +
            "  Speed (float), IsCrouching (bool), IsGrounded (bool), IsRunning (bool)\n" +
            "  Jump (trigger), Attack (trigger), ComboIndex (int, 0=Punch / 1=Kick / 2=Hook)\n" +
            "  Dodge (trigger), DodgeDirection (int, 0=Fwd / 1=Back / 2=Left / 3=Right)\n" +
            "  HeavyHit (trigger), HitReaction (trigger), HitType (int), Hurt (trigger)\n" +
            "  Death (trigger)\n\n" +
            "Controller assigned to scene PlayerCharacter.",
            "OK");
    }

    // ── Humanoid reimport ───────────────────────────────────────────────────

    private static void SetAllHumanoid()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { AnimPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;

            // Loop locomotion & crouch-walking; others play once
            string lower    = path.ToLower();
            bool shouldLoop = lower.Contains("walk") ||
                              lower.Contains("running") ||
                              lower.Contains("crouched_walking");

            ModelImporterClipAnimation[] clips =
                importer.clipAnimations.Length > 0
                    ? importer.clipAnimations
                    : importer.defaultClipAnimations;

            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].loopTime = shouldLoop;
                clips[i].loopPose = shouldLoop;
            }
            importer.clipAnimations = clips;

            importer.SaveAndReimport();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[PlayerUpgrade] Humanoid + Loop({shouldLoop}): {path}");
        }
    }

    // ── Animator Controller builder ─────────────────────────────────────────

    private static AnimatorController BuildFullAnimatorController()
    {
        // Delete existing so we start fresh
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(OutputPath) != null)
            AssetDatabase.DeleteAsset(OutputPath);

        AnimatorController controller =
            AnimatorController.CreateAnimatorControllerAtPath(OutputPath);
        AnimatorStateMachine root = controller.layers[0].stateMachine;

        // ── Parameters ──────────────────────────────────────────────────

        controller.AddParameter("Speed",          AnimatorControllerParameterType.Float);
        controller.AddParameter("IsCrouching",    AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsGrounded",     AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsRunning",      AnimatorControllerParameterType.Bool);
        controller.AddParameter("Jump",           AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack",         AnimatorControllerParameterType.Trigger);
        controller.AddParameter("ComboIndex",     AnimatorControllerParameterType.Int);
        controller.AddParameter("Dodge",          AnimatorControllerParameterType.Trigger);
        controller.AddParameter("DodgeDirection", AnimatorControllerParameterType.Int);
        controller.AddParameter("HeavyHit",       AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hurt",           AnimatorControllerParameterType.Trigger);
        controller.AddParameter("HitReaction",    AnimatorControllerParameterType.Trigger);
        controller.AddParameter("HitType",        AnimatorControllerParameterType.Int);
        controller.AddParameter("Death",          AnimatorControllerParameterType.Trigger);

        // ── Load all clips ──────────────────────────────────────────────

        AnimationClip idle     = LoadClip("Movement",   "Walking");      // first frame as idle pose
        AnimationClip walk     = LoadClip("Movement",   "Walking");
        AnimationClip run      = LoadClip("Movement",   "Running");
        AnimationClip runTired = LoadClip("Movement",   "Running_tired");
        AnimationClip jumpClip = LoadClip("JumpFall",   "Jump");
        AnimationClip fallClip = LoadClip("JumpFall",   "Fall");

        AnimationClip punch    = LoadClip("Attack",     "Punching");
        AnimationClip kick     = LoadClip("Attack",     "Kick");
        AnimationClip hook     = LoadClip("Attack",     "Hook");

        AnimationClip dodgeFwd   = LoadClip("Dodge", "Dodge");
        AnimationClip dodgeBack  = LoadClip("Dodge", "Dodge_backward");
        AnimationClip dodgeLeft  = LoadClip("Dodge", "Dodge_left");
        AnimationClip dodgeRight = LoadClip("Dodge", "Dodge_right");

        AnimationClip hitLeft  = LoadClip("HitReaction", "Hit_leftHeadReaction");
        AnimationClip hitRight = LoadClip("HitReaction", "Hit_rigthHeadReaction");
        AnimationClip hitTorso = LoadClip("HitReaction", "Hit_torsoReaction");
        AnimationClip stunned  = LoadClip("HitReaction", "Stunned");

        AnimationClip crouchDown = LoadClip("Crouch", "Standing_toCrouch");
        AnimationClip crouchWalk = LoadClip("Crouch", "Crouched_walking");
        AnimationClip crouchUp   = LoadClip("Crouch", "Crouched_toStanding");

        AnimationClip dieClip = LoadClip("Death", "Die");

        Debug.Log(
            $"[PlayerUpgrade] Clips loaded — Idle:{idle} Walk:{walk} Run:{run} " +
            $"Punch:{punch} Kick:{kick} Hook:{hook} Die:{dieClip}");

        // ═══════════════════════════════════════════════════════════════════
        //  LOCOMOTION  (blend tree: Idle ←→ Walk ←→ Run)
        // ═══════════════════════════════════════════════════════════════════

        BlendTree locoTree = new BlendTree
        {
            name           = "Locomotion",
            blendType      = BlendTreeType.Simple1D,
            blendParameter = "Speed",
            children = new[]
            {
                new ChildMotion { motion = idle, timeScale = 1f, threshold = 0f   },
                new ChildMotion { motion = walk, timeScale = 1f, threshold = 0.5f },
                new ChildMotion { motion = run,  timeScale = 1f, threshold = 1f   },
            },
            useAutomaticThresholds = false,
        };

        AnimatorState stLoco = AddState(root, "Locomotion", locoTree, new Vector3(0, 0));
        root.defaultState = stLoco;

        // ── RunTired (exhaustion) ────────────────────────────────────────

        AnimatorState stRunTired = AddState(root, "RunTired", runTired, new Vector3(120, 150));
        ConditionTransition(stLoco,     stRunTired, "IsRunning", AnimatorConditionMode.If,    0f);
        ConditionTransition(stRunTired, stLoco,     "IsRunning", AnimatorConditionMode.IfNot, 0f);

        // ═══════════════════════════════════════════════════════════════════
        //  JUMP  (AnyState → Jump on Jump trigger; freezes last frame until IsGrounded, then → Idle)
        // ═══════════════════════════════════════════════════════════════════

        AnimatorState stJump = AddState(root, "Jump", jumpClip, new Vector3(300, 0));
        stJump.writeDefaultValues = false; // Keep last-frame pose when frozen
        {
            AnimatorStateTransition anyJump = root.AddAnyStateTransition(stJump);
            anyJump.hasExitTime = false;
            anyJump.duration    = 0.1f;
            anyJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");
        }
        {
            // Exit when landing (IsGrounded becomes true), NOT based on clip time.
            // Jump clip reaches its last frame and freezes there (writeDefaultValues=false).
            AnimatorStateTransition landJump = stJump.AddTransition(root.defaultState); // → Idle
            landJump.hasExitTime = false;
            landJump.duration    = 0.15f;
            landJump.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        }

        // ═══════════════════════════════════════════════════════════════════
        //  FALL REACTION  (AnyState → Fall_Reaction on HeavyHit)
        // ═══════════════════════════════════════════════════════════════════

        AnimatorState stFallReaction = AddState(root, "Fall_Reaction", fallClip, new Vector3(420, 0));
        {
            AnimatorStateTransition anyFall = root.AddAnyStateTransition(stFallReaction);
            anyFall.hasExitTime = false;
            anyFall.duration    = 0.1f;
            anyFall.AddCondition(AnimatorConditionMode.If, 0, "HeavyHit");
        }
        {
            AnimatorStateTransition exitFall = stFallReaction.AddExitTransition();
            exitFall.hasExitTime = true;
            exitFall.exitTime    = ExitTimeFall;
            exitFall.duration    = BlendDuration;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  ATTACK — sequential combo via ComboIndex (0=Punch / 1=Kick / 2=Hook)
        //
        //  All three states live at root level; each has its own AnyState
        //  transition gated on (Attack == true AND ComboIndex == N).
        //  Exits via exitTime back to the default Locomotion blend tree.
        // ═══════════════════════════════════════════════════════════════════

        AnimatorState stPunch = AddState(root, "Attack_Punch", punch, new Vector3(-150, -250));
        stPunch.speed = 1.2f;
        AnimatorState stKick = AddState(root, "Attack_Kick", kick, new Vector3(0, -250));
        stKick.speed = 1.1f;
        AnimatorState stHook = AddState(root, "Attack_Hook", hook, new Vector3(150, -250));
        stHook.speed = 0.95f;

        // Punch — ComboIndex == 0
        BuildAnyStateAttack(root, stPunch, 0);
        // Kick  — ComboIndex == 1
        BuildAnyStateAttack(root, stKick,  1);
        // Hook  — ComboIndex == 2
        BuildAnyStateAttack(root, stHook,  2);

        // ═══════════════════════════════════════════════════════════════════
        //  DODGE — 4-directional (Dodge trigger + DodgeDirection int)
        //
        //  DodgeDirection mapping: 0 = Fwd, 1 = Back, 2 = Left, 3 = Right
        // ═══════════════════════════════════════════════════════════════════

        AnimatorState stDodgeFwd   = AddState(root, "Dodge_Fwd",   dodgeFwd,   new Vector3(300, -250));
        AnimatorState stDodgeBack  = AddState(root, "Dodge_Back",  dodgeBack,  new Vector3(420, -250));
        AnimatorState stDodgeLeft  = AddState(root, "Dodge_Left",  dodgeLeft,  new Vector3(540, -250));
        AnimatorState stDodgeRight = AddState(root, "Dodge_Right", dodgeRight, new Vector3(660, -250));

        BuildDodge(root, stDodgeFwd,   0);
        BuildDodge(root, stDodgeBack,  1);
        BuildDodge(root, stDodgeLeft,  2);
        BuildDodge(root, stDodgeRight, 3);

        // ═══════════════════════════════════════════════════════════════════
        //  CROUCH  (sub-state machine with transition anims)
        //
        //  Entry:  Locomotion / RunTired → crouchSM::Stand_toCrouch on IsCrouching==true
        //  Inside: Stand_toCrouch → CrouchLocomotion (blend tree) → Crouched_toStand
        //  Exit:   Crouched_toStand exits SM → back to Locomotion (IsCrouching==false)
        // ═══════════════════════════════════════════════════════════════════

        AnimatorStateMachine crouchSM = root.AddStateMachine("Crouch", new Vector3(-150, -450));

        AnimatorState stCrouchDown = crouchSM.AddState("Stand_toCrouch", new Vector3(-120, -30));
        if (crouchDown != null) stCrouchDown.motion = crouchDown;
        crouchSM.defaultState = stCrouchDown;

        // Blend tree inside crouch: idle crouch ↔ crouch walk via Speed
        BlendTree crouchBlend = new BlendTree
        {
            name           = "CrouchBlend",
            blendType      = BlendTreeType.Simple1D,
            blendParameter = "Speed",
            children = new[]
            {
                new ChildMotion { motion = crouchWalk, timeScale = 1f, threshold = 0f   },
                new ChildMotion { motion = crouchWalk, timeScale = 1f, threshold = 0.5f },
            },
            useAutomaticThresholds = false,
        };
        AnimatorState stCrouchLoco = crouchSM.AddState("CrouchLocomotion", new Vector3(0, -30));
        stCrouchLoco.motion = crouchBlend;

        AnimatorState stCrouchUp = crouchSM.AddState("Crouched_toStand", new Vector3(120, -30));
        if (crouchUp != null) stCrouchUp.motion = crouchUp;

        // Inside crouch SM transitions
        {
            AnimatorStateTransition t = stCrouchDown.AddTransition(stCrouchLoco);
            t.hasExitTime = true;
            t.exitTime    = ExitTimeCrouch;
            t.duration    = BlendDuration;
        }
        ConditionTransition(stCrouchLoco, stCrouchUp, "IsCrouching", AnimatorConditionMode.IfNot, 0f);

        // Exit the sub-SM back to root Locomotion
        {
            AnimatorStateTransition t = stCrouchUp.AddExitTransition();
            t.hasExitTime = true;
            t.exitTime    = ExitTimeCrouch;
            t.duration    = BlendDuration;
        }

        // Entry from root locomotion states into crouch SM
        ConditionTransition(stLoco,     stCrouchDown, "IsCrouching", AnimatorConditionMode.If, 0f);
        ConditionTransition(stRunTired, stCrouchDown, "IsCrouching", AnimatorConditionMode.If, 0f);

        // ═══════════════════════════════════════════════════════════════════
        //  HIT REACTION  (sub-state machine, routed by HitType int)
        //
        //  HitType: 0 = LeftHead, 1 = RightHead, 2 = Torso, 3 = Stunned
        //  Entry via HitReaction trigger from AnyState.
        //  All states exit after playing (exitTime).
        // ═══════════════════════════════════════════════════════════════════

        AnimatorStateMachine hitSM = root.AddStateMachine("HitReaction", new Vector3(700, -450));

        AnimatorState stHitL = hitSM.AddState("Hit_LeftHead",  new Vector3(-120, 0));
        if (hitLeft  != null) stHitL.motion = hitLeft;
        AnimatorState stHitR = hitSM.AddState("Hit_RightHead", new Vector3(120, 0));
        if (hitRight != null) stHitR.motion = hitRight;
        AnimatorState stHitT = hitSM.AddState("Hit_Torso",     new Vector3(0, 80));
        if (hitTorso != null) stHitT.motion = hitTorso;
        AnimatorState stStun = hitSM.AddState("Stunned",       new Vector3(0, -80));
        if (stunned  != null) stStun.motion = stunned;

        hitSM.defaultState = stHitT;

        // AnyState → hitSM on HitReaction trigger
        {
            AnimatorStateTransition anyHit = root.AddAnyStateTransition(hitSM);
            anyHit.hasExitTime = false;
            anyHit.duration    = 0.05f;
            anyHit.AddCondition(AnimatorConditionMode.If, 0, "HitReaction");
        }

        // Route by HitType
        AddEntryRoute(hitSM, stHitL, 0);
        AddEntryRoute(hitSM, stHitR, 1);
        AddEntryRoute(hitSM, stHitT, 2);
        AddEntryRoute(hitSM, stStun, 3);

        // Exit after clip plays
        BuildHitExit(stHitL);
        BuildHitExit(stHitR);
        BuildHitExit(stHitT);
        BuildHitExit(stStun);

        // ═══════════════════════════════════════════════════════════════════
        //  DEATH  (terminal — no exit transition)
        // ═══════════════════════════════════════════════════════════════════

        AnimatorState stDeath = AddState(root, "Death", dieClip, new Vector3(700, 0));
        {
            AnimatorStateTransition anyDeath = root.AddAnyStateTransition(stDeath);
            anyDeath.hasExitTime = false;
            anyDeath.duration    = 0.1f;
            anyDeath.AddCondition(AnimatorConditionMode.If, 0, "Death");
            // No exit transition — death is terminal
        }

        // ── Finalise ────────────────────────────────────────────────────

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    // ── Scene assignment ────────────────────────────────────────────────────

    private static void AssignControllerToScenePlayer(AnimatorController controller)
    {
        GameObject playerGO = GameObject.Find("Player");
        if (playerGO == null)
        {
            Debug.LogError("[PlayerUpgrade] 'Player' GameObject not found in scene.");
            return;
        }

        Transform t = playerGO.transform.Find("PlayerCharacter");
        Animator animator = t != null ? t.GetComponent<Animator>() : null;
        if (animator == null)
        {
            Debug.LogError("[PlayerUpgrade] No Animator on PlayerCharacter child.");
            return;
        }

        Undo.RecordObject(animator, "Assign Full-Combat Animator");
        SerializedObject so = new SerializedObject(animator);
        so.FindProperty("m_Controller").objectReferenceValue = controller;
        so.FindProperty("m_ApplyRootMotion").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(animator);
        if (t != null) EditorUtility.SetDirty(t.gameObject);
        EditorSceneManager.MarkSceneDirty(playerGO.scene);

        Debug.Log($"[PlayerUpgrade] Controller={controller.name} | Avatar={animator.avatar}");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  BUILDERS  (transition factories)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Add a state to a state machine, optionally assigning a motion clip.
    /// </summary>
    private static AnimatorState AddState(
        AnimatorStateMachine sm, string name, AnimationClip clip, Vector3 pos)
    {
        AnimatorState state = sm.AddState(name, pos);
        if (clip != null) state.motion = clip;
        return state;
    }

    /// <summary>
    /// Add a state with a BlendTree as its motion.
    /// </summary>
    private static AnimatorState AddState(
        AnimatorStateMachine sm, string name, BlendTree tree, Vector3 pos)
    {
        AnimatorState state = sm.AddState(name, pos);
        state.motion = tree;
        return state;
    }

    /// <summary>
    /// Add a condition-based (non-exit-time) transition with a blend duration.
    /// </summary>
    private static void ConditionTransition(
        AnimatorState from, AnimatorState to,
        string param, AnimatorConditionMode mode, float threshold)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration    = BlendDuration;
        t.AddCondition(mode, threshold, param);
    }

    /// <summary>
    /// Build an AnyState → Attack transition gated on (Attack == true AND ComboIndex == index).
    /// Adds an exitTime-based exit back to the default state.
    /// </summary>
    private static void BuildAnyStateAttack(
        AnimatorStateMachine root, AnimatorState state, int comboIndex)
    {
        AnimatorStateTransition entry = root.AddAnyStateTransition(state);
        entry.hasExitTime = false;
        entry.duration    = 0.05f;
        entry.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        entry.AddCondition(AnimatorConditionMode.Equals, comboIndex, "ComboIndex");

        AnimatorStateTransition exit = state.AddExitTransition();
        exit.hasExitTime = true;
        exit.exitTime    = ExitTimeAttack;
        exit.duration    = BlendDuration;
    }

    /// <summary>
    /// Build an AnyState → Dodge transition gated on (Dodge == true AND DodgeDirection == dir).
    /// Adds an exitTime-based exit back to the default state.
    /// </summary>
    private static void BuildDodge(
        AnimatorStateMachine root, AnimatorState state, int direction)
    {
        AnimatorStateTransition entry = root.AddAnyStateTransition(state);
        entry.hasExitTime = false;
        entry.duration    = 0.05f;
        entry.AddCondition(AnimatorConditionMode.If, 0, "Dodge");
        entry.AddCondition(AnimatorConditionMode.Equals, direction, "DodgeDirection");

        AnimatorStateTransition exit = state.AddExitTransition();
        exit.hasExitTime = true;
        exit.exitTime    = ExitTimeDodge;
        exit.duration    = BlendDuration;
    }

    /// <summary>
    /// Add an exitTime-based exit transition for a HitReaction state.
    /// </summary>
    private static void BuildHitExit(AnimatorState state)
    {
        AnimatorStateTransition t = state.AddExitTransition();
        t.hasExitTime = true;
        t.exitTime    = ExitTimeHit;
        t.duration    = BlendDuration;
    }

    /// <summary>
    /// Add an entry-route condition to a sub-state-machine: HitType == value → targetState.
    /// </summary>
    private static void AddEntryRoute(
        AnimatorStateMachine sm, AnimatorState target, int hitTypeValue)
    {
        AnimatorTransition entry = sm.AddEntryTransition(target);
        entry.AddCondition(AnimatorConditionMode.Equals, hitTypeValue, "HitType");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  CLIP LOADER
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Load the first AnimationClip from an FBX in a subfolder whose filename
    /// contains <paramref name="match"/> (case-insensitive).
    /// </summary>
    private static AnimationClip LoadClip(string subfolder, string match)
    {
        string searchFolder = $"{AnimPath}/{subfolder}";
        string lowerMatch   = match.ToLower();

        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { searchFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.ToLower().EndsWith(".fbx")) continue;
            if (!path.ToLower().Contains(lowerMatch)) continue;

            Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (Object obj in all)
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__"))
                    return clip;
            }
            Debug.LogWarning($"[PlayerUpgrade] No clip found inside {path}");
        }
        Debug.LogWarning($"[PlayerUpgrade] No FBX matching '{match}' in {searchFolder}");
        return null;
    }
}
