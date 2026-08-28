using UnityEngine;
using UnityEditor;
using Oasis.Enemy;

public static class SpawnPatrollingEnemy
{
    private const string PrefabPath = "Assets/Animation/Animation_Vaciado/Vaciado.prefab";

    [MenuItem("Oasis/Spawn Patrolling Enemy")]
    public static void Run()
    {
        // Load prefab
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[SpawnEnemy] Prefab not found at {PrefabPath}. Run 'Oasis/Setup Vaciado Prefab' first.");
            return;
        }

        // Find player position
        var player = GameObject.Find("Player");
        Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;

        // Spawn 20m away from player
        Vector3 spawnPos = playerPos + new Vector3(18f, 0f, 15f);

        var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        enemy.name = "Vaciado_Patrol";
        enemy.transform.position = spawnPos;

        // Add EnemyAI if missing
        var ai = enemy.GetComponent<EnemyAI>();
        if (ai == null) ai = enemy.AddComponent<EnemyAI>();

        // Create waypoint parent
        var waypointParent = new GameObject("Vaciado_Patrol_Waypoints");
        waypointParent.transform.position = spawnPos;

        var waypoints = new Transform[4];
        float radius = 10f;
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f * Mathf.Deg2Rad;
            var wp = new GameObject($"WP_{i + 1}");
            wp.transform.SetParent(waypointParent.transform);
            wp.transform.position = spawnPos + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            waypoints[i] = wp.transform;
        }

        // Assign waypoints
        var so = new SerializedObject(ai);
        var prop = so.FindProperty("PatrolPoints");
        prop.arraySize = waypoints.Length;
        for (int i = 0; i < waypoints.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
        so.ApplyModifiedProperties();

        Selection.activeGameObject = enemy;
        EditorUtility.SetDirty(enemy);
        Debug.Log($"[SpawnEnemy] Vaciado spawned at {spawnPos} with 4 waypoints (radius 10m). Player at {playerPos}");
    }
}
