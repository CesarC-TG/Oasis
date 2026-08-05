using UnityEngine;
using UnityEditor;

/// <summary>
/// Standalone tool to fix animation clip loop settings across the project.
///
/// Scans all FBX models under the configured animation directories and
/// applies common-sense loop heuristics:
///   Loop=true   → Idle, Walk, Run, CrouchWalk, any continuous locomotion
///   Loop=false  → Attack, Jump, Dodge, Hit, Death, one-shot actions
///
/// Run via: Oasis > Fix Animation Loop Settings
///
/// Unlike the SetAllHumanoid() steps in PlayerAnimationSetup / PlayerAnimatorUpgrade,
/// this tool only touches loop settings and does NOT change animation type or avatar setup.
/// Use this when you've re-imported or added new animations and the loop flags are wrong.
/// </summary>
public static class AnimationLoopFixer
{
    private static readonly string[] SearchPaths =
    {
        "Assets/Characters/Animations",
        "Assets/Animation/Animation_Player"
    };

    // ── Keywords: clips whose filename matches ANY of these → loop = true ──
    private static readonly string[] LoopKeywords =
    {
        "idle", "walk", "run", "crouched_walking", "crouch_walk",
        "breathing", "idle_", "_idle"
    };

    // ── Keywords: clips whose filename matches ANY of these → loop = false ──
    //    (checked AFTER LoopKeywords so looping keywords take priority)
    private static readonly string[] OneShotKeywords =
    {
        "attack", "jump", "dodge", "hit", "death", "die",
        "punch", "kick", "hook", "fall", "stunned", "stun",
        "reaction", "crouch_to", "stand_to", "crouch_down",
        "crouch_up", "heavy", "getting_up"
    };

    [MenuItem("Oasis/Fix Animation Loop Settings")]
    public static void Run()
    {
        int totalClips = 0;
        int changedClips = 0;

        foreach (string searchPath in SearchPaths)
        {
            if (!AssetDatabase.IsValidFolder(searchPath))
            {
                Debug.LogWarning($"[LoopFixer] Folder not found, skipping: {searchPath}");
                continue;
            }

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { searchPath });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.ToLower().EndsWith(".fbx")) continue;

                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                var clipAnimations = importer.clipAnimations.Length > 0
                    ? importer.clipAnimations
                    : importer.defaultClipAnimations;

                if (clipAnimations == null || clipAnimations.Length == 0) continue;

                bool modelChanged = false;

                for (int i = 0; i < clipAnimations.Length; i++)
                {
                    var clip = clipAnimations[i];
                    string clipNameLower = clip.name.ToLower();

                    bool shouldLoop = DetermineLoop(clipNameLower);

                    if (clip.loopTime != shouldLoop || clip.loopPose != shouldLoop)
                    {
                        clip.loopTime = shouldLoop;
                        clip.loopPose = shouldLoop;
                        clipAnimations[i] = clip;
                        modelChanged = true;
                        changedClips++;
                        Debug.Log($"[LoopFixer] {(shouldLoop ? "LOOP" : "ONCE")}  →  {path} / {clip.name}");
                    }
                    totalClips++;
                }

                if (modelChanged)
                {
                    importer.clipAnimations = clipAnimations;
                    importer.SaveAndReimport();
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Animation Loop Fixer",
            $"Scanned {totalClips} animation clips across {SearchPaths.Length} directories.\n\n" +
            $"Changed: {changedClips}\n" +
            $"Unchanged: {totalClips - changedClips}\n\n" +
            "See Console for per-clip details.",
            "OK");
    }

    // ── Loop heuristic ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the clip's lowercase name indicates a looping animation.
    /// Loop keywords are checked first; if none match, one-shot keywords are checked.
    /// Default is non-looping (false) for unrecognised names.
    /// </summary>
    static bool DetermineLoop(string clipNameLower)
    {
        foreach (string kw in LoopKeywords)
            if (clipNameLower.Contains(kw))
                return true;

        foreach (string kw in OneShotKeywords)
            if (clipNameLower.Contains(kw))
                return false;

        // Default: non-looping (safe for one-shot clips)
        return false;
    }
}
