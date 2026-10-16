using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Robogee.Player
{
    /// <summary>
    /// 戦場の絆-style MS motor: one shared boost gauge (dash + jump), overheat until 100%,
    /// hold-to-rise jump with ふわジャン, air dash, stun after dash / landing scaled by duration,
    /// rpm-capped turning and no manual pitch (vertical aim comes from red lock).
    /// Spec fields use the wiki's units (km/h, cnt, rpm, f) and are converted with
    /// <see cref="worldScale"/>. Input via <see cref="IUnitInputSource"/> only.
    /// See Documentation/UnitB_Motor_Spec.md
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(UnitPlayerInput))]
    public class UnitBMotor : MonoBehaviour
    {
        const float SecondsPerCnt = 0.9f;
        const float FramesPerCnt = 60f;
        const float GaugeMax = 100f;

        [Header("References")]
        [SerializeField] Transform cameraPivot;
        [SerializeField] MonoBehaviour inputSourceBehaviour;

        [Header("Scale")]
        [Tooltip("Unit height / MS height. 1.8m skeleton vs 18m MS = 0.1.")]
        [SerializeField] float worldScale = 0.1f;

        [Header("絆スペック（wiki機体表の値をそのまま）")]
        [SerializeField] float walkSpeedKmh = 108f;
        [SerializeField] float dashSpeedKmh = 210f;
        [Tooltip("Full gauge → empty while holding dash (ダッシュ時間).")]
        [SerializeField] float dashTimeCnt = 2.3f;
        [SerializeField] float jumpSpeedKmh = 175f;
        [SerializeField] float turnRpm = 10.6f;
        [Tooltip("硬直時間 = small-jump landing stun.")]
        [SerializeField] float stunFrames = 36f;
        [Tooltip("Overheat: seconds-in-cnt until the gauge is back to 100.")]
        [SerializeField] float overheatRecoverCnt = 3.5f;

        [Header("Boost gauge")]
        [Tooltip("Full gauge → empty while holding jump. Jump drains harder than dash.")]
        [SerializeField] float jumpFullTimeCnt = 1.7f;
        [SerializeField] float dashIgnitePercent = 3f;
        [Tooltip("Spent the moment the jump pedal goes down (ふわジャン cost).")]
        [SerializeField] float jumpIgnitePercent = 8f;
        [Tooltip("Normal regen 0 → 100. Starts the instant both pedals are released.")]
        [SerializeField] float regenFullCnt = 3.5f;
        [SerializeField] [Range(0f, 1f)] float airRegenScale = 0.5f;
        [Tooltip("格闘型 boost limiter: a single dash stops after spending this % (0 = none).")]
        [SerializeField] float boostLimiterPercent = 0f;
        [Tooltip("格闘型: no regen for this long after a dash.")]
        [SerializeField] float regenLockAfterDashCnt = 0f;
        [SerializeField] bool allowAirDash = true;

        [Header("Dash feel")]
        [SerializeField] float dashAccelSeconds = 0.12f;
        [Tooltip("Dash direction follows the lever mid-dash at this rate.")]
        [SerializeField] float dashSteerDegPerSec = 540f;
        [SerializeField] [Range(0f, 1f)] float airDashGravityScale = 0.1f;

        [Header("Jump feel (ふわジャン)")]
        [Tooltip("Instant lift when the pedal goes down, as a ratio of jump speed.")]
        [SerializeField] [Range(0f, 1f)] float jumpLiftOnPress = 0.45f;
        [SerializeField] float jumpRampSeconds = 0.25f;
        [Tooltip("After releasing, the rise coasts this long before falling.")]
        [SerializeField] float riseCarrySeconds = 0.22f;
        [SerializeField] float fallGravity = -14f;
        [SerializeField] float maxFallSpeedKmh = 200f;

        [Header("Walk / air")]
        [SerializeField] float walkAccelSeconds = 0.18f;
        [SerializeField] float walkStopSeconds = 0.14f;
        [SerializeField] float airSteerSeconds = 0.9f;
        [SerializeField] float airDrag = 0.35f;
        [SerializeField] float groundedStickForce = -2.5f;

        [Header("硬直")]
        [SerializeField] float bigJumpStunFrames = 58f;
        [Tooltip("Air time at or below this counts as a small jump.")]
        [SerializeField] float smallJumpAirCnt = 0.7f;
        [Tooltip("Air time at which landing stun reaches bigJumpStunFrames.")]
        [SerializeField] float bigJumpAirCnt = 2.5f;
        [SerializeField] float dashStunMinFrames = 22f;
        [SerializeField] float dashStunMaxFrames = 54f;
        [Tooltip("Extra landing stun while overheated (estimate, not from the wiki).")]
        [SerializeField] float overheatLandingExtraFrames = 12f;
        [SerializeField] float landingMinAirSeconds = 0.15f;
        [SerializeField] float stunBrakeDecel = 25f;
        [SerializeField] bool allowTurnWhileStunned = true;

        [Header("Look")]
        [FormerlySerializedAs("mouseSensitivity")]
        [SerializeField] float lookSensitivity = 2f;
        [Tooltip("絆 has no manual pitch. Enable for a limited range.")]
        [SerializeField] bool allowManualPitch = false;
        [SerializeField] float manualPitchLimit = 25f;
        [SerializeField] float pitchReturnSpeed = 6f;
        [SerializeField] float lockPitchLimit = 60f;
        [SerializeField] bool lockCursorOnStart = true;
        [SerializeField] float lockOnTurnSpeed = 14f;

        [Header("Footsteps")]
        [SerializeField] float strideMeters = 1.6f;

        [Header("Debug")]
        [SerializeField] bool showFuelOverlay = false;

        CharacterController _controller;
        IUnitInputSource _input;
        IAimLockProvider _aimLock;
        bool _cursorLocked;
        float _pitch;

        Vector3 _horizontalVelocity;
        float _verticalVelocity;
        float _boost = GaugeMax;
        bool _overheated;

        bool _isDashing;
        float _dashTime;
        float _dashSpent;
        Vector3 _dashDir;
        bool _dashNeedsRelease;

        bool _jumpEngaged;
        bool _jumpThrusting;
        float _carryTimer;
        bool _jumpedThisAir;

        bool _wasGrounded = true;
        float _airTime;
        float _lastFallSpeed;
        float _stunUntil;
        float _regenLockUntil;
        bool _prevDashHeld;
        bool _prevJumpHeld;
        float _stepDistance;

        /// <summary>Walking footfall. Arg: 0..1 relative to walk speed.</summary>
        public event Action<float> Footstep;
        /// <summary>Touchdown after a jump / fall. Arg: 0..1 impact.</summary>
        public event Action<float> Landed;
        /// <summary>Dash ended on the ground (brace into stun). Arg: 0..1 dash length.</summary>
        public event Action<float> DashBraked;
        public event Action Overheated;

        public float BoostNormalized => Mathf.Clamp01(_boost / GaugeMax);
        public bool UsesSharedBoostGauge => true;
        public bool IsOverheated => _overheated;
        public bool IsStunned => Time.time < _stunUntil;
        public float StunRemaining => Mathf.Max(0f, _stunUntil - Time.time);
        public bool IsJumpThrusting => _jumpThrusting;
        public float WalkSpeed => KmhToUnits(walkSpeedKmh);

        // Legacy names (FuelGaugeHud / older callers) — both map to the shared gauge.
        public float JumpBoostFuelNormalized => BoostNormalized;
        public float DashJetFuelNormalized => BoostNormalized;
        public bool IsJumpBoostActive => _jumpThrusting;
        public bool IsDashJetActive => _isDashing;
        public bool IsDashing => _isDashing;
        public bool IsHovering => _jumpThrusting;
        public bool IsGrounded => _controller != null && _controller.isGrounded;
        public Vector3 PlanarVelocity => new Vector3(_horizontalVelocity.x, 0f, _horizontalVelocity.z);
        public float PlanarSpeed => PlanarVelocity.magnitude;

        float KmhToUnits(float kmh) => kmh / 3.6f * worldScale;
        static float CntToSeconds(float cnt) => cnt * SecondsPerCnt;
        static float FramesToSeconds(float frames) => frames * SecondsPerCnt / FramesPerCnt;

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
        }

        void Start()
        {
            ResolveAimLock();
            SetCursorLock(lockCursorOnStart);
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
            if (dt <= 0f)
                return;

            UnitInputFrame frame = _input != null ? _input.Current : default;

            if (frame.ToggleCursorPressed)
                SetCursorLock(!_cursorLocked);

            bool grounded = _controller.isGrounded;
            if (grounded && !_wasGrounded)
                HandleLanding();
            if (grounded)
            {
                _airTime = 0f;
                _jumpedThisAir = false;
            }
            else
            {
                _airTime += dt;
            }
            _wasGrounded = grounded;

            bool stunned = IsStunned;
            if (_cursorLocked)
                ApplyLook(frame, stunned, dt);

            Vector3 moveInput = frame.MovePlanar;
            if (moveInput.sqrMagnitude > 1f)
                moveInput.Normalize();

            bool dashPressed = frame.DashHeld && !_prevDashHeld;
            bool jumpPressed = frame.JumpPressed || (frame.JumpHeld && !_prevJumpHeld);
            if (!frame.DashHeld)
                _dashNeedsRelease = false;

            UpdateJump(frame, jumpPressed, stunned, dt);
            UpdateDash(frame, dashPressed, moveInput, grounded, stunned, dt);
            IntegrateHorizontal(moveInput, grounded, IsStunned, dt);
            IntegrateVertical(grounded, dt);

            if (!grounded)
                _lastFallSpeed = Mathf.Max(0f, -_verticalVelocity);

            Vector3 motion = _horizontalVelocity;
            motion.y = _verticalVelocity;
            _controller.Move(motion * dt);

            UpdateGauge(frame, grounded, dt);
            UpdateFootsteps(grounded, IsStunned, dt);

            _prevDashHeld = frame.DashHeld;
            _prevJumpHeld = frame.JumpHeld;
        }

        bool CanUseBoost(bool stunned) => !_overheated && !stunned && _boost > 0f;

        bool Drain(float amount)
        {
            _boost -= amount;
            if (_boost > 0f)
                return true;
            _boost = 0f;
            TriggerOverheat();
            return false;
        }

        void TriggerOverheat()
        {
            if (_overheated)
                return;
            _overheated = true;
            if (_jumpEngaged)
                _carryTimer = riseCarrySeconds;
            _jumpEngaged = false;
            _jumpThrusting = false;
            Overheated?.Invoke();
        }

        void ApplyStun(float seconds)
        {
            _stunUntil = Mathf.Max(_stunUntil, Time.time + seconds);
        }

        void UpdateJump(UnitInputFrame frame, bool pressed, bool stunned, float dt)
        {
            if (pressed && CanUseBoost(stunned) && Drain(jumpIgnitePercent))
            {
                _jumpEngaged = true;
                _jumpedThisAir = true;
                _carryTimer = 0f;
                float lift = KmhToUnits(jumpSpeedKmh) * jumpLiftOnPress;
                if (_verticalVelocity < lift)
                    _verticalVelocity = lift;
            }

            if (!frame.JumpHeld)
            {
                if (_jumpEngaged)
                    _carryTimer = riseCarrySeconds;
                _jumpEngaged = false;
            }

            bool thrust = _jumpEngaged && frame.JumpHeld && CanUseBoost(stunned);
            if (thrust)
            {
                float rate = GaugeMax / Mathf.Max(0.05f, CntToSeconds(jumpFullTimeCnt));
                thrust = Drain(rate * dt);
            }

            _jumpThrusting = thrust;
            if (!thrust)
                return;

            float target = KmhToUnits(jumpSpeedKmh);
            float accel = target / Mathf.Max(0.01f, jumpRampSeconds);
            _verticalVelocity = Mathf.MoveTowards(_verticalVelocity, target, accel * dt);
        }

        void UpdateDash(UnitInputFrame frame, bool pressed, Vector3 moveInput, bool grounded, bool stunned, float dt)
        {
            if (_isDashing)
            {
                if (!frame.DashHeld)
                {
                    EndDash(grounded, forced: false);
                    return;
                }

                if (!CanUseBoost(stunned))
                {
                    EndDash(grounded, forced: true);
                    return;
                }

                float rate = GaugeMax / Mathf.Max(0.05f, CntToSeconds(dashTimeCnt));
                float amount = rate * dt;
                bool ok = Drain(amount);
                _dashSpent += amount;
                _dashTime += dt;

                if (!ok || (boostLimiterPercent > 0f && _dashSpent >= boostLimiterPercent))
                {
                    EndDash(grounded, forced: true);
                    return;
                }

                SteerDash(moveInput, dt);
                return;
            }

            if (!frame.DashHeld || _dashNeedsRelease)
                return;
            if (!CanUseBoost(stunned))
                return;
            if (!grounded && !allowAirDash)
                return;
            if (!Drain(dashIgnitePercent))
                return;

            _isDashing = true;
            _dashTime = 0f;
            _dashSpent = dashIgnitePercent;
            _dashDir = LeverWorldDir(moveInput);
        }

        Vector3 LeverWorldDir(Vector3 moveInput)
        {
            Vector3 local = moveInput.sqrMagnitude > 0.01f ? moveInput.normalized : Vector3.forward;
            Vector3 world = transform.TransformDirection(local);
            world.y = 0f;
            return world.sqrMagnitude > 0.0001f ? world.normalized : transform.forward;
        }

        void SteerDash(Vector3 moveInput, float dt)
        {
            Vector3 want = LeverWorldDir(moveInput);
            _dashDir = Vector3.RotateTowards(_dashDir, want, dashSteerDegPerSec * Mathf.Deg2Rad * dt, 0f);
        }

        void EndDash(bool grounded, bool forced)
        {
            _isDashing = false;
            if (forced)
                _dashNeedsRelease = true;
            if (regenLockAfterDashCnt > 0f)
                _regenLockUntil = Time.time + CntToSeconds(regenLockAfterDashCnt);

            // Ending in the air defers to landing stun — this is what makes グラップル work.
            if (!grounded)
                return;

            float full = Mathf.Max(0.05f, CntToSeconds(dashTimeCnt));
            float t = Mathf.Clamp01(_dashTime / full);
            ApplyStun(FramesToSeconds(Mathf.Lerp(dashStunMinFrames, dashStunMaxFrames, t)));
            DashBraked?.Invoke(Mathf.Lerp(0.4f, 1f, t));
        }

        void HandleLanding()
        {
            if (_airTime < landingMinAirSeconds && !_jumpedThisAir)
                return;

            float maxFall = Mathf.Max(0.01f, KmhToUnits(maxFallSpeedKmh));
            float impact = Mathf.Clamp01(_lastFallSpeed / maxFall);

            _jumpEngaged = false;
            _jumpThrusting = false;
            _carryTimer = 0f;

            // 着地キャン: still dashing at touchdown → no landing stun, the dash carries on.
            if (_isDashing)
            {
                Landed?.Invoke(impact * 0.5f);
                return;
            }

            float t = Mathf.InverseLerp(CntToSeconds(smallJumpAirCnt), CntToSeconds(bigJumpAirCnt), _airTime);
            float frames = Mathf.Lerp(stunFrames, bigJumpStunFrames, t);
            if (_overheated)
                frames += overheatLandingExtraFrames;
            ApplyStun(FramesToSeconds(frames));
            Landed?.Invoke(Mathf.Max(impact, t));
        }

        void IntegrateHorizontal(Vector3 moveInput, bool grounded, bool stunned, float dt)
        {
            Vector3 planar = PlanarVelocity;

            if (_isDashing)
            {
                float dashSpeed = KmhToUnits(dashSpeedKmh);
                float accel = dashSpeed / Mathf.Max(0.01f, dashAccelSeconds);
                planar = Vector3.MoveTowards(planar, _dashDir * dashSpeed, accel * dt);
            }
            else if (stunned)
            {
                planar = Vector3.MoveTowards(planar, Vector3.zero, stunBrakeDecel * dt);
            }
            else
            {
                Vector3 wish = transform.TransformDirection(moveInput);
                wish.y = 0f;
                if (wish.sqrMagnitude > 1f)
                    wish.Normalize();

                float walk = WalkSpeed;
                if (grounded)
                {
                    Vector3 desired = wish * walk;
                    bool accelerating = desired.sqrMagnitude > 0.0001f && planar.magnitude <= walk + 0.01f;
                    float rate = accelerating
                        ? walk / Mathf.Max(0.01f, walkAccelSeconds)
                        : walk / Mathf.Max(0.01f, walkStopSeconds);
                    planar = Vector3.MoveTowards(planar, desired, rate * dt);
                }
                else
                {
                    // Jump inertia carries; the lever only nudges up to walk speed.
                    float before = planar.magnitude;
                    if (wish.sqrMagnitude > 0.0001f)
                    {
                        planar += wish * (walk / Mathf.Max(0.01f, airSteerSeconds)) * dt;
                        float cap = Mathf.Max(before, walk);
                        if (planar.magnitude > cap)
                            planar = planar.normalized * cap;
                    }
                    planar = Vector3.MoveTowards(planar, Vector3.zero, airDrag * dt);
                }
            }

            _horizontalVelocity = planar;
        }

        void IntegrateVertical(bool grounded, float dt)
        {
            if (_jumpThrusting)
            {
                // Velocity already driven in UpdateJump.
            }
            else if (_isDashing && !grounded)
            {
                _verticalVelocity = Mathf.MoveTowards(_verticalVelocity, 0f, Mathf.Abs(fallGravity) * dt);
                _verticalVelocity += fallGravity * airDashGravityScale * dt;
            }
            else if (_carryTimer > 0f && _verticalVelocity > 0f)
            {
                float decel = KmhToUnits(jumpSpeedKmh) / Mathf.Max(0.01f, riseCarrySeconds);
                _verticalVelocity = Mathf.MoveTowards(_verticalVelocity, 0f, decel * dt);
                _carryTimer -= dt;
            }
            else
            {
                _carryTimer = 0f;
                _verticalVelocity += fallGravity * dt;
            }

            float maxFall = KmhToUnits(maxFallSpeedKmh);
            if (_verticalVelocity < -maxFall)
                _verticalVelocity = -maxFall;

            if (grounded && _verticalVelocity < 0f)
                _verticalVelocity = groundedStickForce;
        }

        void UpdateGauge(UnitInputFrame frame, bool grounded, float dt)
        {
            if (_overheated)
            {
                _boost += GaugeMax / Mathf.Max(0.05f, CntToSeconds(overheatRecoverCnt)) * dt;
                if (_boost >= GaugeMax)
                {
                    _boost = GaugeMax;
                    _overheated = false;
                }
                return;
            }

            // Regen only once both pedals are fully back up.
            if (frame.DashHeld || frame.JumpHeld || _isDashing || _jumpThrusting)
                return;
            if (Time.time < _regenLockUntil)
                return;

            float rate = GaugeMax / Mathf.Max(0.05f, CntToSeconds(regenFullCnt));
            if (!grounded)
                rate *= airRegenScale;
            _boost = Mathf.Min(GaugeMax, _boost + rate * dt);
        }

        void UpdateFootsteps(bool grounded, bool stunned, float dt)
        {
            float speed = PlanarSpeed;
            float walk = Mathf.Max(0.01f, WalkSpeed);
            if (!grounded || _isDashing || stunned || speed < walk * 0.25f)
            {
                _stepDistance = strideMeters * 0.6f;
                return;
            }

            _stepDistance += speed * dt;
            if (_stepDistance < strideMeters)
                return;
            _stepDistance -= strideMeters;
            Footstep?.Invoke(Mathf.Clamp01(speed / walk));
        }

        void ApplyLook(UnitInputFrame frame, bool stunned, float dt)
        {
            if (_aimLock == null)
                ResolveAimLock();

            // Red lock: auto-face the target, manual turning is disabled (as in 絆).
            if (_aimLock != null && _aimLock.TryGetLockedAimPoint(out Vector3 aimPoint))
            {
                ApplyLockLook(aimPoint, dt);
                return;
            }

            if (!stunned || allowTurnWhileStunned)
            {
                float maxYaw = turnRpm * 6f * dt;
                float yaw = Mathf.Clamp(frame.LookX * lookSensitivity, -maxYaw, maxYaw);
                transform.Rotate(0f, yaw, 0f);
            }

            if (cameraPivot == null)
                return;

            if (allowManualPitch)
                _pitch = Mathf.Clamp(_pitch - frame.LookY * lookSensitivity, -manualPitchLimit, manualPitchLimit);
            else
                _pitch = Mathf.Lerp(_pitch, 0f, 1f - Mathf.Exp(-pitchReturnSpeed * dt));
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        void ApplyLockLook(Vector3 aimPoint, float dt)
        {
            Vector3 eye = cameraPivot != null
                ? cameraPivot.position
                : transform.position + Vector3.up * 0.25f;
            Vector3 to = aimPoint - eye;
            if (to.sqrMagnitude < 0.0001f)
                return;

            float t = 1f - Mathf.Exp(-lockOnTurnSpeed * dt);

            float targetYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            float yaw = Mathf.LerpAngle(transform.eulerAngles.y, targetYaw, t);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            if (cameraPivot == null)
                return;

            Vector3 local = Quaternion.Inverse(transform.rotation) * to;
            float targetPitch = -Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg;
            targetPitch = Mathf.Clamp(targetPitch, -lockPitchLimit, lockPitchLimit);
            _pitch = Mathf.Lerp(_pitch, targetPitch, t);
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        void SetCursorLock(bool locked)
        {
            _cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void OnGUI()
        {
            if (!showFuelOverlay)
                return;

            const float w = 280f;
            GUI.Box(new Rect(12, 12, w, 96), "MS BOOST");
            GUI.Label(new Rect(24, 36, w - 24, 20), $"Boost: {_boost:0}/{GaugeMax:0}");
            string state = _overheated ? "OVERHEAT"
                : IsStunned ? $"STUN {StunRemaining:0.00}s"
                : _isDashing ? "DASH"
                : _jumpThrusting ? "JUMP"
                : "WALK";
            GUI.Label(new Rect(24, 56, w - 24, 20), state);
        }

        [ContextMenu("絆プリセット/近距離（標準）")]
        void PresetStandard() => ApplyPreset(108f, 210f, 2.3f, 175f, 10.6f, 36f, 3.5f, 1.7f, 0f, 0f);

        [ContextMenu("絆プリセット/格闘（ショートダッシュ）")]
        void PresetFighter() => ApplyPreset(111f, 236f, 4.4f, 186f, 12.0f, 33f, 3.0f, 1.4f, 25f, 1.0f);

        [ContextMenu("絆プリセット/射撃・支援（重め）")]
        void PresetHeavy() => ApplyPreset(100f, 187f, 2.4f, 162f, 10.3f, 45f, 7.5f, 1.8f, 0f, 0f);

        void ApplyPreset(float walk, float dash, float dashCnt, float jump, float rpm, float stun,
            float ohCnt, float jumpCnt, float limiter, float regenLockCnt)
        {
            walkSpeedKmh = walk;
            dashSpeedKmh = dash;
            dashTimeCnt = dashCnt;
            jumpSpeedKmh = jump;
            turnRpm = rpm;
            stunFrames = stun;
            overheatRecoverCnt = ohCnt;
            jumpFullTimeCnt = jumpCnt;
            boostLimiterPercent = limiter;
            regenLockAfterDashCnt = regenLockCnt;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            worldScale = Mathf.Max(0.001f, worldScale);
            walkSpeedKmh = Mathf.Max(0f, walkSpeedKmh);
            dashSpeedKmh = Mathf.Max(0f, dashSpeedKmh);
            jumpSpeedKmh = Mathf.Max(0f, jumpSpeedKmh);
            dashTimeCnt = Mathf.Max(0.1f, dashTimeCnt);
            jumpFullTimeCnt = Mathf.Max(0.1f, jumpFullTimeCnt);
            regenFullCnt = Mathf.Max(0.1f, regenFullCnt);
            overheatRecoverCnt = Mathf.Max(0.1f, overheatRecoverCnt);
            turnRpm = Mathf.Max(0.1f, turnRpm);
            bigJumpAirCnt = Mathf.Max(smallJumpAirCnt + 0.1f, bigJumpAirCnt);
            if (fallGravity >= 0f)
                fallGravity = -14f;
        }
#endif
    }
}
