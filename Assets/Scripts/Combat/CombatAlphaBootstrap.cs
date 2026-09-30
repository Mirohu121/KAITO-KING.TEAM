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

            var enemyCore = SpawnCpu(enemyUnit, ePos, enemyCoreHp);
            enemyCore.transform.LookAt(new Vector3(pPos.x, ePos.y, pPos.z));

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
            go.transform.rotation = Quaternion.LookRotation(lookForward, Vector3.up);
            go.transform.localScale = Vector3.one;
            return go;
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
