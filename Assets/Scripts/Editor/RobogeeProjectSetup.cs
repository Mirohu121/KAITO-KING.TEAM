#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Robogee.Player;

namespace Robogee.EditorTools
{
    /// <summary>
    /// URP pipeline + TestField scene with UnitBMotor.
    /// Menu: Robogee/Setup/Apply URP And Rebuild Test Field
    /// Batch: -executeMethod Robogee.EditorTools.RobogeeProjectSetup.ApplyFromBatch
    /// </summary>
    public static class RobogeeProjectSetup
    {
        const string ScenePath = "Assets/Scenes/TestField.unity";
        const string UrpFolder = "Assets/Settings";
        const string PipelineAssetPath = UrpFolder + "/URP_Pipeline.asset";
        const string RendererAssetPath = UrpFolder + "/URP_Renderer.asset";
        const string MaterialsFolder = "Assets/_Project/Materials";

        [MenuItem("Robogee/Setup/Apply URP And Rebuild Test Field")]
        public static void ApplyFromMenu() => Apply();

        public static void ApplyFromBatch() => Apply();

        static void Apply()
        {
            CreateUrpAssets();
            AssignPipeline();
            RebuildTestFieldScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Robogee] URP + TestField + UnitBMotor setup complete.");
        }

        static void CreateUrpAssets()
        {
            Directory.CreateDirectory(UrpFolder);

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererAssetPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererAssetPath);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelineAssetPath);
            }

            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(pipeline);
        }

        static void AssignPipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (pipeline == null)
            {
                Debug.LogError("[Robogee] Missing URP pipeline asset.");
                return;
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        static Material CreateLitMaterial(string name, Color color)
        {
            Directory.CreateDirectory(MaterialsFolder);
            string path = $"{MaterialsFolder}/{name}.mat";

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            mat.name = name;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void RebuildTestFieldScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.32f, 0.28f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            ground.GetComponent<Renderer>().sharedMaterial =
                CreateLitMaterial("GroundMat", new Color(0.55f, 0.42f, 0.28f));

            CreateMarker("Marker_N", new Vector3(0f, 0.5f, 15f), new Color(0.8f, 0.2f, 0.2f));
            CreateMarker("Marker_E", new Vector3(15f, 0.5f, 0f), new Color(0.2f, 0.8f, 0.2f));
            CreateMarker("Marker_W", new Vector3(-15f, 0.5f, 0f), new Color(0.2f, 0.4f, 0.9f));

            var player = GameObject.CreatePrimitive(PrimitiveType.Cube);
            player.name = "Player_UnitB_Cube";
            Object.DestroyImmediate(player.GetComponent<BoxCollider>());
            player.transform.position = new Vector3(0f, 1.0f, 0f);
            player.GetComponent<Renderer>().sharedMaterial =
                CreateLitMaterial("UnitBMat", new Color(0.25f, 0.45f, 0.85f));

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
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 75f;
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();

            var motor = player.AddComponent<UnitBMotor>();
            var so = new SerializedObject(motor);
            so.FindProperty("cameraPivot").objectReferenceValue = cameraPivot.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            player.AddComponent<FuelGaugeHud>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
        }

        static void CreateMarker(string name, Vector3 position, Color color)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = name;
            marker.transform.position = position;
            marker.GetComponent<Renderer>().sharedMaterial = CreateLitMaterial(name + "Mat", color);
        }
    }
}
#endif
