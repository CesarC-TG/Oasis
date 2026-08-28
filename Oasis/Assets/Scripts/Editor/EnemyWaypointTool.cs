using UnityEditor;
using UnityEditor.AI;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace Oasis.Editor
{
    /// <summary>
    /// Editor tools: waypoint creation and NavMesh baking shortcuts.
    /// </summary>
    public static class EnemyWaypointTool
    {
        [MenuItem("Oasis/Create Patrol Waypoints")]
        public static void CreateWaypoints()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                Debug.LogWarning("[WaypointTool] Select an enemy GameObject first.");
                return;
            }

            var enemyAI = selected.GetComponent<Enemy.EnemyAI>();
            if (enemyAI == null)
            {
                Debug.LogWarning("[WaypointTool] Selected object has no EnemyAI component.");
                return;
            }

            // Create a parent folder for waypoints
            var waypointParent = new GameObject($"{selected.name}_Waypoints");
            waypointParent.transform.SetParent(selected.transform.parent);
            waypointParent.transform.position = selected.transform.position;
            waypointParent.transform.rotation = Quaternion.identity;

            var waypoints = new Transform[4];
            float radius = 8f;
            float startAngle = 0f;

            for (int i = 0; i < 4; i++)
            {
                float angle = startAngle + (i * 90f) * Mathf.Deg2Rad;
                var wp = new GameObject($"Waypoint_{i + 1}");
                wp.transform.SetParent(waypointParent.transform);
                wp.transform.position = selected.transform.position +
                    new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                // Visualize with an icon in the scene view
                wp.AddComponent<WaypointGizmo>();
                waypoints[i] = wp.transform;
            }

            // Assign to EnemyAI
            var serializedObj = new SerializedObject(enemyAI);
            var patrolProp = serializedObj.FindProperty("PatrolPoints");
            patrolProp.arraySize = waypoints.Length;
            for (int i = 0; i < waypoints.Length; i++)
                patrolProp.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
            serializedObj.ApplyModifiedProperties();

            EditorUtility.SetDirty(enemyAI);
            Selection.activeGameObject = waypointParent;
            Debug.Log($"[WaypointTool] Created 4 waypoints for {selected.name}. Radius: {radius}m");
        }

        [MenuItem("Oasis/Create Patrol Waypoints", true)]
        public static bool CreateWaypointsValidate()
        {
            var selected = Selection.activeGameObject;
            return selected != null && selected.GetComponent<Enemy.EnemyAI>() != null;
        }

        // ─── NavMesh ────────────────────────────────────────────────────────

        [MenuItem("Oasis/Setup & Bake NavMesh")]
        public static void SetupAndBakeNavMesh()
        {
            // Step 1: Add NavMeshSurface to the Ground plane
            var ground = GameObject.Find("Ground");
            if (ground == null)
            {
                // Try without the plane — find any terrain or large collider
                ground = GameObject.Find("Plane");
            }
            if (ground == null)
            {
                Debug.LogError("[NavMesh] No 'Ground' or 'Plane' found in scene. Create a plane named 'Ground' first.");
                return;
            }

            var surface = ground.GetComponent<NavMeshSurface>();
            if (surface == null)
                surface = ground.AddComponent<NavMeshSurface>();

            surface.collectObjects = CollectObjects.All;
            surface.defaultArea = NavMesh.GetAreaFromName("Walkable");

            // Step 2: Mark non-walkable objects
            var enemy = GameObject.Find("Enemy_Vaciado");
            if (enemy != null)
            {
                var enemySurface = enemy.GetComponent<NavMeshSurface>();
                if (enemySurface != null) Object.DestroyImmediate(enemySurface);
            }

            // Step 3: Bake
            NavMesh.RemoveAllNavMeshData();
            surface.BuildNavMesh();

            Debug.Log("[NavMesh] Baked successfully! Ground marked as walkable.");
        }

        [MenuItem("Oasis/Setup & Bake NavMesh", true)]
        public static bool BakeNavMeshValidate() => !Application.isPlaying;
    }

    /// <summary>
    /// Draws a visible sphere gizmo for waypoints in the Scene view.
    /// </summary>
    public class WaypointGizmo : MonoBehaviour
    {
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 1f, 0.8f, 0.7f);
            Gizmos.DrawSphere(transform.position, 0.3f);
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
