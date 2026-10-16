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
        // Bump when the layout changes so scenes saved with an older arena rebuild on open.
        const string LayoutMarker = "Layout_v2_City";

        [InitializeOnLoadMethod]
        static void AutoEnsure()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.delayCall += TryBuildIfNeeded;
        }

        /// <summary>Called from existing RobogeeProjectSetup button — no new menu.</summary>
        public static void RebuildNow()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError("[DesertArena] Missing " + ScenePath);
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            BuildIntoOpenScene();
            EditorPrefs.SetBool(PrefKey, true);
            Debug.Log("[DesertArena] Rebuilt into TestField.");
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

            var root = GameObject.Find(RootName);
            if (root != null && root.transform.Find(LayoutMarker) != null)
            {
                EditorPrefs.SetBool(PrefKey, true);
                return;
            }

            BuildIntoOpenScene();
            EditorPrefs.SetBool(PrefKey, true);
        }

        static void BuildIntoOpenScene()
        {
            Directory.CreateDirectory(MatDir);

            var sand = EnsureMat("DesertSandMat", new Color(0.76f, 0.58f, 0.32f));
            var rock = EnsureMat("DesertRockMat", new Color(0.42f, 0.32f, 0.24f));
            var cliff = EnsureMat("DesertCliffMat", new Color(0.55f, 0.40f, 0.28f));
            var dune = EnsureMat("DesertDuneMat", new Color(0.82f, 0.66f, 0.40f));
            var concrete = EnsureMat("BuildingConcreteMat", new Color(0.66f, 0.63f, 0.58f));
            var concreteDark = EnsureMat("BuildingDarkMat", new Color(0.46f, 0.45f, 0.43f));
            var roof = EnsureMat("BuildingRoofMat", new Color(0.32f, 0.31f, 0.30f));

            var existing = GameObject.Find(RootName);
            if (existing != null)
                Object.DestroyImmediate(existing);

            var root = new GameObject(RootName);
            new GameObject(LayoutMarker).transform.SetParent(root.transform, false);

            // Playable sand floor — covers spawn lane Z=±8 with margin.
            Place(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(48f, 0.4f, 48f)),
                root.transform, "SandFloor", new Vector3(0f, -0.2f, 0f), sand);

            // Outer cliffs — taller than a full boost jump (~7m) so nobody hops out.
            Place(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(52f, 14f, 2.5f)),
                root.transform, "Cliff_N", new Vector3(0f, 6.8f, 24f), cliff);
            Place(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(52f, 14f, 2.5f)),
                root.transform, "Cliff_S", new Vector3(0f, 6.8f, -24f), cliff);
            Place(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(2.5f, 14f, 48f)),
                root.transform, "Cliff_E", new Vector3(24f, 6.8f, 0f), cliff);
            Place(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(2.5f, 14f, 48f)),
                root.transform, "Cliff_W", new Vector3(-24f, 6.8f, 0f), cliff);

            // Ruined city blocks. Units are 1.8m = an 18m MS, so 12m here reads as a 120m tower.
            // Point-symmetric around the origin so both spawns (0, ±8) get the same cover;
            // the straight lane between spawns stays open.
            var city = new GameObject("City").transform;
            city.SetParent(root.transform, false);

            BuildingPair(city, "Tower_A", 10f, 10f, new Vector3(5f, 12f, 5f), concreteDark, roof, tower: true);
            BuildingPair(city, "Tower_B", -11f, 8f, new Vector3(4f, 10f, 6f), concrete, roof, tower: true);
            BuildingPair(city, "Mid_Center", 7f, 1f, new Vector3(4f, 6f, 6f), concrete, roof, tower: false);
            BuildingPair(city, "Mid_Back", -3.5f, 15f, new Vector3(6f, 5f, 4f), concreteDark, roof, tower: false);
            BuildingPair(city, "Mid_Flank", 19f, 2f, new Vector3(4f, 7f, 7f), concrete, roof, tower: false);
            BuildingPair(city, "Low_Flank", 13.5f, -1f, new Vector3(3f, 4.5f, 4f), concreteDark, roof, tower: false);
            BuildingPair(city, "Block_Lane", 3.5f, -3.5f, new Vector3(2.5f, 2.5f, 3f), rock, null, tower: false);
            BuildingPair(city, "Block_Spawn", 5f, 9f, new Vector3(3f, 2.5f, 2.5f), rock, null, tower: false);

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

        static void BuildingPair(Transform parent, string name, float x, float z, Vector3 size,
            Material body, Material roof, bool tower)
        {
            Building(parent, name + "_1", new Vector3(x, 0f, z), size, body, roof, tower);
            Building(parent, name + "_2", new Vector3(-x, 0f, -z), size, body, roof, tower);
        }

        static void Building(Transform parent, string name, Vector3 groundPos, Vector3 size,
            Material body, Material roof, bool tower)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.position = groundPos;

            Place(ShapeGenerator.GenerateCube(PivotLocation.Center, size),
                holder, "Body", groundPos + Vector3.up * (size.y * 0.5f), body);

            if (roof == null)
                return;

            // Cornice slab so the silhouette reads as a building, not a crate.
            var cornice = new Vector3(size.x + 0.3f, 0.25f, size.z + 0.3f);
            Place(ShapeGenerator.GenerateCube(PivotLocation.Center, cornice),
                holder, "Roof", groundPos + Vector3.up * (size.y + 0.125f), roof);

            if (!tower)
                return;

            // Floor bands every ~3m and a rooftop penthouse.
            for (float y = 3f; y < size.y - 1f; y += 3f)
            {
                var band = new Vector3(size.x + 0.08f, 0.35f, size.z + 0.08f);
                Place(ShapeGenerator.GenerateCube(PivotLocation.Center, band),
                    holder, "Band", groundPos + Vector3.up * y, roof);
            }

            var penthouse = new Vector3(size.x * 0.55f, size.y * 0.12f, size.z * 0.55f);
            Place(ShapeGenerator.GenerateCube(PivotLocation.Center, penthouse),
                holder, "Penthouse", groundPos + Vector3.up * (size.y + 0.25f + penthouse.y * 0.5f), body);
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
