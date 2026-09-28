#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Robogee.Player;

namespace Robogee.EditorTools
{
    public static class CreateTestFieldScene
    {
        const string ScenePath = "Assets/Scenes/TestField.unity";

        [MenuItem("Robogee/Setup/Create Test Field Scene")]
        public static void Create()
        {
            Directory.CreateDirectory("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.32f, 0.28f);
            RenderSettings.fog = false;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            var groundRenderer = ground.GetComponent<Renderer>();
            groundRenderer.sharedMaterial = CreateColorMaterial("GroundMat", new Color(0.55f, 0.42f, 0.28f));

            CreateMarker("Marker_N", new Vector3(0f, 0.5f, 15f), new Color(0.8f, 0.2f, 0.2f));
            CreateMarker("Marker_E", new Vector3(15f, 0.5f, 0f), new Color(0.2f, 0.8f, 0.2f));
            CreateMarker("Marker_W", new Vector3(-15f, 0.5f, 0f), new Color(0.2f, 0.4f, 0.9f));

            var player = GameObject.CreatePrimitive(PrimitiveType.Cube);
            player.name = "Player_UnitB_Cube";
            Object.DestroyImmediate(player.GetComponent<BoxCollider>());
            player.transform.position = new Vector3(0f, 1.0f, 0f);
            var playerRenderer = player.GetComponent<Renderer>();
            playerRenderer.sharedMaterial = CreateColorMaterial("UnitBMat", new Color(0.25f, 0.45f, 0.85f));

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1f;
            controller.radius = 0.5f;
            controller.center = Vector3.zero;
            controller.skinWidth = 0.08f;

            var cameraPivot = new GameObject("CameraPivot");
            cameraPivot.transform.SetParent(player.transform, false);
            cameraPivot.transform.localPosition = new Vector3(0f, 0.25f, 0f);

            var camGo = new GameObject("PlayerCamera");
            camGo.transform.SetParent(cameraPivot.transform, false);
            camGo.transform.localPosition = Vector3.zero;
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 75f;
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();

            var mover = player.AddComponent<UnitBMotor>();
            var so = new SerializedObject(mover);
            so.FindProperty("cameraPivot").objectReferenceValue = cameraPivot.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            player.AddComponent<FuelGaugeHud>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            var scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            EditorBuildSettings.scenes = scenes;

            Debug.Log($"[Robogee] Created scene: {ScenePath}");
        }

        static void CreateMarker(string name, Vector3 position, Color color)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = name;
            marker.transform.position = position;
            marker.transform.localScale = new Vector3(1f, 1f, 1f);
            marker.GetComponent<Renderer>().sharedMaterial = CreateColorMaterial(name + "Mat", color);
        }

        static Material CreateColorMaterial(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            var mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.color = color;
            var dir = "Assets/_Project/Materials";
            Directory.CreateDirectory(dir);
            var path = $"{dir}/{name}.mat";
            AssetDatabase.CreateAsset(mat, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        // Batchmode entry: Unity.exe -batchmode -quit -projectPath . -executeMethod Robogee.EditorTools.CreateTestFieldScene.CreateFromBatch
        public static void CreateFromBatch()
        {
            Create();
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
