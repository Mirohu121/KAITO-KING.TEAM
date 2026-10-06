using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Robogee.Player
{
    /// <summary>
    /// Grounded mech motor: weighty walk + slide boost (not free hover-jet).
    /// Input via <see cref="IUnitInputSource"/> only (Input System / AI).
    /// Tune Inspector for Unit A / B / C — same component, different numbers.
    /// See Documentation/UnitB_Motor_Spec.md
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(UnitPlayerInput))]
    public class UnitBMotor : MonoBehaviour
    {
        [Serializable]
        public class FuelTank
        {
            public float max = 100f;
            public float current = 100f;
            public float regenPerSecond = 20f;
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

            /// <summary>Push regen start further out without changing Inspector delay.</summary>
            public void PunishRegen(float extraSeconds)
            {
                if (extraSeconds <= 0f)
                    return;
                _lastSpendTime = Mathf.Max(_lastSpendTime, Time.time) + extraSeconds;
            }
        }

        [Header("References")]
        [SerializeField] Transform cameraPivot;
        [SerializeField] MonoBehaviour inputSourceBehaviour;

        [Header("Look")]
        [FormerlySerializedAs("mouseSensitivity")]
        [SerializeField] float lookSensitivity = 2f;
        [SerializeField] float minPitch = -80f;
        [SerializeField] float maxPitch = 80f;
        [SerializeField] bool lockCursorOnStart = true;
        [SerializeField] float lockOnTurnSpeed = 14f;

        [Header("Weight / Ground Walk")]
        [FormerlySerializedAs("maxSpeed")]
        [SerializeField] float maxWalkSpeed = 5.2f;
        [FormerlySerializedAs("acceleration")]
        [Tooltip("How hard you push toward walk speed. Lower = heavier.")]
        [SerializeField] float walkAcceleration = 14f;
        [FormerlySerializedAs("deceleration")]
        [Tooltip("Friction when no move input (coast). Lower = slides longer.")]
        [SerializeField] float coastFriction = 6f;
        [Tooltip("Extra stop force when pushing opposite to velocity (brake).")]
        [SerializeField] float brakeFriction = 18f;
        [SerializeField] [Range(0.05f, 1f)] float turnResponsiveness = 0.55f;

        [Header("Air")]
        [SerializeField] [Range(0f, 1f)] float airControl = 0.2f;
        [SerializeField] float gravity = -26f;
        [SerializeField] float groundedStickForce = -2f;

        [Header("Jump (limited)")]
        [SerializeField] float jumpHeight = 0.95f;
        [SerializeField] float jumpFuelCost = 28f;
        [SerializeField] bool requireJumpFuel = true;
        [Tooltip("α grounded combat: keep hover off.")]
        [SerializeField] bool enableHover = false;
        [SerializeField] float hoverFuelPerSecond = 40f;
        [SerializeField] float hoverAscendSpeed = 2.2f;
        [SerializeField] float hoverVerticalAcceleration = 10f;
        [SerializeField] float hoverGravityScale = 0.35f;
        [SerializeField] float hoverArmDelay = 0.1f;

        [Header("Slide Boost (Dash Jet)")]
        [FormerlySerializedAs("dashSpeed")]
        [Tooltip("Top speed while slide-boosting.")]
        [SerializeField] float slideMaxSpeed = 12f;
        [Tooltip("How hard boost pushes (not an instant set).")]
        [SerializeField] float slideAcceleration = 38f;
        [Tooltip("Ground friction during slide (low = ice/skid).")]
        [SerializeField] float slideFriction = 1.2f;
        [FormerlySerializedAs("dashFuelPerSecond")]
        [SerializeField] float slideFuelPerSecond = 42f;
        [Tooltip("Minimum fuel to ignite a slide.")]
        [SerializeField] float slideIgniteCost = 8f;
        [FormerlySerializedAs("allowAirDash")]
        [SerializeField] bool allowAirSlide = false;
        [Tooltip("How much stick can steer while sliding (0 = rail).")]
        [SerializeField] [Range(0f, 1f)] float slideSteer = 0.35f;
        [SerializeField] bool slideForwardWhenNoInput = true;
        [Tooltip("After fuel empty, extra wait before jet regen.")]
        [SerializeField] float slideEmptyExtraDelay = 0.6f;

        [Header("Fuel - Jump Boost")]
        [SerializeField] FuelTank jumpBoostFuel = new FuelTank
        {
            max = 100f,
            current = 100f,
            regenPerSecond = 10f,
            regenDelay = 1.1f
        };

        [Header("Fuel - Dash Jet")]
        [SerializeField] FuelTank dashJetFuel = new FuelTank
        {
            max = 100f,
            current = 100f,
            regenPerSecond = 12f,
            regenDelay = 0.9f
        };

        [Header("Debug")]
        [SerializeField] bool showFuelOverlay = false;

        CharacterController _controller;
        IUnitInputSource _input;
        IAimLockProvider _aimLock;
        float _pitch;
        float _verticalVelocity;
        Vector3 _horizontalVelocity;
        bool _cursorLocked;
        bool _isSliding;
        bool _isHovering;
        float _airborneTime;
        bool _slideWasActive;

        // Legacy names kept for HUD / callers.
        public float JumpBoostFuelNormalized => jumpBoostFuel.Normalized;
        public float DashJetFuelNormalized => dashJetFuel.Normalized;
        public bool IsJumpBoostActive => _isHovering || jumpBoostFuel.RecentlySpent;
        public bool IsDashJetActive => _isSliding || dashJetFuel.RecentlySpent;
        public bool IsDashing => _isSliding;
        public bool IsHovering => _isHovering;
        public bool IsGrounded => _controller != null && _controller.isGrounded;
        public Vector3 PlanarVelocity => new Vector3(_horizontalVelocity.x, 0f, _horizontalVelocity.z);
        public float PlanarSpeed => PlanarVelocity.magnitude;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (cameraPivot == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null)
                    cameraPivot = cam.transform;
            }

            ResolveInputSource();
            ResolveAimLock();
            jumpBoostFuel.Clamp();
            dashJetFuel.Clamp();
        }

        void ResolveAimLock()
        {
            _aimLock = null;
            var behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IAimLockProvider provider)
                {
                    _aimLock = provider;
                    return;
                }
            }
        }

        void Start()
        {
            ResolveAimLock();
            SetCursorLock(lockCursorOnStart);
        }

        void ResolveInputSource()
        {
            if (inputSourceBehaviour is IUnitInputSource typed)
            {
                _input = typed;
                return;
            }

            var human = GetComponent<UnitPlayerInput>();
            if (human == null)
                human = gameObject.AddComponent<UnitPlayerInput>();
            _input = human;
            inputSourceBehaviour = human;
        }

        public void SetInputSource(IUnitInputSource source)
        {
            _input = source;
            inputSourceBehaviour = source as MonoBehaviour;
        }

        public void RebindCameraPivot()
        {
            var found = transform.Find("CameraPivot");
            if (found != null)
            {
                cameraPivot = found;
                return;
            }

            var cam = GetComponentInChildren<Camera>();
            if (cam != null)
                cameraPivot = cam.transform.parent != null ? cam.transform.parent : cam.transform;
        }

        public void ResetLook()
        {
            _pitch = 0f;
            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.identity;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            UnitInputFrame frame = _input != null ? _input.Current : default;

            if (frame.ToggleCursorPressed)
                SetCursorLock(!_cursorLocked);

            if (_cursorLocked)
                ApplyLook(frame);

            bool grounded = _controller.isGrounded;
            if (grounded)
                _airborneTime = 0f;
            else
                _airborneTime += dt;

            Vector3 moveInput = frame.MovePlanar;
            if (moveInput.sqrMagnitude > 1f)
                moveInput.Normalize();

            HandleJump(grounded, frame);
            HandleHover(grounded, dt, frame);
            HandleSlideBoost(moveInput, grounded, dt, frame);
            IntegrateVelocity(moveInput, grounded, dt);

            if (!_isHovering)
                jumpBoostFuel.TickRegen(dt);
            if (!_isSliding)
                dashJetFuel.TickRegen(dt);

            jumpBoostFuel.Clamp();
            dashJetFuel.Clamp();
        }

        void OnGUI()
        {
            if (!showFuelOverlay)
                return;

            const float w = 260f;
            GUI.Box(new Rect(12, 12, w, 96), "Mech Fuel");
            GUI.Label(new Rect(24, 36, w - 24, 20),
                $"Jump: {jumpBoostFuel.current:0}/{jumpBoostFuel.max:0}");
            GUI.Label(new Rect(24, 56, w - 24, 20),
                $"Jet:  {dashJetFuel.current:0}/{dashJetFuel.max:0}");
            GUI.Label(new Rect(24, 76, w - 24, 20),
                _isSliding ? "SLIDE" : (_isHovering ? "HOVER" : "WALK"));
        }

        void ApplyLook(UnitInputFrame frame)
        {
            if (_aimLock == null)
                ResolveAimLock();

            if (_aimLock != null && _aimLock.TryGetLockedAimPoint(out Vector3 aimPoint))
            {
                ApplyLockLook(aimPoint);
                return;
            }

            float lookX = frame.LookX * lookSensitivity;
            float lookY = frame.LookY * lookSensitivity;

            // Heavier yaw while sliding.
            float yawScale = _isSliding ? turnResponsiveness * 0.65f : turnResponsiveness;
            transform.Rotate(0f, lookX * yawScale, 0f);

            if (cameraPivot == null)
                return;

            _pitch = Mathf.Clamp(_pitch - lookY, minPitch, maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        void ApplyLockLook(Vector3 aimPoint)
        {
            Vector3 eye = cameraPivot != null
                ? cameraPivot.position
                : transform.position + Vector3.up * 0.25f;
            Vector3 to = aimPoint - eye;
            if (to.sqrMagnitude < 0.0001f)
                return;

            float dt = Time.deltaTime;
            float t = 1f - Mathf.Exp(-lockOnTurnSpeed * dt);

            float targetYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            float yaw = Mathf.LerpAngle(transform.eulerAngles.y, targetYaw, t);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            if (cameraPivot == null)
                return;

            Vector3 local = Quaternion.Inverse(transform.rotation) * to;
            float targetPitch = -Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg;
            targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
            _pitch = Mathf.Lerp(_pitch, targetPitch, t);
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        void HandleJump(bool grounded, UnitInputFrame frame)
        {
            if (!grounded || !frame.JumpPressed)
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

        void HandleHover(bool grounded, float dt, UnitInputFrame frame)
        {
            _isHovering = false;
            if (!enableHover || grounded)
                return;
            if (_airborneTime < hoverArmDelay || !frame.JumpHeld)
                return;

            jumpBoostFuel.SpendUpTo(hoverFuelPerSecond, dt, out float spent);
            if (spent <= 0f)
                return;

            _isHovering = true;
            _verticalVelocity = Mathf.MoveTowards(
                _verticalVelocity,
                hoverAscendSpeed,
                hoverVerticalAcceleration * dt);
        }

        void HandleSlideBoost(Vector3 moveInput, bool grounded, float dt, UnitInputFrame frame)
        {
            _isSliding = false;

            if (!frame.DashHeld)
            {
                _slideWasActive = false;
                return;
            }

            if (!allowAirSlide && !grounded)
                return;

            // Ignite cost stops tap-spam when nearly empty.
            if (!_slideWasActive)
            {
                if (!dashJetFuel.TrySpend(slideIgniteCost))
                    return;
                _slideWasActive = true;
            }

            dashJetFuel.SpendUpTo(slideFuelPerSecond, dt, out float spent);
            if (spent <= 0f)
            {
                dashJetFuel.PunishRegen(slideEmptyExtraDelay);
                _slideWasActive = false;
                return;
            }

            _isSliding = true;

            Vector3 localDir = moveInput;
            if (localDir.sqrMagnitude < 0.01f)
            {
                if (!slideForwardWhenNoInput)
                    return;
                localDir = Vector3.forward;
            }

            Vector3 wish = transform.TransformDirection(localDir);
            wish.y = 0f;
            if (wish.sqrMagnitude > 0.0001f)
                wish.Normalize();

            // Accelerate toward slide top speed (no hard set).
            Vector3 target = wish * slideMaxSpeed;
            _horizontalVelocity = Vector3.MoveTowards(
                _horizontalVelocity,
                target,
                slideAcceleration * dt);

            // Soft steer: blend velocity toward wish a little without killing momentum.
            if (slideSteer > 0f && wish.sqrMagnitude > 0.01f)
            {
                float speed = _horizontalVelocity.magnitude;
                Vector3 steered = Vector3.Lerp(
                    _horizontalVelocity.normalized,
                    wish,
                    slideSteer * dt * 4f);
                if (steered.sqrMagnitude > 0.0001f)
                    _horizontalVelocity = steered.normalized * speed;
            }
        }

        void IntegrateVelocity(Vector3 moveInput, bool grounded, float dt)
        {
            if (!_isSliding)
            {
                Vector3 wish = transform.TransformDirection(moveInput);
                wish.y = 0f;
                if (wish.sqrMagnitude > 1f)
                    wish.Normalize();

                float control = grounded ? 1f : airControl;
                Vector3 planar = _horizontalVelocity;
                planar.y = 0f;

                if (wish.sqrMagnitude > 0.001f)
                {
                    Vector3 desired = wish * maxWalkSpeed;
                    float align = Vector3.Dot(planar.normalized, wish);
                    // Opposite stick = brake harder (vehicle-like).
                    float accel = align < -0.2f ? brakeFriction : walkAcceleration;
                    _horizontalVelocity = Vector3.MoveTowards(
                        planar,
                        desired,
                        accel * control * dt);
                }
                else
                {
                    // Coast to stop — not instant.
                    float friction = grounded ? coastFriction : coastFriction * 0.35f;
                    _horizontalVelocity = Vector3.MoveTowards(
                        planar,
                        Vector3.zero,
                        friction * control * dt);
                }
            }
            else if (grounded)
            {
                // Light drag while sliding so it still feels heavy, not ice forever.
                _horizontalVelocity = Vector3.MoveTowards(
                    _horizontalVelocity,
                    _horizontalVelocity.normalized * Mathf.Min(_horizontalVelocity.magnitude, slideMaxSpeed),
                    slideFriction * dt);
            }

            if (grounded && _verticalVelocity < 0f)
                _verticalVelocity = groundedStickForce;

            if (!_isHovering)
                _verticalVelocity += gravity * dt;
            else if (_verticalVelocity < hoverAscendSpeed)
                _verticalVelocity += gravity * hoverGravityScale * dt;

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
            maxWalkSpeed = Mathf.Max(0f, maxWalkSpeed);
            slideMaxSpeed = Mathf.Max(0f, slideMaxSpeed);
            jumpHeight = Mathf.Max(0f, jumpHeight);
            if (gravity >= 0f)
                gravity = -26f;
        }
#endif
    }
}
