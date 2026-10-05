#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;

namespace Robogee.EditorTools
{
    /// <summary>
    /// Builds a desert-wilderness prototype arena with ProBuilder.
    /// Only creates/replaces the "DesertArena" root — does not touch Miwa Pause objects,
    /// combat systems, or Match_Flow documentation.
    /// </summary>
    public static class DesertArenaProBuilderEnsure
    {
        const string RootName = "DesertArena";
        const string ScenePath = "Assets/Scenes/TestField.unity";
        const string MatDir = "Assets/_Project/Materials";
        const string PrefKey = "Robogee.DesertArena.v1";

        [InitializeOnLoadMethod]
        static void AutoEnsure()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.delayCall += TryBuildIfNeeded;
        }

        static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (scene.path == ScenePath)
                EditorApplication.delayCall += TryBuildIfNeeded;
        }

        static void TryBuildIfNeeded()
        {
            if (Application.isPlaying)
                return;

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
                return;

            if (GameObject.Find(RootName) != null)
            {
                EditorPrefs.SetBool(PrefKey, true);
                return;
            }

            BuildIntoOpenScene();
            EditorPrefs.SetBool(PrefKey, true);
        }

        [MenuItem("Robogee/Setup/Build Desert Arena (ProBuilder)")]
        public static void BuildMenu()
        {
            if (!EnsureTestFieldOpen())
                return;
            BuildIntoOpenScene();
            EditorPrefs.SetBool(PrefKey, true);
            Debug.Log("[DesertArena] Built desert wilderness prototype (ProBuilder).");
        }

        static bool EnsureTestFieldOpen()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.path == ScenePath)
                return true;

            if (!File.Exists(ScenePath))
            {
                Debug.LogError("[DesertArena] Missing " + ScenePath);
                return false;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return true;
        }

        static void BuildIntoOpenScene()
        {
            Directory.CreateDirectory(MatDir);

            var sand = EnsureMat("DesertSandMat", new Color(0.76f, 0.58f, 0.32f));
            var rock = EnsureMat("DesertRockMat", new Color(0.42f, 0.32f, 0.24f));
            var cliff = EnsureMat("DesertCliffMat", new Color(0.55f, 0.40f, 0.28f));
            var dune = EnsureMat("DesertDuneMat", new Color(0.82f, 0.66f, 0.40f));

            var existing = GameObject.Find(RootName);
            if (existing != null)
                Object.DestroyImmediate(existing);

            var root = new GameObject(RootName);

            // Playable sand floor — covers spawn lane Z=±8 with margin.
            Place(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(48f, 0.4f, 48f)),
                root.transform, "SandFloor", new Vector3(0f, -0.2f, 0f), sand);

            // Outer cliffs (keep fighters inside the bowl).
            Place(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(52f, 4f, 2.5f)),
                root.transform, "Cliff_N", new Vector3(0f, 1.5f, 24f), cliff);
            Place(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(52f, 4f, 2.5f)),
                root.transform, "Cliff_S", new Vector3(0f, 1.5f, -24f), cliff);
            Place(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(2.5f, 4f, 48f)),
                root.transform, "Cliff_E", new Vector3(24f, 1.5f, 0f), cliff);
            Place(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(2.5f, 4f, 48f)),
                root.transform, "Cliff_W", new Vector3(-24f, 1.5f, 0f), cliff);

            // Side rocks — leave center duel lane open (X≈0).
            Place(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(4f, 2.2f, 5f)),
                root.transform, "Rock_E1", new Vector3(9f, 1.0f, 4f), rock);
            Place(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(3.2f, 1.6f, 3.5f)),
                root.transform, "Rock_E2", new Vector3(11f, 0.7f, -6f), rock);
            Place(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(4.5f, 2.4f, 4f)),
                root.transform, "Rock_W1", new Vector3(-10f, 1.1f, -3f), rock);
            Place(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(3f, 1.8f, 6f)),
                root.transform, "Rock_W2", new Vector3(-8.5f, 0.8f, 7f), rock);

            // Low dunes at corners (visual wilderness, not blocking spawns).
            Place(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(7f, 1.2f, 7f)),
                root.transform, "Dune_NE", new Vector3(16f, 0.4f, 16f), dune,
                Quaternion.Euler(0f, 25f, 8f));
            Place(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(6f, 1.0f, 8f)),
                root.transform, "Dune_NW", new Vector3(-15f, 0.35f, 15f), dune,
                Quaternion.Euler(0f, -20f, -6f));
            Place(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(8f, 1.1f, 6f)),
                root.transform, "Dune_SE", new Vector3(15f, 0.35f, -15f), dune,
                Quaternion.Euler(0f, -15f, 5f));
            Place(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(7f, 1.3f, 7f)),
                root.transform, "Dune_SW", new Vector3(-16f, 0.4f, -16f), dune,
                Quaternion.Euler(0f, 30f, -7f));

            // Mid-field cover rocks (still clear of exact spawn Z=±8 at X=0).
            Place(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(2.4f, 1.4f, 2.4f)),
                root.transform, "Rock_MidE", new Vector3(5.5f, 0.6f, 0.5f), rock);
            Place(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(2.2f, 1.2f, 2.8f)),
                root.transform, "Rock_MidW", new Vector3(-6f, 0.5f, -1f), rock);

            // Soften old flat Ground so it doesn't fight the new floor.
            var ground = GameObject.Find("Ground");
            if (ground != null)
            {
                ground.SetActive(false);
                EditorUtility.SetDirty(ground);
            }

            // Warmer desert light (non-destructive if light missing).
            var light = Object.FindFirstObjectByType<Light>();
            if (light != null && light.type == LightType.Directional)
            {
                light.color = new Color(1f, 0.92f, 0.75f);
                light.intensity = 1.25f;
                EditorUtility.SetDirty(light);
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.45f, 0.32f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.78f, 0.62f, 0.40f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.012f;

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
        }

        static void Place(ProBuilderMesh pb, Transform parent, string name, Vector3 pos, Material mat,
            Quaternion? rot = null)
        {
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, true);
            pb.transform.position = pos;
            pb.transform.rotation = rot ?? Quaternion.identity;

            var renderer = pb.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = mat;

            pb.ToMesh();
            pb.Refresh();

            var col = pb.GetComponent<MeshCollider>();
            if (col == null)
                col = pb.gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = pb.GetComponent<MeshFilter>().sharedMesh;
        }

        static Material EnsureMat(string name, Color color)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                mat = new Material(shader) { name = name, color = color };
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.color = color;
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                EditorUtility.SetDirty(mat);
            }

            return mat;
        }
    }
}
#endif
