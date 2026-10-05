using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using Robogee.Player;

namespace Robogee.Combat
{
    /// <summary>
    /// TestField α flow: pick Unit A/B/C → 1v1 vs NPC.
    /// Does not change RobotPrefab inspector stats.
    /// </summary>
    public class CombatAlphaBootstrap : MonoBehaviour
    {
        [SerializeField] float playerCoreHp = 100f;
        [SerializeField] float enemyCoreHp = 100f;
        [SerializeField] float arenaHalfLength = 8f;
        [SerializeField] UnitId enemyUnit = UnitId.B;

        UnitSelectBinder _select;
        bool _matchStarted;
        Camera _menuCamera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoWire()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name == null || !scene.name.Contains("TestField"))
                return;
            if (Object.FindFirstObjectByType<CombatAlphaBootstrap>() != null)
                return;

            var go = new GameObject("CombatAlphaBootstrap");
            go.AddComponent<CombatAlphaBootstrap>();
        }

        void Awake()
        {
            Hide("Marker_N");
            Hide("Marker_E");
            Hide("Marker_W");

            EnsureEventSystem();
            EnsureMenuCamera();

            FuelGaugeHud.HideAll();

            // Freeze placeholder scene player until a unit is chosen.
            foreach (var motor in FindObjectsByType<UnitBMotor>(FindObjectsSortMode.None))
            {
                if (motor != null && !motor.name.StartsWith("CPU_Enemy"))
                    motor.gameObject.SetActive(false);
            }

            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t != null && t.name.StartsWith("CPU_Enemy"))
                    Destroy(t.gameObject);
            }

            var systems = GameObject.Find("CombatSystems");
            if (systems == null)
                systems = new GameObject("CombatSystems");

            _select = systems.GetComponent<UnitSelectBinder>();
            if (_select == null)
                _select = systems.AddComponent<UnitSelectBinder>();

            _select.Show(OnUnitPicked);
            Debug.Log("[CombatAlpha] Select Unit A / B / C");
        }

        void OnUnitPicked(UnitId id)
        {
            if (_matchStarted)
                return;
            _matchStarted = true;
            StartMatch(id);
        }

        void StartMatch(UnitId playerUnit)
        {
            DestroyMenuCamera();
            FuelGaugeHud.ShowForCombat();

            // Remove any leftover scene placeholders.
            foreach (var motor in FindObjectsByType<UnitBMotor>(FindObjectsSortMode.None))
            {
                if (motor == null)
                    continue;
                if (motor.name.StartsWith("CPU_Enemy"))
                    continue;
                Destroy(motor.gameObject);
            }

            Vector3 pPos = new Vector3(0f, 1f, -arenaHalfLength);
            if (Physics.Raycast(pPos + Vector3.up * 5f, Vector3.down, out RaycastHit ground, 20f))
                pPos.y = ground.point.y + 1f;

            Vector3 ePos = new Vector3(0f, pPos.y, arenaHalfLength);

            var playerGo = SpawnUnit(playerUnit, pPos, Vector3.forward, asPlayer: true);
            if (playerGo == null)
            {
                Debug.LogError("[CombatAlpha] Failed to load unit prefab " + playerUnit);
                return;
            }

            // Fuel HUD builds on enable now that VisibleAllowed is true.
            foreach (var fuel in playerGo.GetComponentsInChildren<FuelGaugeHud>(true))
            {
                fuel.enabled = false;
                fuel.enabled = true;
            }

            var playerCore = EnsureCombatant(playerGo, CombatantCore.Team.Player, playerCoreHp);
            if (playerGo.GetComponent<UnitMeleeAttack>() == null)
                playerGo.AddComponent<UnitMeleeAttack>();

            var lockOn = playerGo.GetComponent<UnitLockOn>();
            if (lockOn == null)
                lockOn = playerGo.AddComponent<UnitLockOn>();

            var enemyCore = SpawnCpu(enemyUnit, ePos, enemyCoreHp);
            FaceYawOnly(enemyCore.transform, pPos);

            var systems = GameObject.Find("CombatSystems");
            if (systems == null)
                systems = new GameObject("CombatSystems");

            var match = systems.GetComponent<CombatMatchController>();
            if (match == null)
                match = systems.AddComponent<CombatMatchController>();
            match.Bind(playerCore, enemyCore);

            var hud = systems.GetComponent<MatchHudBinder>();
            if (hud == null)
                hud = systems.AddComponent<MatchHudBinder>();
            hud.Bind(match, playerCore, enemyCore);

            var lockHud = systems.GetComponent<LockOnHudBinder>();
            if (lockHud == null)
                lockHud = systems.AddComponent<LockOnHudBinder>();
            lockHud.Bind(lockOn);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Debug.Log($"[CombatAlpha] Fight start — player={UnitPrefabCatalog.DisplayName(playerUnit)} vs NPC {enemyUnit}");
        }

        static GameObject SpawnUnit(UnitId id, Vector3 position, Vector3 lookForward, bool asPlayer)
        {
            var prefab = UnitPrefabCatalog.Load(id);
            if (prefab == null)
                return null;

            var go = Instantiate(prefab);
            go.name = asPlayer ? $"Player_Unit{id}" : $"CPU_Enemy_Unit{id}";
            go.SetActive(true);
            go.transform.position = position;
            FaceYawOnly(go.transform, go.transform.position + lookForward);
            go.transform.localScale = Vector3.one;
            EnsurePlayableComponents(go, asPlayer);
            return go;
        }

        static void FaceYawOnly(Transform t, Vector3 worldTarget)
        {
            if (t == null)
                return;
            Vector3 flat = worldTarget - t.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f)
                return;
            t.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            // Kill any import / LookAt pitch-roll so skeletons stand upright.
            Vector3 e = t.eulerAngles;
            t.rotation = Quaternion.Euler(0f, e.y, 0f);
        }

        /// <summary>
        /// Makes sure gaikotu (or any visual prefab) has motor / input / move-anim wiring.
        /// Does not alter RobotPrefab cube stats assets.
        /// </summary>
        static void EnsurePlayableComponents(GameObject go, bool asPlayer)
        {
            if (go.GetComponent<CharacterController>() == null)
            {
                var cc = go.AddComponent<CharacterController>();
                cc.height = 1.8f;
                cc.radius = 0.35f;
                cc.center = new Vector3(0f, 0.9f, 0f);
                cc.skinWidth = 0.08f;
            }

            if (go.GetComponent<UnitPlayerInput>() == null)
                go.AddComponent<UnitPlayerInput>();
            if (go.GetComponent<UnitBMotor>() == null)
                go.AddComponent<UnitBMotor>();
            if (go.GetComponent<UnitMoveAnimator>() == null)
                go.AddComponent<UnitMoveAnimator>();

            if (asPlayer)
            {
                if (go.GetComponent<FuelGaugeHud>() == null)
                    go.AddComponent<FuelGaugeHud>();
                EnsurePlayerCamera(go);
            }
        }

        static void EnsurePlayerCamera(GameObject go)
        {
            var motor = go.GetComponent<UnitBMotor>();

            Transform pivot = null;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "CameraPivot")
                    continue;
                pivot = t;
                break;
            }

            if (pivot == null)
                pivot = new GameObject("CameraPivot").transform;

            pivot.SetParent(go.transform, false);
            float bodyH = MeasureLocalHeight(go);
            // Camera was inside torso looking at sky — raise to crown and push forward.
            float eyeY = Mathf.Clamp(bodyH * 0.95f, 1.55f, 2.35f);
            pivot.localPosition = new Vector3(0f, eyeY, 0.55f);
            pivot.localRotation = Quaternion.identity;
            pivot.localScale = Vector3.one;

            Transform camTf = null;
            for (int i = 0; i < pivot.childCount; i++)
            {
                if (pivot.GetChild(i).name == "PlayerCamera")
                {
                    camTf = pivot.GetChild(i);
                    break;
                }
            }

            Camera cam = camTf != null ? camTf.GetComponent<Camera>() : null;
            if (cam == null)
            {
                var camGo = camTf != null ? camTf.gameObject : new GameObject("PlayerCamera");
                if (camTf == null)
                    camGo.transform.SetParent(pivot, false);
                cam = camGo.GetComponent<Camera>() ?? camGo.AddComponent<Camera>();
                if (camGo.GetComponent<AudioListener>() == null)
                    camGo.AddComponent<AudioListener>();
                camTf = camGo.transform;
            }

            camTf.localPosition = Vector3.zero;
            camTf.localRotation = Quaternion.identity;
            camTf.localScale = Vector3.one;
            cam.enabled = true;
            cam.gameObject.SetActive(true);
            cam.tag = "MainCamera";
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            cam.fieldOfView = 75f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.targetDisplay = 0;

            // Hide own mesh from FPS camera.
            int hideLayer = ResolvePlayerHideLayer();
            SetLayerRecursively(go.transform, hideLayer);
            SetLayerRecursively(pivot, 0);
            cam.cullingMask = ~(1 << hideLayer);

            foreach (var other in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (other == null || other == cam)
                    continue;
                if (other.transform.IsChildOf(go.transform))
                    continue;
                other.enabled = false;
                if (other.CompareTag("MainCamera"))
                    other.tag = "Untagged";
            }

            var cc = go.GetComponent<CharacterController>();
            if (cc != null)
            {
                float h = Mathf.Clamp(bodyH, 1.4f, 3.0f);
                cc.height = h;
                cc.radius = Mathf.Clamp(h * 0.15f, 0.25f, 0.45f);
                cc.center = new Vector3(0f, h * 0.5f, 0f);
            }

            if (motor != null)
            {
                motor.RebindCameraPivot();
                motor.ResetLook();
            }

            var follow = go.GetComponent<FpsHeadCameraFollow>();
            if (follow == null)
                follow = go.AddComponent<FpsHeadCameraFollow>();
            follow.Bind(pivot, null);

            // Slightly tighter FOV so distant foes don't look oversized from eye height.
            cam.fieldOfView = 70f;
        }

        static int ResolvePlayerHideLayer()
        {
            int layer = LayerMask.NameToLayer("Player");
            return layer >= 0 ? layer : 31;
        }

        static void SetLayerRecursively(Transform root, int layer)
        {
            if (root == null)
                return;
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i), layer);
        }

        static float MeasureLocalHeight(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0)
                return 1.8f;

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
            {
                if (rends[i] != null)
                    b.Encapsulate(rends[i].bounds);
            }

            float h = b.max.y - go.transform.position.y;
            return h > 0.2f ? h : 1.8f;
        }

        static CombatantCore SpawnCpu(UnitId id, Vector3 position, float hp)
        {
            GameObject cpu = SpawnUnit(id, position, Vector3.back, asPlayer: false);
            if (cpu == null)
            {
                cpu = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                cpu.name = "CPU_Enemy_Capsule";
                Destroy(cpu.GetComponent<CapsuleCollider>());
                var cc = cpu.AddComponent<CharacterController>();
                cc.height = 2f;
                cc.radius = 0.5f;
                cc.center = Vector3.zero;
                cpu.AddComponent<UnitBMotor>();
                cpu.transform.position = position;
            }

            foreach (var cam in cpu.GetComponentsInChildren<Camera>())
                Destroy(cam.gameObject);
            foreach (var listener in cpu.GetComponentsInChildren<AudioListener>())
                Destroy(listener);
            foreach (var fuelHud in cpu.GetComponentsInChildren<FuelGaugeHud>())
                Destroy(fuelHud);

            var rend = cpu.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Universal Render Pipeline/Lit")
                             ?? Shader.Find("Standard");
                var mat = new Material(shader);
                var color = new Color(1f, 0.35f, 0.15f, 1f);
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color"))
                    mat.color = color;
                rend.sharedMaterial = mat;
            }

            var core = EnsureCombatant(cpu, CombatantCore.Team.Enemy, hp);
            if (cpu.GetComponent<UnitMeleeAttack>() == null)
                cpu.AddComponent<UnitMeleeAttack>();
            if (cpu.GetComponent<NpcChaseBrain>() == null)
                cpu.AddComponent<NpcChaseBrain>();

            return core;
        }

        static CombatantCore EnsureCombatant(GameObject go, CombatantCore.Team team, float hp)
        {
            var core = go.GetComponent<CombatantCore>();
            if (core == null)
                core = go.AddComponent<CombatantCore>();
            core.Configure(team, hp);
            core.EnsureHurtbox();
            return core;
        }

        void EnsureMenuCamera()
        {
            if (_menuCamera != null)
                return;

            // Avoid "No cameras rendering" during unit select (placeholder player is disabled).
            var go = new GameObject("MenuCamera");
            _menuCamera = go.AddComponent<Camera>();
            _menuCamera.tag = "MainCamera";
            _menuCamera.clearFlags = CameraClearFlags.SolidColor;
            _menuCamera.backgroundColor = new Color(0.12f, 0.14f, 0.18f, 1f);
            _menuCamera.transform.position = new Vector3(0f, 8f, -14f);
            _menuCamera.transform.rotation = Quaternion.Euler(28f, 0f, 0f);
            _menuCamera.fieldOfView = 55f;
            go.AddComponent<AudioListener>();
        }

        void DestroyMenuCamera()
        {
            if (_menuCamera == null)
                return;
            Destroy(_menuCamera.gameObject);
            _menuCamera = null;
        }

        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
                return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        static void Hide(string name)
        {
            var go = GameObject.Find(name);
            if (go != null)
                go.SetActive(false);
        }
    }
}
