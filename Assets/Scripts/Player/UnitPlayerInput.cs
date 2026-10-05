using UnityEngine;
using UnityEngine.InputSystem;

namespace Robogee.Player
{
    /// <summary>
    /// Human / local pad driver via Input System. Fills <see cref="UnitInputFrame"/> only.
    /// Loads <c>Resources/UnitControls</c> when <see cref="actions"/> is unset.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class UnitPlayerInput : MonoBehaviour, IUnitInputSource
    {
        [SerializeField] InputActionAsset actions;
        [SerializeField] string actionMapName = "Gameplay";
        [Tooltip("Disable for NPC-controlled units.")]
        [SerializeField] bool enableActions = true;

        InputActionMap _map;
        InputAction _move;
        InputAction _look;
        InputAction _jump;
        InputAction _dash;
        InputAction _toggleCursor;
        InputAction _attack;
        InputAction _lockOn;
        UnitInputFrame _current;
        bool _ownsAssetInstance;

        public UnitInputFrame Current => _current;
        public bool EnableActions
        {
            get => enableActions;
            set
            {
                enableActions = value;
                if (_map == null)
                    return;
                if (enableActions)
                    _map.Enable();
                else
                    _map.Disable();
            }
        }

        void Awake()
        {
            EnsureActions();
            CacheActions();
        }

        void OnEnable()
        {
            EnsureActions();
            CacheActions();
            if (enableActions && _map != null)
                _map.Enable();
        }

        void OnDisable()
        {
            if (_map != null)
                _map.Disable();
        }

        void OnDestroy()
        {
            if (_ownsAssetInstance && actions != null)
            {
                Destroy(actions);
                actions = null;
            }
        }

        void Update()
        {
            if (_map == null || !_map.enabled)
            {
                _current = default;
                return;
            }

            Vector2 move = _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
            Vector2 look = _look != null ? _look.ReadValue<Vector2>() : Vector2.zero;

            if (move.sqrMagnitude > 1f)
                move.Normalize();

            _current = new UnitInputFrame
            {
                MoveX = move.x,
                MoveY = move.y,
                LookX = look.x,
                LookY = look.y,
                JumpPressed = _jump != null && _jump.WasPressedThisFrame(),
                JumpHeld = _jump != null && _jump.IsPressed(),
                DashHeld = _dash != null && _dash.IsPressed(),
                ToggleCursorPressed = _toggleCursor != null && _toggleCursor.WasPressedThisFrame(),
                AttackPressed = _attack != null && _attack.WasPressedThisFrame(),
                LockOnPressed = _lockOn != null && _lockOn.WasPressedThisFrame()
            };
        }

        void EnsureActions()
        {
            if (actions != null)
                return;

            var loaded = Resources.Load<InputActionAsset>("UnitControls");
            if (loaded == null)
            {
                Debug.LogError(
                    "[UnitPlayerInput] Missing Resources/UnitControls (Input Action Asset)",
                    this);
                return;
            }

            actions = Instantiate(loaded);
            _ownsAssetInstance = true;
        }

        void CacheActions()
        {
            if (actions == null)
                return;

            _map = actions.FindActionMap(actionMapName, throwIfNotFound: false);
            if (_map == null)
            {
                Debug.LogError($"[UnitPlayerInput] Action map '{actionMapName}' not found.", this);
                return;
            }

            _move = _map.FindAction("Move", throwIfNotFound: false);
            _look = _map.FindAction("Look", throwIfNotFound: false);
            _jump = _map.FindAction("Jump", throwIfNotFound: false);
            _dash = _map.FindAction("Dash", throwIfNotFound: false);
            _toggleCursor = _map.FindAction("ToggleCursor", throwIfNotFound: false);
            _attack = _map.FindAction("Attack", throwIfNotFound: false);
            _lockOn = _map.FindAction("LockOn", throwIfNotFound: false);
        }
    }
}
