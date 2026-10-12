using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Robogee.Player
{
    /// <summary>
    /// Grounded MS motor inspired by 戦場の絆 feel:
    /// heavy walk, boost dash with post-boost lag, jump with landing stun, EN overheat.
    /// Input via <see cref="IUnitInputSource"/> only.
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

        [Header("Weight / Walk (絆: レバー歩き)")]
        [FormerlySerializedAs("maxSpeed")]
        [SerializeField] float maxWalkSpeed = 4.6f;
        [FormerlySerializedAs("acceleration")]
        [SerializeField] float walkAcceleration = 11f;
        [FormerlySerializedAs("deceleration")]
        [SerializeField] float coastFriction = 8f;
        [SerializeField] float brakeFriction = 16f;
        [SerializeField] [Range(0.05f, 1f)] float turnResponsiveness = 0.5f;

        [Header("Air")]
        [SerializeField] [Range(0f, 1f)] float airControl = 0.18f;
        [SerializeField] float gravity = -28f;
        [SerializeField] float groundedStickForce = -2.5f;

        [Header("Jump + Landing Stun (絆: 着地硬直)")]
        [SerializeField] float jumpHeight = 1.15f;
        [SerializeField] float jumpFuelCost = 22f;
        [SerializeField] bool requireJumpFuel = true;
        [SerializeField] bool enableHover = false;
        [SerializeField] float hoverFuelPerSecond = 40f;
        [SerializeField] float hoverAscendSpeed = 2.2f;
        [SerializeField] float hoverVerticalAcceleration = 10f;
        [SerializeField] float hoverGravityScale = 0.35f;
        [SerializeField] float hoverArmDelay = 0.1f;
        [Tooltip("Seconds of reduced control after landing.")]
        [SerializeField] float landingStunSeconds = 0.32f;
        [SerializeField] [Range(0f, 1f)] float landingMoveScale = 0.25f;

        [Header("Boost Dash (絆: 右ペダル / EN)")]
        [FormerlySerializedAs("dashSpeed")]
        [FormerlySerializedAs("slideMaxSpeed")]
        [SerializeField] float boostMaxSpeed = 11.5f;
        [FormerlySerializedAs("slideAcceleration")]
        [SerializeField] float boostAcceleration = 42f;
        [FormerlySerializedAs("slideFriction")]
        [SerializeField] float boostFriction = 2.5f;
        [FormerlySerializedAs("dashFuelPerSecond")]
        [FormerlySerializedAs("slideFuelPerSecond")]
        [SerializeField] float boostFuelPerSecond = 38f;
        [FormerlySerializedAs("slideIgniteCost")]
        [SerializeField] float boostIgniteCost = 6f;
        [FormerlySerializedAs("allowAirDash")]
        [FormerlySerializedAs("allowAirSlide")]
        [SerializeField] bool allowAirBoost = false;
        [FormerlySerializedAs("slideSteer")]
        [SerializeField] [Range(0f, 1f)] float boostSteer = 0.28f;
        [FormerlySerializedAs("slideForwardWhenNoInput")]
        [SerializeField] bool boostForwardWhenNoInput = true;
        [Tooltip("隙 after releasing boost (cannot re-boost).")]
        [SerializeField] float boostRecoverySeconds = 0.38f;
        [Tooltip("Overheat lock when EN hits empty.")]
        [SerializeField] float boostOverheatSeconds = 1.45f;
        [FormerlySerializedAs("slideEmptyExtraDelay")]
        [SerializeField] float boostEmptyExtraDelay = 0.5f;

        [Header("Fuel - Jump Boost")]
        [SerializeField] FuelTank jumpBoostFuel = new FuelTank
        {
            max = 100f,
            current = 100f,
            regenPerSecond = 11f,
            regenDelay = 0.85f
        };

        [Header("Fuel - Dash Jet (Boost EN)")]
        [SerializeField] FuelTank dashJetFuel = new FuelTank
        {
            max = 100f,
            current = 100f,
            regenPerSecond = 14f,
            regenDelay = 0.7f
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
        bool _isBoosting;
        bool _isHovering;
        float _airborneTime;
        bool _boostWasActive;
        float _boostLockUntil;
        float _landingStunUntil;
        bool _wasGrounded = true;

        // Legacy / HUD names
        public float JumpBoostFuelNormalized => jumpBoostFuel.Normalized;
        public float DashJetFuelNormalized => dashJetFuel.Normalized;
        public bool IsJumpBoostActive => _isHovering || jumpBoostFuel.RecentlySpent;
        public bool IsDashJetActive => _isBoosting || dashJetFuel.RecentlySpent;
        public bool IsDashing => _isBoosting;
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
            if (!_wasGrounded && grounded)
                _landingStunUntil = Time.time + landingStunSeconds;
            _wasGrounded = grounded;

            if (grounded)
                _airborneTime = 0f;
            else
                _airborneTime += dt;

            Vector3 moveInput = frame.MovePlanar;
            if (moveInput.sqrMagnitude > 1f)
                moveInput.Normalize();

            bool stunned = Time.time < _landingStunUntil;
            if (stunned)
                moveInput *= landingMoveScale;

            HandleJump(grounded, stunned, frame);
            HandleHover(grounded, dt, frame);
            HandleBoost(moveInput, grounded, stunned, dt, frame);
            IntegrateVelocity(moveInput, grounded, stunned, dt);

            if (!_isHovering)
                jumpBoostFuel.TickRegen(dt);
            if (!_isBoosting && Time.time >= _boostLockUntil)
                dashJetFuel.TickRegen(dt);

            jumpBoostFuel.Clamp();
            dashJetFuel.Clamp();
        }

        void OnGUI()
        {
            if (!showFuelOverlay)
                return;

            const float w = 280f;
            GUI.Box(new Rect(12, 12, w, 110), "MS EN");
            GUI.Label(new Rect(24, 36, w - 24, 20),
                $"Jump: {jumpBoostFuel.current:0}/{jumpBoostFuel.max:0}");
            GUI.Label(new Rect(24, 56, w - 24, 20),
                $"Boost: {dashJetFuel.current:0}/{dashJetFuel.max:0}");
            string state = _isBoosting ? "BOOST"
                : (Time.time < _boostLockUntil ? "OVERHEAT/LAG"
                : (Time.time < _landingStunUntil ? "LAND STUN" : "WALK"));
            GUI.Label(new Rect(24, 76, w - 24, 20), state);
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

            float yawScale = _isBoosting ? turnResponsiveness * 0.55f : turnResponsiveness;
            if (Time.time < _landingStunUntil)
                yawScale *= 0.45f;
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

        void HandleJump(bool grounded, bool stunned, UnitInputFrame frame)
        {
            if (!grounded || stunned || !frame.JumpPressed)
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

        void HandleBoost(Vector3 moveInput, bool grounded, bool stunned, float dt, UnitInputFrame frame)
        {
            bool wasBoosting = _isBoosting;
            _isBoosting = false;

            if (stunned || Time.time < _boostLockUntil)
            {
                if (wasBoosting)
                    BeginBoostRecovery(boostRecoverySeconds);
                _boostWasActive = false;
                return;
            }

            if (!frame.DashHeld)
            {
                if (wasBoosting || _boostWasActive)
                    BeginBoostRecovery(boostRecoverySeconds);
                _boostWasActive = false;
                return;
            }

            if (!allowAirBoost && !grounded)
                return;

            if (!_boostWasActive)
            {
                if (!dashJetFuel.TrySpend(boostIgniteCost))
                    return;
                _boostWasActive = true;
            }

            dashJetFuel.SpendUpTo(boostFuelPerSecond, dt, out float spent);
            if (spent <= 0f)
            {
                // EN empty → overheat (絆-like 使用不能)
                dashJetFuel.PunishRegen(boostEmptyExtraDelay);
                BeginBoostRecovery(boostOverheatSeconds);
                _boostWasActive = false;
                return;
            }

            _isBoosting = true;

            Vector3 localDir = moveInput;
            if (localDir.sqrMagnitude < 0.01f)
            {
                if (!boostForwardWhenNoInput)
                    return;
                localDir = Vector3.forward;
            }

            Vector3 wish = transform.TransformDirection(localDir);
            wish.y = 0f;
            if (wish.sqrMagnitude > 0.0001f)
                wish.Normalize();

            Vector3 target = wish * boostMaxSpeed;
            _horizontalVelocity = Vector3.MoveTowards(
                _horizontalVelocity,
                target,
                boostAcceleration * dt);

            if (boostSteer > 0f && wish.sqrMagnitude > 0.01f)
            {
                float speed = _horizontalVelocity.magnitude;
                Vector3 steered = Vector3.Lerp(
                    _horizontalVelocity.normalized,
                    wish,
                    boostSteer * dt * 3.5f);
                if (steered.sqrMagnitude > 0.0001f)
                    _horizontalVelocity = steered.normalized * speed;
            }
        }

        void BeginBoostRecovery(float seconds)
        {
            _boostLockUntil = Mathf.Max(_boostLockUntil, Time.time + seconds);
        }

        void IntegrateVelocity(Vector3 moveInput, bool grounded, bool stunned, float dt)
        {
            if (!_isBoosting)
            {
                Vector3 wish = transform.TransformDirection(moveInput);
                wish.y = 0f;
                if (wish.sqrMagnitude > 1f)
                    wish.Normalize();

                float control = grounded ? 1f : airControl;
                if (stunned)
                    control *= landingMoveScale;

                Vector3 planar = _horizontalVelocity;
                planar.y = 0f;

                if (wish.sqrMagnitude > 0.001f && !stunned)
                {
                    Vector3 desired = wish * maxWalkSpeed;
                    float align = planar.sqrMagnitude > 0.01f
                        ? Vector3.Dot(planar.normalized, wish)
                        : 1f;
                    float accel = align < -0.2f ? brakeFriction : walkAcceleration;
                    _horizontalVelocity = Vector3.MoveTowards(
                        planar,
                        desired,
                        accel * control * dt);
                }
                else
                {
                    float friction = grounded ? coastFriction : coastFriction * 0.35f;
                    if (stunned)
                        friction *= 1.6f;
                    _horizontalVelocity = Vector3.MoveTowards(
                        planar,
                        Vector3.zero,
                        friction * Mathf.Max(0.2f, control) * dt);
                }
            }
            else if (grounded)
            {
                float cap = Mathf.Min(_horizontalVelocity.magnitude, boostMaxSpeed);
                if (_horizontalVelocity.sqrMagnitude > 0.0001f)
                {
                    _horizontalVelocity = Vector3.MoveTowards(
                        _horizontalVelocity,
                        _horizontalVelocity.normalized * cap,
                        boostFriction * dt);
                }
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
            boostMaxSpeed = Mathf.Max(0f, boostMaxSpeed);
            jumpHeight = Mathf.Max(0f, jumpHeight);
            if (gravity >= 0f)
                gravity = -28f;
        }
#endif
    }
}
