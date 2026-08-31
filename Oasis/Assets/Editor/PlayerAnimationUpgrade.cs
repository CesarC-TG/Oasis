using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class PlayerAnimationUpgrade
{
    private const string AnimDir     = "Assets/Characters/Animations";
    private const string OutputPath  = AnimDir + "/PlayerAnimator.controller";

    [MenuItem("Oasis/Player Animator/Upgrade (v2 BlendTree)")]
    public static void Upgrade()
    {
        SetClipLoopSettings();
        var controller = BuildController();
        AssignToScenePlayer(controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Oasis",
            "Animator v2 aplicado: BlendTree (Idle/Walk/Run), Crouch y Jump con aterrizaje.", "OK");
    }

    static void SetClipLoopSettings()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { AnimDir });
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
                clips[i].loopTime           = !isJump;
                clips[i].loopPose           = !isJump;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[Upgrade] Loop settings applied: {path} (jump={isJump})");
        }
    }

    static AnimatorController BuildController()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(OutputPath) != null)
            AssetDatabase.DeleteAsset(OutputPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(OutputPath);

        controller.AddParameter("Speed",        AnimatorControllerParameterType.Float);
        controller.AddParameter("IsCrouching",  AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsGrounded",   AnimatorControllerParameterType.Bool);
        controller.AddParameter("Jump",         AnimatorControllerParameterType.Trigger);

        var root = controller.layers[0].stateMachine;

        AnimationClip idle = LoadClip("PlayerCharacter@Idle");
        AnimationClip walk = LoadClip("PlayerCharacter@Walking");
        AnimationClip run  = LoadClip("PlayerCharacter@Running");
        AnimationClip jump = LoadClip("PlayerCharacter@Jumping");

        var locomotion = BuildLocomotionBlendTree(controller, idle, walk, run);

        var stMove = root.AddState("Locomotion", new Vector3(50,  50, 0));
        var stCrouch = root.AddState("Crouch",   new Vector3(250, 120, 0));
        var stJump   = root.AddState("Jump",     new Vector3(250, -40, 0));

        stMove.motion = locomotion;
        stCrouch.motion = walk;
        stJump.motion   = jump;

        root.defaultState = stMove;

        var toCrouch = stMove.AddTransition(stCrouch);
        toCrouch.hasExitTime = false;
        toCrouch.duration    = 0.15f;
        toCrouch.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");

        var toMove = stCrouch.AddTransition(stMove);
        toMove.hasExitTime = false;
        toMove.duration    = 0.15f;
        toMove.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");

        var anyJump = root.AddAnyStateTransition(stJump);
        anyJump.hasExitTime = false;
        anyJump.duration    = 0.1f;
        anyJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");

        var land = stJump.AddTransition(stMove);
        land.hasExitTime = true;
        land.exitTime    = 0.6f;
        land.duration    = 0.2f;
        land.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");

        EditorUtility.SetDirty(controller);
        return controller;
    }

    static BlendTree BuildLocomotionBlendTree(AnimatorController controller,
                                              AnimationClip idle, AnimationClip walk, AnimationClip run)
    {
        var blendTree = new BlendTree();
        blendTree.name = "Locomotion";
        blendTree.blendType = BlendTreeType.Simple1D;
        blendTree.blendParameter = "Speed";
        blendTree.useAutomaticThresholds = false;

        var so = new SerializedObject(controller);
        so.FindProperty("m_AnimatorLayers").arraySize = 1;

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
            Debug.LogWarning("[Upgrade] 'Player' not found in scene. Controller created but not assigned.");
            return;
        }

        Transform character = playerGO.transform.Find("PlayerCharacter");
        var animator = character != null ? character.GetComponent<Animator>() : null;
        if (animator == null)
        {
            Debug.LogWarning("[Upgrade] PlayerCharacter/Animator not found. Assign the controller manually.");
            return;
        }

        var so = new SerializedObject(animator);
        so.FindProperty("m_Controller").objectReferenceValue = controller;
        so.FindProperty("m_ApplyRootMotion").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(animator);
        EditorSceneManager.MarkSceneDirty(playerGO.scene);
    }

    static AnimationClip LoadClip(string fbxName)
    {
        string path = $"{AnimDir}/{fbxName}.fbx";
        Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var obj in all)
            if (obj is AnimationClip clip && !clip.name.StartsWith("__"))
                return clip;
        Debug.LogWarning($"[Upgrade] No clip found in: {path}");
        return null;
    }
}
