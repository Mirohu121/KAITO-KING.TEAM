#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Robogee.Player;

namespace Robogee.EditorTools
{
    /// <summary>
    /// Temporary: build a playable gaikotu prefab (replaces cube A/B/C at runtime)
    /// and wire Animator params for WASD (Speed / MoveX / MoveY / IsMoving).
    /// </summary>
    [InitializeOnLoad]
    public static class GaikotuPlayableEnsure
    {
        const string FbxPath = "Assets/gaikotu_rig.fbx";
        const string SourcePrefabPath = "Assets/gaikotu_rig.prefab";
        const string ControllerPath = "Assets/gaikotu.controller";
        const string PlayablePath = "Assets/Resources/Gaikotu/gaikotu_playable.prefab";
        const string PrefKey = "Robogee.GaikotuPlayable.v10";
        const string ControllerBuildKey = "Robogee.GaikotuWasdController.v3";

        static GaikotuPlayableEnsure()
        {
            EditorApplication.delayCall += Ensure;
        }

        /// <summary>Called from existing RobogeeProjectSetup button — no new menu.</summary>
        public static void RebuildNow()
        {
            EditorPrefs.DeleteKey(PrefKey);
            EditorPrefs.DeleteKey(ControllerBuildKey);
            Ensure();
            Debug.Log("[Gaikotu] Rebuilt WASD animator + playable prefab.");
        }

        static void Ensure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (!File.Exists(SourcePrefabPath) || !File.Exists(ControllerPath))
                return;

            EnsureDir("Assets/Resources");
            EnsureDir("Assets/Resources/Gaikotu");

            EnsureAnimatorController();

            if (EditorPrefs.GetBool(PrefKey, false) &&
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayablePath) != null)
                return;

            BuildPlayablePrefab();
            EditorPrefs.SetBool(PrefKey, true);
        }

        static void EnsureAnimatorController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                return;

            // Rebuild when ControllerBuildKey bumps (WASD mapping).
            if (EditorPrefs.GetBool(ControllerBuildKey, false) && controller.layers.Length > 0)
            {
                var existing = controller.layers[0].stateMachine;
                if (existing != null && existing.states.Any(s => s.state != null && s.state.name == "Locomotion")
                    && existing.states.Length == 1)
                    return;
            }

            EnsureParam(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureParam(controller, "MoveX", AnimatorControllerParameterType.Float);
            EnsureParam(controller, "MoveY", AnimatorControllerParameterType.Float);
            EnsureParam(controller, "IsMoving", AnimatorControllerParameterType.Bool);

            if (controller.layers.Length == 0)
                controller.AddLayer("Base Layer");

            var clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
                .OfType<AnimationClip>()
                .Where(c => c != null && !c.name.StartsWith("__preview"))
                .ToList();

            AnimationClip FindClip(params string[] keys)
            {
                foreach (var key in keys)
                {
                    var hit = clips.FirstOrDefault(c =>
                        c.name.EndsWith(key, System.StringComparison.OrdinalIgnoreCase)
                        || c.name.IndexOf("|" + key, System.StringComparison.OrdinalIgnoreCase) >= 0
                        || c.name.Equals(key, System.StringComparison.OrdinalIgnoreCase));
                    if (hit != null)
                        return hit;
                }

                return null;
            }

            var clipBase = FindClip("Base", "B_Base", "Idle");
            var clipW = FindClip("B_W", "W");
            var clipA = FindClip("B_A", "A");
            var clipS = FindClip("B_S", "S");
            var clipD = FindClip("B_D", "D");

            EnableLoop(clipBase);
            EnableLoop(clipW);
            EnableLoop(clipA);
            EnableLoop(clipS);
            EnableLoop(clipD);

            var sm = controller.layers[0].stateMachine;
            foreach (var child in sm.states.ToArray())
                sm.RemoveState(child.state);

            var loco = sm.AddState("Locomotion");
            sm.defaultState = loco;

            // 2D directional blend: MoveX = A/D, MoveY = S/W (Input System Move).
            var tree = new BlendTree
            {
                name = "WASD_Blend",
                blendType = BlendTreeType.SimpleDirectional2D,
                blendParameter = "MoveX",
                blendParameterY = "MoveY",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(tree, controller);

            if (clipBase != null)
                tree.AddChild(clipBase, new Vector2(0f, 0f));
            if (clipW != null)
                tree.AddChild(clipW, new Vector2(0f, 1f));
            if (clipS != null)
                tree.AddChild(clipS, new Vector2(0f, -1f));
            if (clipA != null)
                tree.AddChild(clipA, new Vector2(-1f, 0f));
            if (clipD != null)
                tree.AddChild(clipD, new Vector2(1f, 0f));

            loco.motion = tree;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            EditorPrefs.SetBool(ControllerBuildKey, true);

            Debug.Log(
                "[Gaikotu] WASD clips → " +
                $"Base={(clipBase != null ? clipBase.name : "missing")}, " +
                $"W={(clipW != null ? clipW.name : "missing")}, " +
                $"A={(clipA != null ? clipA.name : "missing")}, " +
                $"S={(clipS != null ? clipS.name : "missing")}, " +
                $"D={(clipD != null ? clipD.name : "missing")}");
        }

        static void EnsureParam(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name == name)
                    return;
            }

            controller.AddParameter(name, type);
        }

        static void EnableLoop(AnimationClip clip)
        {
            if (clip == null)
                return;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (settings.loopTime)
                return;
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }

        static void BuildPlayablePrefab()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
            if (source == null)
                return;

            var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            root.name = "gaikotu_playable";
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;

            // Drop mesh/physics leftovers that conflict with CharacterController.
            foreach (var col in root.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(col);

            var cc = root.GetComponent<CharacterController>();
            if (cc == null)
                cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.skinWidth = 0.08f;

            if (root.GetComponent<UnitPlayerInput>() == null)
                root.AddComponent<UnitPlayerInput>();
            var motor = root.GetComponent<UnitBMotor>();
            if (motor == null)
                motor = root.AddComponent<UnitBMotor>();
            if (root.GetComponent<UnitMoveAnimator>() == null)
                root.AddComponent<UnitMoveAnimator>();
            if (root.GetComponent<FuelGaugeHud>() == null)
                root.AddComponent<FuelGaugeHud>();
            if (root.GetComponent<FpsHeadCameraFollow>() == null)
                root.AddComponent<FpsHeadCameraFollow>();

            var animator = root.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
            }

        // FPS on unit root (not head bone — avoids sky-look from bone rest pose).
        Transform pivot = root.transform.Find("CameraPivot");
        if (pivot == null)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "CameraPivot")
                {
                    pivot = t;
                    break;
                }
            }
        }

        if (pivot == null)
        {
            var pivotGo = new GameObject("CameraPivot");
            pivot = pivotGo.transform;
        }

        pivot.SetParent(root.transform, false);
        pivot.localPosition = new Vector3(0f, 1.75f, 0.55f);
        pivot.localRotation = Quaternion.identity;
        pivot.localScale = Vector3.one;

        Transform camTf = pivot.Find("PlayerCamera");
        if (camTf == null)
        {
            var camGo = new GameObject("PlayerCamera");
            camGo.transform.SetParent(pivot, false);
            camTf = camGo.transform;
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 75f;
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();
        }

        camTf.localPosition = Vector3.zero;
        camTf.localRotation = Quaternion.identity;
        camTf.localScale = Vector3.one;

            var so = new SerializedObject(motor);
            var camProp = so.FindProperty("cameraPivot");
            if (camProp != null)
            {
                camProp.objectReferenceValue = pivot;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, PlayablePath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void EnsureDir(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureDir(parent);
            AssetDatabase.CreateFolder(parent ?? "Assets", name);
        }
    }
}
#endif
