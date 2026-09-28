using System;
using UnityEngine;

namespace Robogee.Player
{
    /// <summary>
    /// Unit B baseline FPS motor (sturdy / D.Mon-inspired).
    /// Dual fuel: Jump Boost (jump + hover) / Dash Jet (horizontal jet).
    /// Duplicate this component (or prefab) and retune Inspector values for Unit A / C.
    /// See Documentation/UnitB_Motor_Spec.md
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class UnitBMotor : MonoBehaviour
    {
        [Serializable]
        public class FuelTank
        {
            [Tooltip("Maximum fuel capacity.")]
            public float max = 100f;

            [Tooltip("Current fuel (auto-clamped).")]
            public float current = 100f;

            [Tooltip("Fuel restored per second while regenerating.")]
            public float regenPerSecond = 20f;

            [Tooltip("Delay after last spend before regen starts.")]
            public float regenDelay = 0.75f;

            float _lastSpendTime = -999f;

            public float Normalized => max > 0.0001f ? Mathf.Clamp01(current / max) : 0f;
            public bool RecentlySpent => Time.time - _lastSpendTime < Mathf.Max(0.2f, regenDelay);

            public bool TrySpend(float amount)
            {
                if (amount <= 0f)
                    return true;
                if (current < amount)
                    return false;
                current -= amount;
                _lastSpendTime = Time.time;
                return true;
            }

            public void SpendUpTo(float amountPerSecond, float dt, out float spent)
            {
                spent = 0f;
                if (amountPerSecond <= 0f || dt <= 0f || current <= 0f)
                    return;

                float want = amountPerSecond * dt;
                spent = Mathf.Min(want, current);
                current -= spent;
                if (spent > 0f)
                    _lastSpendTime = Time.time;
            }

            public void TickRegen(float dt)
            {
                if (dt <= 0f || current >= max)
                    return;
                if (Time.time < _lastSpendTime + regenDelay)
                    return;
                current = Mathf.Min(max, current + regenPerSecond * dt);
            }

            public void Clamp()
            {
                max = Mathf.Max(0.01f, max);
                current = Mathf.Clamp(current, 0f, max);
            }
        }

        [Header("References")]
        [SerializeField] Transform cameraPivot;

        [Header("Look")]
        [SerializeField] float mouseSensitivity = 2f;
        [SerializeField] float minPitch = -80f;
        [SerializeField] float maxPitch = 80f;
        [SerializeField] bool lockCursorOnStart = true;

        [Header("Weight / Ground Feel (tune per unit)")]
        [Tooltip("Top walking speed.")]
        [SerializeField] float maxSpeed = 6f;
        [Tooltip("How quickly you reach maxSpeed. Lower = heavier.")]
        [SerializeField] float acceleration = 28f;
        [Tooltip("How quickly you stop. Lower = slides more.")]
        [SerializeField] float deceleration = 32f;
        [Tooltip("Yaw responsiveness. Lower = heavier turning.")]
        [SerializeField] [Range(0.05f, 1f)] float turnResponsiveness = 0.8f;

        [Header("Air Feel")]
        [Tooltip("0 = no mid-air steering, 1 = full ground control.")]
        [SerializeField] [Range(0f, 1f)] float airControl = 0.35f;
        [SerializeField] float gravity = -22f;
        [SerializeField] float groundedStickForce = -2f;
        [Tooltip("Horizontal speed multiplier while hovering.")]
        [SerializeField] float hoverMoveSpeedScale = 0.85f;

        [Header("Jump (Jump Boost fuel)")]
        [SerializeField] float jumpHeight = 1.35f;
        [SerializeField] float jumpFuelCost = 12f;
        [Tooltip("If true, jump is refused when fuel is insufficient.")]
        [SerializeField] bool requireJumpFuel = true;

        [Header("Hover / Jetpack (Jump hold in air, Jump Boost fuel)")]
        [Tooltip("After leaving ground, hold Jump to hover/ascend.")]
        [SerializeField] bool enableHover = true;
        [SerializeField] float hoverFuelPerSecond = 28f;
        [Tooltip("Target upward speed while holding Jump with fuel.")]
        [SerializeField] float hoverAscendSpeed = 4.5f;
        [Tooltip("How fast vertical velocity blends toward hoverAscendSpeed.")]
        [SerializeField] float hoverVerticalAcceleration = 18f;
        [Tooltip("If Jump held but not ascending hard, cancel some gravity (float).")]
        [SerializeField] float hoverGravityScale = 0.15f;
        [Tooltip("Small grace after jump so hold is easy to catch.")]
        [SerializeField] float hoverArmDelay = 0.08f;

        [Header("Dash / Jet Drive (Dash Jet fuel)")]
        [SerializeField] KeyCode dashKey = KeyCode.LeftShift;
        [SerializeField] float dashSpeed = 11f;
        [SerializeField] float dashFuelPerSecond = 35f;
        [SerializeField] bool allowAirDash = true;
        [SerializeField] bool boostUpWhileDash = false;
        [SerializeField] float dashUpSpeed = 2f;
        [Tooltip("If no move input, dash along look forward.")]
        [SerializeField] bool dashForwardWhenNoInput = true;

        [Header("Fuel - Jump Boost")]
        [SerializeField] FuelTank jumpBoostFuel = new FuelTank
        {
            max = 100f,
            current = 100f,
            regenPerSecond = 16f,
            regenDelay = 0.7f
        };

        [Header("Fuel - Dash Jet")]
        [SerializeField] FuelTank dashJetFuel = new FuelTank
        {
            max = 100f,
            current = 100f,
            regenPerSecond = 22f,
            regenDelay = 0.5f
        };

        [Header("Debug")]
        [Tooltip("Legacy top-left text overlay. Prefer FuelGaugeHud.")]
        [SerializeField] bool showFuelOverlay = false;

        CharacterController _controller;
        float _pitch;
        float _verticalVelocity;
        Vector3 _horizontalVelocity;
        bool _cursorLocked;
        bool _isDashing;
        bool _isHovering;
        float _airborneTime;

        public float JumpBoostFuelNormalized => jumpBoostFuel.Normalized;
        public float DashJetFuelNormalized => dashJetFuel.Normalized;
        public bool IsJumpBoostActive => _isHovering || jumpBoostFuel.RecentlySpent;
        public bool IsDashJetActive => _isDashing || dashJetFuel.RecentlySpent;
        public bool IsDashing => _isDashing;
        public bool IsHovering => _isHovering;
        public bool IsGrounded => _controller != null && _controller.isGrounded;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (cameraPivot == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null)
                    cameraPivot = cam.transform;
            }

            jumpBoostFuel.Clamp();
            dashJetFuel.Clamp();
        }

        void Start()
        {
            SetCursorLock(lockCursorOnStart);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (Input.GetKeyDown(KeyCode.Escape))
                SetCursorLock(!_cursorLocked);

            if (_cursorLocked)
                ApplyLook();

            bool grounded = _controller.isGrounded;
            if (grounded)
            {
                _airborneTime = 0f;
            }
            else
            {
                _airborneTime += dt;
            }

            Vector3 moveInput = ReadMoveInput();
            HandleJump(grounded);
            HandleHover(grounded, dt);
            HandleDash(moveInput, dt);
            IntegrateMovement(moveInput, grounded, dt);

            if (!_isHovering)
                jumpBoostFuel.TickRegen(dt);
            if (!_isDashing)
                dashJetFuel.TickRegen(dt);

            jumpBoostFuel.Clamp();
            dashJetFuel.Clamp();
        }

        void OnGUI()
        {
            if (!showFuelOverlay)
                return;

            const float w = 240f;
            GUI.Box(new Rect(12, 12, w, 96), "Unit B Fuel");
            GUI.Label(new Rect(24, 36, w - 24, 20),
                $"Jump Boost: {jumpBoostFuel.current:0}/{jumpBoostFuel.max:0}");
            GUI.Label(new Rect(24, 56, w - 24, 20),
                $"Dash Jet:   {dashJetFuel.current:0}/{dashJetFuel.max:0}");
            GUI.Label(new Rect(24, 76, w - 24, 20),
                _isHovering ? "Hover: ON" : (_isDashing ? "Dash: ON" : "Idle"));
        }

        static Vector3 ReadMoveInput()
        {
            float x = Input.GetAxisRaw("Horizontal");
            float z = Input.GetAxisRaw("Vertical");
            var input = new Vector3(x, 0f, z);
            if (input.sqrMagnitude > 1f)
                input.Normalize();
            return input;
        }

        void ApplyLook()
        {
            float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

            transform.Rotate(0f, mouseX * turnResponsiveness, 0f);

            if (cameraPivot == null)
                return;

            _pitch = Mathf.Clamp(_pitch - mouseY, minPitch, maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        void HandleJump(bool grounded)
        {
            if (!grounded)
                return;
            if (!Input.GetButtonDown("Jump"))
                return;

            if (requireJumpFuel)
            {
                if (!jumpBoostFuel.TrySpend(jumpFuelCost))
                    return;
            }
            else
            {
                jumpBoostFuel.TrySpend(jumpFuelCost);
            }

            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _airborneTime = 0f;
        }

        void HandleHover(bool grounded, float dt)
        {
            _isHovering = false;

            if (!enableHover || grounded)
                return;
            if (_airborneTime < hoverArmDelay)
                return;
            if (!Input.GetButton("Jump"))
                return;

            jumpBoostFuel.SpendUpTo(hoverFuelPerSecond, dt, out float spent);
            if (spent <= 0f)
                return;

            _isHovering = true;

            // Blend toward ascend speed (Valkyrie-like hold-to-rise).
            _verticalVelocity = Mathf.MoveTowards(
                _verticalVelocity,
                hoverAscendSpeed,
                hoverVerticalAcceleration * dt);
        }

        void HandleDash(Vector3 moveInput, float dt)
        {
            _isDashing = false;

            if (!Input.GetKey(dashKey))
                return;
            if (!allowAirDash && !_controller.isGrounded)
                return;

            dashJetFuel.SpendUpTo(dashFuelPerSecond, dt, out float spent);
            if (spent <= 0f)
                return;

            _isDashing = true;

            Vector3 localDir = moveInput;
            if (localDir.sqrMagnitude < 0.01f)
            {
                if (!dashForwardWhenNoInput)
                    return;
                localDir = Vector3.forward;
            }

            Vector3 worldDir = transform.TransformDirection(localDir);
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude > 0.0001f)
                worldDir.Normalize();

            _horizontalVelocity = worldDir * dashSpeed;

            if (boostUpWhileDash)
                _verticalVelocity = Mathf.Max(_verticalVelocity, dashUpSpeed);
        }

        void IntegrateMovement(Vector3 moveInput, bool grounded, float dt)
        {
            if (!_isDashing)
            {
                float speed = maxSpeed;
                if (_isHovering)
                    speed *= hoverMoveSpeedScale;

                Vector3 desired = transform.TransformDirection(moveInput) * speed;
                desired.y = 0f;

                float control = grounded ? 1f : airControl;
                float accel = desired.sqrMagnitude > _horizontalVelocity.sqrMagnitude
                    ? acceleration
                    : deceleration;

                _horizontalVelocity = Vector3.MoveTowards(
                    _horizontalVelocity,
                    desired,
                    accel * control * dt);
            }

            if (grounded && _verticalVelocity < 0f)
                _verticalVelocity = groundedStickForce;

            bool skipGravity = _isHovering || (_isDashing && boostUpWhileDash);
            if (!skipGravity)
            {
                _verticalVelocity += gravity * dt;
            }
            else if (_isHovering && hoverGravityScale > 0f && _verticalVelocity < hoverAscendSpeed)
            {
                // Mild residual gravity while approaching ascend speed keeps feel grounded.
                _verticalVelocity += gravity * hoverGravityScale * dt;
            }

            Vector3 motion = _horizontalVelocity;
            motion.y = _verticalVelocity;
            _controller.Move(motion * dt);
        }

        void SetCursorLock(bool locked)
        {
            _cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            jumpBoostFuel.Clamp();
            dashJetFuel.Clamp();
            maxSpeed = Mathf.Max(0f, maxSpeed);
            dashSpeed = Mathf.Max(0f, dashSpeed);
            jumpHeight = Mathf.Max(0f, jumpHeight);
            hoverFuelPerSecond = Mathf.Max(0f, hoverFuelPerSecond);
            hoverAscendSpeed = Mathf.Max(0f, hoverAscendSpeed);
            if (gravity >= 0f)
                gravity = -22f;
        }
#endif
    }
}
