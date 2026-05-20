using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class PlayerAnimationSetup
{
    private const string AnimPath    = "Assets/Characters/Animations";
    private const string OutputPath  = "Assets/Characters/Animations/PlayerAnimator.controller";
    private const string IdleFbxPath = "Assets/Characters/Animations/PlayerCharacter@Idle.fbx";

    [MenuItem("Oasis/Setup Player Animator")]
    public static void Run()
    {
        SetAllHumanoid();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var controller = BuildAnimatorController();
        ReplacePlayerCharacterInScene(controller);

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Oasis",
            "PlayerCharacter reemplazado con rig de Mixamo.\nAnimator y Avatar asignados.", "OK");
    }

    // ── Humanoid reimport ────────────────────────────────────────────────────

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

            // Enable Loop Time on the contained clips (Idle/Walk/Run loop; Jump does not)
            bool shouldLoop = !path.Contains("Jumping");
            var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].loopTime = shouldLoop;
                clips[i].loopPose = shouldLoop;
            }
            importer.clipAnimations = clips;

            importer.SaveAndReimport();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[Setup] Humanoid + Loop({shouldLoop}) forced: {path}");
        }
    }

    // ── Animator Controller ──────────────────────────────────────────────────

    static AnimatorController BuildAnimatorController()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(OutputPath) != null)
            AssetDatabase.DeleteAsset(OutputPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(OutputPath);

        controller.AddParameter("Speed",       AnimatorControllerParameterType.Float);
        controller.AddParameter("IsCrouching", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsGrounded",  AnimatorControllerParameterType.Bool);
        controller.AddParameter("Jump",        AnimatorControllerParameterType.Trigger);

        var root = controller.layers[0].stateMachine;

        AnimationClip idle = LoadClip("PlayerCharacter@Idle");
        AnimationClip walk = LoadClip("PlayerCharacter@Walking");
        AnimationClip run  = LoadClip("PlayerCharacter@Running");
        AnimationClip jump = LoadClip("PlayerCharacter@Jumping");

        Debug.Log($"[Setup] Clips — Idle:{idle?.name} Walk:{walk?.name} Run:{run?.name} Jump:{jump?.name}");

        var stIdle = AddState(root, "Idle", idle, new Vector3(-200,   0));
        var stWalk = AddState(root, "Walk", walk, new Vector3(  50, -80));
        var stRun  = AddState(root, "Run",  run,  new Vector3(  50,  80));
        var stJump = AddState(root, "Jump", jump, new Vector3( 300,   0));

        root.defaultState = stIdle;

        Transition(stIdle, stWalk, "Speed", AnimatorConditionMode.Greater, 0.1f, 0.15f);
        Transition(stIdle, stRun,  "Speed", AnimatorConditionMode.Greater, 0.7f, 0.15f);
        Transition(stWalk, stIdle, "Speed", AnimatorConditionMode.Less,    0.1f, 0.20f);
        Transition(stWalk, stRun,  "Speed", AnimatorConditionMode.Greater, 0.7f, 0.15f);
        Transition(stRun,  stWalk, "Speed", AnimatorConditionMode.Less,    0.7f, 0.15f);
        Transition(stRun,  stIdle, "Speed", AnimatorConditionMode.Less,    0.1f, 0.20f);

        var anyJump = root.AddAnyStateTransition(stJump);
        anyJump.hasExitTime = false; anyJump.duration = 0.1f;
        anyJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");

        var exitJump = stJump.AddTransition(stIdle);
        exitJump.hasExitTime = true; exitJump.exitTime = 0.85f; exitJump.duration = 0.2f;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    // ── Replace PlayerCharacter in scene ─────────────────────────────────────

    static void ReplacePlayerCharacterInScene(AnimatorController controller)
    {
        // Find Player parent
        GameObject playerGO = GameObject.Find("Player");
        if (playerGO == null)
        {
            Debug.LogError("[Setup] 'Player' GameObject not found in scene.");
            return;
        }

        // Find existing PlayerCharacter child (capture local transform values)
        Transform existing = playerGO.transform.Find("PlayerCharacter");
        Vector3 localPos  = existing != null ? existing.localPosition : new Vector3(0, -1, 0);
        Quaternion localRot = existing != null ? existing.localRotation : Quaternion.identity;
        Vector3 localScale = existing != null ? existing.localScale : Vector3.one;

        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
            Debug.Log("[Setup] Removed old PlayerCharacter from scene.");
        }

        // Instantiate the Mixamo-rigged Idle model
        GameObject idleFbx = AssetDatabase.LoadAssetAtPath<GameObject>(IdleFbxPath);
        if (idleFbx == null)
        {
            Debug.LogError($"[Setup] Idle FBX not found at {IdleFbxPath}");
            return;
        }

        GameObject newPlayer = (GameObject)PrefabUtility.InstantiatePrefab(idleFbx, playerGO.scene);
        newPlayer.name = "PlayerCharacter";
        newPlayer.transform.SetParent(playerGO.transform, false);
        newPlayer.transform.localPosition = localPos;
        newPlayer.transform.localRotation = localRot;
        newPlayer.transform.localScale    = localScale;

        // Find Avatar in the FBX
        Avatar foundAvatar = null;
        Object[] all = AssetDatabase.LoadAllAssetsAtPath(IdleFbxPath);
        foreach (var obj in all)
        {
            if (obj is Avatar a) { foundAvatar = a; break; }
        }
        if (foundAvatar == null)
            Debug.LogError($"[Setup] No Avatar found in {IdleFbxPath}. Asset count: {all.Length}");
        else
            Debug.Log($"[Setup] Avatar found: {foundAvatar.name}");

        // Ensure Animator exists
        var animator = newPlayer.GetComponent<Animator>();
        if (animator == null) animator = newPlayer.AddComponent<Animator>();

        // Use SerializedObject for reliable assignment on prefab instances
        Undo.RecordObject(animator, "Assign Animator");
        var so = new SerializedObject(animator);
        so.FindProperty("m_Controller").objectReferenceValue = controller;
        if (foundAvatar != null)
            so.FindProperty("m_Avatar").objectReferenceValue = foundAvatar;
        so.FindProperty("m_ApplyRootMotion").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(animator);
        EditorUtility.SetDirty(newPlayer);
        EditorSceneManager.MarkSceneDirty(playerGO.scene);

        // Verify
        Debug.Log($"[Setup] AFTER assign — Controller: {animator.runtimeAnimatorController} | Avatar: {animator.avatar} | isHuman: {animator.isHuman}");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

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

    static AnimationClip LoadClip(string fbxName)
    {
        string path = $"{AnimPath}/{fbxName}.fbx";
        Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var obj in all)
            if (obj is AnimationClip clip && !clip.name.StartsWith("__"))
                return clip;
        Debug.LogWarning($"[Setup] No clip found in: {path}");
        return null;
    }
}
